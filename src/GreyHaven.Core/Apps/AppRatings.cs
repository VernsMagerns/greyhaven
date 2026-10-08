using GreyHaven.Core.Compatibility;
using GreyHaven.Core.Hardware;

namespace GreyHaven.Core.Apps;

/// <summary>How a Windows app does on Linux, in the words people see.</summary>
public enum AppVerdict
{
    /// <summary>There's a Linux version.</summary>
    Works,
    /// <summary>The Windows version runs with a compatibility layer (Proton, Wine, Lutris...).</summary>
    WorksWithLayer,
    /// <summary>No Linux app; use its website.</summary>
    UseWeb,
    /// <summary>No Linux version and it won't run; see the alternatives.</summary>
    WontWork,
    /// <summary>Linux does this job itself (e.g. driver tools).</summary>
    NotNeeded,
    /// <summary>GreyHaven doesn't know this app (or there are too few reports).</summary>
    NotSure,
}

/// <summary>A Linux app GreyHaven may suggest, and where to get it on each distro.</summary>
public sealed record LinuxApp
{
    public required string Id { get; init; }
    public required string Name { get; init; }
    /// <summary>One plain line about what it does.</summary>
    public required string What { get; init; }
    public string? Flathub { get; init; }
    public bool FlathubVerified { get; init; }
    public string? Snap { get; init; }
    public bool Classic { get; init; }
    public string? Apt { get; init; }
    public string? Website { get; init; }
    /// <summary>For web apps (e.g. Photopea): the address to open.</summary>
    public string? Web { get; init; }
    public string? Note { get; init; }
    public IReadOnlyList<string> Preinstalled { get; init; } = [];
    public required string Source { get; init; }
}

/// <param name="Closeness">same | close | partial: how close it is to what the person uses now.</param>
/// <param name="Uses">Hardware it depends on, e.g. a capture card (Webcam).</param>
public sealed record AppAlternative(LinuxApp? App, string? WebName, string? WebUrl, string Closeness, string Note, IReadOnlyList<ComponentCategory> Uses);

public sealed record RatedApp
{
    public required InstalledApp App { get; init; }
    /// <summary>The app's usual name (from GreyHaven's data), or its name as Windows lists it.</summary>
    public required string Name { get; init; }
    /// <summary>games, creative, office, communication, streaming, dictation, cloud, browsers, media, security, utilities, development, hardware, other.</summary>
    public required string Category { get; init; }
    public required AppVerdict Verdict { get; init; }
    /// <summary>One plain sentence.</summary>
    public required string Summary { get; init; }
    /// <summary>For WorksWithLayer: "Proton", "Wine", "Lutris" or "Heroic".</summary>
    public string? Layer { get; init; }
    public Confidence? Confidence { get; init; }
    /// <summary>The same app on Linux (Works) or the app that runs it.</summary>
    public LinuxApp? Linux { get; init; }
    public string? WebUrl { get; init; }
    public IReadOnlyList<AppAlternative> Alternatives { get; init; } = [];
    /// <summary>"anti-cheat" or "no Linux version", for WontWork.</summary>
    public string? Reason { get; init; }
    public IReadOnlyList<ComponentCategory> Uses { get; init; } = [];
    public IReadOnlyList<Source> Sources { get; init; } = [];
    /// <summary>curated | anticheat | protondb | none: where the rating came from.</summary>
    public required string MatchedBy { get; init; }
}

public static class AppWords
{
    public static string Verdict(AppVerdict v) => v switch
    {
        AppVerdict.Works => "Works",
        AppVerdict.WorksWithLayer => "Works with a helper",
        AppVerdict.UseWeb => "Use the website",
        AppVerdict.WontWork => "Won't work",
        AppVerdict.NotNeeded => "Not needed",
        _ => "Not sure",
    };

    public static string Category(string c) => c switch
    {
        "games" => "Games",
        "creative" => "Photos, video and design",
        "office" => "Office and notes",
        "communication" => "Chat and calls",
        "streaming" => "Streaming and cameras",
        "dictation" => "Dictation",
        "cloud" => "Cloud storage",
        "browsers" => "Web browsers",
        "media" => "Music and video",
        "security" => "Passwords and VPN",
        "utilities" => "Tools",
        "development" => "Programming",
        "hardware" => "Settings for your devices",
        _ => "Other apps",
    };

    /// <summary>"38 apps work. 6 have alternatives. 2 won't work." (plus a calm line for unknown apps).</summary>
    public static string Summary(IReadOnlyList<RatedApp> apps)
    {
        static string N(int n, string one, string many) => $"{n} {(n == 1 ? one : many)}";
        var works = apps.Count(a => a.Verdict is AppVerdict.Works or AppVerdict.WorksWithLayer or AppVerdict.NotNeeded);
        var alt = apps.Count(a => a.Verdict == AppVerdict.UseWeb || (a.Verdict == AppVerdict.WontWork && a.Alternatives.Count > 0));
        var wont = apps.Count(a => a.Verdict == AppVerdict.WontWork && a.Alternatives.Count == 0);
        var unsure = apps.Count(a => a.Verdict == AppVerdict.NotSure);
        var s = new List<string>();
        if (works > 0) s.Add($"{N(works, "app works", "apps work")}.");
        if (alt > 0) s.Add($"{N(alt, "has a website or alternative", "have a website or alternative")}.");
        if (wont > 0) s.Add($"{N(wont, "app won't work", "apps won't work")}.");
        if (unsure > 0) s.Add($"We don't know {N(unsure, "app", "apps")} yet.");
        return string.Join(" ", s);
    }
}
