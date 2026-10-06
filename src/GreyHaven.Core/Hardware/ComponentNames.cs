namespace GreyHaven.Core.Hardware;

/// <summary>
/// Display names that tell apart separate devices Windows gives the same name, e.g. two
/// "High Definition Audio Controller"s (one on the motherboard, one on the graphics card).
/// Display only: compatibility lookups never use names.
/// </summary>
public static class ComponentNames
{
    /// <summary>
    /// One name per component, in the same order. Unique names are unchanged. Same-named parts
    /// of the same kind get their maker, e.g. "(AMD)" and "(NVIDIA)", when that tells them
    /// apart, otherwise "(1 of 2)", "(2 of 2)".
    /// </summary>
    public static IReadOnlyList<string> Distinct(IReadOnlyList<HardwareComponent> components)
    {
        var names = components.Select(c => c.Name).ToArray();
        var groups = components
            .Select((c, i) => (c, i))
            .GroupBy(x => (x.c.Category, Name: x.c.Name.ToLowerInvariant()))
            .Where(g => g.Count() > 1);

        foreach (var group in groups)
        {
            var members = group.ToList();
            var makers = members.Select(x => x.c.Id is { } id ? VendorName(id.Vendor) : null).ToList();
            var byMaker = makers.All(m => m is not null) && makers.Distinct(StringComparer.Ordinal).Count() == makers.Count;
            for (var n = 0; n < members.Count; n++)
            {
                var (c, i) = members[n];
                names[i] = byMaker ? $"{c.Name} ({makers[n]})" : $"{c.Name} ({n + 1} of {members.Count})";
            }
        }
        return names;
    }

    /// <summary>Common PCI/USB vendor IDs, for display only.</summary>
    public static string? VendorName(string vendorId) => vendorId.ToLowerInvariant() switch
    {
        "8086" or "8087" => "Intel",
        "1022" or "1002" => "AMD",
        "10de" => "NVIDIA",
        "10ec" or "0bda" => "Realtek",
        "14e4" or "0a5c" => "Broadcom",
        "1b21" => "ASMedia",
        "168c" or "0cf3" => "Qualcomm Atheros",
        "17cb" => "Qualcomm",
        "14c3" or "0e8d" => "MediaTek",
        "144d" => "Samsung",
        "1912" => "Renesas",
        "1106" => "VIA",
        "1b4b" => "Marvell",
        "046d" => "Logitech",
        _ => null,
    };
}
