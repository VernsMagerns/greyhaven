using System.Management;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using GreyHaven.Core.Scanning;
using Microsoft.Win32;

namespace GreyHaven.Scan.Windows;

/// <summary>
/// Reads hardware and system state from Windows using WMI, the registry and Win32 calls.
/// Everything here works <b>without administrator rights</b>. Each read is independent: if one
/// fails, the rest still run and a plain-English warning is recorded.
///
/// Privacy: PnP device instance IDs (which can embed serial numbers) are never read, and only
/// devices on PCI, USB, HD Audio, HID and ACPI buses are kept, which excludes Bluetooth-paired
/// and software devices that can carry user-chosen names.
/// </summary>
[SupportedOSPlatform("windows")]
public sealed partial class WindowsRawHardwareSource : IRawHardwareSource
{
    private static readonly string[] KeptBusPrefixes = [@"PCI\", @"USB\", @"HDAUDIO\", @"HID\", @"ACPI\"];

    public Task<RawSnapshot> CaptureAsync(CancellationToken cancellationToken = default)
    {
        // Shell.Application (used for BitLocker status) is apartment-threaded, so capture on an STA thread.
        var tcs = new TaskCompletionSource<RawSnapshot>(TaskCreationOptions.RunContinuationsAsynchronously);
        var thread = new Thread(() =>
        {
            try { tcs.SetResult(Capture(cancellationToken)); }
            catch (Exception ex) { tcs.SetException(ex); }
        }) { IsBackground = true, Name = "GreyHaven scan" };
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        return tcs.Task;
    }

    private static RawSnapshot Capture(CancellationToken ct)
    {
        var warnings = new List<string>();

        var devices = Try(ReadPnpDevices, [], "Could not list hardware devices (WMI Win32_PnPEntity).", warnings);
        ct.ThrowIfCancellationRequested();
        var processors = Try(ReadProcessors, [], "Could not read processor details (WMI Win32_Processor).", warnings);
        ct.ThrowIfCancellationRequested();

        const string ntKey = @"SOFTWARE\Microsoft\Windows NT\CurrentVersion";
        var system = new RawSystemData
        {
            FirmwareType = Try(ReadFirmwareType, null, "Could not read the firmware type.", warnings),
            SecureBootEnabled = ReadDword(@"SYSTEM\CurrentControlSet\Control\SecureBoot\State", "UEFISecureBootEnabled"),
            HiberbootEnabled = ReadDword(@"SYSTEM\CurrentControlSet\Control\Session Manager\Power", "HiberbootEnabled"),
            HibernateEnabled = ReadDword(@"SYSTEM\CurrentControlSet\Control\Power", "HibernateEnabled"),
            BitLockerProtection = Try(ReadBitLockerProtection, null, "Could not read BitLocker status.", warnings),
            WindowsProductName = ReadString(ntKey, "ProductName"),
            WindowsDisplayVersion = ReadString(ntKey, "DisplayVersion"),
            WindowsCurrentBuild = ReadString(ntKey, "CurrentBuild"),
            WindowsUbr = ReadDword(ntKey, "UBR"),
            WindowsEditionId = ReadString(ntKey, "EditionID"),
            NativeMachine = Try(ReadNativeMachine, null, "Could not read the CPU architecture.", warnings),
            ChassisTypes = Try(ReadChassisTypes, [], "Could not read the chassis type.", warnings),
            HasBattery = Try(() => (bool?)Query("SELECT Name FROM Win32_Battery").Any(), null, "Could not check for a battery.", warnings),
            SystemManufacturer = Try(() => ReadComputerSystem("Manufacturer"), null, "Could not read the PC manufacturer.", warnings),
            SystemModel = Try(() => ReadComputerSystem("Model"), null, "Could not read the PC model.", warnings),
            ReadWarnings = warnings,
        };

        return new RawSnapshot
        {
            CapturedAt = DateTimeOffset.UtcNow,
            System = system,
            Processors = processors,
            Devices = devices,
        };
    }

    // ---- WMI -----------------------------------------------------------------------------

    private static IReadOnlyList<RawPnpDevice> ReadPnpDevices()
    {
        var result = new List<RawPnpDevice>();
        foreach (var mo in Query("SELECT Name, Manufacturer, PNPClass, Service, HardwareID, CompatibleID, ConfigManagerErrorCode, Present FROM Win32_PnPEntity"))
        {
            var hardwareIds = Prop<string[]>(mo, "HardwareID") ?? [];
            if (!hardwareIds.Any(h => KeptBusPrefixes.Any(p => h.StartsWith(p, StringComparison.OrdinalIgnoreCase))))
                continue;

            result.Add(new RawPnpDevice
            {
                Name = Prop<string>(mo, "Name"),
                Manufacturer = Prop<string>(mo, "Manufacturer"),
                PnpClass = Prop<string>(mo, "PNPClass"),
                Service = Prop<string>(mo, "Service"),
                HardwareIds = hardwareIds,
                CompatibleIds = Prop<string[]>(mo, "CompatibleID") ?? [],
                ConfigManagerErrorCode = Prop<uint?>(mo, "ConfigManagerErrorCode") is { } code ? (int)code : null,
                Present = Prop<bool?>(mo, "Present"),
            });
        }
        return result;
    }

    private static IReadOnlyList<RawProcessor> ReadProcessors() =>
        Query("SELECT Name, Manufacturer, Caption, NumberOfCores, NumberOfLogicalProcessors FROM Win32_Processor")
            .Select(mo => new RawProcessor
            {
                Name = Prop<string>(mo, "Name"),
                Manufacturer = Prop<string>(mo, "Manufacturer"),
                Caption = Prop<string>(mo, "Caption"),
                NumberOfCores = Prop<uint?>(mo, "NumberOfCores") is { } c ? (int)c : null,
                NumberOfLogicalProcessors = Prop<uint?>(mo, "NumberOfLogicalProcessors") is { } l ? (int)l : null,
            })
            .ToList();

    private static IReadOnlyList<int> ReadChassisTypes() =>
        Query("SELECT ChassisTypes FROM Win32_SystemEnclosure")
            .SelectMany(mo => Prop<ushort[]>(mo, "ChassisTypes") ?? [])
            .Select(t => (int)t)
            .ToList();

    private static string? ReadComputerSystem(string property) =>
        Query($"SELECT {property} FROM Win32_ComputerSystem").Select(mo => Prop<string>(mo, property)).FirstOrDefault();

    private static List<ManagementBaseObject> Query(string wql)
    {
        using var searcher = new ManagementObjectSearcher(@"root\CIMV2", wql);
        return searcher.Get().Cast<ManagementBaseObject>().ToList();
    }

    private static T? Prop<T>(ManagementBaseObject mo, string name)
    {
        try { return mo[name] is T value ? value : default; }
        catch (ManagementException) { return default; } // property not present on this Windows version
    }

    // ---- Registry ------------------------------------------------------------------------

    private static RegistryKey OpenHklm() => RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry64);

