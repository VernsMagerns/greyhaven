using GreyHaven.Core.Compatibility;
using GreyHaven.Core.Hardware;

namespace GreyHaven.Compat.Data;

/// <summary>Contents of <c>data/makers.yaml</c>.</summary>
public sealed class MakerCatalog
{
    public List<MakerInfo> Makers { get; set; } = [];

    /// <summary>The PC maker if recognised, otherwise the motherboard maker (custom-built PCs).</summary>
    public PcMaker? For(SystemInfo system)
    {
        var pc = Match(system.SystemManufacturer, system.SystemModel);
        if (pc is not null) return pc.ToMaker(system.FormFactor, fromMotherboard: false);
        var board = Match(system.BoardManufacturer, model: null);
        return board?.ToMaker(system.FormFactor, fromMotherboard: true);
    }

    private MakerInfo? Match(string? manufacturer, string? model)
    {
        var maker = manufacturer?.ToLowerInvariant() ?? "";
        var m = model?.ToLowerInvariant() ?? "";
        if (maker.Length == 0) return null;
        return Makers.FirstOrDefault(x =>
            x.Manufacturers.Any(k => maker.Contains(k, StringComparison.Ordinal)) &&
            (x.ModelContains.Count == 0 || model is null || x.ModelContains.Any(k => m.Contains(k, StringComparison.Ordinal))));
    }

    public void Validate()
    {
        var errors = new List<string>();
        foreach (var m in Makers)
        {
            if (m.Manufacturers.Count == 0) errors.Add($"{m.Name}: needs manufacturers.");
            if (m.BootMenuKey is null && (m.BootMenuKeyLaptop is null || m.BootMenuKeyDesktop is null)) errors.Add($"{m.Name}: needs bootMenuKey, or both bootMenuKeyLaptop and bootMenuKeyDesktop.");
            if (!m.SupportUrl.StartsWith("https://", StringComparison.Ordinal)) errors.Add($"{m.Name}: supportUrl must be https.");
            if (string.IsNullOrWhiteSpace(m.Checked)) errors.Add($"{m.Name}: say how it was checked.");
        }
        if (errors.Count > 0) throw new InvalidDataException("makers.yaml:\n  " + string.Join("\n  ", errors));
    }
}

public sealed class MakerInfo
{
    public string Name { get; set; } = "";
    /// <summary>Lower-case substrings of the manufacturer Windows reports.</summary>
    public List<string> Manufacturers { get; set; } = [];
    public List<string> ModelContains { get; set; } = [];
    public string? BootMenuKey { get; set; }
    public string? BootMenuKeyLaptop { get; set; }
    public string? BootMenuKeyDesktop { get; set; }
    public string? BootMenuNote { get; set; }
    public string SupportUrl { get; set; } = "";
    public string? Source { get; set; }
    public string? Checked { get; set; }
    /// <summary>The key that opens the startup settings (BIOS setup).</summary>
    public string? SetupKey { get; set; }
    /// <summary>Menu path to the Secure Boot setting, with " > " between menus.</summary>
    public string? SecureBootPath { get; set; }
    public string? SecureBootSource { get; set; }

    internal PcMaker ToMaker(FormFactor formFactor, bool fromMotherboard) => new(
        Name,
        BootMenuKey ?? (formFactor == FormFactor.Laptop ? BootMenuKeyLaptop! : BootMenuKeyDesktop!),
        BootMenuNote,
        SupportUrl,
        fromMotherboard,
        SetupKey,
        SecureBootPath);
}
