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
    public IReadOnlyList<Source> Sources { get; init; } = [];
    /// <summary>Set for anything not rated "works out of the box" where a fix is known.</summary>
    public Fix? Fix { get; init; }
    /// <summary>The Linux driver (kernel module or package) the rating is based on, if any.</summary>
    public string? LinuxDriver { get; init; }

    /// <summary>True when the rating rests on weak or missing data; the UI labels these.</summary>
    public bool IsWeakData => Rating == Rating.Unknown || Confidence == Confidence.Low;
}

/// <summary>How much a component matters to the overall score.</summary>
public enum Importance { Critical, Important, Minor }

public sealed record RatedComponent
{
    public required HardwareComponent Component { get; init; }
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
    public required string KernelVersion { get; init; }
    public required ScoreResult Score { get; init; }
    /// <summary>System-level notes, e.g. "Secure Boot must be turned off to install Pop!_OS."</summary>
    public IReadOnlyList<string> Notes { get; init; } = [];
}

public sealed record Recommendation
{
    /// <summary>Null when no distro can be recommended for this PC (e.g. unsupported ARM laptop).</summary>
    public string? DistroId { get; init; }
    public required string Paragraph { get; init; }
}

/// <summary>Something that can't be judged from Windows and should be tested with a live USB.</summary>
public sealed record Caveat
{
    public required string Id { get; init; }
    public required string Title { get; init; }
    public required string Why { get; init; }
    public required string WhatToTest { get; init; }
}

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
    public required DateTimeOffset GeneratedAt { get; init; }
}
