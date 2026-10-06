using GreyHaven.Compat;
using GreyHaven.Compat.Data;
using GreyHaven.Compat.Kernel;
using GreyHaven.Compat.Rules;
using GreyHaven.Core.Hardware;

namespace GreyHaven.Tests.Ratings;

/// <summary>
/// A small in-memory compatibility dataset with two fake distros, so rating logic is tested
/// against known inputs rather than the real (changing) data:
///   alpha: ISO kernel 6.10, updates kernel 7.0, supports Secure Boot
///   beta:  kernel 7.0 on both, does NOT support Secure Boot, has an "nvidia" edition
/// </summary>
internal static class TestData
{
    /// <summary>Targets may carry a provenance after a '|', e.g. "xhci_pci|borrowed:ref".</summary>
    public static AliasIndex Index(string id, string kind, params (string Pattern, string Target)[] entries) =>
        AliasIndex.Create(new AliasIndexInfo(id, kind, new Dictionary<string, string>()), entries.Select(e =>
            e.Target.Split('|') is [var t, var prov] ? new AliasEntry(e.Pattern, t, prov) : new AliasEntry(e.Pattern, e.Target)));

    public static CompatibilityData Build(
        IEnumerable<(string Pattern, string Module)>? alphaKernel = null,
        IEnumerable<(string Pattern, string Module)>? alphaUpdates = null,
        IEnumerable<(string Pattern, string Module)>? betaKernel = null,
        IEnumerable<(string Pattern, string Package)>? packages = null,
        IEnumerable<(string Pattern, string Target)>? fingerprint = null,
        IEnumerable<Rule>? rules = null,
        ModuleCatalog? modules = null,
        string builtOn = "2026-10-01",
        bool alphaKernelApproximate = false)
    {
        var catalog = new DistroCatalog
        {
            SchemaVersion = 1,
            SecureBootEasyModeDistro = "alpha",
            Kernels = new()
            {
                ["alpha-6.10"] = new KernelInfo
                {
                    Version = "6.10.0-1-generic", Series = "6.10", Index = "a", Packages = ["https://example.test/alpha-6.10.deb"],
                    Approximation = alphaKernelApproximate ? "Built from the matching upstream kernel." : null,
                },
                ["alpha-7.0"] = new KernelInfo { Version = "7.0.0-1-generic", Series = "7.0", Index = "b" },
                ["beta-7.0"] = new KernelInfo { Version = "7.0.5-generic", Series = "7.0", Index = "c" },
            },
            DriverPackageSets = new() { ["pkgs"] = new DriverPackageSet { Index = "p" } },
            FingerprintSets = new() { ["fp"] = new FingerprintSet { Version = "1.95.0", Index = "f" } },
            Distros =
            [
                new DistroInfo
                {
                    Id = "alpha", Name = "Alpha Linux", Version = "1", TieBreakRank = 1, DriverTool = "Driver Tool",
                    Audience = "Alpha suits beginners.", DriverPackageSet = "pkgs", FingerprintSet = "fp",
                    SecureBoot = new SecureBootSupport { Supported = true },
                    Editions = [new DistroEdition { Id = "std", Name = "Standard", IsoKernel = "alpha-6.10" }],
                    UpdatesKernel = "alpha-7.0",
                },
                new DistroInfo
                {
                    Id = "beta", Name = "Beta OS", Version = "2", TieBreakRank = 2, DriverTool = "Beta Drivers",
                    Audience = "Beta suits gamers.", DriverPackageSet = "pkgs", FingerprintSet = "fp",
                    SecureBoot = new SecureBootSupport { Supported = false, Note = "Beta OS needs Secure Boot off." },
                    Editions =
                    [
                        new DistroEdition { Id = "std", Name = "Standard ISO", IsoKernel = "beta-7.0" },
                        new DistroEdition { Id = "nv", Name = "NVIDIA ISO", IsoKernel = "beta-7.0", SelectWhen = "nvidiaGpu", PreinstalledDrivers = ["nvidia"] },
                    ],
                    UpdatesKernel = "beta-7.0",
                },
            ],
        };
        catalog.Validate();

        return new CompatibilityData
        {
            Distros = catalog,
            Rules = new RuleSet((rules ?? []).ToList(), ["alpha", "beta"]),
            Modules = modules ?? new ModuleCatalog
            {
                Modules = new()
                {
                    ["bcma"] = new ModuleInfo { Role = "bus" },
                    ["xhci_pci"] = new ModuleInfo { Confidence = "high" },
                },
                IgnorePackages = ["oem-*"],
            },
            Caveats = new CaveatCatalog
            {
                Caveats =
                [
                    new CaveatInfo { Id = "sleep", Title = "Sleep", Why = "w", Test = "t", AppliesWhen = new CaveatCondition { Always = true } },
                    new CaveatInfo { Id = "battery", Title = "Battery", Why = "w", Test = "t", AppliesWhen = new CaveatCondition { FormFactor = "laptop" } },
                    new CaveatInfo { Id = "hybrid", Title = "Hybrid", Why = "w", Test = "t", AppliesWhen = new CaveatCondition { MinGpus = 2 } },
                    new CaveatInfo { Id = "fp", Title = "Fingerprint", Why = "w", Test = "t", AppliesWhen = new CaveatCondition { Category = "fingerprintReader" } },
                ],
            },
            SecureBootGuides = new SecureBootGuideCatalog
            {
                Fallback = new FallbackGuide { Title = "General guide", Path = "docs/secure-boot.md" },
                Guides =
                [
                    new ManufacturerGuide { Manufacturers = ["dell"], Title = "Dell guide", Url = "https://dell.example/sb" },
                    new ManufacturerGuide { Manufacturers = ["lenovo"], ModelContains = ["think"], Title = "Lenovo Think guide", Url = "https://lenovo.example/sb" },
                ],
            },
            Dataset = new DatasetInfo { BuiltOn = builtOn },
            KernelIndexes = new Dictionary<string, AliasIndex>
            {
                ["alpha-6.10"] = Index("alpha-6.10", "kernel", [.. alphaKernel ?? []]),
                ["alpha-7.0"] = Index("alpha-7.0", "kernel", [.. alphaUpdates ?? alphaKernel ?? []]),
                ["beta-7.0"] = Index("beta-7.0", "kernel", [.. betaKernel ?? alphaKernel ?? []]),
            },
            PackageIndexes = new Dictionary<string, AliasIndex> { ["pkgs"] = Index("pkgs", "packages", [.. packages ?? []]) },
            FingerprintIndexes = new Dictionary<string, AliasIndex> { ["fp"] = Index("fp", "fingerprint", [.. fingerprint ?? []]) },
        };
    }

