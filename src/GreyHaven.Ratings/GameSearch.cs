using GreyHaven.Compat.Data;
using GreyHaven.Core.Apps;

namespace GreyHaven.Ratings;

/// <summary>A game found by "Check a game": its rating (same rules as the apps list) and its ProtonDB page.</summary>
public sealed record GameMatch(RatedApp Rating, string? ProtonDbUrl);

/// <summary>
/// "Check a game": searches the games GreyHaven ships data for (ProtonDB's report dump and Are We
/// Anti-Cheat Yet), on this PC. Nothing is sent anywhere and protondb.com isn't fetched; results
/// link to ProtonDB's page for the game, opened in the browser.
/// </summary>
public sealed class GameSearch
{
    private sealed record Entry(string Name, string Key, string? SteamId, AntiCheatGame? AntiCheat, int Reports);

    private readonly AppCatalog _data;
    private readonly List<Entry> _games;

    public GameSearch(AppCatalog data)
    {
        _data = data;
        var bySteam = data.AntiCheat.Where(g => g.Steam is not null).GroupBy(g => g.Steam!).ToDictionary(g => g.Key, g => g.First());
        _games = data.ProtonDbTitles.Select(t => new Entry(t.Value, AppNames.Normalize(t.Value), t.Key, bySteam.GetValueOrDefault(t.Key),
                data.ProtonDb.TryGetValue(t.Key, out var p) ? p.Reports : 0))
            .Concat(data.AntiCheat.Where(g => g.Steam is null || !data.ProtonDbTitles.ContainsKey(g.Steam))
                .Select(g => new Entry(g.Name, AppNames.Normalize(g.Name), g.Steam, g, 0)))
            .ToList();
    }

    public const int MinimumLength = 2;

    /// <summary>
    /// Best matches first: the exact name, then names starting with the search, then names with
    /// every word in them; more-reported (more popular) games first within each.
    /// </summary>
    public IReadOnlyList<GameMatch> Search(string query, int max = 8)
    {
        var q = AppNames.Normalize(query ?? "");
        if (q.Length < MinimumLength) return [];
        var words = q.Split(' ');
        return _games
            .Select(g => (g, rank: g.Key == q ? 0 : g.Key.StartsWith(q, StringComparison.Ordinal) ? 1
                : words.All(w => g.Key.Contains(w, StringComparison.Ordinal)) ? 2 : -1))
            .Where(x => x.rank >= 0)
            .OrderBy(x => x.rank).ThenByDescending(x => x.g.AntiCheat is not null).ThenByDescending(x => x.g.Reports).ThenBy(x => x.g.Name, StringComparer.OrdinalIgnoreCase)
            .Take(max)
            .Select(x => Match(x.g))
            .ToList();
    }

    /// <summary>Every game with its rating and report count (for the design preview's search box).</summary>
    public IEnumerable<(GameMatch Match, int Reports, bool AntiCheat)> All() =>
        _games.Select(g => (Match(g), g.Reports, g.AntiCheat is not null));

    private GameMatch Match(Entry g)
    {
        var app = g.SteamId is { } id ? new InstalledApp(g.Name, null, AppSource.Steam, id)
            : g.AntiCheat?.EpicNamespace is { } ns ? new InstalledApp(g.Name, null, AppSource.Epic, ns)
            : new InstalledApp(g.Name, null, AppSource.Installed, null);
        var rated = AppRater.Rate(app, _data);
        // Unknown non-Steam games: the generic "doesn't know this app" line reads oddly for a game.
        if (rated.Verdict == AppVerdict.NotSure && app.Source == AppSource.Installed)
            rated = rated with { Category = "games", Summary = "GreyHaven doesn't have reports for this game yet." };
        return new GameMatch(rated, g.SteamId is null ? null : $"https://www.protondb.com/app/{g.SteamId}");
    }
}
