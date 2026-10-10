using System.Text.Json;
using GreyHaven.Core;

namespace GreyHaven.Compat.Online;

/// <summary>A newer GreyHaven release: its version and the page to get it from.</summary>
public sealed record AvailableUpdate(string Version, string PageUrl);

/// <summary>
/// Notify-only update check: reads the latest published release of GreyHaven on GitHub and says
/// whether it's newer than this copy. It never downloads or installs anything; the person
/// decides. Sends nothing about the PC (GitHub sees the request, like any website). Pre-releases
/// (such as the rolling test build) are ignored, because GitHub's "latest" leaves them out.
/// </summary>
public sealed class UpdateChecker(HttpClient http)
{
    public static string LatestReleaseApi => $"https://api.github.com/repos/{GreyHavenInfo.GitHubRepository}/releases/latest";

    /// <summary>The newer release, or null if this copy is up to date, the check failed, or the repository isn't public yet.</summary>
    public async Task<AvailableUpdate?> CheckAsync(string currentVersion, CancellationToken ct = default)
    {
        if (!GreyHavenInfo.CanLinkToRepository) return null;
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, LatestReleaseApi);
            request.Headers.Accept.ParseAdd("application/vnd.github+json");
            using var response = await http.SendAsync(request, ct).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode) return null;
            using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false));
            return Newer(doc.RootElement, currentVersion);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException) { return null; }
    }

    /// <summary>Reads GitHub's release JSON (tag "v1.2.0" or "1.2.0"); public for tests.</summary>
    public static AvailableUpdate? Newer(JsonElement release, string currentVersion)
    {
        if (release.TryGetProperty("draft", out var d) && d.GetBoolean()) return null;
        if (release.TryGetProperty("prerelease", out var p) && p.GetBoolean()) return null;
        var tag = release.GetProperty("tag_name").GetString() ?? "";
        var page = release.GetProperty("html_url").GetString() ?? "";
        if (!page.StartsWith($"https://github.com/{GreyHavenInfo.GitHubRepository}/releases/", StringComparison.Ordinal)) return null;
        return Parse(tag) is { } latest && Parse(currentVersion) is { } current && latest > current
            ? new AvailableUpdate(latest.ToString(3), page)
            : null;
    }

    private static Version? Parse(string text)
    {
        var t = text.Trim().TrimStart('v', 'V');
        var dash = t.IndexOfAny(['-', '+']);
        if (dash >= 0) t = t[..dash];
        return Version.TryParse(t, out var v) ? new Version(v.Major, v.Minor, Math.Max(0, v.Build)) : null;
    }
}
