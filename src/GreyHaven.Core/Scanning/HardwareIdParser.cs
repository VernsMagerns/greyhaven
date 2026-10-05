using System.Text.RegularExpressions;
using GreyHaven.Core.Hardware;

namespace GreyHaven.Core.Scanning;

/// <summary>
/// Parses Windows PnP hardware/compatible ID strings into a <see cref="DeviceId"/>.
///
/// Formats handled:
/// <code>
/// PCI\VEN_8086&amp;DEV_2723&amp;SUBSYS_00848086&amp;REV_1A   (SUBSYS = subdevice then subvendor)
/// PCI\CC_028000                                          (compatible ID: class code)
/// USB\VID_8087&amp;PID_0029&amp;REV_0001[&amp;MI_00]
/// HDAUDIO\FUNC_01&amp;VEN_10EC&amp;DEV_0287&amp;SUBSYS_17AA22C0   (SUBSYS = subvendor then subdevice)
/// HID\VEN_06CB&amp;DEV_CE26&amp;Col02  /  HID\VID_046D&amp;PID_C52B&amp;MI_00
/// ACPI\QCOM0C5D  /  ACPI\VEN_QCOM&amp;DEV_0C5D
/// </code>
/// </summary>
public static partial class HardwareIdParser
{
    [GeneratedRegex(@"^PCI\\VEN_(?<v>[0-9A-F]{4})&DEV_(?<d>[0-9A-F]{4})(?:&SUBSYS_(?<sd>[0-9A-F]{4})(?<sv>[0-9A-F]{4}))?(?:&REV_(?<r>[0-9A-F]{2}))?", RegexOptions.IgnoreCase)]
    private static partial Regex PciRegex();

    [GeneratedRegex(@"^PCI\\(?:VEN_[0-9A-F]{4}&(?:DEV_[0-9A-F]{4}&)?)?CC_(?<cc>[0-9A-F]{4,6})$", RegexOptions.IgnoreCase)]
    private static partial Regex PciClassRegex();

    [GeneratedRegex(@"^USB\\VID_(?<v>[0-9A-F]{4})&PID_(?<d>[0-9A-F]{4})(?:&REV_(?<r>[0-9A-F]{4}))?", RegexOptions.IgnoreCase)]
    private static partial Regex UsbRegex();

    [GeneratedRegex(@"^USB\\Class_(?<c>[0-9A-F]{2})", RegexOptions.IgnoreCase)]
    private static partial Regex UsbClassRegex();

    [GeneratedRegex(@"^HDAUDIO\\FUNC_(?<f>[0-9A-F]{2})&VEN_(?<v>[0-9A-F]{4})&DEV_(?<d>[0-9A-F]{4})(?:&SUBSYS_(?<sv>[0-9A-F]{4})(?<sd>[0-9A-F]{4}))?(?:&REV_(?<r>[0-9A-F]{4}))?", RegexOptions.IgnoreCase)]
    private static partial Regex HdAudioRegex();

    [GeneratedRegex(@"^HID\\(?:VEN|VID)_(?<v>[0-9A-Z]{3,4})&(?:DEV|PID)_(?<d>[0-9A-F]{4})", RegexOptions.IgnoreCase)]
    private static partial Regex HidRegex();

    [GeneratedRegex(@"^ACPI\\(?:VEN_(?<v>[0-9A-Z]{3,4})&DEV_(?<d>[0-9A-F]{4})|(?<id>[0-9A-Z]{3,4}[0-9A-F]{4}))$", RegexOptions.IgnoreCase)]
    private static partial Regex AcpiRegex();

