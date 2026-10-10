using System.Text.Json;
using GreyHaven.Compat;
using GreyHaven.Compat.Online;
using GreyHaven.Core;
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
    /// <summary>Notify-only check for a newer GreyHaven when the app starts (GitHub releases).</summary>
    public bool CheckForUpdates { get; init; } = true;
    /// <summary>Quiet sound effects (ticks, start, done, problem). On unless turned off.</summary>
    public bool Sounds { get; init; } = true;
}

/// <summary>GreyHaven's sound effects (assets/sounds, made by tools/sounds/build.py).</summary>
public enum SoundCue { Tick, Start, Done, Problem }

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
    /// <summary>Asks where to save a report (a Save dialog). Null if the user cancels.</summary>
    string? ChooseSaveLocation(string suggestedFileName);
    /// <summary>Shows a file the user just saved, selected in File Explorer.</summary>
    void ShowInFolder(string path);
    ISettingsStore Settings { get; }
    /// <summary>"Check a game": searches GreyHaven's bundled game data, on this PC.</summary>
    IReadOnlyList<GameMatch> SearchGames(string query);
    /// <summary>Plays a sound effect, if sounds are on.</summary>
    void Play(SoundCue cue);
    /// <summary>A newer GreyHaven release, or null (up to date, offline, or not public yet). Never downloads anything.</summary>
    Task<AvailableUpdate?> CheckForUpdateAsync(CancellationToken ct);
}

/// <summary>Real implementation; the WPF app supplies the scanner and the Windows-specific bits.</summary>
public sealed class AppServices(
    Func<IHardwareScanner> scannerFactory,
    Lazy<CompatibilityData> data,
    HttpClient http,
    ISettingsStore settings,
    Action<string> open,
    Action<string> copy,
    Func<string, string?> chooseSaveLocation,
    Action<string> showInFolder,
    string? demoSample,
    Action<SoundCue>? play = null) : IAppServices
{
    public string? DemoSample => demoSample;
    public ISettingsStore Settings => settings;
    private readonly Lazy<GameSearch> _games = new(() => new GameSearch(data.Value.Apps));
    public IReadOnlyList<GameMatch> SearchGames(string query) => _games.Value.Search(query);
    public void Play(SoundCue cue) => play?.Invoke(cue);
    public Task<AvailableUpdate?> CheckForUpdateAsync(CancellationToken ct) => new UpdateChecker(http).CheckAsync(GreyHavenInfo.Version, ct);

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
    public string? ChooseSaveLocation(string suggestedFileName) => chooseSaveLocation(suggestedFileName);
    public void ShowInFolder(string path) => showInFolder(path);
}
