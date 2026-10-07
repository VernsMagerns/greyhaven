namespace GreyHaven.Core.Scanning;

/// <summary>
/// Small, stable ID tables used only to <em>categorise</em> devices and detect the storage mode.
/// Compatibility ratings never come from here; they come from the data layer (milestone 2).
/// </summary>
public static class KnownDeviceIds
{
    /// <summary>
    /// Intel Volume Management Device controllers (PCI vendor 8086), as listed in the Linux
    /// kernel's drivers/pci/controller/vmd.c. Disks behind VMD are hidden from Windows' PCI view.
    /// </summary>
    public static readonly IReadOnlySet<string> IntelVmdDevices = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "201d", "28c0", "467f", "4c3d", "7d0b", "9a0b", "a77f", "ad0b", "b60b", "b06f",
    };

    /// <summary>Windows storage driver services that indicate Intel VMD.</summary>
    public static readonly IReadOnlySet<string> IntelVmdServices = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "iaStorVD",
    };

    /// <summary>Windows storage driver services that indicate Intel RST in RAID mode.</summary>
    public static readonly IReadOnlySet<string> IntelRstServices = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "iaStorA", "iaStorAC", "iaStorAVC", "iaStorV",
    };

    /// <summary>
    /// Intel Image Processing Unit (IPU6 family) PCI devices (vendor 8086). Windows exposes these
    /// as generic system devices, but they are the laptop's camera pipeline.
    /// </summary>
    public static readonly IReadOnlySet<string> IntelIpuDevices = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "9a19", "9a39", "465d", "462e", "a75d", "7d19",
    };

    /// <summary>USB vendors that only make fingerprint sensors, used when no Windows driver is installed.</summary>
    public static readonly IReadOnlySet<string> FingerprintOnlyUsbVendors = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "27c6", // Goodix
        "138a", // Validity Sensors (Synaptics)
        "1c7a", // LighTuning / Egis
        "2808", // FocalTech fingerprint
        "10a5", // Fingerprint Cards (FPC)
        "147e", // UPEK
        "08ff", // AuthenTec
        "298d", // Next Biometrics
    };

    /// <summary>
    /// ACPI ID prefixes of PS/2 touchpad makers (e.g. "SYN1E5A" Synaptics, "ETD0108" Elantech).
    /// Older laptops expose the touchpad as an ACPI mouse rather than a HID device.
    /// </summary>
    public static readonly IReadOnlyList<string> AcpiTouchpadPrefixes = ["SYN", "ETD", "ELAN", "ALP", "DLL", "LEN"];

    /// <summary>HD Audio codec vendors whose codecs on PCs are HDMI/DisplayPort audio on the GPU.</summary>
    public static readonly IReadOnlySet<string> DisplayAudioCodecVendors = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "8086", "10de", "1002",
    };

    /// <summary>
    /// Software devices that pretend to be hardware, keyed like <c>DeviceId.Key</c>. They exist
    /// only inside Windows, so they're left out of the scan.
    /// </summary>
    public static readonly IReadOnlySet<string> VirtualDevices = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "usb:0955:9000", // NVIDIA Virtual Audio Device (Wave Extensible), installed with NVIDIA's driver (nvvad.inf)
    };

    /// <summary>
    /// AMD placeholder PCI functions on Ryzen 3000/Threadripper 3000 ("Starship/Matisse") that
    /// have no driver on any OS (class 13, non-essential instrumentation). Listed in case Windows
    /// doesn't report their class code.
    /// </summary>
    public static readonly IReadOnlySet<string> PlaceholderPciDevices = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "pci:1022:1485", // Starship/Matisse Reserved SPP
        "pci:1022:148a", // Starship/Matisse PCIe Dummy Function
    };

    /// <summary>USB vendors that only make audio interfaces, used when a maker's driver hides the USB class.</summary>
    public static readonly IReadOnlySet<string> AudioOnlyUsbVendors = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "1235", // Focusrite (Scarlett, Clarett, Vocaster)
    };
}
