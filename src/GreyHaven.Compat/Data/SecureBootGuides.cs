using GreyHaven.Core;
using GreyHaven.Core.Compatibility;
using GreyHaven.Core.Hardware;

namespace GreyHaven.Compat.Data;

/// <summary>Contents of <c>data/secure-boot-guides.yaml</c>.</summary>
public sealed class SecureBootGuideCatalog
{
    public FallbackGuide Fallback { get; set; } = new();
    /// <summary>Matched against the PC maker (and model/form factor).</summary>
    public List<ManufacturerGuide> Guides { get; set; } = [];
    /// <summary>Matched against the motherboard maker when the PC maker isn't recognised, e.g. on a custom-built PC.</summary>
    public List<ManufacturerGuide> Motherboards { get; set; } = [];

    /// <summary>
    /// The PC maker's own guide when the manufacturer is recognised; otherwise the motherboard
    /// maker's; otherwise GreyHaven's general guide.
    /// </summary>
    public GuideLink For(HardwareProfile profile)
    {
        var system = profile.System;
        var cpuVendor = profile.Components.FirstOrDefault(c => c.Cpu is not null)?.Cpu!.Vendor;
        var maker = system.SystemManufacturer?.ToLowerInvariant() ?? "";
        var model = system.SystemModel?.ToLowerInvariant() ?? "";
        foreach (var g in Guides)
        {
            if (!g.Manufacturers.Any(m => maker.Contains(m, StringComparison.Ordinal))) continue;
            if (g.ModelContains.Count > 0 && !g.ModelContains.Any(m => model.Contains(m, StringComparison.Ordinal))) continue;
            if (g.FormFactor is not null && !string.Equals(g.FormFactor, system.FormFactor.ToString(), StringComparison.OrdinalIgnoreCase)) continue;
            return new GuideLink(g.Title, g.Url, FromManufacturer: true);
        }

        var board = system.BoardManufacturer?.ToLowerInvariant() ?? "";
        foreach (var g in Motherboards)
        {
            if (!g.Manufacturers.Any(m => board.Contains(m, StringComparison.Ordinal))) continue;
            if (g.CpuVendors.Count > 0 && !g.CpuVendors.Contains(cpuVendor ?? "", StringComparer.OrdinalIgnoreCase)) continue;
            return new GuideLink(g.Title, g.Url, FromManufacturer: true) { MatchedBy = "motherboard" };
        }

        // GreyHaven's own guide ships with the app. Link the web copy only once the repository
        // is set and public; never a placeholder or a private (not found) URL.
        return new GuideLink(
            Fallback.Title,
            GreyHavenInfo.CanLinkToRepository ? $"https://github.com/{GreyHavenInfo.GitHubRepository}/blob/main/{Fallback.Path}" : null,
            FromManufacturer: false)
        {
            LocalPath = Fallback.Path,
        };
    }
}

public sealed class FallbackGuide
{
    public string Title { get; set; } = "";
    /// <summary>Repository-relative path; also shipped next to the app.</summary>
    public string Path { get; set; } = "";
}

public sealed class ManufacturerGuide
{
    /// <summary>Lower-case substrings of the manufacturer Windows reports, e.g. "dell".</summary>
    public List<string> Manufacturers { get; set; } = [];
    /// <summary>Optional lower-case substrings of the model, e.g. "think".</summary>
    public List<string> ModelContains { get; set; } = [];
    /// <summary>Optional: laptop | desktop.</summary>
    public string? FormFactor { get; set; }
    /// <summary>Optional, motherboard guides only: CPU vendors the guide is written for, e.g. "AuthenticAMD".</summary>
    public List<string> CpuVendors { get; set; } = [];
    public string Title { get; set; } = "";
    public string Url { get; set; } = "";
    public string? Checked { get; set; }
}

/// <summary>Contents of <c>data/dataset.yaml</c>.</summary>
public sealed class DatasetInfo
{
    /// <summary>ISO date, e.g. "2026-10-05".</summary>
    public string BuiltOn { get; set; } = "";

    public DateOnly BuiltOnDate =>
        DateOnly.TryParseExact(BuiltOn, "yyyy-MM-dd", out var d) ? d : throw new InvalidDataException($"dataset.yaml: builtOn '{BuiltOn}' must be a date like 2026-10-05.");
}
