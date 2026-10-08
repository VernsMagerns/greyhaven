using System.Text.RegularExpressions;

namespace GreyHaven.Core.Apps;

/// <summary>Where an installed app was found.</summary>
public enum AppSource { Installed, Store, Steam, Epic }

/// <summary>
/// One entry as Windows lists it, before filtering (part of the raw scan). Only names and IDs are
/// read: no install folders, versions or file paths. The Windows user name is replaced with
/// "&lt;user&gt;" (some apps put it in their name).
/// </summary>
public sealed record RawApp
{
    public required string Name { get; init; }
    public string? Publisher { get; init; }
    public AppSource Source { get; init; }
    /// <summary>Steam app ID, Epic catalog namespace, or Microsoft Store package name (before the first "_").</summary>
    public string? Id { get; init; }
    /// <summary>Registry SystemComponent = 1: hidden from Windows' own list.</summary>
    public bool SystemComponent { get; init; }
    /// <summary>Registry ReleaseType, e.g. "Update" or "Hotfix".</summary>
    public string? ReleaseType { get; init; }
    /// <summary>Registry ParentKeyName is set: an update or add-on of another entry.</summary>
    public bool HasParent { get; init; }
}

/// <summary>An app the person installed, after filtering out drivers, runtimes, updates and Windows parts.</summary>
public sealed record InstalledApp(string Name, string? Publisher, AppSource Source, string? Id)
{
    /// <summary>The tidied name used for matching (see <see cref="AppNames.Normalize"/>).</summary>
    public string Key => AppNames.Normalize(Name);
}

public static partial class AppNames
{
    /// <summary>
    /// Lower case, without ®/™/©, bracketed notes like "(x64)", trailing version numbers and
    /// "x64"/"64-bit", and single spaces. "Mozilla Firefox (x64 en-US)" → "mozilla firefox".
    /// </summary>
    public static string Normalize(string name)
    {
        var s = name.Replace("®", "").Replace("™", "").Replace("©", "");
        s = Brackets().Replace(s, " ");
        s = TrailingVersion().Replace(s, "");
        s = TrailingArch().Replace(s, "");
        s = Spaces().Replace(s, " ").Trim().ToLowerInvariant();
        return s;
    }

    [GeneratedRegex(@"\([^)]*\)|\[[^\]]*\]")] private static partial Regex Brackets();
    [GeneratedRegex(@"(\s+[-–]?\s*v?\d+(\.\d+)+[a-z0-9.\- ]*)$", RegexOptions.IgnoreCase)] private static partial Regex TrailingVersion();
    [GeneratedRegex(@"\s+(x64|x86|64-bit|32-bit)$", RegexOptions.IgnoreCase)] private static partial Regex TrailingArch();
    [GeneratedRegex(@"\s+")] private static partial Regex Spaces();
}

/// <summary>Turns the raw list into the person's apps: no updates, runtimes, drivers or Windows parts, no duplicates.</summary>
public static partial class AppInventory
{
    /// <summary>
    /// Tidied names that are never apps a person chose: runtimes, redistributables, drivers,
    /// updates and other plumbing. Kept short and specific; anything not listed is shown.
    /// </summary>
    private static readonly Regex[] Ignore =
    [
        new(@"^microsoft visual c\+\+"), new(@"^microsoft \.net"), new(@"^\.net "), new(@"^microsoft asp\.net"),
        new(@"^microsoft windows desktop runtime"), new(@"^windows software development kit"), new(@"^windows sdk"),
        new(@"^microsoft edge webview2"), new(@"^microsoft update health tools"), new(@"^update for "), new(@"^security update for "),
        new(@"^hotfix for "), new(@"\bkb\d{6,}\b"), new(@"^microsoft gameinput"), new(@"^directx"), new(@"^microsoft xna"),
        new(@"^vulkan run ?time"), new(@"^openal$"), new(@"^java .*update"), new(@"^python \d.*\b(launcher|executables|core|documentation|test suite|pip|tcl|utility|development|add to path)\b"),
        new(@"\bdriver\b"), new(@"\bdrivers\b"), new(@"^realtek "), new(@"^intel\b.*\b(management engine|chipset|serial io|rapid storage|graphics|wireless|bluetooth|optane)"),
        new(@"^amd (chipset|software|ryzen master driver)"), new(@"^nvidia (physx|graphics|hd audio|usbc|frameview|broadcast)"),
        new(@"^nvidia (rtx )?voice"), new(@"redistributable"), new(@"\bruntime\b"), new(@"^steamworks common redistributables$"),
        new(@"^microsoft (office|365) .*(proofing|language pack|mui)"), new(@"^windows pc health check$"),
    ];

