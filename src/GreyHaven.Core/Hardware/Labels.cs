using GreyHaven.Core.Compatibility;

namespace GreyHaven.Core.Hardware;

/// <summary>Plain-English labels for display. Shared by the CLI, UI and reports.</summary>
public static class Labels
{
    public static string Of(ComponentCategory c) => c switch
    {
        ComponentCategory.Cpu => "Processor",
        ComponentCategory.Gpu => "Graphics",
        ComponentCategory.StorageController => "Storage controller",
        ComponentCategory.NvmeDrive => "NVMe SSD",
        ComponentCategory.Wifi => "Wi-Fi",
        ComponentCategory.Ethernet => "Wired network (Ethernet)",
        ComponentCategory.UsbNetwork => "USB network adapter",
        ComponentCategory.Bluetooth => "Bluetooth",
        ComponentCategory.AudioController => "Sound controller",
        ComponentCategory.AudioCodec => "Sound chip (speakers, headphones, mic)",
        ComponentCategory.DisplayAudio => "HDMI/DisplayPort audio",
        ComponentCategory.Webcam => "Camera",
        ComponentCategory.Touchpad => "Touchpad",
        ComponentCategory.Touchscreen => "Touchscreen",
        ComponentCategory.FingerprintReader => "Fingerprint reader",
        ComponentCategory.CardReader => "Memory card reader",
        ComponentCategory.Thunderbolt => "Thunderbolt",
        ComponentCategory.UsbController => "USB controller",
        ComponentCategory.InputDevice => "Keyboard, mouse or other input",
        ComponentCategory.Chipset => "System chipset",
        _ => "Other device",
    };

    /// <summary>How much a part matters, in plain words: "Essential", "Important" or "Other".</summary>
    public static string Of(Importance i) => i switch
    {
        Importance.Critical => "Essential",
        Importance.Important => "Important",
        _ => "Other",
    };

    /// <summary>What each importance means, for report keys.</summary>
    public static string Explain(Importance i) => i switch
    {
        Importance.Critical => "Essential parts: the PC can't be used properly without them. They count most toward the score.",
        Importance.Important => "Important parts: you'd notice if they didn't work. They count less toward the score.",
        _ => "Other parts: count least toward the score; some, such as chipset parts, aren't scored at all.",
    };

    public static string Of(SecureBootState s) => s switch
    {
        SecureBootState.Enabled => "On",
        SecureBootState.Disabled => "Off",
        SecureBootState.NotSupported => "Not available (legacy BIOS mode)",
        _ => "Unknown",
    };

    public static string Of(FirmwareMode f) => f switch
    {
        FirmwareMode.Uefi => "UEFI",
        FirmwareMode.LegacyBios => "Legacy BIOS",
        _ => "Unknown",
    };

    public static string Of(StorageControllerMode m) => m switch
    {
        StorageControllerMode.Ahci => "AHCI (Linux-friendly)",
        StorageControllerMode.Nvme => "NVMe (Linux-friendly)",
        StorageControllerMode.IntelVmd => "Intel VMD / RST",
        StorageControllerMode.IntelRstRaid => "Intel RST RAID",
        StorageControllerMode.Raid => "RAID",
        _ => "Unknown",
    };

    public static string Of(FastStartupState f) => f switch
    {
        FastStartupState.Enabled => "On",
        FastStartupState.Disabled => "Off",
        _ => "Unknown",
    };

    public static string Of(BitLockerState b) => b switch
    {
        BitLockerState.NotEncrypted => "Off",
        BitLockerState.On => "On",
        BitLockerState.Encrypting => "Encrypting",
        BitLockerState.Decrypting => "Decrypting",
        BitLockerState.Suspended => "Suspended",
        BitLockerState.Locked => "On (locked)",
        BitLockerState.WaitingForActivation => "Waiting for activation",
        _ => "Unknown",
    };

    public static string Of(CpuArchitecture a) => a switch
    {
        CpuArchitecture.X64 => "x64",
        CpuArchitecture.X86 => "x86 (32-bit)",
        CpuArchitecture.Arm64 => "ARM64",
        _ => "Unknown",
    };

    public static string Of(FormFactor f) => f switch
    {
        FormFactor.Laptop => "Laptop",
        FormFactor.Desktop => "Desktop",
        _ => "Unknown",
    };
}
