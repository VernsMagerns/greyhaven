using GreyHaven.Core.Hardware;
using GreyHaven.Core.Scanning;

namespace GreyHaven.Tests.Scanning;

public class ProfileBuilderTests
{
    private static RawSnapshot Snapshot(RawSystemData? system = null, IReadOnlyList<RawPnpDevice>? devices = null, IReadOnlyList<RawProcessor>? processors = null) => new()
    {
        CapturedAt = new DateTimeOffset(2026, 10, 1, 0, 0, 0, TimeSpan.Zero),
        System = system ?? new RawSystemData(),
        Devices = devices ?? [],
        Processors = processors ?? [new RawProcessor { Name = "Test CPU", Manufacturer = "GenuineIntel", Caption = "Intel64 Family 6 Model 140 Stepping 1", NumberOfCores = 4 }],
    };

    private static RawPnpDevice Pci(string vd, string cc, string pnpClass = "System", string? service = null, string name = "dev") => new()
    {
        Name = name,
        PnpClass = pnpClass,
        Service = service,
        HardwareIds = [$@"PCI\VEN_{vd[..4]}&DEV_{vd[5..]}&SUBSYS_00000000&REV_01"],
        CompatibleIds = [$@"PCI\CC_{cc}"],
        ConfigManagerErrorCode = 0,
        Present = true,
    };

    private static HardwareProfile Build(RawSnapshot s) => ProfileBuilder.Build(s, "test");

    [Fact]
    public void Usb_composite_entries_merge_into_most_specific_category()
    {
        RawPnpDevice[] devices =
        [
            new() { Name = "USB Composite Device", PnpClass = "USB", HardwareIds = [@"USB\VID_046D&PID_082D&REV_0011"] },
            new() { Name = "C920 mic", PnpClass = "MEDIA", HardwareIds = [@"USB\VID_046D&PID_082D&REV_0011&MI_02"], CompatibleIds = [@"USB\Class_01"] },
            new() { Name = "HD Pro Webcam C920", PnpClass = "Camera", Service = "usbvideo", HardwareIds = [@"USB\VID_046D&PID_082D&REV_0011&MI_00"], CompatibleIds = [@"USB\Class_0e"] },
        ];

        var p = Build(Snapshot(devices: devices));

        var cam = Assert.Single(p.Components, c => c.Id?.Bus == DeviceBus.Usb);
        Assert.Equal(ComponentCategory.Webcam, cam.Category);
        Assert.Equal("HD Pro Webcam C920", cam.Name);
        Assert.Equal("usbvideo", cam.WindowsDriver);
        Assert.Equal(1, cam.Count);
    }

    [Fact]
    public void Identical_pci_devices_are_counted()
    {
        var p = Build(Snapshot(devices: [Pci("8086:1533", "020000", "Net"), Pci("8086:1533", "020000", "Net")]));

        var nic = Assert.Single(p.Components, c => c.Category == ComponentCategory.Ethernet);
        Assert.Equal(2, nic.Count);
    }

    [Fact]
    public void Non_present_devices_are_skipped()
    {
        var p = Build(Snapshot(devices: [Pci("10de:2484", "030000", "Display") with { Present = false }]));
        Assert.DoesNotContain(p.Components, c => c.Category == ComponentCategory.Gpu);
    }

    [Fact]
    public void Problem_code_is_kept()
    {
        var p = Build(Snapshot(devices: [Pci("14e4:4365", "028000", "Net") with { ConfigManagerErrorCode = 28 }]));
        Assert.Equal(28, Assert.Single(p.Components, c => c.Category == ComponentCategory.Wifi).WindowsProblemCode);
    }

    [Fact]
    public void Missing_name_gets_id_based_fallback()
    {
        var p = Build(Snapshot(devices: [Pci("14e4:4365", "028000", "Net") with { Name = "  " }]));
        Assert.Equal("Unnamed Wifi device (14e4:4365)", Assert.Single(p.Components, c => c.Category == ComponentCategory.Wifi).Name);
    }

    [Fact]
    public void Cpu_family_and_model_are_parsed()
    {
        var cpu = Assert.Single(Build(Snapshot()).Components, c => c.Category == ComponentCategory.Cpu).Cpu!;
        Assert.Equal((6, 140, 1, 4), (cpu.Family!.Value, cpu.Model!.Value, cpu.Stepping!.Value, cpu.Cores!.Value));
    }

    [Fact]
    public void Missing_processor_is_a_warning_not_a_crash()
    {
        var p = Build(Snapshot(processors: []) with { Processors = [] });
        Assert.Contains(p.Warnings, w => w.Contains("processor"));
    }

    // ---- Storage mode ----

