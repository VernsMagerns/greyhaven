using GreyHaven.Core.Hardware;

namespace GreyHaven.Core.Compatibility;

/// <summary>How well a component works on a distro.</summary>
public enum Rating
{
    WorksOutOfTheBox,
    WorksWithExtraDriver,
    Partial,
    Unsupported,
    Unknown,
}

public enum Confidence { High, Medium, Low }

/// <summary>Where a piece of evidence came from.</summary>
public enum SourceKind
{
    /// <summary>A hand-written rule in data/rules/.</summary>
    CuratedRule,
    /// <summary>The distro kernel's own driver ID tables.</summary>
    KernelDriver,
    /// <summary>A driver package's Modaliases in the distro's repositories.</summary>
    DriverPackage,
    /// <summary>libfprint's list of supported and known-unsupported fingerprint readers.</summary>
    Libfprint,
    /// <summary>linux-hardware.org community probes.</summary>
    LinuxHardwareDb,
    /// <summary>Official documentation cited by GreyHaven's data, e.g. Microsoft's Windows 10 lifecycle page.</summary>
    Reference,
    /// <summary>Community app data: Are We Anti-Cheat Yet, ProtonDB.</summary>
    Community,
}

public sealed record Source(string Title, string? Url, SourceKind Kind);

/// <summary>What Secure Boot means for a fix.</summary>
public enum SecureBootImpact
{
    None,
    /// <summary>The driver may need a Machine Owner Key (MOK) enrolled when Secure Boot is on.</summary>
    MokMayBeRequired,
    /// <summary>The driver is built locally (DKMS) and needs a MOK enrolled when Secure Boot is on.</summary>
    MokRequired,
}

/// <summary>How to get a component working on a specific distro.</summary>
public sealed record Fix
{
    /// <summary>The rule it comes from, e.g. "nvidia-gpu". Used to pick sections of the public phone guide.</summary>
    public string? Id { get; init; }
    /// <summary>Driver name, e.g. "NVIDIA proprietary driver".</summary>
    public required string Driver { get; init; }
    /// <summary>Plain-English steps, e.g. using a graphical driver tool.</summary>
    public IReadOnlyList<string> Steps { get; init; } = [];
    /// <summary>Exact terminal commands, in order.</summary>
    public IReadOnlyList<string> Commands { get; init; } = [];
    public string? Note { get; init; }
    public bool NeedsInternet { get; init; }
    public SecureBootImpact SecureBoot { get; init; }
    /// <summary>Set when Secure Boot is on and affects this fix on this distro.</summary>
    public string? SecureBootAdvice { get; init; }
}

/// <summary>A component's rating on one distro.</summary>
public sealed record ComponentRating
{
    public required string DistroId { get; init; }
    public required Rating Rating { get; init; }
    public required Confidence Confidence { get; init; }
    /// <summary>Plain-English explanation of the rating.</summary>
    public required string Explanation { get; init; }
    /// <summary>
    /// One short, beginner-level line for the main view (about a 6th-grade reading level),
    /// e.g. "Works after you add NVIDIA's driver." Null means use the default for the rating
    /// (<see cref="Hardware.PlainWords.Summary"/>).
    /// </summary>
    public string? Summary { get; init; }
    public IReadOnlyList<Source> Sources { get; init; } = [];
    /// <summary>Set for anything not rated "works out of the box" where a fix is known.</summary>
    public Fix? Fix { get; init; }
    /// <summary>The Linux driver (kernel module or package) the rating is based on, if any.</summary>
    public string? LinuxDriver { get; init; }

    /// <summary>
    /// Set when the rating depends on approximated data (e.g. a kernel index built from a
    /// near-identical kernel). Such ratings are capped at medium confidence.
    /// </summary>
    public string? ApproximationNote { get; init; }

    /// <summary>True when the rating rests on weak or missing data; the UI labels these.</summary>
    public bool IsWeakData => Rating == Rating.Unknown || Confidence == Confidence.Low;
}

/// <summary>How much a component matters to the overall score.</summary>
public enum Importance { Critical, Important, Minor }

/// <summary>How Linux goes on the PC: next to Windows (pick one at each start) or instead of it.</summary>
public enum InstallChoice { KeepWindows, ReplaceWindows }

public sealed record RatedComponent
{
    public required HardwareComponent Component { get; init; }

    /// <summary>
    /// The name to show: the component's name, plus a distinguishing detail when another part of
    /// the same kind has the same name (see <see cref="ComponentNames"/>).
    /// </summary>
    public string DisplayName { get => _displayName ?? Component.Name; init => _displayName = value; }
    private readonly string? _displayName;

