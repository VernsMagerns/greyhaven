using System.Buffers.Binary;
using System.Formats.Tar;
using System.IO.Compression;
using System.Text;
using GreyHaven.Compat.Kernel;
using ZstdSharp;

namespace GreyHaven.IndexBuilder;

/// <summary>
/// Extracts module alias patterns from a Debian kernel modules package (.deb) without needing
/// dpkg: .deb is an <c>ar</c> archive holding <c>data.tar[.zst|.gz]</c>, which holds
/// <c>*.ko[.zst]</c> ELF modules and <c>modules.builtin.modinfo</c>.
/// </summary>
internal static class DebReader
{
    public static List<AliasEntry> ReadAliases(string debPath, Action<string> log)
    {
        using var deb = File.OpenRead(debPath);
        using var data = OpenDataTar(deb);
        var reader = new TarReader(data);
        var aliases = new List<AliasEntry>();
        int modules = 0, builtin = 0;

        while (reader.GetNextEntry() is { } entry)
        {
            if (entry.DataStream is null || entry.EntryType is not (TarEntryType.RegularFile or TarEntryType.V7RegularFile)) continue;
            var name = entry.Name;

            if (name.EndsWith("/modules.builtin.modinfo", StringComparison.Ordinal))
            {
                foreach (var field in SplitNul(ReadAll(entry.DataStream)))
                {
                    // "<module>.alias=<pattern>"
                    var dot = field.IndexOf('.');
                    var eq = field.IndexOf('=');
                    if (dot > 0 && eq > dot && field.AsSpan(dot + 1, eq - dot - 1).SequenceEqual("alias"))
                    {
                        aliases.Add(new AliasEntry(field[(eq + 1)..], field[..dot]));
                        builtin++;
                    }
                }
                continue;
            }

            byte[] elf;
            if (name.EndsWith(".ko.zst", StringComparison.Ordinal))
            {
                using var z = new DecompressionStream(entry.DataStream);
                elf = ReadAll(z);
            }
            else if (name.EndsWith(".ko", StringComparison.Ordinal))
            {
                elf = ReadAll(entry.DataStream);
            }
            else continue;

            modules++;
            var fileModule = Path.GetFileName(name).Split('.')[0].Replace('-', '_');
            var modinfo = SplitNul(ReadModinfo(elf, name)).ToList();
            var module = modinfo.FirstOrDefault(f => f.StartsWith("name=", StringComparison.Ordinal))?[5..] ?? fileModule;
            foreach (var f in modinfo)
                if (f.StartsWith("alias=", StringComparison.Ordinal))
                    aliases.Add(new AliasEntry(f[6..], module));
        }

        log($"  {modules} modules, {builtin} built-in aliases, {aliases.Count} aliases total");
        if (modules == 0) throw new InvalidDataException($"{Path.GetFileName(debPath)} contains no kernel modules.");
        return aliases;
    }

    /// <summary>Module names listed in <c>modules.builtin</c> (drivers compiled into the kernel), e.g. "xhci_pci".</summary>
    public static HashSet<string> ReadBuiltinModuleNames(string debPath)
    {
        var names = new HashSet<string>(StringComparer.Ordinal);
        foreach (var (_, data) in ReadFiles(debPath, "/modules.builtin"))
            foreach (var line in Encoding.UTF8.GetString(data).Split('\n', StringSplitOptions.RemoveEmptyEntries))
                names.Add(Path.GetFileName(line.Trim()).Split('.')[0].Replace('-', '_'));
        return names;
    }

    /// <summary>Returns the contents of every file in the package whose path ends with <paramref name="suffix"/>.</summary>
    public static List<(string Path, byte[] Data)> ReadFiles(string debPath, string suffix)
    {
        using var deb = File.OpenRead(debPath);
        using var data = OpenDataTar(deb);
        var reader = new TarReader(data);
        var found = new List<(string, byte[])>();
        while (reader.GetNextEntry() is { } entry)
        {
            if (entry.DataStream is not null && entry.Name.EndsWith(suffix, StringComparison.Ordinal))
                found.Add((entry.Name, ReadAll(entry.DataStream)));
        }
        return found;
    }

