namespace GreyHaven.Compat.Data;

/// <summary>Contents of <c>data/modules.yaml</c>: how to interpret particular kernel modules and driver packages.</summary>
public sealed class ModuleCatalog
{
    public Dictionary<string, ModuleInfo> Modules { get; set; } = [];
    /// <summary>Glob patterns of driver packages that never indicate hardware support (e.g. OEM metapackages).</summary>
    public List<string> IgnorePackages { get; set; } = [];

    public ModuleInfo? Get(string module) => Modules.GetValueOrDefault(module);

    public bool CountsAsDriver(string module) => Get(module)?.Role is not ("bus" or "ignore");
}

public sealed class ModuleInfo
{
    /// <summary>
    /// driver (default) | bus (binds the device but provides no function by itself, e.g. bcma) |
    /// ignore (irrelevant match, e.g. a legacy framebuffer driver).
    /// </summary>
    public string Role { get; set; } = "driver";
    /// <summary>Confidence when this module is the evidence: high | medium | low. Default medium.</summary>
    public string? Confidence { get; set; }
    public string? Note { get; set; }
}

/// <summary>Contents of <c>data/caveats.yaml</c>.</summary>
public sealed class CaveatCatalog
{
    public List<CaveatInfo> Caveats { get; set; } = [];
}

public sealed class CaveatInfo
{
    public string Id { get; set; } = "";
    public string Title { get; set; } = "";
    public string Why { get; set; } = "";
    public string Test { get; set; } = "";
    /// <summary>Optional wording for one kind of PC (keys: laptop, desktop), used instead of <see cref="Test"/>.</summary>
    public Dictionary<string, string> TestByFormFactor { get; set; } = [];
    public CaveatCondition AppliesWhen { get; set; } = new();

    /// <summary>The test wording for this kind of PC, e.g. no lid or touchpad on a desktop.</summary>
    public string TestFor(string formFactor) =>
        TestByFormFactor.TryGetValue(formFactor.ToLowerInvariant(), out var t) ? t : Test;
}

public sealed class CaveatCondition
{
    public bool Always { get; set; }
    /// <summary>laptop | desktop.</summary>
    public string? FormFactor { get; set; }
    /// <summary>The PC has at least this many GPUs.</summary>
    public int? MinGpus { get; set; }
    /// <summary>The PC has a component of this category; the caveat is attached to those components.</summary>
    public string? Category { get; set; }
}