    public required Importance Importance { get; init; }
    public required IReadOnlyList<ComponentRating> Ratings { get; init; }
    /// <summary>Ids of <see cref="Caveat"/>s that concern this component.</summary>
    public IReadOnlyList<string> CaveatIds { get; init; } = [];

    public ComponentRating For(string distroId) => Ratings.First(r => r.DistroId == distroId);
}

public enum ScoreBand { Great, Good, Fair, Poor }

/// <summary>The summary score for one distro. See docs/scoring.md.</summary>
public sealed record ScoreResult
{
    /// <summary>0–100.</summary>
    public required int Score { get; init; }
    public required ScoreBand Band { get; init; }
    /// <summary>Share (0–1) of the weighted components that could be rated (not Unknown).</summary>
    public required double Coverage { get; init; }
    /// <summary>True when a critical component couldn't be rated.</summary>
    public required bool Incomplete { get; init; }
    /// <summary>Plain-English reason the score was capped, if it was.</summary>
    public string? CapReason { get; init; }
    public int ExtraDriverCount { get; init; }
    public int UnknownCount { get; init; }
}

public sealed record DistroSummary
{
    public required string DistroId { get; init; }
    public required string DisplayName { get; init; }
    /// <summary>The edition (ISO) GreyHaven picked for this PC, e.g. Pop!_OS "NVIDIA ISO".</summary>
    public required string Edition { get; init; }
    public required string EditionId { get; init; }
    public required string KernelVersion { get; init; }
    public required ScoreResult Score { get; init; }
    /// <summary>System-level notes, e.g. "Secure Boot must be turned off to install Pop!_OS."</summary>
    public IReadOnlyList<string> Notes { get; init; } = [];
    /// <summary>"What's the difference?": a plain summary, pros and cons, with sources.</summary>
    public DistroAbout? About { get; init; }
}

public sealed record DistroAbout(string Summary, IReadOnlyList<string> Pros, IReadOnlyList<string> Cons, IReadOnlyList<Source> Sources)
{
    /// <summary>Other versions of Linux exist; a neutral list to explore later.</summary>
    public const string OtherVersions = "There are many more versions of Linux, like Fedora, Zorin OS and Kubuntu. Once you're comfortable, you can try any of them from a USB stick the same way.";
    public const string OtherVersionsTitle = "DistroWatch - a list of versions of Linux";
    public const string OtherVersionsUrl = "https://distrowatch.com/";
}

/// <summary>Which way to switch. See <see cref="Recommendation"/>.</summary>
public enum PathKind
{
    /// <summary>The only path: the best distro needs no firmware changes.</summary>
    Recommended,
    /// <summary>Keeps Secure Boot on (offered when the best distro would need it off).</summary>
    Easy,
    /// <summary>The best-supported distro, which needs Secure Boot turned off.</summary>
    Advanced,
}

/// <summary>Something the user must do in Windows or the firmware before switching, for the pre-switch checklist.</summary>
public enum PreSwitchRequirement
{
    /// <summary>Turn off Secure Boot in the firmware settings. Always instructions-only, never a one-click fix.</summary>
    TurnOffSecureBoot,
}

/// <param name="Url">Web address; null for GreyHaven's own guide until the repository is public (use <see cref="LocalPath"/>).</param>
public sealed record GuideLink(string Title, string? Url, bool FromManufacturer)
{
    /// <summary>"motherboard" when the guide was chosen from the motherboard maker (custom-built PCs); otherwise null.</summary>
    public string? MatchedBy { get; init; }

    /// <summary>For GreyHaven's own guides: path of the copy shipped with the app, so it works offline.</summary>
    public string? LocalPath { get; init; }
}

/// <summary>One way to switch: a distro, the ISO to download, and what it takes.</summary>
public sealed record RecommendationPath
{
    public required PathKind Kind { get; init; }
    public required string DistroId { get; init; }
    /// <summary>The edition (ISO) to download for this PC, e.g. Pop!_OS "nvidia".</summary>
    public required string EditionId { get; init; }
    public required string EditionName { get; init; }
    /// <summary>Short label, e.g. "Easy: Ubuntu".</summary>
    public required string Title { get; init; }
    /// <summary>Plain-English explanation of this path, including any extra steps.</summary>
    public required string Paragraph { get; init; }
    public IReadOnlyList<PreSwitchRequirement> Requirements { get; init; } = [];
    /// <summary>Set when <see cref="PreSwitchRequirement.TurnOffSecureBoot"/> applies: what it means, in plain words.</summary>
    public string? SecureBootNotice { get; init; }
    /// <summary>How-to guide for turning Secure Boot off: the PC maker's own instructions when known.</summary>
    public GuideLink? SecureBootGuide { get; init; }
}

