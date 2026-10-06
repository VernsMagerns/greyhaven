namespace GreyHaven.Compat.Data;

/// <summary>Contents of <c>data/distros.yaml</c>.</summary>
public sealed class DistroCatalog
{
    public int SchemaVersion { get; set; }
    /// <summary>Distro offered as the "easy" path (keeps Secure Boot on) when the best distro needs it off.</summary>
    public string? SecureBootEasyModeDistro { get; set; }
    public Dictionary<string, KernelInfo> Kernels { get; set; } = [];
    public Dictionary<string, DriverPackageSet> DriverPackageSets { get; set; } = [];
    public Dictionary<string, FingerprintSet> FingerprintSets { get; set; } = [];
    public List<DistroInfo> Distros { get; set; } = [];

    public DistroInfo Get(string id) =>
        Distros.FirstOrDefault(d => d.Id == id) ?? throw new KeyNotFoundException($"Unknown distro '{id}'.");

    /// <summary>Checks cross-references so a typo in the YAML fails loudly at load time.</summary>
    public void Validate()
    {
        if (SchemaVersion != 1) throw new InvalidDataException($"distros.yaml: unsupported schemaVersion {SchemaVersion}.");
        if (Distros.Count == 0) throw new InvalidDataException("distros.yaml: no distros defined.");
        foreach (var d in Distros)
        {
            if (string.IsNullOrWhiteSpace(d.Id) || string.IsNullOrWhiteSpace(d.Name))
                throw new InvalidDataException("distros.yaml: every distro needs an id and a name.");
            if (d.Editions.Count == 0) throw new InvalidDataException($"distros.yaml: '{d.Id}' has no editions.");
            foreach (var e in d.Editions)
                if (!Kernels.ContainsKey(e.IsoKernel))
                    throw new InvalidDataException($"distros.yaml: edition '{d.Id}/{e.Id}' uses unknown kernel '{e.IsoKernel}'.");
            if (d.UpdatesKernel is not null && !Kernels.ContainsKey(d.UpdatesKernel))
                throw new InvalidDataException($"distros.yaml: '{d.Id}' uses unknown updatesKernel '{d.UpdatesKernel}'.");
            if (d.DriverPackageSet is not null && !DriverPackageSets.ContainsKey(d.DriverPackageSet))
                throw new InvalidDataException($"distros.yaml: '{d.Id}' uses unknown driverPackageSet '{d.DriverPackageSet}'.");
            if (d.FingerprintSet is not null && !FingerprintSets.ContainsKey(d.FingerprintSet))
                throw new InvalidDataException($"distros.yaml: '{d.Id}' uses unknown fingerprintSet '{d.FingerprintSet}'.");
            if (d.Editions.Count(e => e.SelectWhen is null) != 1)
                throw new InvalidDataException($"distros.yaml: '{d.Id}' must have exactly one default edition (without selectWhen).");
        }
        foreach (var (id, k) in Kernels)
            if (k.BuiltinClassAliasesFrom is not null && !Kernels.ContainsKey(k.BuiltinClassAliasesFrom))
                throw new InvalidDataException($"distros.yaml: kernel '{id}' borrows from unknown kernel '{k.BuiltinClassAliasesFrom}'.");
        if (SecureBootEasyModeDistro is { } easy &&
            (Distros.FirstOrDefault(d => d.Id == easy) is not { } easyDistro || !easyDistro.SecureBoot.Supported))
            throw new InvalidDataException($"distros.yaml: secureBootEasyModeDistro '{easy}' must be a distro that supports Secure Boot.");
        foreach (var d in Distros)
            if (d.ReleaseCheck is { } rc && rc.Kind is not ("ubuntuMetaRelease" or "directoryListing" or "popApi"))
                throw new InvalidDataException($"distros.yaml: '{d.Id}' has unknown releaseCheck kind '{rc.Kind}'.");
        if (Distros.Select(d => d.Id).Distinct().Count() != Distros.Count)
            throw new InvalidDataException("distros.yaml: duplicate distro ids.");
    }
}

public sealed class KernelInfo
{
    public string Version { get; set; } = "";
    /// <summary>Major.minor, e.g. "7.0". Used for kernelAtLeast conditions in rules.</summary>
    public string Series { get; set; } = "";
    /// <summary>Path of the alias index, relative to the data directory.</summary>
    public string Index { get; set; } = "";
    public string? VerifiedFrom { get; set; }
    /// <summary>Set when the index was built from a near-identical kernel; explains why.</summary>
    public string? Approximation { get; set; }
    public List<string> Packages { get; set; } = [];
    /// <summary>
    /// For kernels that don't record aliases for built-in drivers: borrow the class-wide alias
    /// patterns of this kernel's built-in modules from the named (newer) kernel's index.
    /// </summary>
    public string? BuiltinClassAliasesFrom { get; set; }
}

public sealed class DriverPackageSet
{
    public string Index { get; set; } = "";
    public List<string> PackageIndexes { get; set; } = [];
}

public sealed class FingerprintSet
{
    public string Version { get; set; } = "";
    public string Index { get; set; } = "";
    /// <summary>URL of the libfprint .deb whose udev hwdb lists supported and known-unsupported readers.</summary>
    public string Package { get; set; } = "";
}

public sealed class DistroInfo
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string? Edition { get; set; }
    public string Version { get; set; } = "";
    public string? Base { get; set; }
    public string? Website { get; set; }
    /// <summary>Lower wins when two distros score the same.</summary>
    public int TieBreakRank { get; set; }
    /// <summary>One sentence on who this distro suits, used in the recommendation paragraph.</summary>
    public string? Audience { get; set; }
    public string? DriverTool { get; set; }
    public string? DriverPackageSet { get; set; }
    public string? FingerprintSet { get; set; }
    public SecureBootSupport SecureBoot { get; set; } = new();
    public List<DistroEdition> Editions { get; set; } = [];
    public string? UpdatesKernel { get; set; }
    public DistroLiveUsb LiveUsb { get; set; } = new();
    public ReleaseCheck? ReleaseCheck { get; set; }
    public List<SourceLink> Sources { get; set; } = [];

    public string DisplayName => Edition is null ? $"{Name} {Version}" : $"{Name} {Version} {Edition}";
}

public sealed class SecureBootSupport
{
    public bool Supported { get; set; } = true;
    public string? Note { get; set; }
}

public sealed class DistroEdition
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string IsoKernel { get; set; } = "";
    /// <summary>Condition for choosing this edition, e.g. "nvidiaGpu". Null = the default edition.</summary>
    public string? SelectWhen { get; set; }
    /// <summary>Driver families already installed on this edition, e.g. "nvidia".</summary>
    public List<string> PreinstalledDrivers { get; set; } = [];
    /// <summary>ISO build number the data was verified against (Pop!_OS publishes numbered builds).</summary>
    public int? IsoBuild { get; set; }
    /// <summary>Size in bytes of the official installer file (ISO), shown to users before downloading.</summary>
    public long? InstallerBytes { get; set; }
}

/// <summary>How to detect a newer release of a distro. See DistroReleaseChecker.</summary>
public sealed class ReleaseCheck
{
    /// <summary>ubuntuMetaRelease | directoryListing | popApi.</summary>
    public string Kind { get; set; } = "";
    public string Url { get; set; } = "";
}

public sealed class SourceLink
{
    public string Title { get; set; } = "";
    public string? Url { get; set; }
    /// <summary>Distro ids this source is about; empty means it applies to every distro.</summary>
    public List<string> Distros { get; set; } = [];

    public bool AppliesTo(string distroId) => Distros.Count == 0 || Distros.Contains(distroId);
}
