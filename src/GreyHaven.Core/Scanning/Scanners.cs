using GreyHaven.Core.Hardware;
using GreyHaven.Core.Serialization;

namespace GreyHaven.Core.Scanning;

/// <summary>Produces a <see cref="HardwareProfile"/>. The UI and CLI depend only on this.</summary>
public interface IHardwareScanner
{
    Task<HardwareProfile> ScanAsync(CancellationToken cancellationToken = default);
}

/// <summary>Reads raw hardware data from the OS. Implemented by GreyHaven.Scan.Windows.</summary>
public interface IRawHardwareSource
{
    Task<RawSnapshot> CaptureAsync(CancellationToken cancellationToken = default);
}

/// <summary>Scans live hardware via an <see cref="IRawHardwareSource"/>.</summary>
public sealed class LiveScanner(IRawHardwareSource source) : IHardwareScanner
{
    public async Task<HardwareProfile> ScanAsync(CancellationToken cancellationToken = default)
    {
        var raw = await source.CaptureAsync(cancellationToken).ConfigureAwait(false);
        return ProfileBuilder.Build(raw, "live-scan");
    }
}

/// <summary>
/// Replays a saved raw snapshot (a sample in <c>samples/</c> or a <c>--dump-raw</c> file from a real PC).
/// Lets the app be developed and demoed without real hardware.
/// </summary>
public sealed class SnapshotFileScanner(string path, string? sourceLabel = null) : IHardwareScanner
{
    public async Task<HardwareProfile> ScanAsync(CancellationToken cancellationToken = default)
    {
        await using var stream = File.OpenRead(path);
        var raw = await GreyHavenJson.DeserializeAsync<RawSnapshot>(stream, cancellationToken).ConfigureAwait(false)
                  ?? throw new InvalidDataException($"'{path}' is not a GreyHaven raw snapshot.");
        return ProfileBuilder.Build(raw, sourceLabel ?? $"raw:{Path.GetFileName(path)}");
    }
}

/// <summary>Finds the bundled sample snapshots (<c>samples/*.raw.json</c>).</summary>
public static class SampleProfiles
{
    public const string FileSuffix = ".raw.json";

    public static string DefaultDirectory => Path.Combine(AppContext.BaseDirectory, "samples");

    public static IReadOnlyList<string> List(string? directory = null)
    {
        var dir = directory ?? DefaultDirectory;
        if (!Directory.Exists(dir)) return [];
        return Directory.GetFiles(dir, "*" + FileSuffix)
            .Select(f => Path.GetFileName(f)[..^FileSuffix.Length])
            .Order(StringComparer.Ordinal)
            .ToList();
    }

    public static IHardwareScanner Open(string name, string? directory = null)
    {
        var path = Path.Combine(directory ?? DefaultDirectory, name + FileSuffix);
        if (!File.Exists(path))
            throw new FileNotFoundException($"No sample profile named '{name}'. Available: {string.Join(", ", List(directory))}", path);
        return new SnapshotFileScanner(path, $"sample:{name}");
    }
}
