namespace GreyHaven.Core.Hardware;

/// <summary>What a component is. Drives importance weighting and which advice applies.</summary>
public enum ComponentCategory
{
    Cpu,
    Gpu,
    StorageController,
    NvmeDrive,
    Wifi,
    Ethernet,
    /// <summary>USB network adapter; Windows does not reliably say whether it is Wi-Fi or Ethernet.</summary>
    UsbNetwork,
    Bluetooth,
    AudioController,
    AudioCodec,
    /// <summary>HDMI/DisplayPort audio codec on a GPU.</summary>
    DisplayAudio,
    Webcam,
    Touchpad,
    Touchscreen,
    FingerprintReader,
    CardReader,
    Thunderbolt,
    UsbController,
    InputDevice,
    /// <summary>Chipset infrastructure: bridges, SMBus, management engine, sensors.</summary>
    Chipset,
    Other,
}

/// <summary>CPU details. CPUs have no PCI/USB ID, so they are identified by vendor and CPUID family/model.</summary>
public sealed record CpuDetails(
    string Vendor,
    int? Family,
    int? Model,
    int? Stepping,
    int? Cores,
    int? LogicalProcessors);

/// <summary>One scanned component. Contains no serial numbers, instance paths or user-identifying data.</summary>
public sealed record HardwareComponent
{
    public required ComponentCategory Category { get; init; }

    /// <summary>Friendly name for display only. Never used for compatibility lookups.</summary>
    public required string Name { get; init; }

    public string? Manufacturer { get; init; }

    /// <summary>Hardware ID. Null only for CPUs (see <see cref="Cpu"/>).</summary>
    public DeviceId? Id { get; init; }

    public CpuDetails? Cpu { get; init; }

    /// <summary>Windows driver service name, e.g. "Netwtw10". Technical detail; shown one click away.</summary>
    public string? WindowsDriver { get; init; }

    /// <summary>Windows Device Manager problem code; 0 = working, 28 = no driver installed.</summary>
    public int? WindowsProblemCode { get; init; }

    /// <summary>How many identical devices were found (e.g. two identical NICs).</summary>
    public int Count { get; init; } = 1;
}
