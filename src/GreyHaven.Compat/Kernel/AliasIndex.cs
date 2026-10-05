using System.IO.Compression;
using System.Text;
using GreyHaven.Core.Hardware;

namespace GreyHaven.Compat.Kernel;

/// <summary>One alias pattern and the module (or driver package) that claims it.</summary>
public sealed record AliasEntry(string Pattern, string Target)
{
    /// <summary>
    /// True when the pattern names a specific vendor and device (e.g. <c>pci:v00008086d00002723…</c>),
    /// false for class- or vendor-wide patterns (e.g. <c>pci:v*d*sv*sd*bc0Csc03i30*</c>).
    /// </summary>
    public bool IsDeviceSpecific { get; } = ComputeDeviceSpecific(Pattern);

    private static bool ComputeDeviceSpecific(string p)
    {
        static bool Exact(string s, int start, int len) =>
            s.Length >= start + len && s.AsSpan(start, len).IndexOfAny("*?[") < 0;

        if (p.StartsWith("pci:v", StringComparison.Ordinal)) return Exact(p, 5, 8) && p.Length > 14 && p[13] == 'd' && Exact(p, 14, 8);
        if (p.StartsWith("usb:v", StringComparison.Ordinal)) return Exact(p, 5, 4) && p.Length > 10 && p[9] == 'p' && Exact(p, 10, 4);
        if (p.StartsWith("hdaudio:v", StringComparison.Ordinal)) return Exact(p, 9, 8);
        if (p.StartsWith("hid:", StringComparison.Ordinal))
        {
            var v = p.IndexOf('v', StringComparison.Ordinal);
            return v > 0 && Exact(p, v + 1, 8) && p.Length > v + 10 && p[v + 9] == 'p' && Exact(p, v + 10, 8);
        }
        if (p.StartsWith("acpi", StringComparison.Ordinal))
        {
            var colon = p.IndexOf(':');
            var end = colon < 0 ? -1 : p.IndexOf(':', colon + 1);
            return end > colon + 1 && p.AsSpan(colon + 1, end - colon - 1).IndexOfAny("*?[") < 0;
        }
        return false;
    }
}

/// <summary>Header metadata of an index file (provenance).</summary>
public sealed record AliasIndexInfo(string Id, string Kind, IReadOnlyDictionary<string, string> Headers)
{
    public string? Get(string key) => Headers.TryGetValue(key, out var v) ? v : null;
}

/// <summary>
/// A set of alias patterns, either from a kernel's modules (<c>kind: kernel</c>) or from driver
/// packages' Modaliases fields (<c>kind: packages</c>).
///
/// File format (gzip-compressed UTF-8 text):
/// <code>
/// #greyhaven-index 1
/// #id: ubuntu-7.0.0-30
/// #kind: kernel
/// #&lt;key&gt;: &lt;value&gt;          (any number of provenance headers)
/// &lt;pattern&gt;\t&lt;target&gt;
/// </code>
/// </summary>
public sealed class AliasIndex
{
    public const string Magic = "#greyhaven-index 1";

    private readonly Dictionary<string, List<AliasEntry>> _byPrefix;

    public AliasIndexInfo Info { get; }
    public IEnumerable<AliasEntry> Entries => _byPrefix.Values.SelectMany(v => v);
    public int Count { get; }

    private AliasIndex(AliasIndexInfo info, IEnumerable<AliasEntry> entries)
    {
        Info = info;
        _byPrefix = new Dictionary<string, List<AliasEntry>>(StringComparer.Ordinal);
        var n = 0;
        foreach (var e in entries)
        {
            var key = PrefixOf(e.Pattern);
            if (key is null) continue;
            if (!_byPrefix.TryGetValue(key, out var list)) _byPrefix[key] = list = [];
            list.Add(e);
            n++;
        }
        Count = n;
    }

    public static AliasIndex Create(AliasIndexInfo info, IEnumerable<AliasEntry> entries) => new(info, entries);

    /// <summary>All entries whose pattern matches this device. Empty if nothing claims it.</summary>
    public IReadOnlyList<AliasEntry> Match(DeviceId id)
    {
        var modalias = Modalias.For(id);
        if (modalias is null) return [];
        var key = PrefixOf(modalias);
        if (key is null || !_byPrefix.TryGetValue(key, out var candidates)) return [];
        return candidates.Where(e => Glob.IsMatch(e.Pattern, modalias)).ToList();
    }

    private static string? PrefixOf(string s)
    {
        if (s.StartsWith("pci:", StringComparison.Ordinal)) return "pci";
        if (s.StartsWith("usb:", StringComparison.Ordinal)) return "usb";
        if (s.StartsWith("hdaudio:", StringComparison.Ordinal)) return "hdaudio";
        if (s.StartsWith("hid:", StringComparison.Ordinal)) return "hid";
        if (s.StartsWith("acpi", StringComparison.Ordinal)) return "acpi";
        return null;
    }

    public static bool IsRelevantPattern(string pattern) => PrefixOf(pattern) is not null;

    // ---- File I/O ----------------------------------------------------------------------

    public static AliasIndex Load(string path)
    {
        using var file = File.OpenRead(path);
        return Load(file);
    }

    public static AliasIndex Load(Stream gzipStream)
    {
        using var gz = new GZipStream(gzipStream, CompressionMode.Decompress);
        using var reader = new StreamReader(gz, Encoding.UTF8);
        if (reader.ReadLine() != Magic) throw new InvalidDataException("Not a GreyHaven index file.");

        var headers = new Dictionary<string, string>(StringComparer.Ordinal);
        var entries = new List<AliasEntry>();
        while (reader.ReadLine() is { } line)
        {
            if (line.Length == 0) continue;
            if (line[0] == '#')
            {
                var colon = line.IndexOf(':');
                if (colon > 1) headers[line[1..colon].Trim()] = line[(colon + 1)..].Trim();
                continue;
            }
            var tab = line.IndexOf('\t');
            if (tab <= 0) throw new InvalidDataException($"Malformed index line: {line}");
            entries.Add(new AliasEntry(line[..tab], line[(tab + 1)..]));
        }

        var id = headers.GetValueOrDefault("id") ?? throw new InvalidDataException("Index file has no #id header.");
        var kind = headers.GetValueOrDefault("kind") ?? "kernel";
        return new AliasIndex(new AliasIndexInfo(id, kind, headers), entries);
    }

    /// <summary>Writes an index deterministically (sorted, de-duplicated) so regenerating it gives a stable diff.</summary>
    public static void Write(string path, IReadOnlyDictionary<string, string> headers, IEnumerable<AliasEntry> entries)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
        using var file = File.Create(path);
        using var gz = new GZipStream(file, CompressionLevel.SmallestSize);
        using var w = new StreamWriter(gz, new UTF8Encoding(false)) { NewLine = "\n" };
        w.WriteLine(Magic);
        foreach (var (k, v) in headers) w.WriteLine($"#{k}: {v}");
        foreach (var e in entries
                     .Where(e => IsRelevantPattern(e.Pattern))
                     .DistinctBy(e => (e.Pattern, e.Target))
                     .OrderBy(e => e.Pattern, StringComparer.Ordinal)
                     .ThenBy(e => e.Target, StringComparer.Ordinal))
        {
            w.WriteLine($"{e.Pattern}\t{e.Target}");
        }
    }
}
