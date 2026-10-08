using GreyHaven.Compat.Data;
using GreyHaven.Compat.Kernel;
using GreyHaven.Compat.Rules;

namespace GreyHaven.Compat;

/// <summary>
/// Everything in the <c>data/</c> directory, loaded and cross-checked: distros, kernel and
/// package indexes, fingerprint lists, curated rules, module metadata and caveats.
/// All offline; nothing here touches the network.
/// </summary>
public sealed class CompatibilityData
{
    public required DistroCatalog Distros { get; init; }
    public required RuleSet Rules { get; init; }
    public required ModuleCatalog Modules { get; init; }
    public required CaveatCatalog Caveats { get; init; }
    public required SecureBootGuideCatalog SecureBootGuides { get; init; }
    public required DatasetInfo Dataset { get; init; }
    public WindowsLifecycle Windows { get; init; } = new();
    public LiveUsbCatalog LiveUsb { get; init; } = new();
    public MakerCatalog Makers { get; init; } = new();
    /// <summary>App compatibility (data/apps).</summary>
    public AppCatalog Apps { get; init; } = new();
    public required IReadOnlyDictionary<string, AliasIndex> KernelIndexes { get; init; }
    public required IReadOnlyDictionary<string, AliasIndex> PackageIndexes { get; init; }
    public required IReadOnlyDictionary<string, AliasIndex> FingerprintIndexes { get; init; }

    public static string DefaultDirectory => Path.Combine(AppContext.BaseDirectory, "data");

    public static CompatibilityData Load(string? directory = null)
    {
        var dir = directory ?? DefaultDirectory;
        if (!Directory.Exists(dir)) throw new DirectoryNotFoundException($"Compatibility data not found at {dir}.");

        var distros = Yaml.Load<DistroCatalog>(Path.Combine(dir, "distros.yaml"));
        distros.Validate();
        var liveUsb = Yaml.Load<LiveUsbCatalog>(Path.Combine(dir, "live-usb.yaml"));
        liveUsb.Validate();
        var makers = Yaml.Load<MakerCatalog>(Path.Combine(dir, "makers.yaml"));
        makers.Validate();

        AliasIndex LoadIndex(string relative, string expectedId, string expectedKind)
        {
            var index = AliasIndex.Load(Path.Combine(dir, relative));
            if (index.Info.Id != expectedId || index.Info.Kind != expectedKind)
                throw new InvalidDataException($"{relative}: expected index '{expectedId}' ({expectedKind}) but found '{index.Info.Id}' ({index.Info.Kind}). Rebuild the indexes.");
            return index;
        }

        return new CompatibilityData
        {
            Distros = distros,
            Rules = RuleSet.LoadDirectory(Path.Combine(dir, "rules"), distros.Distros.Select(d => d.Id).ToList()),
            Modules = Yaml.Load<ModuleCatalog>(Path.Combine(dir, "modules.yaml")),
            Caveats = Yaml.Load<CaveatCatalog>(Path.Combine(dir, "caveats.yaml")),
            SecureBootGuides = Yaml.Load<SecureBootGuideCatalog>(Path.Combine(dir, "secure-boot-guides.yaml")),
            Dataset = Yaml.Load<DatasetInfo>(Path.Combine(dir, "dataset.yaml")),
            Windows = Yaml.Load<WindowsLifecycle>(Path.Combine(dir, "windows.yaml")),
            Apps = AppCatalog.Load(dir),
            LiveUsb = liveUsb,
            Makers = makers,
            KernelIndexes = distros.Kernels.ToDictionary(k => k.Key, k => LoadIndex(k.Value.Index, k.Key, "kernel")),
            PackageIndexes = distros.DriverPackageSets.ToDictionary(k => k.Key, k => LoadIndex(k.Value.Index, k.Key, "packages")),
            FingerprintIndexes = distros.FingerprintSets.ToDictionary(k => k.Key, k => LoadIndex(k.Value.Index, k.Key, "fingerprint")),
        };
    }
}
