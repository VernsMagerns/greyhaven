using GreyHaven.Compat.Data;
using GreyHaven.Core.Apps;
using GreyHaven.Core.Compatibility;
using GreyHaven.Core.Hardware;

namespace GreyHaven.Ratings;

/// <summary>
/// Rates installed apps for Linux. For each app, the first source that knows it wins:
/// <list type="number">
///   <item>GreyHaven's hand-checked list (data/apps/windows-apps.yaml).</item>
///   <item>Are We Anti-Cheat Yet, for games with anti-cheat (by Steam ID, Epic ID or exact name).</item>
///   <item>ProtonDB reports, for other Steam games (at least 5 reports in two years).</item>
///   <item>Otherwise: Not sure. GreyHaven never guesses.</item>
/// </list>
/// Everything happens on this PC; app names are never sent anywhere.
/// </summary>
public static class AppRater
{
    public const int MinimumReports = 5;

    public static IReadOnlyList<RatedApp> Rate(IReadOnlyList<InstalledApp> apps, AppCatalog data) =>
        apps.Select(a => Rate(a, data)).OrderBy(a => a.Category).ThenBy(a => a.Name, StringComparer.OrdinalIgnoreCase).ToList();

    public static RatedApp Rate(InstalledApp app, AppCatalog data)
    {
        if (data.WindowsApps.FirstOrDefault(w => w.Matches(app)) is { } known) return FromCurated(app, known, data);
        if (FindAntiCheat(app, data) is { } game) return FromAntiCheat(app, game);
        if (app.Source == AppSource.Steam && app.Id is { } id && data.ProtonDb.TryGetValue(id, out var p) && p.Reports >= MinimumReports)
            return FromProtonDb(app, p, data.ProtonDbDump);
        var isGame = app.Source is AppSource.Steam or AppSource.Epic;
        return new RatedApp
        {
            App = app,
            Name = app.Name,
            Category = isGame ? "games" : "other",
            Verdict = AppVerdict.NotSure,
            Summary = app.Source switch
            {
                AppSource.Steam => "Too few Linux players have reported on this game to say. Try it: Steam on Linux can install it for you.",
                AppSource.Epic => "We don't know this game yet. Heroic Games Launcher can try it on Linux.",
                _ => "GreyHaven doesn't know this app yet. Search its website for a Linux version.",
            },
            Linux = app.Source switch
            {
                AppSource.Steam => data.LinuxApps.GetValueOrDefault("steam"),
                AppSource.Epic => data.LinuxApps.GetValueOrDefault("heroic"),
                _ => null,
            },
            MatchedBy = "none",
        };
    }

    private static RatedApp FromCurated(InstalledApp app, WindowsAppInfo w, AppCatalog data)
    {
        var linux = w.Linux is null ? null : data.LinuxApps.GetValueOrDefault(w.Linux);
        var verdict = w.Rating switch
        {
            "native" => AppVerdict.Works,
            "layer" => AppVerdict.WorksWithLayer,
            "web" => AppVerdict.UseWeb,
            "wontWork" => AppVerdict.WontWork,
            "notNeeded" => AppVerdict.NotNeeded,
            _ => AppVerdict.NotSure,
        };
        var summary = w.Summary ?? (verdict == AppVerdict.Works ? $"{w.Name} has a Linux version." : "");
        if (w.Note is not null) summary += " " + w.Note;
        return new RatedApp
        {
            App = app,
            Name = w.Name,
            Category = w.Category,
            Verdict = verdict,
            Summary = summary,
            Layer = w.Layer is null ? null : char.ToUpperInvariant(w.Layer[0]) + w.Layer[1..],
            Confidence = w.Confidence is null ? null : Compat.Rules.RuleSet.ParseConfidence(w.Confidence),
            Linux = linux,
            WebUrl = w.Web,
            Reason = w.Reason switch { "antiCheat" => "anti-cheat", "noLinuxVersion" => "no Linux version", _ => null },
            Alternatives = w.Alternatives.Select(a => new AppAlternative(
                a.App is null ? null : data.LinuxApps[a.App], a.Web?.Name, a.Web?.Url, a.Closeness, a.Note, Categories(a.Uses))).ToList(),
            Uses = Categories(w.Uses),
            Sources = w.Source is { Url: { } url } s ? [new Source(s.Title, url, SourceKind.Reference)] : [],
            MatchedBy = "curated",
        };
    }

