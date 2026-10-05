using GreyHaven.Core.Hardware;

namespace GreyHaven.Compat.Kernel;

/// <summary>
/// Builds the Linux "modalias" string a device would present, from its Windows IDs, so it can be
/// matched against module alias patterns.
///
/// Fields Windows doesn't tell us (e.g. a HID device's group, a USB device's bcdDevice) are filled
/// with 'Z', which is not a hex digit: they only match a <c>*</c> in a pattern. That makes matching
/// conservative: a driver that needs a specific value we don't know is never assumed to bind.
/// </summary>
public static class Modalias
{
    private const char Unknown = 'Z';

    public static string? For(DeviceId id) => id.Bus switch
    {
        DeviceBus.Pci => Pci(id),
        DeviceBus.Usb => Usb(id),
        DeviceBus.HdAudio => HdAudio(id),
        DeviceBus.Hid => Hid(id),
        DeviceBus.Acpi => $"acpi:{id.Vendor}:",
        _ => null,
    };

    /// <summary>The alias prefix used by modules for this bus, e.g. "pci:" or "acpi*:".</summary>
    public static string PatternPrefix(DeviceBus bus) => bus switch
    {
        DeviceBus.Pci => "pci:",
        DeviceBus.Usb => "usb:",
        DeviceBus.HdAudio => "hdaudio:",
        DeviceBus.Hid => "hid:",
        DeviceBus.Acpi => "acpi",
        _ => "",
    };

    // pci:v00008086d00002723sv00008086sd00000084bc02sc80i00
    private static string Pci(DeviceId id)
    {
        var cc = id.ClassCode;
        return "pci:" +
               $"v{Pad(id.Vendor, 8)}d{Pad(id.Device, 8)}" +
               $"sv{Pad(id.SubVendor, 8)}sd{Pad(id.SubDevice, 8)}" +
               $"bc{Part(cc, 0)}sc{Part(cc, 2)}i{(cc is { Length: 6 } ? cc[4..].ToUpperInvariant() : new string(Unknown, 2))}";
    }

    // usb:v8087p0026d0002dcE0dsc01dp01icE0isc01ip01in00
    private static string Usb(DeviceId id)
    {
        var ic = id.InterfaceClass;
        var rev = id.Revision is { Length: 4 } r ? r.ToUpperInvariant() : new string(Unknown, 4);
        return "usb:" +
               $"v{Pad(id.Vendor, 4)}p{Pad(id.Device, 4)}d{rev}" +
               $"dc{U2}dsc{U2}dp{U2}" +
               $"ic{Part(ic, 0)}isc{Part(ic, 2)}ip{Part(ic, 4)}in{U2}";
    }

    // hdaudio:v10EC0287r00100000a01  (revision unknown from Windows' 4-digit REV)
    private static string HdAudio(DeviceId id) =>
        $"hdaudio:v{Pad(id.Vendor, 4)}{Pad(id.Device, 4)}r{new string(Unknown, 8)}a01";

    // hid:b0018g0000v000006CBp0000CE26 - bus (I2C vs USB) and group are unknown from Windows.
    private static string Hid(DeviceId id) =>
        $"hid:b{new string(Unknown, 4)}g{new string(Unknown, 4)}v{Pad(id.Vendor, 8)}p{Pad(id.Device, 8)}";

    private static readonly string U2 = new(Unknown, 2);

    private static string Pad(string? hex, int width) =>
        hex is null ? new string(Unknown, width) : hex.ToUpperInvariant().PadLeft(width, '0');

    private static string Part(string? hex, int offset) =>
        hex is not null && hex.Length >= offset + 2 ? hex.Substring(offset, 2).ToUpperInvariant() : U2;
}