    /// <summary>Microsoft Store packages that are parts of Windows or libraries, not apps a person chose.</summary>
    private static readonly Regex[] IgnoreStore =
    [
        new(@"^Microsoft\.(VCLibs|NET\.|UI\.Xaml|WindowsAppRuntime|Services\.Store|DirectX|Windows\.|Win32WebViewHost|DesktopAppInstaller|StorePurchaseApp|WindowsStore|SecHealthUI|LanguageExperiencePack|Winget|WidgetsPlatformRuntime|WebMediaExtensions|WebpImageExtension|HEIFImageExtension|VP9VideoExtensions|HEVCVideoExtension|RawImageExtension|AV1VideoExtension|AVCEncoderVideoExtension|MPEG2VideoExtension|ApplicationCompatibilityEnhancements|OneDriveSync|XboxIdentityProvider|XboxGameOverlay|XboxGamingOverlay|XboxSpeechToTextOverlay|Xbox\.TCUI|GamingServices|StartExperiencesApp|CrossDevice|Edge\.GameAssist|MicrosoftEdge)", RegexOptions.IgnoreCase),
        new(@"^(windows\.|MicrosoftWindows\.|NcsiUwpApp|Microsoft\.AAD|Microsoft\.AccountsControl|Microsoft\.LockApp|Microsoft\.ECApp|Microsoft\.CredDialogHost|Microsoft\.BioEnrollment)", RegexOptions.IgnoreCase),
        new(@"^(RealtekSemiconductorCorp|NVIDIACorp\.NVIDIAControlPanel|AdvancedMicroDevicesInc|AppUp\.Intel|DolbyLaboratories|WavesAudio)", RegexOptions.IgnoreCase),
    ];

    /// <summary>Microsoft Store apps from Microsoft that people choose (everything else from Microsoft comes with Windows).</summary>
    private static readonly HashSet<string> MicrosoftStoreApps = new(StringComparer.OrdinalIgnoreCase)
    {
        "Microsoft.GamingApp", "Microsoft.4297127D64EC6", "Microsoft.MinecraftUWP", "Microsoft.MinecraftEducationEdition",
    };

    public static bool IsIgnored(RawApp a)
    {
        if (a.Source == AppSource.Store && a.Id is { } msId && !MicrosoftStoreApps.Contains(msId) &&
            (msId.StartsWith("Microsoft.", StringComparison.OrdinalIgnoreCase) || msId.StartsWith("MicrosoftCorporationII.", StringComparison.OrdinalIgnoreCase) ||
             msId.StartsWith("MicrosoftWindows.", StringComparison.OrdinalIgnoreCase) || msId.StartsWith("Clipchamp.", StringComparison.OrdinalIgnoreCase)))
            return true;
        if (a.SystemComponent || a.HasParent) return true;
        if (a.ReleaseType is { } t && (t.Contains("update", StringComparison.OrdinalIgnoreCase) || t.Contains("hotfix", StringComparison.OrdinalIgnoreCase))) return true;
        if (a.Source == AppSource.Store && a.Id is { } id && IgnoreStore.Any(r => r.IsMatch(id))) return true;
        if (a.Name.StartsWith("ms-resource:", StringComparison.OrdinalIgnoreCase) || a.Name.StartsWith('@')) return true; // unresolved Store name
        var key = AppNames.Normalize(a.Name);
        return key.Length == 0 || Ignore.Any(r => r.IsMatch(key));
    }

