using System.IO.Compression;
using System.Text.Json;
using System.Text.RegularExpressions;
using GreyHaven.Core.Apps;
using GreyHaven.Core.Hardware;

namespace GreyHaven.Compat.Data;

/// <summary>
/// App compatibility data (data/apps): hand-checked Windows apps and Linux apps (CC-BY-4.0), Are
/// We Anti-Cheat Yet's game list (MIT) and a summary of ProtonDB's reports (ODbL).
/// </summary>
public sealed partial class AppCatalog
{
    public IReadOnlyList<WindowsAppInfo> WindowsApps { get; init; } = [];
    public IReadOnlyDictionary<string, LinuxApp> LinuxApps { get; init; } = new Dictionary<string, LinuxApp>();
    public IReadOnlyList<AntiCheatGame> AntiCheat { get; init; } = [];
    /// <summary>Steam app ID → (reports, % works, % without tweaks).</summary>
    public IReadOnlyDictionary<string, (int Reports, int Works, int WithoutTweaks)> ProtonDb { get; init; } = new Dictionary<string, (int, int, int)>();
    public string? ProtonDbDump { get; init; }

    private static readonly string[] Verdicts = ["native", "layer", "web", "wontWork", "notNeeded"];
    private static readonly string[] Closeness = ["same", "close", "partial"];

    public static AppCatalog Load(string dir)
    {
        var apps = Path.Combine(dir, "apps");
        if (!Directory.Exists(apps)) return new AppCatalog();
        var windows = Yaml.Load<WindowsAppFile>(Path.Combine(apps, "windows-apps.yaml")).Apps;
        var linux = Yaml.Load<LinuxAppFile>(Path.Combine(apps, "linux-apps.yaml")).LinuxApps;

        using var ac = JsonDocument.Parse(File.ReadAllText(Path.Combine(apps, "anticheat.json")));
        var games = ac.RootElement.GetProperty("games").EnumerateArray().Select(g => new AntiCheatGame(
            g.GetProperty("name").GetString()!,
            Str(g, "slug"),
            g.GetProperty("status").GetString()!,
            g.GetProperty("anticheats").EnumerateArray().Select(x => x.GetString()!).ToList(),
            Str(g, "steam"), Str(g, "epic"), Str(g, "epicNamespace"), Str(g, "reference"))).ToList();

        using var gz = new GZipStream(File.OpenRead(Path.Combine(apps, "protondb-summary.json.gz")), CompressionMode.Decompress);
        using var pdb = JsonDocument.Parse(gz);
        var proton = pdb.RootElement.GetProperty("games").EnumerateObject().ToDictionary(
            p => p.Name, p => (p.Value[0].GetInt32(), p.Value[1].GetInt32(), p.Value[2].GetInt32()));

        var catalog = new AppCatalog
        {
            WindowsApps = windows,
            LinuxApps = linux.ToDictionary(l => l.Id, l => l.ToLinuxApp()),
            AntiCheat = games,
            ProtonDb = proton,
            ProtonDbDump = pdb.RootElement.TryGetProperty("dump", out var d) ? d.GetString() : null,
        };
        catalog.Validate();
        return catalog;
    }

    private static string? Str(JsonElement e, string name) =>
        e.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;

    private void Validate()
    {
        var errors = new List<string>();
        foreach (var l in LinuxApps.Values)
        {
            if (!l.Source.StartsWith("https://", StringComparison.Ordinal)) errors.Add($"{l.Id}: source must be https.");
            if (l.Flathub is null && l.Snap is null && l.Apt is null && l.Website is null && l.Web is null) errors.Add($"{l.Id}: say where to get it.");
            if (l.Flathub is { } f && !FlathubId().IsMatch(f)) errors.Add($"{l.Id}: '{f}' isn't a Flathub app ID.");
        }
        foreach (var a in WindowsApps)
        {
            if (!Verdicts.Contains(a.Rating)) errors.Add($"{a.Id}: unknown rating '{a.Rating}'.");
            if (a.Match.Names.Count + a.Match.StartsWith.Count + a.Match.Store.Count + a.Match.Steam.Count == 0) errors.Add($"{a.Id}: nothing to match.");
            if (a.Source?.Url is not { } url || !url.StartsWith("https://", StringComparison.Ordinal)) errors.Add($"{a.Id}: needs an https source.");
            if (a.Rating == "native" && (a.Linux is null || !LinuxApps.ContainsKey(a.Linux))) errors.Add($"{a.Id}: native needs a linux app from linux-apps.yaml.");
            if (a.Rating == "web" && a.Web is null) errors.Add($"{a.Id}: web needs a web address.");
            if (a.Rating == "wontWork" && a.Reason is not ("noLinuxVersion" or "antiCheat")) errors.Add($"{a.Id}: wontWork needs reason noLinuxVersion or antiCheat.");
            if (a.Rating == "layer" && a.Layer is null) errors.Add($"{a.Id}: layer needs a layer.");
            if (a.Rating != "native" && a.Summary is null) errors.Add($"{a.Id}: needs a summary.");
            foreach (var alt in a.Alternatives)
            {
                if (alt.App is not null && !LinuxApps.ContainsKey(alt.App)) errors.Add($"{a.Id}: alternative '{alt.App}' isn't in linux-apps.yaml.");
                if (alt.App is null && alt.Web?.Url is null) errors.Add($"{a.Id}: an alternative needs an app or a web address.");
                if (!Closeness.Contains(alt.Closeness)) errors.Add($"{a.Id}: closeness must be same, close or partial.");
            }
            foreach (var u in a.Uses.Concat(a.Alternatives.SelectMany(x => x.Uses)))
                if (!Enum.TryParse<ComponentCategory>(u, ignoreCase: true, out _)) errors.Add($"{a.Id}: unknown hardware '{u}'.");
        }
        var dupes = WindowsApps.GroupBy(a => a.Id).Where(g => g.Count() > 1).Select(g => g.Key).ToList();
        if (dupes.Count > 0) errors.Add($"Duplicate app ids: {string.Join(", ", dupes)}.");
        if (errors.Count > 0) throw new InvalidDataException("data/apps: " + string.Join(" ", errors));
    }

