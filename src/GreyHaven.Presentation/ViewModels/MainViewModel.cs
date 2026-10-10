using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GreyHaven.Presentation.Theming;
using GreyHaven.Ratings;

namespace GreyHaven.Presentation.ViewModels;

public enum Screen { Start, Working, Results, Walkthrough, Error }

/// <summary>Top-level state: start screen, scanning, results or error, plus app settings.</summary>
public sealed partial class MainViewModel : ObservableObject
{
    private readonly IAppServices _services;
    private readonly ISwitchServices _switch;

    /// <param name="switchServices">The real Windows actions; demo mode always uses <see cref="DemoSwitchServices"/>, which never writes anything.</param>
    public MainViewModel(IAppServices services, ISwitchServices? switchServices = null)
    {
        _services = services;
        _switch = services.DemoSample is not null || switchServices is null ? new DemoSwitchServices() : switchServices;
        var settings = services.Settings.Load();
        Theme = settings.Theme;
        UseOnlineLookups = settings.UseOnlineLookups;
        SoundsOn = settings.Sounds;
        CheckForUpdates = settings.CheckForUpdates;
        CancelScanCommand = ScanCommand.CreateCancelCommand();
        if (CheckForUpdates && services.DemoSample is null) _ = LookForUpdateAsync();
    }

    /// <summary>"GreyHaven 1.1.0 is out." Set when a newer release exists; nothing is downloaded.</summary>
    [ObservableProperty] public partial string? UpdateText { get; private set; }
    private string? _updatePage;

    /// <summary>Notify-only update check, at most once per start. Failures are silent: it's a convenience.</summary>
    [ObservableProperty]
    public partial bool CheckForUpdates { get; set; }
    partial void OnCheckForUpdatesChanged(bool value) => SaveSettings();

    public string CheckForUpdatesText => "Tell me when a new version of GreyHaven is out";

    internal async Task LookForUpdateAsync()
    {
        try
        {
            if (await _services.CheckForUpdateAsync(CancellationToken.None) is { } update)
            {
                UpdateText = $"GreyHaven {update.Version} is out. You have {Core.GreyHavenInfo.Version}.";
                _updatePage = update.PageUrl;
            }
        }
        catch (Exception) { /* never let a convenience check break the app */ }
    }

    [RelayCommand]
    private void OpenUpdate() { if (_updatePage is not null) _services.Open(_updatePage); }

    public string WindowTitle => _services.DemoSample is { } s && !ScreenshotMode.HideDemoBadge ? $"GreyHaven - demo ({s})" : "GreyHaven";

    public string? DemoText => _services.DemoSample is null || ScreenshotMode.HideDemoBadge ? null : "Demo: this is a sample PC, not yours.";

    public string IntroText =>
        "GreyHaven checks the parts inside your PC. Then it tells you which version of Linux fits best. It won't change anything.";

    public string PrivacyText => "GreyHaven never reads your files or personal details.";

    public string OnlineLookupsText => "Also check online for parts we don't know";

    public string OnlineLookupsDetail => "Only sends part ID numbers.";

    [ObservableProperty]
    public partial Screen Screen { get; private set; } = Screen.Start;

    [ObservableProperty]
    public partial string StatusText { get; private set; } = "";

    /// <summary>"2 of 5" during online lookups; null otherwise.</summary>
    [ObservableProperty]
    public partial string? ProgressText { get; private set; }

    [ObservableProperty]
    public partial ResultsViewModel? Results { get; private set; }

    [ObservableProperty]
    public partial JourneyViewModel? Walkthrough { get; private set; }

    [ObservableProperty]
    public partial string? ErrorMessage { get; private set; }

    [ObservableProperty]
    public partial string? ErrorDetails { get; private set; }

    [ObservableProperty]
    public partial ThemeChoice Theme { get; set; }

    [ObservableProperty]
    public partial bool UseOnlineLookups { get; set; }

    /// <summary>Raised when the theme choice changes; the app applies it.</summary>
    public event EventHandler<ThemeChoice>? ThemeChanged;

    partial void OnThemeChanged(ThemeChoice value)
    {
        SaveSettings();
        ThemeChanged?.Invoke(this, value);
    }

