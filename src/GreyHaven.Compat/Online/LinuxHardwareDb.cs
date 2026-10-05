using System.Net;
using System.Security.Authentication;
using System.Text.Json;
using System.Text.RegularExpressions;
using GreyHaven.Core.Hardware;

namespace GreyHaven.Compat.Online;

public enum CommunityStatus { Found, NotFound, Unavailable, NotApplicable }

/// <summary>What linux-hardware.org says about a device.</summary>
public sealed record CommunityResult
{
    public required CommunityStatus Status { get; init; }
    public string? Url { get; init; }
    public string? DeviceName { get; init; }
    /// <summary>Total computers with this device ("Status (N)" on the page).</summary>
    public int? TotalComputers { get; init; }
    /// <summary>Status counts from the first page of probes (a sample of recent computers).</summary>
    public int Works { get; init; }
    public int Detected { get; init; }
    public int Failed { get; init; }
    public int Malfunctioning { get; init; }
    /// <summary>For <see cref="CommunityStatus.Unavailable"/>: plain-English reason.</summary>
    public string? Reason { get; init; }
    public DateTimeOffset FetchedAt { get; init; }

    public int SampleSize => Works + Detected + Failed + Malfunctioning;
}

/// <summary>Looks up community data for one device. Implemented for linux-hardware.org; mockable in tests.</summary>
public interface ICommunityLookup
{
    Task<CommunityResult> LookupAsync(DeviceId id, CancellationToken cancellationToken = default);
}

/// <summary>
/// Client for linux-hardware.org device pages. The site has no API; it serves HTML only, asks
/// crawlers for ≥10 s between requests (robots.txt) and returns 429 when rate-limited.
///
/// Behaviour:
/// - Only device IDs are sent (in the URL). No other data leaves the PC.
/// - Waits at least <see cref="MinInterval"/> between requests.
/// - After a 429, stops querying for the rest of the session.
/// - Never bypasses TLS certificate errors; reports the site as unavailable instead.
/// - Results are cached on disk for <see cref="CacheLifetime"/>; failures are not cached.
/// </summary>
public sealed partial class LinuxHardwareDbClient(HttpClient http, string? cacheDirectory, TimeProvider? time = null) : ICommunityLookup
{
    public const string BaseUrl = "https://linux-hardware.org/";
    public static readonly TimeSpan MinInterval = TimeSpan.FromSeconds(10);
    public static readonly TimeSpan CacheLifetime = TimeSpan.FromDays(30);

    private readonly TimeProvider _time = time ?? TimeProvider.System;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private DateTimeOffset _lastRequest = DateTimeOffset.MinValue;
    private string? _sessionBlockedReason;

    public static string? PageUrl(DeviceId id)
    {
        var bus = id.Bus switch { DeviceBus.Pci => "pci", DeviceBus.Usb => "usb", _ => null };
        if (bus is null) return null;
        var key = id.HasSubsystem ? $"{id.Vendor}-{id.Device}-{id.SubVendor}-{id.SubDevice}" : $"{id.Vendor}-{id.Device}";
        return $"{BaseUrl}?id={bus}:{key}";
    }

