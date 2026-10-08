using System.ComponentModel;
using System.Management;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using Microsoft.Win32.SafeHandles;

namespace GreyHaven.Helper;

/// <summary>
/// Raw access to \\.\PhysicalDriveN without Windows' cache (so the read-back check reads the
/// stick, not memory), using a sector-aligned buffer as unbuffered I/O requires.
/// </summary>
[SupportedOSPlatform("windows")]
internal sealed unsafe partial class RawDisk : Stream
{
    private const uint GenericRead = 0x80000000, GenericWrite = 0x40000000;
    private const uint FileShareRead = 1, FileShareWrite = 2, OpenExisting = 3;
    private const uint NoBuffering = 0x20000000, WriteThrough = 0x80000000;
    private const uint FsctlLockVolume = 0x00090018, FsctlDismountVolume = 0x00090020, IoctlDiskUpdateProperties = 0x00070140;
    private const int BufferSize = 4 << 20;

    private readonly SafeFileHandle _handle;
    private readonly int _sector;
    private readonly void* _buffer;
    private long _position;

    private RawDisk(SafeFileHandle handle, long length, int sector)
    {
        _handle = handle;
        Length = length;
        _sector = sector;
        _buffer = NativeMemory.AlignedAlloc(BufferSize, (nuint)Math.Max(sector, 4096));
    }

    public static RawDisk Open(int number, long length, int sectorSize)
    {
        var h = CreateFile($@"\\.\PhysicalDrive{number}", GenericRead | GenericWrite, FileShareRead | FileShareWrite, 0, OpenExisting, NoBuffering | WriteThrough, 0);
        if (h.IsInvalid) throw new IOException("GreyHaven couldn't open the USB stick.", new Win32Exception(Marshal.GetLastPInvokeError()));
        return new RawDisk(h, length, sectorSize);
    }

    /// <summary>Locks and dismounts every volume on the disk, so Windows lets the whole disk be written. Unlocks on dispose.</summary>
    public static IDisposable LockVolumes(int number)
    {
        var handles = new List<SafeFileHandle>();
        var scope = new ManagementScope(@"\\.\root\Microsoft\Windows\Storage");
        using var s = new ManagementObjectSearcher(scope, new ObjectQuery($"SELECT AccessPaths FROM MSFT_Partition WHERE DiskNumber = {number}"));
        foreach (var p in s.Get().Cast<ManagementBaseObject>())
            foreach (var path in (p["AccessPaths"] as string[]) ?? [])
            {
                if (!path.StartsWith(@"\\?\Volume{", StringComparison.OrdinalIgnoreCase)) continue;
                var h = CreateFile(path.TrimEnd('\\'), GenericRead | GenericWrite, FileShareRead | FileShareWrite, 0, OpenExisting, 0, 0);
                if (h.IsInvalid) continue;
                if (!DeviceIoControl(h, FsctlLockVolume, 0, 0, 0, 0, out _, 0))
                {
                    h.Dispose();
                    throw new IOException("Something is using the USB stick (an open file or window). Close it and try again.");
                }
                DeviceIoControl(h, FsctlDismountVolume, 0, 0, 0, 0, out _, 0);
                handles.Add(h);
            }
        return new Locks(handles);
    }

    private sealed class Locks(List<SafeFileHandle> handles) : IDisposable
    {
        public void Dispose() { foreach (var h in handles) h.Dispose(); }
    }

    /// <summary>Tells Windows to read the stick's new partition table.</summary>
    public void RefreshLayout() => DeviceIoControl(_handle, IoctlDiskUpdateProperties, 0, 0, 0, 0, out _, 0);

    public override bool CanRead => true;
    public override bool CanSeek => true;
    public override bool CanWrite => true;
    public override long Length { get; }
    public override long Position { get => _position; set => _position = value; }

    public override void Write(byte[] buffer, int offset, int count) => Write(buffer.AsSpan(offset, count));

    public override void Write(ReadOnlySpan<byte> data)
    {
        // ImageWriter only writes whole sectors at sector-aligned positions.
        if (data.Length % _sector != 0 || _position % _sector != 0) throw new IOException("Unaligned write.");
        var span = new Span<byte>(_buffer, BufferSize);
        while (!data.IsEmpty)
        {
            var n = Math.Min(BufferSize, data.Length);
            data[..n].CopyTo(span);
            RandomAccess.Write(_handle, span[..n], _position);
            _position += n;
            data = data[n..];
        }
    }

    public override int Read(byte[] buffer, int offset, int count) => Read(buffer.AsSpan(offset, count));

    public override int Read(Span<byte> destination)
    {
        if (_position >= Length) return 0;
        var span = new Span<byte>(_buffer, BufferSize);
        var aligned = _position / _sector * _sector;
        var skip = (int)(_position - aligned);
        var want = (int)Math.Min(Math.Min(destination.Length + skip, BufferSize), Length - aligned);
        want = (want + _sector - 1) / _sector * _sector;
        var got = RandomAccess.Read(_handle, span[..want], aligned);
        var n = Math.Max(0, Math.Min(destination.Length, got - skip));
        span.Slice(skip, n).CopyTo(destination);
        _position += n;
        return n;
    }

    public override ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken ct = default) { Write(buffer.Span); return ValueTask.CompletedTask; }
    public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken ct = default) => ValueTask.FromResult(Read(buffer.Span));
    public override void Flush() { }
    public override long Seek(long offset, SeekOrigin origin) => _position = origin switch { SeekOrigin.Begin => offset, SeekOrigin.Current => _position + offset, _ => Length + offset };
    public override void SetLength(long value) => throw new NotSupportedException();

    protected override void Dispose(bool disposing)
    {
        if (disposing) _handle.Dispose();
        NativeMemory.AlignedFree(_buffer);
        base.Dispose(disposing);
    }

    [LibraryImport("kernel32.dll", EntryPoint = "CreateFileW", SetLastError = true, StringMarshalling = StringMarshalling.Utf16)]
    private static partial SafeFileHandle CreateFile(string name, uint access, uint share, nint security, uint disposition, uint flags, nint template);

    [LibraryImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool DeviceIoControl(SafeFileHandle device, uint code, nint inBuffer, uint inSize, nint outBuffer, uint outSize, out uint returned, nint overlapped);
}