    public static TheoryData<RawPnpDevice[], StorageControllerMode> StorageCases => new()
    {
        { [Pci("8086:467f", "010400", "SCSIAdapter", "iaStorVD")], StorageControllerMode.IntelVmd },
        { [Pci("8086:9a0b", "088000", "SCSIAdapter")], StorageControllerMode.Unknown }, // VMD ID but not a storage class: not a storage controller
        { [Pci("8086:9a0b", "010400", "SCSIAdapter")], StorageControllerMode.IntelVmd }, // VMD by ID without the Intel driver
        { [Pci("8086:282a", "010400", "SCSIAdapter", "iaStorAC")], StorageControllerMode.IntelRstRaid },
        { [Pci("8086:282a", "010400", "SCSIAdapter")], StorageControllerMode.IntelRstRaid },
        { [Pci("1022:7905", "010400", "SCSIAdapter", "rcraid")], StorageControllerMode.Raid },
        { [Pci("1022:7901", "010601", "HDC", "storahci"), Pci("144d:a80a", "010802", "SCSIAdapter", "stornvme")], StorageControllerMode.Ahci },
        { [Pci("144d:a80a", "010802", "SCSIAdapter", "stornvme")], StorageControllerMode.Nvme },
        { [], StorageControllerMode.Unknown },
    };

    [Theory]
    [MemberData(nameof(StorageCases))]
    public void Storage_mode(RawPnpDevice[] devices, StorageControllerMode expected)
    {
        Assert.Equal(expected, Build(Snapshot(devices: devices)).System.StorageMode);
    }

    // ---- System settings ----

    [Theory]
    [InlineData(2, 1, SecureBootState.Enabled)]
    [InlineData(2, 0, SecureBootState.Disabled)]
    [InlineData(2, null, SecureBootState.Unknown)]
    [InlineData(1, null, SecureBootState.NotSupported)]
    [InlineData(1, 1, SecureBootState.NotSupported)] // legacy BIOS can't Secure Boot whatever the registry says
    public void Secure_boot(int firmware, int? enabled, SecureBootState expected)
    {
        var p = Build(Snapshot(new RawSystemData { FirmwareType = firmware, SecureBootEnabled = enabled }));
        Assert.Equal(expected, p.System.SecureBoot);
    }

    [Theory]
    [InlineData(1, 1, FastStartupState.Enabled)]
    [InlineData(1, null, FastStartupState.Enabled)]
    [InlineData(1, 0, FastStartupState.Disabled)] // no hibernation, so Fast Startup can't run
    [InlineData(0, 1, FastStartupState.Disabled)]
    [InlineData(null, 1, FastStartupState.Unknown)]
    public void Fast_startup(int? hiberboot, int? hibernate, FastStartupState expected)
    {
        var p = Build(Snapshot(new RawSystemData { HiberbootEnabled = hiberboot, HibernateEnabled = hibernate }));
        Assert.Equal(expected, p.System.FastStartup);
    }

    [Theory]
    [InlineData(null, BitLockerState.Unknown)]
    [InlineData(1, BitLockerState.On)]
    [InlineData(2, BitLockerState.NotEncrypted)]
    [InlineData(3, BitLockerState.Encrypting)]
    [InlineData(8, BitLockerState.WaitingForActivation)]
    [InlineData(99, BitLockerState.Unknown)]
    public void BitLocker(int? raw, BitLockerState expected)
    {
        Assert.Equal(expected, ProfileBuilder.MapBitLocker(raw));
    }

    [Theory]
    [InlineData(0x8664, CpuArchitecture.X64)]
    [InlineData(0xAA64, CpuArchitecture.Arm64)]
    [InlineData(0x014C, CpuArchitecture.X86)]
    [InlineData(null, CpuArchitecture.Unknown)]
    public void Architecture(int? machine, CpuArchitecture expected)
    {
        Assert.Equal(expected, Build(Snapshot(new RawSystemData { NativeMachine = machine })).System.Architecture);
    }

    [Fact]
    public void Arm_pc_gets_a_warning()
    {
        var p = Build(Snapshot(new RawSystemData { NativeMachine = 0xAA64 }));
        Assert.True(p.System.IsArm);
        Assert.Contains(p.Warnings, w => w.Contains("ARM"));
    }

    [Theory]
    [InlineData(new[] { 10 }, null, FormFactor.Laptop)]
    [InlineData(new[] { 3 }, false, FormFactor.Desktop)]
    [InlineData(new[] { 2 }, true, FormFactor.Laptop)] // unknown chassis, but has a battery
    [InlineData(new int[0], null, FormFactor.Unknown)]
    public void Form_factor(int[] chassis, bool? battery, FormFactor expected)
    {
        var p = Build(Snapshot(new RawSystemData { ChassisTypes = chassis, HasBattery = battery }));
        Assert.Equal(expected, p.System.FormFactor);
    }

    [Theory]
    [InlineData("22631", "Core", "Windows 11 Home")]
    [InlineData("19045", "Professional", "Windows 10 Pro")]
    public void Windows_11_is_detected_from_build_not_product_name(string build, string edition, string expectedPrefix)
    {
        var p = Build(Snapshot(new RawSystemData { WindowsProductName = "Windows 10 Pro", WindowsCurrentBuild = build, WindowsEditionId = edition }));
        Assert.StartsWith(expectedPrefix, p.System.Windows.FriendlyName);
    }

    [Fact]
    public void Read_warnings_are_carried_into_the_profile()
    {
        var p = Build(Snapshot(new RawSystemData { ReadWarnings = ["Could not read the chassis type."] }));
        Assert.Contains("Could not read the chassis type.", p.Warnings);
    }
}