public sealed record Recommendation
{
    /// <summary>Introduction shown above the paths (or the whole recommendation when there's one path).</summary>
    public required string Paragraph { get; init; }
    /// <summary>
    /// Empty when no distro can be recommended (e.g. unsupported ARM laptop). One path normally;
    /// two (Easy, Advanced) when the best distro would need Secure Boot turned off.
    /// </summary>
    public IReadOnlyList<RecommendationPath> Paths { get; init; } = [];
    public PathKind? DefaultPath { get; init; }

    public RecommendationPath? Get(PathKind kind) => Paths.FirstOrDefault(p => p.Kind == kind);
}

/// <summary>A newer release of a supported distro than the bundled data describes.</summary>
public sealed record NewerRelease(string DistroId, string DistroName, string Current, string Found, string? Url);

/// <summary>How current the bundled compatibility data is.</summary>
public sealed record DataFreshness
{
    public required DateOnly BuiltOn { get; init; }
    public required int AgeDays { get; init; }
    public IReadOnlyList<NewerRelease> NewerReleases { get; init; } = [];
    /// <summary>Whether newer releases were checked for (online); false when offline or not attempted.</summary>
    public bool ReleasesChecked { get; init; }
    /// <summary>Set when the user should check for a GreyHaven update.</summary>
    public string? Notice { get; init; }

    public bool IsStale => Notice is not null;
}

/// <summary>Something that can't be judged from Windows and should be tested with a live USB.</summary>
public sealed record Caveat
{
    public required string Id { get; init; }
    public required string Title { get; init; }
    public required string Why { get; init; }
    public required string WhatToTest { get; init; }
}

/// <summary>The PC's (or, for custom-built PCs, the motherboard's) maker, from data/makers.yaml.</summary>
/// <param name="BootMenuKey">Key for the one-time startup menu, e.g. "F12", or an instruction for Surface.</param>
/// <param name="SupportUrl">The maker's official support page, for BIOS updates.</param>
/// <param name="SetupKey">The key that opens the startup settings (BIOS setup), e.g. "Delete".</param>
/// <param name="SecureBootPath">Where Secure Boot is in those settings, e.g. "Settings > Security > Secure Boot".</param>
public sealed record PcMaker(string Name, string BootMenuKey, string? BootMenuNote, string SupportUrl, bool FromMotherboard,
    string? SetupKey = null, string? SecureBootPath = null);

/// <summary>One thing to try in a live session, e.g. "Wi-Fi: join your network and open a website".</summary>
public sealed record LiveUsbCheck(string Title, string Test);

/// <summary>How to try a distro from a USB stick on this PC before installing, and what to test.</summary>
public sealed record LiveUsbGuide
{
    public required string DistroId { get; init; }
    /// <summary>e.g. "Pop!_OS 24.04".</summary>
    public required string DistroName { get; init; }
    /// <summary>The download to use, e.g. "NVIDIA ISO".</summary>
    public required string EditionName { get; init; }
    public string? Website { get; init; }
    /// <summary>The distro's official guide to writing the download to a USB stick.</summary>
    public Source? MakeUsbGuide { get; init; }
    /// <summary>The distro's official guide to starting the live session, if separate.</summary>
    public Source? TryItGuide { get; init; }
    /// <summary>Size of the installer file (ISO) for this edition, from the official download.</summary>
    public long? InstallerBytes { get; init; }
    /// <summary>Things to check, for the parts this PC has, plus what can't be judged from Windows.</summary>
    public IReadOnlyList<LiveUsbCheck> Checks { get; init; } = [];
    /// <summary>Preparation specific to this PC, e.g. turning Secure Boot off or having the BitLocker recovery key.</summary>
    public IReadOnlyList<string> BeforeYouStart { get; init; } = [];
    /// <summary>Parts that need an extra step after installing, so may not fully work in the live session.</summary>
    public IReadOnlyList<string> ExpectedInLiveSession { get; init; } = [];
    /// <summary>Optional: commands to share a hardware report with linux-hardware.org.</summary>
    public IReadOnlyList<string> ShareCommands { get; init; } = [];
    public string? ShareNote { get; init; }
    public Source? ShareSource { get; init; }
}

/// <summary>Background worth knowing when deciding whether to switch, e.g. Windows 10's end of support.</summary>
public sealed record ContextNote(string Text, Source Source);

/// <summary>Everything the UI and reports need.</summary>
public sealed record CompatibilityReport
{
    public required HardwareProfile Profile { get; init; }
    public required IReadOnlyList<DistroSummary> Distros { get; init; }
    public required IReadOnlyList<RatedComponent> Components { get; init; }
    public required Recommendation Recommendation { get; init; }
    public IReadOnlyList<Caveat> Caveats { get; init; } = [];
    /// <summary>Notes about the data itself, e.g. "Online lookups were skipped: you appear to be offline."</summary>
    public IReadOnlyList<string> DataNotes { get; init; } = [];