    private static Stream OpenDataTar(Stream deb)
    {
        Span<byte> magic = stackalloc byte[8];
        deb.ReadExactly(magic);
        if (!magic.SequenceEqual("!<arch>\n"u8)) throw new InvalidDataException("Not a .deb (ar) file.");

        var header = new byte[60];
        while (deb.Read(header, 0, 60) == 60)
        {
            var member = Encoding.ASCII.GetString(header, 0, 16).Trim().TrimEnd('/');
            var size = long.Parse(Encoding.ASCII.GetString(header, 48, 10).Trim());
            if (member.StartsWith("data.tar", StringComparison.Ordinal))
            {
                var body = new MemoryStream(checked((int)size));
                CopyBytes(deb, body, size);
                body.Position = 0;
                return member switch
                {
                    "data.tar" => body,
                    "data.tar.zst" => Buffer(new DecompressionStream(body)),
                    "data.tar.gz" => Buffer(new GZipStream(body, CompressionMode.Decompress)),
                    _ => throw new NotSupportedException($"Unsupported .deb payload '{member}'."),
                };
            }
            deb.Seek(size + (size & 1), SeekOrigin.Current);
        }
        throw new InvalidDataException("No data.tar in .deb.");
    }

    // TarReader needs a seekable or fully readable stream; decompress to memory once.
    private static MemoryStream Buffer(Stream s)
    {
        var ms = new MemoryStream();
        using (s) s.CopyTo(ms);
        ms.Position = 0;
        return ms;
    }

    private static void CopyBytes(Stream from, Stream to, long count)
    {
        var buf = new byte[81920];
        while (count > 0)
        {
            var n = from.Read(buf, 0, (int)Math.Min(buf.Length, count));
            if (n == 0) throw new EndOfStreamException();
            to.Write(buf, 0, n);
            count -= n;
        }
    }

    private static byte[] ReadAll(Stream s)
    {
        using var ms = new MemoryStream();
        s.CopyTo(ms);
        return ms.ToArray();
    }

    /// <summary>Returns the raw bytes of the ELF64 little-endian <c>.modinfo</c> section.</summary>
    private static ReadOnlySpan<byte> ReadModinfo(byte[] elf, string nameForErrors)
    {
        if (elf.Length < 64 || !elf.AsSpan(0, 4).SequenceEqual((ReadOnlySpan<byte>)[0x7F, (byte)'E', (byte)'L', (byte)'F']))
            throw new InvalidDataException($"{nameForErrors} is not an ELF file.");
        if (elf[4] != 2 || elf[5] != 1) throw new InvalidDataException($"{nameForErrors}: only 64-bit little-endian ELF is supported.");

        var shoff = (long)BinaryPrimitives.ReadUInt64LittleEndian(elf.AsSpan(0x28));
        var shentsize = BinaryPrimitives.ReadUInt16LittleEndian(elf.AsSpan(0x3A));
        var shnum = BinaryPrimitives.ReadUInt16LittleEndian(elf.AsSpan(0x3C));
        var shstrndx = BinaryPrimitives.ReadUInt16LittleEndian(elf.AsSpan(0x3E));

        (long Off, long Size, uint Name) Section(int i)
        {
            var h = elf.AsSpan((int)(shoff + i * shentsize));
            return ((long)BinaryPrimitives.ReadUInt64LittleEndian(h[0x18..]),
                    (long)BinaryPrimitives.ReadUInt64LittleEndian(h[0x20..]),
                    BinaryPrimitives.ReadUInt32LittleEndian(h));
        }

        var strtab = Section(shstrndx);
        for (var i = 0; i < shnum; i++)
        {
            var s = Section(i);
            var nameStart = (int)(strtab.Off + s.Name);
            var nameEnd = Array.IndexOf(elf, (byte)0, nameStart);
            if (elf.AsSpan(nameStart, nameEnd - nameStart).SequenceEqual(".modinfo"u8))
                return elf.AsSpan((int)s.Off, (int)s.Size);
        }
        return [];
    }

    private static IEnumerable<string> SplitNul(ReadOnlySpan<byte> bytes)
    {
        var result = new List<string>();
        foreach (var range in bytes.Split((byte)0))
        {
            var part = bytes[range];
            if (!part.IsEmpty) result.Add(Encoding.UTF8.GetString(part));
        }
        return result;
    }
}
