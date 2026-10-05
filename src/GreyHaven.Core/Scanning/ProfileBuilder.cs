using System.Text.RegularExpressions;
using GreyHaven.Core.Hardware;

namespace GreyHaven.Core.Scanning;

/// <summary>Turns a <see cref="RawSnapshot"/> into a <see cref="HardwareProfile"/>. Pure logic; runs on any OS.</summary>
public static partial class ProfileBuilder
{
    public static HardwareProfile Build(RawSnapshot raw, string source, DateTimeOffset? scannedAt = null)
    {
        var warnings = new List<string>(raw.System.ReadWarnings);
        var components = new List<HardwareComponent>();
        components.AddRange(BuildCpus(raw.Processors, warnings));
        components.AddRange(BuildDevices(raw.Devices));
        components.Sort((a, b) =>
        {
            var c = a.Category.CompareTo(b.Category);
            return c != 0 ? c : string.Compare(a.Name, b.Name, StringComparison.OrdinalIgnoreCase);
        });

        var system = BuildSystemInfo(raw.System, components, warnings);
        if (system.IsArm)
            warnings.Add("This PC has an ARM processor. Linux support for ARM Windows PCs is limited and the distros GreyHaven covers do not offer standard installers for most of them.");

        return new HardwareProfile
        {
            GeneratedBy = $"{GreyHavenInfo.ProductName} {GreyHavenInfo.Version}",
            ScannedAt = scannedAt ?? raw.CapturedAt,
            Source = source,
            Description = raw.Description,
            System = system,
            Components = components,
            Warnings = warnings,
        };
    }

    // ---- Devices -------------------------------------------------------------------------

    private sealed record Candidate(RawPnpDevice Raw, DeviceId Id, ComponentCategory Category);

    private static IEnumerable<HardwareComponent> BuildDevices(IReadOnlyList<RawPnpDevice> devices)
    {
        var candidates = new List<Candidate>();
        foreach (var d in devices)
        {
            if (d.Present == false) continue;
            var id = HardwareIdParser.Parse(d.HardwareIds, d.CompatibleIds);
            if (id is null) continue;
            var category = ComponentClassifier.Classify(d, id);
            if (category is null) continue;
            candidates.Add(new Candidate(d, id, category.Value));
        }

        // USB composite devices and HID collections show up as several PnP entries for one
        // physical device; merge them. Other buses: identical IDs are separate physical devices.
        foreach (var group in candidates.GroupBy(c => c.Id.Bus is DeviceBus.Usb or DeviceBus.Hid ? c.Id.Key : null))
        {
            if (group.Key is null)
            {
                foreach (var same in group.GroupBy(c => (c.Id.FullKey, c.Category)))
                    yield return ToComponent(same.First(), same.ToList(), same.Count());
                continue;
            }

            var members = group.ToList();
            var best = members
                .OrderBy(c => Specificity(c.Category))
                .ThenBy(c => HardwareIdParser.IsUsbInterface(c.Raw.HardwareIds) ? 1 : 0)
                .First();
            yield return ToComponent(best, members, 1);
        }
    }

    /// <summary>Lower = more informative category when merging USB interface entries.</summary>
    private static int Specificity(ComponentCategory c) => c switch
    {
        ComponentCategory.Other => 3,
        ComponentCategory.InputDevice => 2,
        ComponentCategory.UsbNetwork or ComponentCategory.AudioController => 1,
        _ => 0,
    };

    private static HardwareComponent ToComponent(Candidate best, List<Candidate> members, int count)
    {
        // Any non-zero problem code on a merged entry means the device isn't fully working.
        var problem = members.Max(m => m.Raw.ConfigManagerErrorCode);
        var name = CleanName(best.Raw.Name) ?? FallbackName(best.Category, best.Id);
        return new HardwareComponent
        {
            Category = best.Category,
            Name = name,
            Manufacturer = CleanName(best.Raw.Manufacturer),
            Id = best.Id,
            WindowsDriver = string.IsNullOrWhiteSpace(best.Raw.Service) ? null : best.Raw.Service.Trim(),
            WindowsProblemCode = problem,
            Count = count,
        };
    }

    private static string FallbackName(ComponentCategory category, DeviceId id) =>
        $"Unnamed {category} device ({id.VendorDevice})";

    private static string? CleanName(string? s)
    {
        if (string.IsNullOrWhiteSpace(s)) return null;
        s = MultiSpaceRegex().Replace(s.Trim(), " ");
        return s.Length == 0 ? null : s;
    }

    // ---- CPUs ----------------------------------------------------------------------------

    private static IEnumerable<HardwareComponent> BuildCpus(IReadOnlyList<RawProcessor> processors, List<string> warnings)
    {
        if (processors.Count == 0)
        {
            warnings.Add("The processor could not be read.");
            yield break;
        }

        foreach (var group in processors.GroupBy(p => (p.Name, p.Caption)))
        {
            var p = group.First();
            var m = p.Caption is null ? Match.Empty : CpuCaptionRegex().Match(p.Caption);
            int? Grp(string g) => m.Success && m.Groups[g].Success ? int.Parse(m.Groups[g].Value) : null;

            yield return new HardwareComponent
            {
                Category = ComponentCategory.Cpu,
                Name = CleanName(p.Name) ?? "Unknown processor",
                Manufacturer = CleanName(p.Manufacturer),
                Cpu = new CpuDetails(
                    Vendor: CleanName(p.Manufacturer) ?? "Unknown",
                    Family: Grp("family"),
                    Model: Grp("model"),
                    Stepping: Grp("stepping"),
                    Cores: p.NumberOfCores,
                    LogicalProcessors: p.NumberOfLogicalProcessors),
                Count = group.Count(),
            };
        }
    }