    /// <summary>The person's apps and how each does on Linux. Private: left out of saved reports unless they choose.</summary>
    public IReadOnlyList<Apps.RatedApp> Apps { get; init; } = [];
    /// <summary>Context about this PC's Windows, shown near the top in calm, plain words.</summary>
    public IReadOnlyList<ContextNote> ContextNotes { get; init; } = [];
    /// <summary>One live-USB test guide per distro (for the edition picked for this PC).</summary>
    public IReadOnlyList<LiveUsbGuide> LiveUsbGuides { get; init; } = [];
    /// <summary>Who made this PC (or its motherboard), when recognised: boot menu key and support page.</summary>
    public PcMaker? Maker { get; init; }
    public required DataFreshness Data { get; init; }
    public required DateTimeOffset GeneratedAt { get; init; }

    /// <summary>The path the user chose; defaults to <see cref="Recommendation.DefaultPath"/>. Drives fixes, checklist, ISO and export.</summary>
    public PathKind? SelectedPath { get; init; }

    public RecommendationPath? SelectedRecommendation =>
        SelectedPath is { } k ? Recommendation.Get(k) : null;

    /// <summary>The distro the chosen path uses, or null if nothing is recommended.</summary>
    public string? SelectedDistroId => SelectedRecommendation?.DistroId;

    /// <summary>The live-USB guide for the chosen path's distro.</summary>
    public LiveUsbGuide? SelectedLiveUsbGuide => LiveUsbGuides.FirstOrDefault(g => g.DistroId == SelectedDistroId);

    /// <summary>Keep Windows too, or replace it. Chosen at the start of "Get ready"; null until then.</summary>
    public InstallChoice? SelectedInstall { get; init; }

    /// <summary>Apps the person marked "I need this" (by name). Private, like the app list.</summary>
    public IReadOnlyList<string> MustHaveApps { get; init; } = [];

    /// <summary>Must-have apps that won't (or might not) work on Linux.</summary>
    public IReadOnlyList<Apps.RatedApp> MustHaveProblems =>
        Apps.Where(a => MustHaveApps.Contains(a.Name) && GreyHaven.Core.Apps.AppWords.MustHaveWarning(a) is not null).ToList();

    /// <summary>Returns a copy with an install choice.</summary>
    public CompatibilityReport WithInstall(InstallChoice? choice) => this with { SelectedInstall = choice };

    /// <summary>Returns a copy with another path selected.</summary>
    public CompatibilityReport WithPath(PathKind kind) =>
        Recommendation.Get(kind) is null
            ? throw new ArgumentException($"This report has no '{kind}' path.", nameof(kind))
            : this with { SelectedPath = kind };
}

/// <summary>
/// One page of the public phone guide (trying, installing and setting up Linux), for a distro,
/// download and install choice. General: nothing about any one PC is in it.
/// </summary>
/// <param name="DistroName">Short name, e.g. "Ubuntu".</param>
/// <param name="Checks">Live-USB checks for any PC; people skip the parts they don't have.</param>
/// <param name="LaptopChecks">Only for laptops (touchpad, battery, closing the lid).</param>
/// <param name="DesktopChecks">Only for desktops (the same tests, worded for a desktop).</param>
/// <param name="Fixes">Every extra step that can apply on this download, shown only to PCs that need it.</param>
public sealed record PhoneGuidePage(string DistroId, string DistroName, string EditionId, string EditionName, InstallChoice Install,
    IReadOnlyList<LiveUsbCheck> Checks, IReadOnlyList<LiveUsbCheck> LaptopChecks, IReadOnlyList<LiveUsbCheck> DesktopChecks, IReadOnlyList<PhoneGuideFix> Fixes)
{
    public string FileName => PhoneGuidePages.FileName(DistroId, EditionId, Install);
}

/// <param name="Id">The rule id, e.g. "nvidia-gpu", as used after "#x=".</param>
/// <param name="Title">What it's for, e.g. "NVIDIA graphics".</param>
public sealed record PhoneGuideFix(string Id, string Title, Fix Fix);

public static class PhoneGuidePages
{
    public static string FileName(string distroId, string editionId, InstallChoice install) =>
        PageName(distroId, editionId, install) + ".html";

    /// <summary>The page's address on the site: Cloudflare Pages serves "x.html" at "x" (and redirects to it).</summary>
    public static string PageName(string distroId, string editionId, InstallChoice install) =>
        $"{distroId}-{editionId}-{(install == InstallChoice.KeepWindows ? "keep" : "replace")}";

    /// <summary>Pop!_OS's installer can't make room next to Windows by itself.</summary>
    public static bool CanKeepWindows(string distroId) => distroId != "pop-os";
}
