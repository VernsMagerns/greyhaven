using System.Text.Json;
using System.Text.RegularExpressions;
using GreyHaven.Compat.Data;
using GreyHaven.Core.Compatibility;

namespace GreyHaven.Compat.Online;

public sealed record ReleaseCheckResult(IReadOnlyList<NewerRelease> Newer, IReadOnlyList<string> Errors)
{
    /// <summary>True when every distro could be checked.</summary>
    public bool Complete => Errors.Count == 0;
}

/// <summary>Finds releases of the supported distros that are newer than the bundled data describes.</summary>
public interface IDistroReleaseChecker
{
    Task<ReleaseCheckResult> CheckAsync(DistroCatalog catalog, CancellationToken cancellationToken = default);
}

/// <summary>
/// Checks official release sources configured per distro in distros.yaml (<c>releaseCheck</c>):
/// <list type="bullet">
///   <item><c>ubuntuMetaRelease</c>: Ubuntu's meta-release feed (what Ubuntu's own upgrader uses).</item>
///   <item><c>directoryListing</c>: version folders on an official mirror (Linux Mint).</item>
///   <item><c>popApi</c>: Pop!_OS's ISO build API; a newer build or LTS version counts.</item>
/// </list>
/// Sends no information about the PC. Never throws for network problems; they're reported in
/// <see cref="ReleaseCheckResult.Errors"/>.
/// </summary>
public sealed partial class DistroReleaseChecker(HttpClient http) : IDistroReleaseChecker
{
    public async Task<ReleaseCheckResult> CheckAsync(DistroCatalog catalog, CancellationToken cancellationToken = default)
    {
        var newer = new List<NewerRelease>();
        var errors = new List<string>();
        foreach (var d in catalog.Distros.Where(d => d.ReleaseCheck is not null))
        {
            try
            {
                var found = d.ReleaseCheck!.Kind switch
                {
                    "ubuntuMetaRelease" => await CheckUbuntu(d, cancellationToken).ConfigureAwait(false),
                    "directoryListing" => await CheckDirectory(d, cancellationToken).ConfigureAwait(false),
                    "popApi" => await CheckPop(d, cancellationToken).ConfigureAwait(false),
                    _ => [],
                };
                newer.AddRange(found);
            }
            catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException or FormatException)
            {
                if (cancellationToken.IsCancellationRequested) throw;
                errors.Add($"Couldn't check for new {d.Name} releases ({ex.GetType().Name}).");
            }
        }
        return new ReleaseCheckResult(newer, errors);
    }

    private async Task<IEnumerable<NewerRelease>> CheckUbuntu(DistroInfo d, CancellationToken ct)
    {
        var text = await http.GetStringAsync(d.ReleaseCheck!.Url, ct).ConfigureAwait(false);
        var latest = ParseUbuntuMetaRelease(text);
        return latest is not null && CompareVersions(latest, d.Version) > 0
            ? [new NewerRelease(d.Id, d.Name, d.Version, latest, "https://ubuntu.com/download/desktop")]
            : [];
    }

    private async Task<IEnumerable<NewerRelease>> CheckDirectory(DistroInfo d, CancellationToken ct)
    {
        var html = await http.GetStringAsync(d.ReleaseCheck!.Url, ct).ConfigureAwait(false);
        var latest = ParseDirectoryVersions(html).MaxBy(v => v, Comparer<string>.Create(CompareVersions));
        return latest is not null && CompareVersions(latest, d.Version) > 0
            ? [new NewerRelease(d.Id, d.Name, d.Version, latest, d.Website)]
            : [];
    }

    private async Task<IEnumerable<NewerRelease>> CheckPop(DistroInfo d, CancellationToken ct)
    {
        var baseUrl = d.ReleaseCheck!.Url.TrimEnd('/') + "/";
        var result = new List<NewerRelease>();

        // Newer ISO builds of the current version (a rebuild can ship a newer kernel).
        foreach (var e in d.Editions.Where(e => e.IsoBuild is not null))
        {
            var build = await PopBuild(baseUrl, d.Version, e.Id, ct).ConfigureAwait(false);
            if (build is { } b && b > e.IsoBuild)
                result.Add(new NewerRelease(d.Id, d.Name, $"{d.Version} {e.Name} build {e.IsoBuild}", $"{d.Version} {e.Name} build {b}", d.Website));
        }

        // Newer LTS versions (Pop!_OS follows Ubuntu's YY.04 LTS numbering).
        var year = int.Parse(d.Version.Split('.')[0]);
        var channel = d.Editions.First(e => e.SelectWhen is null).Id;
        for (var y = year + 2; y <= year + 4; y += 2)
        {
            var v = $"{y}.04";
            if (await PopBuild(baseUrl, v, channel, ct).ConfigureAwait(false) is not null)
                result.Add(new NewerRelease(d.Id, d.Name, d.Version, v, d.Website));
        }
        return result;
    }

    private async Task<int?> PopBuild(string baseUrl, string version, string channel, CancellationToken ct)
    {
        using var resp = await http.GetAsync($"{baseUrl}{version}/{channel}", ct).ConfigureAwait(false);
        if (!resp.IsSuccessStatusCode) return null;
        var body = await resp.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
        if (string.IsNullOrWhiteSpace(body)) return null; // the API returns an empty body for unknown versions
        using var doc = JsonDocument.Parse(body);
        return doc.RootElement.TryGetProperty("build", out var b) && int.TryParse(b.GetString(), out var n) ? n : null;
    }

    // ---- Parsing (public for tests) --------------------------------------------------------

    /// <summary>Latest supported version in Ubuntu's meta-release file, e.g. "26.04.1".</summary>
    public static string? ParseUbuntuMetaRelease(string text)
    {
        string? best = null;
        foreach (var block in text.Replace("\r", "").Split("\n\n"))
        {
            var version = UbuntuVersionRegex().Match(block);
            if (!version.Success || !block.Contains("Supported: 1", StringComparison.Ordinal)) continue;
            var v = version.Groups[1].Value;
            if (best is null || CompareVersions(v, best) > 0) best = v;
        }
        return best;
    }

    public static IEnumerable<string> ParseDirectoryVersions(string html) =>
        DirectoryRegex().Matches(html).Select(m => m.Groups[1].Value).Distinct();

    /// <summary>Compares dotted versions numerically, ignoring suffixes like " LTS": 26.04.1 &gt; 26.04, 23 &gt; 22.3.</summary>
    public static int CompareVersions(string a, string b)
    {
        static int[] Parts(string s) => VersionPrefixRegex().Match(s).Value.Split('.', StringSplitOptions.RemoveEmptyEntries).Select(int.Parse).ToArray();
        var x = Parts(a);
        var y = Parts(b);
        for (var i = 0; i < Math.Max(x.Length, y.Length); i++)
        {
            var c = (i < x.Length ? x[i] : 0).CompareTo(i < y.Length ? y[i] : 0);
            if (c != 0) return c;
        }
        return 0;
    }

    [GeneratedRegex(@"^Version:\s*([0-9][0-9.]*)", RegexOptions.Multiline)]
    private static partial Regex UbuntuVersionRegex();

    [GeneratedRegex(@"href=""([0-9]+(?:\.[0-9]+)?)/""")]
    private static partial Regex DirectoryRegex();

    [GeneratedRegex(@"^[0-9][0-9.]*")]
    private static partial Regex VersionPrefixRegex();
}
