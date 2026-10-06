namespace GreyHaven.Core.Hardware;

public enum CpuArchitecture { Unknown, X64, X86, Arm64 }

public enum FirmwareMode { Unknown, Uefi, LegacyBios }

public enum SecureBootState { Unknown, Enabled, Disabled, NotSupported }

/// <summary>How the system's storage controller is configured. Intel RST/RAID/VMD modes can hide disks from Linux installers.</summary>
public enum StorageControllerMode { Unknown, Ahci, Nvme, IntelVmd, IntelRstRaid, Raid }

public enum FastStartupState { Unknown, Enabled, Disabled }

public enum BitLockerState
{
    Unknown,
    NotEncrypted,
    On,
    Encrypting,
    Decrypting,
    Suspended,
    Locked,
    /// <summary>Device encryption is prepared but not yet active (no recovery key protector yet).</summary>
    WaitingForActivation,
}

public enum FormFactor { Unknown, Desktop, Laptop }

public sealed record WindowsVersion(
    string? ProductName,
    string? DisplayVersion,
    int? Build,
    int? Revision,
    string? Edition)
{
    /// <summary>Windows 11 reports ProductName "Windows 10 ..." in the registry; the build number is authoritative.</summary>
    public bool IsWindows11 => Build >= 22000;

    /// <summary>Maps the registry EditionID (e.g. "Core") to the marketing name (e.g. "Home").</summary>
    public string? FriendlyEdition => Edition switch
    {
        null or "" => null,
        "Core" => "Home",
        "CoreN" => "Home N",
        "CoreSingleLanguage" => "Home Single Language",
        "Professional" => "Pro",
        "ProfessionalN" => "Pro N",
        "ProfessionalWorkstation" => "Pro for Workstations",
        "ProfessionalEducation" => "Pro Education",
        "EnterpriseS" => "Enterprise LTSC",
        "IoTEnterpriseS" => "IoT Enterprise LTSC",
        _ => Edition,
    };

    public string FriendlyName
    {
        get
        {
            if (Build is null) return ProductName ?? "Windows (unknown version)";
            var family = IsWindows11 ? "Windows 11" : "Windows 10";
            var edition = FriendlyEdition is null ? "" : $" {FriendlyEdition}";
            var display = DisplayVersion is null ? "" : $" {DisplayVersion}";
            var rev = Revision is null ? "" : $".{Revision}";
            return $"{family}{edition}{display} (build {Build}{rev})";
        }
    }
}

/// <summary>System-level facts relevant to installing Linux.</summary>
public sealed record SystemInfo
{
    public required WindowsVersion Windows { get; init; }
    public CpuArchitecture Architecture { get; init; }
    public FirmwareMode Firmware { get; init; }
    public SecureBootState SecureBoot { get; init; }
    public StorageControllerMode StorageMode { get; init; }
    public FastStartupState FastStartup { get; init; }
    /// <summary>Fast Startup has no effect when hibernation is off.</summary>
    public bool? HibernationEnabled { get; init; }
    public BitLockerState BitLocker { get; init; }
    public FormFactor FormFactor { get; init; }
    public bool? HasBattery { get; init; }
    /// <summary>e.g. "LENOVO". Not personal data; useful for model-specific advice.</summary>
    public string? SystemManufacturer { get; init; }
    public string? SystemModel { get; init; }
    /// <summary>
    /// Motherboard maker, e.g. "ASUSTeK COMPUTER INC.". On custom-built PCs this is the useful
    /// one: the PC "manufacturer" is often a placeholder like "System manufacturer".
    /// </summary>
    public string? BoardManufacturer { get; init; }

    public bool IsArm => Architecture == CpuArchitecture.Arm64;
}