    private static List<ComponentCategory> Categories(IEnumerable<string> names) =>
        names.Select(n => Enum.Parse<ComponentCategory>(n, ignoreCase: true)).ToList();

    private static AntiCheatGame? FindAntiCheat(InstalledApp app, AppCatalog data) => app switch
    {
        { Source: AppSource.Steam, Id: { } id } => data.AntiCheat.FirstOrDefault(g => g.Steam == id),
        { Source: AppSource.Epic, Id: { } ns } => data.AntiCheat.FirstOrDefault(g => g.EpicNamespace == ns),
        _ => null,
    } ?? data.AntiCheat.FirstOrDefault(g => AppNames.Normalize(g.Name) == app.Key);

    private static RatedApp FromAntiCheat(InstalledApp app, AntiCheatGame g)
    {
        var ac = g.AntiCheats.Count == 0 ? "its anti-cheat" : $"its anti-cheat ({string.Join(", ", g.AntiCheats)})";
        var (verdict, confidence, summary) = g.Status switch
        {
            "Supported" => (AppVerdict.WorksWithLayer, Confidence.High, $"Works with Proton, Steam's built-in helper for Windows games. The game's makers allow {ac} on Linux."),
            "Running" => (AppVerdict.WorksWithLayer, Confidence.Low, $"Runs with Proton, but {ac} isn't switched on for Linux, so online play may not work."),
            "Denied" => (AppVerdict.WontWork, (Confidence?)Confidence.High, $"The game's makers have chosen to block Linux with {ac}, so it won't start, even with Proton."),
            "Planned" => (AppVerdict.WontWork, Confidence.Medium, $"Linux support for {ac} is planned but not ready yet, so online play doesn't work."),
            _ => (AppVerdict.WontWork, Confidence.Medium, $"{Capital(ac)} doesn't work on Linux yet, so online play doesn't work."),
        };
        return new RatedApp
        {
            App = app,
            Name = app.Name,
            Category = "games",
            Verdict = verdict,
            Summary = summary,
            Layer = verdict == AppVerdict.WorksWithLayer ? "Proton" : null,
            Confidence = confidence,
            Reason = verdict == AppVerdict.WontWork ? "anti-cheat" : null,
            Sources = [new Source($"Are We Anti-Cheat Yet - {g.Name}", g.PageUrl, SourceKind.Community)],
            MatchedBy = "anticheat",
        };
    }

    private static string Capital(string s) => char.ToUpperInvariant(s[0]) + s[1..];

    private static RatedApp FromProtonDb(InstalledApp app, (int Reports, int Works, int WithoutTweaks) p, string? dump)
    {
        var (verdict, confidence, summary) = p.Works switch
        {
            >= 80 => (AppVerdict.WorksWithLayer, p.Reports >= 20 ? Confidence.High : Confidence.Medium,
                $"Works with Proton, Steam's built-in helper for Windows games: {p.Works}% of {p.Reports} recent Linux players say it runs." +
                (p.WithoutTweaks < 50 ? " Many changed a setting first." : "")),
            >= 50 => (AppVerdict.WorksWithLayer, Confidence.Low,
                $"Often runs with Proton, but only {p.Works}% of {p.Reports} recent Linux players say it works well."),
            _ => (AppVerdict.WontWork, p.Reports >= 20 ? Confidence.High : Confidence.Medium,
                $"Most Linux players say it doesn't run: only {p.Works}% of {p.Reports} recent reports say it works."),
        };
        return new RatedApp
        {
            App = app,
            Name = app.Name,
            Category = "games",
            Verdict = verdict,
            Summary = summary,
            Layer = verdict == AppVerdict.WorksWithLayer ? "Proton" : null,
            Confidence = confidence,
            Reason = verdict == AppVerdict.WontWork ? "player reports" : null,
            Sources = [new Source($"ProtonDB player reports{(dump is null ? "" : $" ({dump}, Open Database License)")}", $"https://www.protondb.com/app/{app.Id}", SourceKind.Community)],
            MatchedBy = "protondb",
        };
    }
}