    public static Rule Rule(string id, RuleMatch match, params RuleOutcome[] ratings) => new()
    {
        Id = id,
        Match = match,
        Ratings = [.. ratings],
        Sources = [new SourceLink { Title = "Test source", Url = "https://example.test/source" }],
        File = "test.yaml",
    };

    public static HardwareComponent Pci(string vd, string cc, ComponentCategory category, string? subsystem = null, string name = "Test device")
    {
        var sub = subsystem?.Split(':');
        return new HardwareComponent
        {
            Category = category,
            Name = name,
            Id = new DeviceId(DeviceBus.Pci, vd[..4], vd[5..], sub?[0], sub?[1], ClassCode: cc),
        };
    }

    public static HardwareComponent Usb(string vd, ComponentCategory category, string? interfaceClass = null) => new()
    {
        Category = category,
        Name = "Test USB device",
        Id = new DeviceId(DeviceBus.Usb, vd[..4], vd[5..], InterfaceClass: interfaceClass),
    };

    public static HardwareComponent Cpu(string vendor = "GenuineIntel") => new()
    {
        Category = ComponentCategory.Cpu,
        Name = "Test CPU",
        Cpu = new CpuDetails(vendor, 6, 140, 1, 4, 8),
    };

    public static HardwareProfile Profile(
        IEnumerable<HardwareComponent> components,
        SecureBootState secureBoot = SecureBootState.Disabled,
        FormFactor formFactor = FormFactor.Desktop,
        CpuArchitecture arch = CpuArchitecture.X64,
        string? manufacturer = null,
        string? model = null,
        BitLockerState bitLocker = BitLockerState.NotEncrypted) => new()
    {
        GeneratedBy = "test",
        ScannedAt = DateTimeOffset.UnixEpoch,
        Source = "test",
        System = new SystemInfo
        {
            Windows = new WindowsVersion("Windows 10 Pro", "22H2", 19045, 1, "Professional"),
            Architecture = arch,
            Firmware = FirmwareMode.Uefi,
            SecureBoot = secureBoot,
            FormFactor = formFactor,
            SystemManufacturer = manufacturer,
            SystemModel = model,
            BitLocker = bitLocker,
        },
        Components = components.ToList(),
    };
}