    public async Task<CommunityResult> LookupAsync(DeviceId id, CancellationToken cancellationToken = default)
    {
        var url = PageUrl(id);
        if (url is null) return new CommunityResult { Status = CommunityStatus.NotApplicable, FetchedAt = _time.GetUtcNow() };

        if (TryReadCache(url) is { } cached) return cached;
        if (_sessionBlockedReason is not null) return Unavailable(url, _sessionBlockedReason);

        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (_sessionBlockedReason is not null) return Unavailable(url, _sessionBlockedReason);

            var wait = _lastRequest + MinInterval - _time.GetUtcNow();
            if (wait > TimeSpan.Zero) await Task.Delay(wait, _time, cancellationToken).ConfigureAwait(false);
            _lastRequest = _time.GetUtcNow();

            using var response = await http.GetAsync(url, cancellationToken).ConfigureAwait(false);
            if (response.StatusCode == HttpStatusCode.TooManyRequests)
            {
                _sessionBlockedReason = "linux-hardware.org asked GreyHaven to slow down (rate limit), so the remaining online lookups were skipped.";
                return Unavailable(url, _sessionBlockedReason);
            }
            if (response.StatusCode == HttpStatusCode.NotFound)
                return Cache(url, new CommunityResult { Status = CommunityStatus.NotFound, Url = url, FetchedAt = _time.GetUtcNow() });
            if (!response.IsSuccessStatusCode)
                return Unavailable(url, $"linux-hardware.org returned an error ({(int)response.StatusCode}).");

            var html = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            return Cache(url, Parse(html, url, _time.GetUtcNow()));
        }
        catch (HttpRequestException ex) when (ex.InnerException is AuthenticationException)
        {
            _sessionBlockedReason = "linux-hardware.org's security certificate could not be verified, so GreyHaven did not connect to it.";
            return Unavailable(url, _sessionBlockedReason);
        }
        catch (HttpRequestException)
        {
            _sessionBlockedReason = "linux-hardware.org could not be reached. You may be offline.";
            return Unavailable(url, _sessionBlockedReason);
        }
        catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            _sessionBlockedReason = "linux-hardware.org did not respond in time.";
            return Unavailable(url, _sessionBlockedReason);
        }
        finally
        {
            _gate.Release();
        }
    }

    /// <summary>Parses a device page. Public for tests.</summary>
    public static CommunityResult Parse(string html, string url, DateTimeOffset fetchedAt)
    {
        var title = TitleRegex().Match(html);
        var name = title.Success ? WebUtility.HtmlDecode(title.Groups[1].Value).Trim() : null;
        var total = TotalRegex().Match(html);
        if (string.IsNullOrEmpty(name) || name.Contains("Not found", StringComparison.OrdinalIgnoreCase) || !html.Contains("Kernel Drivers") && !total.Success)
            return new CommunityResult { Status = CommunityStatus.NotFound, Url = url, FetchedAt = fetchedAt };

        int Count(string cls) => html.Split($"<td class='{cls}'>", StringSplitOptions.None).Length - 1;
        return new CommunityResult
        {
            Status = CommunityStatus.Found,
            Url = url,
            DeviceName = name,
            TotalComputers = total.Success ? int.Parse(total.Groups[1].Value) : null,
            Works = Count("works"),
            Detected = Count("detected"),
            Failed = Count("failed"),
            Malfunctioning = Count("malfunc"),
            FetchedAt = fetchedAt,
        };
    }

    private static CommunityResult Unavailable(string url, string reason) =>
        new() { Status = CommunityStatus.Unavailable, Url = url, Reason = reason };

    // ---- Cache -------------------------------------------------------------------------

    private string? CachePath(string url) => cacheDirectory is null
        ? null
        : Path.Combine(cacheDirectory, "lhdb-" + Convert.ToHexStringLower(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(url)))[..24] + ".json");

    private CommunityResult? TryReadCache(string url)
    {
        var path = CachePath(url);
        if (path is null || !File.Exists(path)) return null;
        try
        {
            var r = JsonSerializer.Deserialize<CommunityResult>(File.ReadAllText(path));
            return r is not null && _time.GetUtcNow() - r.FetchedAt < CacheLifetime ? r : null;
        }
        catch (Exception ex) when (ex is JsonException or IOException)
        {
            return null; // corrupt cache entry: refetch
        }
    }

    private CommunityResult Cache(string url, CommunityResult result)
    {
        var path = CachePath(url);
        if (path is null) return result;
        try
        {
            Directory.CreateDirectory(cacheDirectory!);
            File.WriteAllText(path, JsonSerializer.Serialize(result));
        }
        catch (IOException) { /* caching is best-effort */ }
        catch (UnauthorizedAccessException) { }
        return result;
    }

    public static string DefaultCacheDirectory =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "GreyHaven", "cache");

    [GeneratedRegex(@"<title>\s*(.*?)\s*</title>", RegexOptions.Singleline)]
    private static partial Regex TitleRegex();

    [GeneratedRegex(@"<h2>Status \((\d+)\)</h2>")]
    private static partial Regex TotalRegex();
}
