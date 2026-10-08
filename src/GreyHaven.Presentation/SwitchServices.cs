using GreyHaven.Media;

namespace GreyHaven.Presentation;

/// <summary>
/// What the walkthrough needs to actually switch: find USB sticks, get and check the installer,
/// run one elevated action, restart into the stick, and save the guide. Faked in tests; in demo
/// mode (<see cref="DemoSwitchServices"/>) everything is simulated and nothing is written.
/// </summary>
public interface ISwitchServices
{
    bool IsDemo { get; }
    Task<IReadOnlyList<DiskInfo>> ListUsbSticksAsync(CancellationToken ct);
    InstallerSource? InstallerFor(string distroId, string editionId);
    Task<ResolvedInstaller> ResolveInstallerAsync(InstallerSource source, CancellationToken ct);
    /// <summary>Downloads (or finds) the installer and checks it; returns its path.</summary>
    Task<string> DownloadAsync(ResolvedInstaller installer, IProgress<MediaProgress> progress, CancellationToken ct);
    Task<HelperMessage> RunElevatedAsync(HelperAction action, Func<HelperRequest, HelperRequest>? details, IProgress<HelperMessage>? progress, CancellationToken ct);
    /// <summary>Restarts to Windows' Advanced startup ("shutdown /r /o"). False if it couldn't (or in demo mode).</summary>
    bool RestartToAdvancedStartup();
    /// <summary>Saves the person's guide as Documents\GreyHaven\GreyHaven guide.html; returns the path, or null if it couldn't.</summary>
    string? SaveGuide(string html);
    /// <summary>Deletes the downloaded installer once the stick is checked, so its space can go to Linux.</summary>
    void RemoveDownload(string path);
}

/// <summary>Demo mode: a pretend 16 GB stick, a quick pretend download and write. Never touches a disk.</summary>
public sealed class DemoSwitchServices(TimeSpan? step = null) : ISwitchServices
{
    private readonly TimeSpan _step = step ?? TimeSpan.FromMilliseconds(60);
    public bool IsDemo => true;
    public List<HelperAction> Elevated { get; } = [];
    public string? SavedGuide { get; private set; }
    public void RemoveDownload(string path) { }

    public Task<IReadOnlyList<DiskInfo>> ListUsbSticksAsync(CancellationToken ct) =>
        Task.FromResult<IReadOnlyList<DiskInfo>>([new DiskInfo(9, "SanDisk Ultra (pretend)", 16_008_609_792, true, true, false, false, false)]);

    public InstallerSource? InstallerFor(string distroId, string editionId) => new() { DistroName = distroId, Api = "https://example.invalid/demo" };

    public Task<ResolvedInstaller> ResolveInstallerAsync(InstallerSource source, CancellationToken ct) =>
        Task.FromResult(new ResolvedInstaller("https://example.invalid/demo.iso", "demo.iso", new string('0', 64), 6_000_000_000, CheckKind.SignedChecksum));

    public async Task<string> DownloadAsync(ResolvedInstaller installer, IProgress<MediaProgress> progress, CancellationToken ct)
    {
        foreach (var stage in new[] { InstallerDownloader.Downloading, InstallerDownloader.Checking })
            for (var i = 1; i <= 10; i++)
            {
                await Task.Delay(_step, ct).ConfigureAwait(false);
                progress.Report(new MediaProgress(stage, i * 600_000_000L, 6_000_000_000));
            }
        return Path.Combine(Path.GetTempPath(), "demo.iso");
    }

    public async Task<HelperMessage> RunElevatedAsync(HelperAction action, Func<HelperRequest, HelperRequest>? details, IProgress<HelperMessage>? progress, CancellationToken ct)
    {
        Elevated.Add(action);
        if (action == HelperAction.WriteUsb)
            foreach (var stage in new[] { InstallerDownloader.Checking, ImageWriter.Writing, ImageWriter.Verifying })
                for (var i = 1; i <= 10; i++)
                {
                    await Task.Delay(_step, ct).ConfigureAwait(false);
                    progress?.Report(new HelperMessage(HelperMessage.Progress, stage, i * 600_000_000L, 6_000_000_000));
                }
        return new HelperMessage(HelperMessage.Ok, Text: action == HelperAction.WriteUsb ? "Your USB stick is ready." : "Done.");
    }

    public bool RestartToAdvancedStartup() => false;
    public string? SaveGuide(string html) { SavedGuide = html; return @"Documents\GreyHaven\GreyHaven guide.html"; }
}

/// <summary>Turns an edition's "download" entry in data/distros.yaml into an <see cref="InstallerSource"/>, reading the pinned key file.</summary>
public static class InstallerSources
{
    public static InstallerSource? For(GreyHaven.Compat.CompatibilityData data, string distroId, string editionId, string dataDirectory)
    {
        var distro = data.Distros.Distros.FirstOrDefault(d => d.Id == distroId);
        var dl = distro?.Editions.FirstOrDefault(e => e.Id == editionId)?.Download;
        if (distro is null || dl is null) return null;
        string? key = null;
        if (dl.Key is { } k)
        {
            var path = Path.GetFullPath(Path.Combine(dataDirectory, k));
            // Only key files inside the data folder.
            if (!path.StartsWith(Path.GetFullPath(dataDirectory), StringComparison.OrdinalIgnoreCase) || !File.Exists(path)) return null;
            key = File.ReadAllText(path);
        }
        return new InstallerSource
        {
            DistroName = distro.Name, File = dl.File, Url = dl.Url, Checksums = dl.Checksums, Signature = dl.Signature,
            Key = key, KeyFingerprint = dl.KeyFingerprint, Api = dl.Api,
        };
    }
}