    public static IReadOnlyList<InstalledApp> Build(IEnumerable<RawApp> raw)
    {
        var apps = new List<InstalledApp>();
        foreach (var a in raw.Where(a => !IsIgnored(a)))
        {
            // Steam games show up both in Steam's files and in Windows' list; keep one.
            if (a.Source == AppSource.Steam || a.Id is not null)
            {
                if (apps.Any(x => x.Source == a.Source && x.Id == a.Id && a.Id is not null)) continue;
            }
            var app = new InstalledApp(a.Name.Trim(), string.IsNullOrWhiteSpace(a.Publisher) ? null : a.Publisher.Trim(), a.Source, a.Id);
            if (apps.Any(x => x.Key == app.Key && x.Source == app.Source)) continue;
            apps.Add(app);
        }
        // A Steam game found in Steam's own files wins over its Windows uninstall entry with the same name.
        apps.RemoveAll(x => x.Source == AppSource.Installed && apps.Any(s => s.Source == AppSource.Steam && s.Key == x.Key));
        return apps.OrderBy(a => a.Key, StringComparer.Ordinal).ToList();
    }
}

/// <summary>
/// Reads Steam's text files (libraryfolders.vdf, appmanifest_*.acf): quoted keys and values, and
/// braces for nested sections. Only what GreyHaven needs is kept: library folders, app ID, name.
/// </summary>
public static class SteamFiles
{
    public sealed class Node : Dictionary<string, object>
    {
        public Node() : base(StringComparer.OrdinalIgnoreCase) { }
        public string? Text(string key) => TryGetValue(key, out var v) ? v as string : null;
        public Node? Child(string key) => TryGetValue(key, out var v) ? v as Node : null;
    }

    public static Node Parse(string text)
    {
        var tokens = Tokens(text).GetEnumerator();
        return ParseBody(tokens);
    }

    private static Node ParseBody(IEnumerator<string> t)
    {
        var node = new Node();
        while (t.MoveNext())
        {
            var key = t.Current;
            if (key == "}") break;
            if (!t.MoveNext()) break;
            node[key] = t.Current == "{" ? ParseBody(t) : t.Current;
        }
        return node;
    }

    private static IEnumerable<string> Tokens(string s)
    {
        for (var i = 0; i < s.Length; i++)
        {
            var c = s[i];
            if (c is '{' or '}') { yield return c.ToString(); continue; }
            if (c == '/' && i + 1 < s.Length && s[i + 1] == '/') { while (i < s.Length && s[i] != '\n') i++; continue; }
            if (c != '"') continue;
            var sb = new System.Text.StringBuilder();
            for (i++; i < s.Length && s[i] != '"'; i++)
            {
                if (s[i] == '\\' && i + 1 < s.Length) { i++; sb.Append(s[i]); }
                else sb.Append(s[i]);
            }
            yield return sb.ToString();
        }
    }

    /// <summary>Library folders listed in libraryfolders.vdf.</summary>
    public static IReadOnlyList<string> LibraryFolders(string vdf)
    {
        var root = Parse(vdf).Child("libraryfolders");
        if (root is null) return [];
        return root.Values.OfType<Node>().Select(n => n.Text("path")).OfType<string>().Where(p => p.Length > 0).ToList();
    }

    /// <summary>App ID and name from an appmanifest_*.acf file, or null.</summary>
    public static (string AppId, string Name)? Manifest(string acf)
    {
        var state = Parse(acf).Child("AppState");
        return state?.Text("appid") is { } id && state.Text("name") is { } name ? (id, name) : null;
    }

    /// <summary>Steam tools that aren't games: Proton, runtimes, redistributables.</summary>
    public static bool IsTool(string appId, string name) =>
        appId is "228980" or "1070560" or "1391110" or "1628350" or "1493710" ||
        name.StartsWith("Proton", StringComparison.OrdinalIgnoreCase) ||
        name.StartsWith("Steam Linux Runtime", StringComparison.OrdinalIgnoreCase) ||
        name.StartsWith("Steamworks", StringComparison.OrdinalIgnoreCase);
}