    /// <summary>
    /// Builds the most specific <see cref="DeviceId"/> from a device's hardware and compatible IDs,
    /// or null if the device is not on a bus GreyHaven rates (e.g. ROOT\, SWD\, BTHENUM\).
    /// </summary>
    public static DeviceId? Parse(IReadOnlyList<string> hardwareIds, IReadOnlyList<string> compatibleIds)
    {
        var parsed = hardwareIds.Select(ParseSingle).OfType<DeviceId>().ToList();
        if (parsed.Count == 0) return null;

        var first = parsed[0];
        if (first.Bus != DeviceBus.Pci) return first;

        // Windows lists the most specific PCI ID (with SUBSYS and REV) first, but don't rely
        // on ordering. The class code lives in a separate ID, usually in CompatibleIds.
        var best = parsed.FirstOrDefault(p => p.Bus == DeviceBus.Pci && p.HasSubsystem) ?? first;
        var cc = FindPciClassCode(hardwareIds) ?? FindPciClassCode(compatibleIds);
        return best with { ClassCode = cc };
    }

    public static DeviceId? ParseSingle(string id)
    {
        if (string.IsNullOrWhiteSpace(id)) return null;
        id = id.Trim();

        var m = PciRegex().Match(id);
        if (m.Success)
            return new DeviceId(DeviceBus.Pci, Hex(m, "v")!, Hex(m, "d")!, Hex(m, "sv"), Hex(m, "sd"), Revision: Hex(m, "r"));

        m = UsbRegex().Match(id);
        if (m.Success)
            return new DeviceId(DeviceBus.Usb, Hex(m, "v")!, Hex(m, "d")!, Revision: Hex(m, "r"));

        m = HdAudioRegex().Match(id);
        if (m.Success)
            return new DeviceId(DeviceBus.HdAudio, Hex(m, "v")!, Hex(m, "d")!, Hex(m, "sv"), Hex(m, "sd"), Revision: Hex(m, "r"));

        m = HidRegex().Match(id);
        if (m.Success)
            return new DeviceId(DeviceBus.Hid, Hex(m, "v")!, Hex(m, "d")!);

        m = AcpiRegex().Match(id);
        if (m.Success)
        {
            var acpi = m.Groups["id"].Success
                ? m.Groups["id"].Value
                : m.Groups["v"].Value + m.Groups["d"].Value;
            acpi = acpi.ToUpperInvariant();
            return new DeviceId(DeviceBus.Acpi, acpi, "");
        }
        return null;
    }

    /// <summary>Returns the PCI class code (preferring the 6-digit form), e.g. "028000".</summary>
    public static string? FindPciClassCode(IEnumerable<string> ids)
    {
        string? best = null;
        foreach (var id in ids)
        {
            var m = PciClassRegex().Match(id.Trim());
            if (!m.Success) continue;
            var cc = m.Groups["cc"].Value.ToLowerInvariant();
            if (best is null || cc.Length > best.Length) best = cc;
        }
        return best;
    }

    /// <summary>Returns the USB interface class (two hex digits, e.g. "e0" for Bluetooth) if present.</summary>
    public static string? FindUsbClass(IEnumerable<string> ids)
    {
        foreach (var id in ids)
        {
            var m = UsbClassRegex().Match(id.Trim());
            if (m.Success) return m.Groups["c"].Value.ToLowerInvariant();
        }
        return null;
    }

    /// <summary>True for USB composite-device interface entries (…&amp;MI_xx).</summary>
    public static bool IsUsbInterface(IEnumerable<string> hardwareIds) =>
        hardwareIds.Any(h => h.Contains("&MI_", StringComparison.OrdinalIgnoreCase));

    /// <summary>The HDA function group: "01" = audio, "02" = modem.</summary>
    public static string? FindHdAudioFunction(IEnumerable<string> hardwareIds)
    {
        foreach (var id in hardwareIds)
        {
            var m = HdAudioRegex().Match(id.Trim());
            if (m.Success) return m.Groups["f"].Value;
        }
        return null;
    }

    private static string? Hex(Match m, string group) =>
        m.Groups[group].Success ? m.Groups[group].Value.ToLowerInvariant() : null;
}
