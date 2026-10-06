using GreyHaven.Core.Hardware;

namespace GreyHaven.Compat.Data;

/// <summary>Contents of <c>data/live-usb.yaml</c>.</summary>
public sealed class LiveUsbCatalog
{
    public List<LiveUsbCheckInfo> Checks { get; set; } = [];
    public HwProbeInfo HwProbe { get; set; } = new();

    public void Validate()
    {
        var categories = Enum.GetNames<ComponentCategory>().Select(n => char.ToLowerInvariant(n[0]) + n[1..]).ToHashSet();
        var errors = new List<string>();
        foreach (var c in Checks)
        {
            if (string.IsNullOrWhiteSpace(c.Title) || string.IsNullOrWhiteSpace(c.Test)) errors.Add($"check '{c.Id}' needs a title and a test.");
            if (c.Categories.Count == 0) errors.Add($"check '{c.Id}' needs at least one category.");
            foreach (var cat in c.Categories.Where(cat => !categories.Contains(cat)))
                errors.Add($"check '{c.Id}': unknown category '{cat}'.");
        }
        if (!HwProbe.Commands.ContainsKey("default")) errors.Add("hwProbe.commands needs a 'default' entry.");
        if (errors.Count > 0) throw new InvalidDataException("live-usb.yaml:\n  " + string.Join("\n  ", errors));
    }
}

public sealed class LiveUsbCheckInfo
{
    public string Id { get; set; } = "";
    /// <summary>camelCase <see cref="ComponentCategory"/> names; the check is shown when the PC has one.</summary>
    public List<string> Categories { get; set; } = [];
    public string Title { get; set; } = "";
    public string Test { get; set; } = "";

    public bool AppliesTo(ComponentCategory c) =>
        Categories.Contains(char.ToLowerInvariant(c.ToString()[0]) + c.ToString()[1..]);
}

public sealed class HwProbeInfo
{
    /// <summary>Terminal commands per distro id, or "default".</summary>
    public Dictionary<string, List<string>> Commands { get; set; } = [];
    public string? Note { get; set; }
    public SourceLink? Source { get; set; }

    public IReadOnlyList<string> CommandsFor(string distroId) =>
        Commands.TryGetValue(distroId, out var c) ? c : Commands.GetValueOrDefault("default") ?? [];
}

/// <summary>A distro's official pages for making a USB stick and starting a live session.</summary>
public sealed class DistroLiveUsb
{
    public SourceLink? MakeUsb { get; set; }
    /// <summary>Omit when <see cref="MakeUsb"/> covers starting the live session too.</summary>
    public SourceLink? TryIt { get; set; }
}