    /// <summary>Whether the app is dark right now (the app reports it: "Match Windows" depends on Windows).</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ThemeToggleGlyph), nameof(ThemeToggleName))]
    public partial bool IsDarkNow { get; set; }

    /// <summary>A moon in light mode (switch to dark), a sun in dark mode (Segoe MDL2 Assets, on every Windows).</summary>
    public string ThemeToggleGlyph => IsDarkNow ? "\uE706" : "\uE708";
    public string ThemeToggleName => IsDarkNow ? "Switch to light mode" : "Switch to dark mode";

    /// <summary>The light/dark toggle. Until it's first used, GreyHaven follows Windows.</summary>
    [RelayCommand]
    private void ToggleTheme() => Theme = IsDarkNow ? ThemeChoice.Light : ThemeChoice.Dark;

    partial void OnUseOnlineLookupsChanged(bool value) => SaveSettings();

    /// <summary>Sound effects on or off (the speaker button next to light/dark). The app reads this before playing.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SoundToggleGlyph), nameof(SoundToggleName))]
    public partial bool SoundsOn { get; set; }

    partial void OnSoundsOnChanged(bool value) => SaveSettings();

    /// <summary>A speaker (Segoe MDL2 Volume / Mute).</summary>
    public string SoundToggleGlyph => SoundsOn ? "\uE767" : "\uE74F";
    public string SoundToggleName => SoundsOn ? "Turn sounds off" : "Turn sounds on";

    [RelayCommand]
    private void ToggleSounds()
    {
        SoundsOn = !SoundsOn;
        _services.Play(SoundCue.Tick); // only heard when turning on
    }

    private void SaveSettings() =>
        _services.Settings.Save(new AppSettings { Theme = Theme, UseOnlineLookups = UseOnlineLookups, Sounds = SoundsOn, CheckForUpdates = CheckForUpdates });

    public System.Windows.Input.ICommand CancelScanCommand { get; }

    [RelayCommand(IncludeCancelCommand = false)]
    private async Task ScanAsync(CancellationToken cancellationToken)
    {
        Screen = Screen.Working;
        Results = null;
        ErrorMessage = null;
        ErrorDetails = null;
        ProgressText = null;
        _services.Play(SoundCue.Start);
        try
        {
            StatusText = "Looking at the parts inside your PC…";
            var profile = await _services.ScanAsync(cancellationToken);

            StatusText = "Checking how each part works with Linux…";
            // Progress<T> posts back to the UI thread it was created on.
            var progress = new Progress<RatingProgress>(p =>
            {
                StatusText = p.Message + "...";
                ProgressText = p.Total is null ? null : $"{p.Current} of {p.Total}";
            });
            var report = await _services.RateAsync(profile, UseOnlineLookups, progress, cancellationToken);

            Results = new ResultsViewModel(report, _services);
            Screen = Screen.Results;
            _services.Play(SoundCue.Done);
        }
        catch (OperationCanceledException)
        {
            Screen = Screen.Start;
            StatusText = "Stopped. Nothing was changed.";
        }
        catch (Exception ex)
        {
            ErrorMessage = "Something went wrong while checking your PC. Nothing on your PC was changed. You can try again.";
            ErrorDetails = $"{ex.GetType().Name}: {ex.Message}";
            Screen = Screen.Error;
            _services.Play(SoundCue.Problem);
        }
        finally
        {
            ProgressText = null;
        }
    }

    /// <summary>"What's next": the step-by-step walkthrough for the path chosen on the results screen.</summary>
    [RelayCommand]
    private void ShowWalkthrough()
    {
        if (Results?.HasRecommendation != true) return;
        var results = Results;
        Walkthrough = new JourneyViewModel(results.Report, _services, _switch, () => Screen = Screen.Results, results.UseReport);
        Screen = Screen.Walkthrough;
    }

    [RelayCommand]
    private void StartOver()
    {
        Results = null;
        Walkthrough = null;
        ErrorMessage = null;
        ErrorDetails = null;
        StatusText = "";
        Screen = Screen.Start;
    }
}
