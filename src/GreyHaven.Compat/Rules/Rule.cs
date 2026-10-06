using GreyHaven.Compat.Data;

namespace GreyHaven.Compat.Rules;

/// <summary>A file in <c>data/rules/</c>.</summary>
public sealed class RuleFile
{
    public List<Rule> Rules { get; set; } = [];
}

/// <summary>
/// A curated compatibility rule. See data/rules/README.md for the full format.
/// Explanations may use placeholders: {distro}, {kernel}, {kernelSeries}, {driverTool},
/// {edition}, {driverPackages}, {libfprint}.
/// </summary>
public sealed class Rule
{
    public string Id { get; set; } = "";
    public string? Title { get; set; }
    public RuleMatch Match { get; set; } = new();
    /// <summary>Evaluated in order per distro; the first whose <c>when</c> holds is used.</summary>
    public List<RuleOutcome> Ratings { get; set; } = [];
    public RuleFix? Fix { get; set; }
    /// <summary>Default confidence: high | medium | low.</summary>
    public string Confidence { get; set; } = "high";
    public List<SourceLink> Sources { get; set; } = [];

    /// <summary>Set by the loader: which file the rule came from (for error messages).</summary>
    [YamlDotNet.Serialization.YamlIgnore]
    public string File { get; set; } = "";
}

/// <summary>All specified conditions must hold for a rule to apply to a component.</summary>
public sealed class RuleMatch
{
    /// <summary>Device keys, e.g. "pci:14e4:4365" or with subsystem "pci:14e4:4365:1043:85d1".</summary>
    public List<string> Ids { get; set; } = [];
    public List<string> ExcludeIds { get; set; } = [];
    /// <summary>Vendor-wide match, e.g. "pci:10de".</summary>
    public string? Vendor { get; set; }
    /// <summary>PCI class code prefix, e.g. "03" or "0104".</summary>
    public string? ClassCode { get; set; }
    /// <summary>Component category, e.g. "touchpad" (camelCase of ComponentCategory).</summary>
    public string? Category { get; set; }
    /// <summary>Device bus: pci | usb | hdAudio | hid | acpi.</summary>
    public string? Bus { get; set; }
    /// <summary>CPU match (for category cpu).</summary>
    public CpuMatch? Cpu { get; set; }
}

public sealed class CpuMatch
{
    /// <summary>x64 | arm64 | x86.</summary>
    public string? Architecture { get; set; }
    /// <summary>e.g. "GenuineIntel", "AuthenticAMD".</summary>
    public List<string> Vendors { get; set; } = [];
}

public sealed class RuleOutcome
{
    public RuleCondition? When { get; set; }
    /// <summary>worksOutOfTheBox | worksWithExtraDriver | partial | unsupported | unknown.</summary>
    public string Rating { get; set; } = "";
    public string Explanation { get; set; } = "";
    /// <summary>Optional one short beginner-level line for the main view; same placeholders as the explanation.</summary>
    public string? Summary { get; set; }
    public string? Confidence { get; set; }
    /// <summary>Overrides the rule's fix for this outcome.</summary>
    public RuleFix? Fix { get; set; }
}

public sealed class RuleCondition
{
    public List<string> Distros { get; set; } = [];
    /// <summary>Kernel series on the distro's ISO is at least this, e.g. "6.2".</summary>
    public string? KernelAtLeast { get; set; }
    public string? KernelBelow { get; set; }
    /// <summary>The chosen edition ships this driver family preinstalled (e.g. "nvidia").</summary>
    public string? PreinstalledDriver { get; set; }
    /// <summary>A package matching this glob in the distro's driver package index claims the device.</summary>
    public string? DriverPackage { get; set; }
    /// <summary>At least one driver package claims the device and every one matches this glob.</summary>
    public string? OnlyDriverPackages { get; set; }
    /// <summary>Secure Boot is on (true) or off (false) on this PC.</summary>
    public bool? SecureBoot { get; set; }
}

public sealed class RuleFix
{
    public string Driver { get; set; } = "";
    /// <summary>Plain-English steps per distro id, or "default".</summary>
    public Dictionary<string, List<string>> Steps { get; set; } = [];
    /// <summary>Exact commands per distro id, or "default".</summary>
    public Dictionary<string, List<string>> Commands { get; set; } = [];
    public string? Note { get; set; }
    public bool NeedsInternet { get; set; }
    /// <summary>none | mokMayBeRequired | mokRequired.</summary>
    public string SecureBoot { get; set; } = "none";
}