    // ---- System --------------------------------------------------------------------------

    private static readonly HashSet<int> LaptopChassis = [8, 9, 10, 11, 14, 30, 31, 32];
    private static readonly HashSet<int> DesktopChassis = [3, 4, 5, 6, 7, 13, 15, 16, 24, 35, 36];

    private static SystemInfo BuildSystemInfo(RawSystemData s, IReadOnlyList<HardwareComponent> components, List<string> warnings)
    {
        var firmware = s.FirmwareType switch
        {
            1 => FirmwareMode.LegacyBios,
            2 => FirmwareMode.Uefi,
            _ => FirmwareMode.Unknown,
        };
        if (firmware == FirmwareMode.Unknown) warnings.Add("Could not tell whether the PC boots in UEFI or legacy BIOS mode.");

        var secureBoot = (firmware, s.SecureBootEnabled) switch
        {
            (FirmwareMode.LegacyBios, _) => SecureBootState.NotSupported,
            (_, 1) => SecureBootState.Enabled,
            (_, 0) => SecureBootState.Disabled,
            _ => SecureBootState.Unknown,
        };
        if (secureBoot == SecureBootState.Unknown) warnings.Add("Could not read the Secure Boot setting.");

        var fastStartup = (s.HiberbootEnabled, s.HibernateEnabled) switch
        {
            (_, 0) => FastStartupState.Disabled, // Fast Startup needs hibernation
            (0, _) => FastStartupState.Disabled,
            (1, _) => FastStartupState.Enabled,
            _ => FastStartupState.Unknown,
        };

        var bitLocker = MapBitLocker(s.BitLockerProtection);
        if (bitLocker == BitLockerState.Unknown) warnings.Add("Could not read whether BitLocker drive encryption is on.");

        var arch = s.NativeMachine switch
        {
            0x8664 => CpuArchitecture.X64,
            0xAA64 => CpuArchitecture.Arm64,
            0x014C => CpuArchitecture.X86,
            _ => CpuArchitecture.Unknown,
        };

        var formFactor =
            s.ChassisTypes.Any(LaptopChassis.Contains) ? FormFactor.Laptop :
            s.ChassisTypes.Any(DesktopChassis.Contains) ? FormFactor.Desktop :
            s.HasBattery == true ? FormFactor.Laptop :
            FormFactor.Unknown;

        int? build = int.TryParse(s.WindowsCurrentBuild, out var b) ? b : null;

        return new SystemInfo
        {
            Windows = new WindowsVersion(s.WindowsProductName, s.WindowsDisplayVersion, build, s.WindowsUbr, s.WindowsEditionId),
            Architecture = arch,
            Firmware = firmware,
            SecureBoot = secureBoot,
            StorageMode = DetectStorageMode(components),
            FastStartup = fastStartup,
            HibernationEnabled = s.HibernateEnabled is null ? null : s.HibernateEnabled != 0,
            BitLocker = bitLocker,
            FormFactor = formFactor,
            HasBattery = s.HasBattery,
            SystemManufacturer = CleanName(s.SystemManufacturer),
            SystemModel = CleanName(s.SystemModel),
        };
    }

    /// <summary>
    /// Maps the shell's System.Volume.BitLockerProtection value. The mapping is based on observed
    /// values documented by the community, not official Microsoft docs; it is on the list of
    /// things to verify on real Windows hardware. Unrecognised values map to Unknown.
    /// </summary>
    public static BitLockerState MapBitLocker(int? value) => value switch
    {
        1 => BitLockerState.On,
        2 => BitLockerState.NotEncrypted,
        3 => BitLockerState.Encrypting,
        4 => BitLockerState.Decrypting,
        5 => BitLockerState.Suspended,
        6 => BitLockerState.Locked,
        8 => BitLockerState.WaitingForActivation,
        _ => BitLockerState.Unknown,
    };

    public static StorageControllerMode DetectStorageMode(IReadOnlyList<HardwareComponent> components)
    {
        var controllers = components.Where(c => c.Category == ComponentCategory.StorageController && c.Id is not null).ToList();

        bool IsVmd(HardwareComponent c) =>
            (c.WindowsDriver is not null && KnownDeviceIds.IntelVmdServices.Contains(c.WindowsDriver)) ||
            (c.Id!.Vendor == "8086" && KnownDeviceIds.IntelVmdDevices.Contains(c.Id.Device));
        bool IsRst(HardwareComponent c) =>
            c.WindowsDriver is not null && KnownDeviceIds.IntelRstServices.Contains(c.WindowsDriver);

        if (controllers.Any(IsVmd)) return StorageControllerMode.IntelVmd;
        if (controllers.Any(IsRst)) return StorageControllerMode.IntelRstRaid;
        var raid = controllers.FirstOrDefault(c => c.Id!.ClassAndSubclass == "0104");
        if (raid is not null) return raid.Id!.Vendor == "8086" ? StorageControllerMode.IntelRstRaid : StorageControllerMode.Raid;
        if (controllers.Any(c => c.Id!.ClassAndSubclass == "0106")) return StorageControllerMode.Ahci;
        if (components.Any(c => c.Category == ComponentCategory.NvmeDrive)) return StorageControllerMode.Nvme;
        return StorageControllerMode.Unknown;
    }

    [GeneratedRegex(@"Family (?<family>\d+) Model (?<model>\d+)(?: Stepping (?<stepping>\d+))?", RegexOptions.IgnoreCase)]
    private static partial Regex CpuCaptionRegex();

    [GeneratedRegex(@"\s{2,}")]
    private static partial Regex MultiSpaceRegex();
}
