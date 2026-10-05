namespace GreyHaven.Core.Hardware;

/// <summary>The bus a device is enumerated on. Determines how its IDs are matched on Linux.</summary>
public enum DeviceBus
{
    Pci,
    Usb,
    /// <summary>HD Audio codec (e.g. Realtek ALC287). Linux matches these by codec vendor/device ID.</summary>
    HdAudio,
    /// <summary>HID device (I2C or USB), e.g. a touchpad. Linux matches these by HID vendor/product.</summary>
    Hid,
    /// <summary>ACPI-enumerated device (common on ARM PCs). Vendor holds the full ACPI/PNP ID.</summary>
    Acpi,
}

/// <summary>
/// A hardware identifier. All hex values are lower-case without a prefix, e.g. vendor "8086",
/// device "2723". Compatibility lookups use these IDs only, never friendly names.
/// </summary>
public sealed record DeviceId(
    DeviceBus Bus,
    string Vendor,
    string Device,
    string? SubVendor = null,
    string? SubDevice = null,
    /// <summary>PCI class code as 4 or 6 hex digits, e.g. "0280" or "028000".</summary>
    string? ClassCode = null,
    string? Revision = null,
    /// <summary>USB interface class/subclass/protocol as 6 hex digits, e.g. "e00101" (Bluetooth), "0e0100" (video).</summary>
    string? InterfaceClass = null)
{
    /// <summary>"8086:2723" (or the ACPI ID for ACPI devices).</summary>
    public string VendorDevice => Bus == DeviceBus.Acpi ? Vendor : $"{Vendor}:{Device}";

    /// <summary>Stable lookup key, e.g. "pci:8086:2723" or "acpi:QCOM0C5D".</summary>
    public string Key => $"{Bus.ToString().ToLowerInvariant()}:{VendorDevice}";

    public bool HasSubsystem => SubVendor is not null && SubDevice is not null;

    /// <summary>"pci:8086:2723:8086:0084" when a subsystem is known, otherwise <see cref="Key"/>.</summary>
    public string FullKey => HasSubsystem ? $"{Key}:{SubVendor}:{SubDevice}" : Key;

    /// <summary>The two-digit base class of the PCI class code, e.g. "02" for network.</summary>
    public string? BaseClass => ClassCode is { Length: >= 2 } ? ClassCode[..2] : null;

    /// <summary>Base class + subclass, e.g. "0280".</summary>
    public string? ClassAndSubclass => ClassCode is { Length: >= 4 } ? ClassCode[..4] : null;

    public override string ToString() => FullKey;
}
