namespace GreyHaven.Media;

/// <summary>A disk as Windows describes it. Filled by the Windows code (WMI MSFT_Disk / Win32_DiskDrive).</summary>
/// <param name="Number">Windows' disk number (\\.\PhysicalDriveN).</param>
/// <param name="IsUsb">Bus type is USB.</param>
/// <param name="IsRemovableMedia">Windows calls it removable (most USB sticks; some fast ones say "fixed").</param>
/// <param name="HoldsWindows">It has the Windows, boot or system partition, or a page file.</param>
/// <param name="Drives">Its drive letters, e.g. "E:", or null if Windows shows none.</param>
/// <param name="UsedBytes">Space used on those drives, or null if Windows can't read them.</param>
public sealed record DiskInfo(int Number, string Model, long SizeBytes, bool IsUsb, bool IsRemovableMedia, bool HoldsWindows, bool IsReadOnly, bool IsOffline,
    string? Drives = null, long? UsedBytes = null);

/// <summary>Whether a disk may be written, and what to tell the person if not.</summary>
/// <param name="IsLargeDrive">Bigger than a USB stick (an external SSD or hard drive): it may hold their backup, so erasing it needs a second, explicit confirmation, checked again by the helper.</param>
public sealed record StickCheck(bool CanUse, string? Problem, string? Warning, bool IsLargeDrive = false);

/// <summary>
/// The rules for which disks GreyHaven will ever write. Checked in the app (to list sticks) and
/// again inside the elevated helper, right before writing, which never trusts the app's word.
/// </summary>
public static class UsbSafety
{
    public const long MinimumBytes = 8L * 1000 * 1000 * 1000 * 95 / 100;   // "8 GB" sticks hold a little less
    /// <summary>Bigger than this is almost certainly an external drive, not a stick: allowed, but only with a second confirmation.</summary>
    public const long MaximumBytes = 256L * 1000 * 1000 * 1000;

    /// <summary>Only USB disks are shown at all; the rest are never listed.</summary>
    public static bool IsListed(DiskInfo d) => d.IsUsb && !d.HoldsWindows;

    public static StickCheck Check(DiskInfo d, long imageBytes)
    {
        if (!d.IsUsb) return new(false, "This isn't a USB drive.", null);
        if (d.HoldsWindows) return new(false, "Windows is on this drive.", null);
        if (d.IsReadOnly) return new(false, "This stick is write-protected. Some sticks have a small lock switch on the side.", null);
        if (d.IsOffline) return new(false, "Windows has this drive switched off (offline).", null);
        if (d.SizeBytes > MaximumBytes)
            return new(true, null, $"This drive holds {Gb(d.SizeBytes)}, so it's an external drive (like an SSD), not a USB stick. It works, but everything on it is erased, including any backup you made on it.", IsLargeDrive: true);
        if (d.SizeBytes < Math.Max(MinimumBytes, imageBytes))
            return new(false, $"This stick is too small: {Gb(d.SizeBytes)}. You need one that holds at least {Gb(Math.Max(8_000_000_000, imageBytes))}.", null);
        return new(true, null, d.IsRemovableMedia ? null : "Windows calls this a hard drive rather than a stick. Make sure it's the one you want to erase.");
    }

    public static string Gb(long bytes) => $"{Math.Round(bytes / 1e9):0} GB";

    /// <summary>"E:, 3.2 GB used" / "E:, empty" / "Windows can't show what's on it": what's on the stick now (it will be erased).</summary>
    public static string Contents(DiskInfo d)
    {
        if (d.UsedBytes is not { } used) return "Windows can't show what's on it.";
        var amount = used < 50_000_000 ? "empty" : used < 10_000_000_000
            ? string.Create(System.Globalization.CultureInfo.InvariantCulture, $"{used / 1e9:0.0} GB used")
            : $"{Gb(used)} used";
        return d.Drives is { } drives ? $"{drives} {amount}" : char.ToUpperInvariant(amount[0]) + amount[1..];
    }
}

/// <summary>Writes an installer image to a disk and reads it back. The disk is anything with a sector size (a file in tests).</summary>
public static class ImageWriter
{
    public const string Writing = "Writing to the stick";
    public const string Verifying = "Checking the stick";

    /// <summary>How much of the end of the stick is cleared before writing.</summary>
    public const int EndWipeBytes = 1 << 20;

    /// <summary>
    /// Zeros the last megabyte of the stick. A stick that was GPT-formatted keeps a backup copy of
    /// its old partition table there, which the installer image doesn't overwrite; some PCs' startup
    /// menus then show the stick as damaged or not at all. Rufus clears it the same way.
    /// </summary>
    public static void ClearEnd(Stream disk, long diskLength, int sectorSize)
    {
        if (sectorSize <= 0 || (sectorSize & (sectorSize - 1)) != 0) throw new ArgumentException("Sector size must be a power of two.", nameof(sectorSize));
        var length = (int)Math.Min(EndWipeBytes, diskLength / sectorSize * sectorSize);
        var start = (diskLength - length) / sectorSize * sectorSize;
        disk.Position = start;
        disk.Write(new byte[length]);
        disk.Flush();
    }

    /// <summary>
    /// Copies <paramref name="image"/> to the start of <paramref name="disk"/> in sector-aligned
    /// chunks (the last one padded with zeros), then reads back the image's length and compares
    /// its SHA-256 with <paramref name="expectedSha256"/>. Throws if they differ.
    /// </summary>
    public static async Task WriteAndVerifyAsync(Stream image, long imageLength, Stream disk, int sectorSize, string expectedSha256,
        IProgress<MediaProgress>? progress, CancellationToken ct)
    {
        if (sectorSize <= 0 || (sectorSize & (sectorSize - 1)) != 0) throw new ArgumentException("Sector size must be a power of two.", nameof(sectorSize));
        var chunk = 4 << 20; // a multiple of every sector size
        var buffer = new byte[chunk];
        long done = 0;
        disk.Position = 0;
        while (done < imageLength)
        {
            ct.ThrowIfCancellationRequested();
            var want = (int)Math.Min(chunk, imageLength - done);
            var got = 0;
            while (got < want)
            {
                var n = await image.ReadAsync(buffer.AsMemory(got, want - got), ct).ConfigureAwait(false);
                if (n == 0) throw new IOException("The installer file ended early.");
                got += n;
            }
            var aligned = (got + sectorSize - 1) / sectorSize * sectorSize;
            if (aligned > got) Array.Clear(buffer, got, aligned - got);
            await disk.WriteAsync(buffer.AsMemory(0, aligned), ct).ConfigureAwait(false);
            done += got;
            progress?.Report(new MediaProgress(Writing, done, imageLength));
        }
        await disk.FlushAsync(ct).ConfigureAwait(false);

        disk.Position = 0;
        var sha = await Checksums.Sha256Async(disk, imageLength, new Progress<long>(d => progress?.Report(new MediaProgress(Verifying, d, imageLength))), ct).ConfigureAwait(false);
        if (sha != expectedSha256)
            throw new IOException("The stick doesn't read back exactly what was written. It may be faulty. Try another stick.");
    }
}
