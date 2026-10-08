using System.Text.Json;

namespace GreyHaven.Media;

/// <summary>Where an installer comes from (data/distros.yaml, edition "download").</summary>
public sealed record InstallerSource
{
    public required string DistroName { get; init; }
    public string? File { get; init; }
    public string? Url { get; init; }
    public string? Checksums { get; init; }
    public string? Signature { get; init; }
    /// <summary>The public key itself (armored), not a path.</summary>
    public string? Key { get; init; }
    public string? KeyFingerprint { get; init; }
    /// <summary>Pop!_OS: System76's build API.</summary>
    public string? Api { get; init; }
}

public enum CheckKind { SignedChecksum, ChecksumOverHttps }

/// <param name="Sha256">The installer's expected SHA-256, from a source GreyHaven checked.</param>
public sealed record ResolvedInstaller(string Url, string FileName, string Sha256, long? Bytes, CheckKind Check);

public sealed class InstallerCheckException(string message) : Exception(message);

/// <summary>Finds the installer's address and expected fingerprint, refusing anything that doesn't check out.</summary>
public static class InstallerResolver
{
    public static async Task<ResolvedInstaller> ResolveAsync(HttpClient http, InstallerSource s, CancellationToken ct)
    {
        if (s.Api is { } api)
        {
            RequireHttps(api);
            using var doc = JsonDocument.Parse(await http.GetStringAsync(api, ct).ConfigureAwait(false));
            var r = doc.RootElement;
            var url = r.GetProperty("url").GetString() ?? throw new InstallerCheckException("System76's download information has no address.");
            var sha = r.GetProperty("sha_sum").GetString() ?? "";
            RequireHttps(url);
            if (new Uri(url).Host != "iso.pop-os.org") throw new InstallerCheckException("System76's download information points somewhere unexpected.");
            if (sha.Length != 64 || !sha.All(Uri.IsHexDigit)) throw new InstallerCheckException("System76's download information has no valid checksum.");
            long? size = r.TryGetProperty("size", out var z) && z.TryGetInt64(out var n) ? n : null;
            return new ResolvedInstaller(url, Path.GetFileName(new Uri(url).LocalPath), sha.ToLowerInvariant(), size, CheckKind.ChecksumOverHttps);
        }

        if (s.Url is null || s.File is null || s.Checksums is null || s.Signature is null || s.Key is null || s.KeyFingerprint is null)
            throw new InstallerCheckException($"GreyHaven doesn't know how to check {s.DistroName}'s download.");
        RequireHttps(s.Url); RequireHttps(s.Checksums); RequireHttps(s.Signature);
        var list = await http.GetByteArrayAsync(s.Checksums, ct).ConfigureAwait(false);
        var sig = await http.GetByteArrayAsync(s.Signature, ct).ConfigureAwait(false);
        if (!Signatures.Verify(list, sig, s.Key, s.KeyFingerprint))
            throw new InstallerCheckException($"The list of fingerprints for {s.DistroName} isn't signed by {s.DistroName}'s key. GreyHaven won't use it.");
        var expected = Checksums.Find(System.Text.Encoding.UTF8.GetString(list), s.File)
                       ?? throw new InstallerCheckException($"{s.DistroName}'s signed list doesn't include {s.File}.");
        return new ResolvedInstaller(s.Url, s.File, expected, null, CheckKind.SignedChecksum);
    }

    private static void RequireHttps(string url)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var u) || u.Scheme != Uri.UriSchemeHttps)
            throw new InstallerCheckException("GreyHaven only downloads over a secure (https) connection.");
    }
}

/// <summary>Progress for the UI: what's happening and how far along (0–1).</summary>
public sealed record MediaProgress(string Stage, long Done, long? Total)
{
    public double? Fraction => Total is > 0 ? Math.Clamp((double)Done / Total.Value, 0, 1) : null;
}

/// <summary>Downloads the installer to a folder, resuming a partial download, and checks its fingerprint.</summary>
public static class InstallerDownloader
{
    public const string Downloading = "Downloading";
    public const string Checking = "Checking the file";

    /// <summary>
    /// Returns the path of a checked file. An existing complete file is checked, not downloaded
    /// again. A file whose fingerprint doesn't match is deleted, and an exception says so.
    /// </summary>
    public static async Task<string> DownloadAsync(HttpClient http, ResolvedInstaller installer, string folder, IProgress<MediaProgress>? progress, CancellationToken ct)
    {
        Directory.CreateDirectory(folder);
        var path = Path.Combine(folder, installer.FileName);
        var part = path + ".part";
        if (!File.Exists(path))
        {
            long have = File.Exists(part) ? new FileInfo(part).Length : 0;
            using var request = new HttpRequestMessage(HttpMethod.Get, installer.Url);
            if (have > 0) request.Headers.Range = new System.Net.Http.Headers.RangeHeaderValue(have, null);
            using var response = await http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct).ConfigureAwait(false);
            if (have > 0 && response.StatusCode != System.Net.HttpStatusCode.PartialContent) have = 0; // server ignored the range: start again
            response.EnsureSuccessStatusCode();
            var total = (response.Content.Headers.ContentLength is { } len ? len + have : installer.Bytes);
            await using (var file = new FileStream(part, have > 0 ? FileMode.Append : FileMode.Create, FileAccess.Write, FileShare.None, 1 << 20, useAsync: true))
            await using (var body = await response.Content.ReadAsStreamAsync(ct).ConfigureAwait(false))
            {
                var buffer = new byte[1 << 20];
                long done = have;
                int n;
                while ((n = await body.ReadAsync(buffer, ct).ConfigureAwait(false)) > 0)
                {
                    await file.WriteAsync(buffer.AsMemory(0, n), ct).ConfigureAwait(false);
                    done += n;
                    progress?.Report(new MediaProgress(Downloading, done, total));
                }
            }
            File.Move(part, path, overwrite: true);
        }

        long size = new FileInfo(path).Length;
        await using (var check = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 1 << 20, useAsync: true))
        {
            var sha = await Checksums.Sha256Async(check, null, new Progress<long>(d => progress?.Report(new MediaProgress(Checking, d, size))), ct).ConfigureAwait(false);
            if (sha == installer.Sha256) return path;
        }
        File.Delete(path);
        throw new InstallerCheckException("The downloaded file isn't exactly the official one, so GreyHaven deleted it. Try again; if it happens again, your internet connection may be changing files.");
    }
}