    [GeneratedRegex(@"^[A-Za-z][A-Za-z0-9_-]*(\.[A-Za-z0-9_-]+){2,}$")]
    private static partial Regex FlathubId();
}

public sealed record AntiCheatGame(string Name, string? Slug, string Status, IReadOnlyList<string> AntiCheats, string? Steam, string? Epic, string? EpicNamespace, string? Reference)
{
    public string PageUrl => Slug is null ? "https://areweanticheatyet.com/" : $"https://areweanticheatyet.com/game/{Slug}";
}

public sealed class WindowsAppFile { public List<WindowsAppInfo> Apps { get; set; } = []; }
public sealed class LinuxAppFile { public List<LinuxAppInfo> LinuxApps { get; set; } = []; }

public sealed class WindowsAppInfo
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string Category { get; set; } = "other";
    public AppMatch Match { get; set; } = new();
    public string Rating { get; set; } = "";
    public string? Linux { get; set; }
    public string? Layer { get; set; }
    public string? Confidence { get; set; }
    public string? Web { get; set; }
    public string? Reason { get; set; }
    public string? Summary { get; set; }
    public string? Note { get; set; }
    public List<AlternativeInfo> Alternatives { get; set; } = [];
    public List<string> Uses { get; set; } = [];
    public SourceLink? Source { get; set; }

    /// <summary>Matches by store ID first, then the tidied name.</summary>
    public bool Matches(InstalledApp app)
    {
        if (app.Id is { } id)
        {
            if (app.Source == AppSource.Store && Match.Store.Contains(id, StringComparer.OrdinalIgnoreCase)) return true;
            if (app.Source == AppSource.Steam && Match.Steam.Contains(id)) return true;
        }
        var key = app.Key;
        return Match.Names.Contains(key, StringComparer.Ordinal) || Match.StartsWith.Any(p => key.StartsWith(p, StringComparison.Ordinal));
    }
}

public sealed class AppMatch
{
    public List<string> Names { get; set; } = [];
    public List<string> StartsWith { get; set; } = [];
    public List<string> Store { get; set; } = [];
    public List<string> Steam { get; set; } = [];
}

public sealed class AlternativeInfo
{
    public string? App { get; set; }
    public WebLink? Web { get; set; }
    public string Closeness { get; set; } = "";
    public string Note { get; set; } = "";
    public List<string> Uses { get; set; } = [];
}

public sealed class WebLink
{
    public string Name { get; set; } = "";
    public string? Url { get; set; }
}

public sealed class LinuxAppInfo
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string What { get; set; } = "";
    public string? Flathub { get; set; }
    public bool FlathubVerified { get; set; }
    public string? Snap { get; set; }
    public bool Classic { get; set; }
    public string? Apt { get; set; }
    public string? Website { get; set; }
    public string? Web { get; set; }
    public string? Note { get; set; }
    public List<string> Preinstalled { get; set; } = [];
    public string Source { get; set; } = "";

    public LinuxApp ToLinuxApp() => new()
    {
        Id = Id, Name = Name, What = What, Flathub = Flathub, FlathubVerified = FlathubVerified, Snap = Snap, Classic = Classic,
        Apt = Apt, Website = Website, Web = Web, Note = Note, Preinstalled = Preinstalled, Source = Source,
    };
}