    private static int? ReadDword(string path, string name)
    {
        try
        {
            using var hklm = OpenHklm();
            using var key = hklm.OpenSubKey(path);
            return key?.GetValue(name) is int v ? v : null;
        }
        catch (Exception ex) when (ex is UnauthorizedAccessException or System.Security.SecurityException or IOException)
        {
            return null;
        }
    }

    private static string? ReadString(string path, string name)
    {
        try
        {
            using var hklm = OpenHklm();
            using var key = hklm.OpenSubKey(path);
            return key?.GetValue(name)?.ToString();
        }
        catch (Exception ex) when (ex is UnauthorizedAccessException or System.Security.SecurityException or IOException)
        {
            return null;
        }
    }

    // ---- Win32 / COM ---------------------------------------------------------------------

    private static int? ReadFirmwareType() => GetFirmwareType(out var type) ? type : null;

    private static int? ReadNativeMachine()
    {
        try
        {
            if (IsWow64Process2(GetCurrentProcess(), out _, out var native)) return native;
        }
        catch (EntryPointNotFoundException) { /* Windows 10 before 1511 */ }

        return RuntimeInformation.OSArchitecture switch
        {
            Architecture.X64 => 0x8664,
            Architecture.Arm64 => 0xAA64,
            Architecture.X86 => 0x014C,
            _ => null,
        };
    }

    /// <summary>
    /// Reads System.Volume.BitLockerProtection for the system drive through the Windows shell.
    /// Unlike the BitLocker WMI class, this does not require administrator rights.
    /// </summary>
    private static int? ReadBitLockerProtection()
    {
        var shellType = Type.GetTypeFromProgID("Shell.Application");
        if (shellType is null) return null;
        var shell = Activator.CreateInstance(shellType);
        try
        {
            const int ssfDrives = 17; // "This PC"
            var drive = Path.GetPathRoot(Environment.SystemDirectory)?.TrimEnd('\\') ?? "C:";
            var folder = Invoke(shell, "NameSpace", ssfDrives);
            var item = Invoke(folder, "ParseName", drive);
            var value = Invoke(item, "ExtendedProperty", "System.Volume.BitLockerProtection");
            return value is null ? null : Convert.ToInt32(value, System.Globalization.CultureInfo.InvariantCulture);
        }
        finally
        {
            if (shell is not null && Marshal.IsComObject(shell)) Marshal.FinalReleaseComObject(shell);
        }

        static object? Invoke(object? target, string method, params object[] args) =>
            target?.GetType().InvokeMember(method, System.Reflection.BindingFlags.InvokeMethod, null, target, args);
    }

    [LibraryImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool GetFirmwareType(out int firmwareType);

    [LibraryImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool IsWow64Process2(nint process, out ushort processMachine, out ushort nativeMachine);

    [LibraryImport("kernel32.dll")]
    private static partial nint GetCurrentProcess();

    // ---- Helpers -------------------------------------------------------------------------

    private static T Try<T>(Func<T> read, T fallback, string warning, List<string> warnings)
    {
        try { return read(); }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            warnings.Add(warning);
            return fallback;
        }
    }
}
