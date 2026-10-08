using System.Management;
using System.Runtime.Versioning;
using GreyHaven.Media;

namespace GreyHaven.Scan.Windows;

/// <summary>A disk plus what writing to it needs (sector size).</summary>
public sealed record WindowsDisk(DiskInfo Info, int SectorSize);

/// <summary>
/// Lists disks with Windows' storage interface (WMI root\Microsoft\Windows\Storage, no admin
/// needed). Used by the app to show USB sticks, and by the elevated helper to re-check the disk
/// right before writing. A disk "holds Windows" if it has the system or boot partition, or a page file.
/// </summary>
[SupportedOSPlatform("windows")]
public static class WindowsDisks
{
    private const int BusTypeUsb = 7;

    public static IReadOnlyList<WindowsDisk> List()
    {
        var storage = new ManagementScope(@"\\.\root\Microsoft\Windows\Storage");
        var pageFileDisks = PageFileDisks(storage);
        var removable = RemovableDiskNumbers();
        var disks = new List<WindowsDisk>();
        using var searcher = new ManagementObjectSearcher(storage, new ObjectQuery(
            "SELECT Number, FriendlyName, Size, BusType, IsSystem, IsBoot, IsReadOnly, IsOffline, LogicalSectorSize FROM MSFT_Disk"));
        foreach (var d in searcher.Get().Cast<ManagementBaseObject>())
        {
            var number = Convert.ToInt32(d["Number"], System.Globalization.CultureInfo.InvariantCulture);
            var info = new DiskInfo(
                number,
                (d["FriendlyName"] as string)?.Trim() ?? $"Disk {number}",
                Convert.ToInt64(d["Size"], System.Globalization.CultureInfo.InvariantCulture),
                Convert.ToInt32(d["BusType"], System.Globalization.CultureInfo.InvariantCulture) == BusTypeUsb,
                removable.Contains(number),
                d["IsSystem"] is true || d["IsBoot"] is true || pageFileDisks.Contains(number),
                d["IsReadOnly"] is true,
                d["IsOffline"] is true);
            disks.Add(new WindowsDisk(info, Convert.ToInt32(d["LogicalSectorSize"] ?? 512, System.Globalization.CultureInfo.InvariantCulture)));
        }
        return disks.OrderBy(d => d.Info.Number).ToList();
    }

    /// <summary>USB sticks only (never internal drives), each with whether GreyHaven may write it.</summary>
    public static IReadOnlyList<WindowsDisk> UsbSticks() => List().Where(d => UsbSafety.IsListed(d.Info)).ToList();

    private static HashSet<int> RemovableDiskNumbers()
    {
        using var s = new ManagementObjectSearcher("SELECT Index, MediaType FROM Win32_DiskDrive");
        return s.Get().Cast<ManagementBaseObject>()
            .Where(d => (d["MediaType"] as string)?.Contains("Removable", StringComparison.OrdinalIgnoreCase) == true)
            .Select(d => Convert.ToInt32(d["Index"], System.Globalization.CultureInfo.InvariantCulture))
            .ToHashSet();
    }

    /// <summary>Disks with a page file on one of their partitions (e.g. "D:\pagefile.sys").</summary>
    private static HashSet<int> PageFileDisks(ManagementScope storage)
    {
        var letters = new HashSet<char>();
        using (var s = new ManagementObjectSearcher("SELECT Name FROM Win32_PageFileUsage"))
            foreach (var p in s.Get().Cast<ManagementBaseObject>())
                if (p["Name"] is string name && name.Length > 1 && name[1] == ':') letters.Add(char.ToUpperInvariant(name[0]));
        var disks = new HashSet<int>();
        using var parts = new ManagementObjectSearcher(storage, new ObjectQuery("SELECT DiskNumber, DriveLetter FROM MSFT_Partition"));
        foreach (var p in parts.Get().Cast<ManagementBaseObject>())
            if (p["DriveLetter"] is char c && letters.Contains(char.ToUpperInvariant(c)))
                disks.Add(Convert.ToInt32(p["DiskNumber"], System.Globalization.CultureInfo.InvariantCulture));
        return disks;
    }
}
