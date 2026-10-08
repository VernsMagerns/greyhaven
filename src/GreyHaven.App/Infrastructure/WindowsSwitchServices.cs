using System.Diagnostics;
using System.IO;
using System.Net.Http;
using GreyHaven.Compat;
using GreyHaven.Media;
using GreyHaven.Presentation;
using GreyHaven.Scan.Windows;

namespace GreyHaven.App.Infrastructure;

/// <summary>The real switching actions on Windows. Anything needing administrator rights goes through GreyHaven.Helper.exe.</summary>
public sealed class WindowsSwitchServices(Lazy<CompatibilityData> data) : ISwitchServices
{
    /// <summary>Its own client: a 6 GB download takes longer than the app's usual 30-second limit.</summary>
    private readonly HttpClient http = GreyHaven.Compat.Online.GreyHavenHttp.Create(Timeout.InfiniteTimeSpan);
    private readonly WindowsHelperClient _helper = new(Path.Combine(AppContext.BaseDirectory, "GreyHaven.Helper.exe"));

    public bool IsDemo => false;

    /// <summary>Where installers are downloaded (kept for retries; the after-install step can delete it).</summary>
    public static string DownloadFolder =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "GreyHaven", "downloads");

    public Task<IReadOnlyList<DiskInfo>> ListUsbSticksAsync(CancellationToken ct) =>
        Task.Run<IReadOnlyList<DiskInfo>>(() =>
        {
            try { return WindowsDisks.UsbSticks().Select(d => d.Info).ToList(); }
            catch (System.Management.ManagementException) { return []; }
        }, ct);

    public InstallerSource? InstallerFor(string distroId, string editionId) =>
        InstallerSources.For(data.Value, distroId, editionId, CompatibilityData.DefaultDirectory);

    public Task<ResolvedInstaller> ResolveInstallerAsync(InstallerSource source, CancellationToken ct) => InstallerResolver.ResolveAsync(http, source, ct);

    public Task<string> DownloadAsync(ResolvedInstaller installer, IProgress<MediaProgress> progress, CancellationToken ct) =>
        Task.Run(() => InstallerDownloader.DownloadAsync(http, installer, DownloadFolder, progress, ct), ct);

    public Task<HelperMessage> RunElevatedAsync(HelperAction action, Func<HelperRequest, HelperRequest>? details, IProgress<HelperMessage>? progress, CancellationToken ct) =>
        _helper.RunAsync(action, details, progress, ct);

    /// <summary>"shutdown /r /o": restart to Windows' Advanced startup menu. Needs no administrator rights.</summary>
    public bool RestartToAdvancedStartup()
    {
        try
        {
            using var p = Process.Start(new ProcessStartInfo(Path.Combine(Environment.SystemDirectory, "shutdown.exe")) { UseShellExecute = false, CreateNoWindow = true, ArgumentList = { "/r", "/o", "/t", "0" } });
            if (p is null) return false;
            p.WaitForExit(10_000);
            return p.HasExited && p.ExitCode == 0;
        }
        catch (System.ComponentModel.Win32Exception) { return false; }
    }

    public void RemoveDownload(string path)
    {
        try { File.Delete(path); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { } // it can be deleted by hand; nothing depends on it
    }

    public string? SaveGuide(string html)
    {
        try
        {
            var folder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "GreyHaven");
            Directory.CreateDirectory(folder);
            var path = Path.Combine(folder, Reports.JourneyBuilder.GuideFileName);
            File.WriteAllText(path, html);
            return path;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { return null; }
    }
}
