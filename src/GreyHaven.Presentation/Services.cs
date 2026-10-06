using System.Text.Json;
using GreyHaven.Compat;
using GreyHaven.Compat.Online;
using GreyHaven.Core.Compatibility;
using GreyHaven.Core.Hardware;
using GreyHaven.Core.Scanning;
using GreyHaven.Presentation.Theming;
using GreyHaven.Ratings;

namespace GreyHaven.Presentation;

/// <summary>User preferences, saved between runs.</summary>
public sealed record AppSettings
{
    public ThemeChoice Theme { get; init; } = ThemeChoice.System;
    /// <summary>Allow online lookups (device IDs to linux-hardware.org, distro release feeds).</summary>
    public bool UseOnlineLookups { get; init; } = true;
}

public interface ISettingsStore
{
    AppSettings Load();
    void Save(AppSettings settings);
}

/// <summary>Stores settings as JSON in %LOCALAPPDATA%\GreyHaven. Failures fall back to defaults; settings are a convenience.</summary>
public sealed class JsonSettingsStore(string path) : ISettingsStore
{
    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter() },
    };

    public static string DefaultPath =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "GreyHaven", "settings.json");

    public AppSettings Load()
    {
        try
        {
            return File.Exists(path) ? JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(path), Options) ?? new() : new();
        }
        catch (Exception ex) when (ex is JsonException or IOException or UnauthorizedAccessException)
        {
            return new AppSettings();
        }
    }

    public void Save(AppSettings settings)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(path, JsonSerializer.Serialize(settings, Options));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { /* not worth interrupting the user */ }
    }
}

/// <summary>Everything the view models need from the outside world. Faked in tests.</summary>
public interface IAppServices
{
    /// <summary>Set when running on sample hardware (demo mode), e.g. "nvidia-desktop".</summary>
    string? DemoSample { get; }
    Task<HardwareProfile> ScanAsync(CancellationToken cancellationToken);
    Task<CompatibilityReport> RateAsync(HardwareProfile profile, bool useOnlineLookups, IProgress<RatingProgress> progress, CancellationToken cancellationToken);
    /// <summary>Opens a web link or a file shipped with the app.</summary>
    void Open(string target);
    void CopyToClipboard(string text);
    ISettingsStore Settings { get; }
}

/// <summary>Real implementation; the WPF app supplies the scanner and the Windows-specific bits.</summary>
public sealed class AppServices(
    Func<IHardwareScanner> scannerFactory,
    Lazy<CompatibilityData> data,
    HttpClient http,
    ISettingsStore settings,
    Action<string> open,
    Action<string> copy,
    string? demoSample) : IAppServices
{
    public string? DemoSample => demoSample;
    public ISettingsStore Settings => settings;

    public Task<HardwareProfile> ScanAsync(CancellationToken cancellationToken) =>
        Task.Run(() => scannerFactory().ScanAsync(cancellationToken), cancellationToken);

    public Task<CompatibilityReport> RateAsync(HardwareProfile profile, bool useOnlineLookups, IProgress<RatingProgress> progress, CancellationToken cancellationToken) =>
        Task.Run(async () =>
        {
            var engine = new RatingEngine(
                data.Value,
                new LinuxHardwareDbClient(http, LinuxHardwareDbClient.DefaultCacheDirectory),
                new DistroReleaseChecker(http));
            return await engine.RateAsync(profile, new RatingOptions { UseOnlineLookups = useOnlineLookups }, progress, cancellationToken).ConfigureAwait(false);
        }, cancellationToken);

    public void Open(string target) => open(target);
    public void CopyToClipboard(string text) => copy(text);
}
