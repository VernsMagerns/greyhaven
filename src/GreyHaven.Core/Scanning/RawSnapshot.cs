namespace GreyHaven.Core.Scanning;

/// <summary>
/// Unprocessed data read from Windows (WMI, registry, Win32). Kept separate from
/// <see cref="ProfileBuilder"/> so the interpretation logic can be tested on any OS and
/// real-machine dumps (<c>greyhaven scan --dump-raw</c>) can be replayed for debugging.
///
/// Privacy: PnP instance paths (which can embed serial numbers), MAC addresses, host names,
/// user names and Bluetooth/software-enumerated devices (which can carry user-chosen names)
/// are never read into this structure.
/// </summary>
public sealed record RawSnapshot
{
    public int SchemaVersion { get; init; } = 1;
    public DateTimeOffset CapturedAt { get; init; }
    /// <summary>Set on hand-written sample snapshots.</summary>
    public string? Description { get; init; }
    public required RawSystemData System { get; init; }
    public IReadOnlyList<RawProcessor> Processors { get; init; } = [];
    public IReadOnlyList<RawPnpDevice> Devices { get; init; } = [];
}

/// <summary>A row from WMI <c>Win32_PnPEntity</c>.</summary>
public sealed record RawPnpDevice
{
    public string? Name { get; init; }
    public string? Manufacturer { get; init; }
    /// <summary>Device setup class, e.g. "Net", "Display", "Bluetooth", "Camera", "Biometric".</summary>
    public string? PnpClass { get; init; }
    /// <summary>Driver service name, e.g. "Netwtw10", "iaStorVD".</summary>
    public string? Service { get; init; }
    public IReadOnlyList<string> HardwareIds { get; init; } = [];
    public IReadOnlyList<string> CompatibleIds { get; init; } = [];
    /// <summary>0 = working; 28 = no driver installed; null = unknown.</summary>
    public int? ConfigManagerErrorCode { get; init; }
    public bool? Present { get; init; }
}

/// <summary>A row from WMI <c>Win32_Processor</c>. <c>ProcessorId</c> is deliberately not read.</summary>
public sealed record RawProcessor
{
    public string? Name { get; init; }
    public string? Manufacturer { get; init; }
    /// <summary>e.g. "Intel64 Family 6 Model 140 Stepping 1".</summary>
    public string? Caption { get; init; }
    public int? NumberOfCores { get; init; }
    public int? NumberOfLogicalProcessors { get; init; }
}

public sealed record RawSystemData
{
    /// <summary><c>GetFirmwareType</c>: 1 = BIOS, 2 = UEFI.</summary>
    public int? FirmwareType { get; init; }

    /// <summary>Registry <c>SecureBoot\State\UEFISecureBootEnabled</c>; null when the value is absent.</summary>
    public int? SecureBootEnabled { get; init; }

    /// <summary>Registry <c>Session Manager\Power\HiberbootEnabled</c> (Fast Startup).</summary>
    public int? HiberbootEnabled { get; init; }

    /// <summary>Registry <c>Control\Power\HibernateEnabled</c>.</summary>
    public int? HibernateEnabled { get; init; }

    /// <summary>Shell property <c>System.Volume.BitLockerProtection</c> for the system drive.</summary>
    public int? BitLockerProtection { get; init; }

    public string? WindowsProductName { get; init; }
    public string? WindowsDisplayVersion { get; init; }
    public string? WindowsCurrentBuild { get; init; }
    public int? WindowsUbr { get; init; }
    public string? WindowsEditionId { get; init; }

    /// <summary><c>IsWow64Process2</c> native machine: 0x8664 = x64, 0xAA64 = ARM64, 0x014C = x86.</summary>
    public int? NativeMachine { get; init; }

    /// <summary><c>Win32_SystemEnclosure.ChassisTypes</c> (SMBIOS chassis type codes).</summary>
    public IReadOnlyList<int> ChassisTypes { get; init; } = [];

    /// <summary>Whether <c>Win32_Battery</c> returned any rows.</summary>
    public bool? HasBattery { get; init; }

    public string? SystemManufacturer { get; init; }
    public string? SystemModel { get; init; }

    /// <summary><c>Win32_LogicalDisk</c> FreeSpace and Size of the Windows drive (usually C:).</summary>
    public long? SystemDriveFreeBytes { get; init; }
    public long? SystemDriveTotalBytes { get; init; }

    /// <summary><c>Win32_BaseBoard.Manufacturer</c>: the motherboard maker, e.g. "Micro-Star International Co., Ltd.".</summary>
    public string? BaseBoardManufacturer { get; init; }

    /// <summary>Plain-English notes about values that could not be read.</summary>
    public IReadOnlyList<string> ReadWarnings { get; init; } = [];
}
