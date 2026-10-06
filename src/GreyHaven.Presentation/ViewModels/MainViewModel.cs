using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GreyHaven.Presentation.Theming;
using GreyHaven.Ratings;

namespace GreyHaven.Presentation.ViewModels;

public enum Screen { Start, Working, Results, Error }

public sealed record ThemeOption(ThemeChoice Value, string Label);

/// <summary>Top-level state: start screen, scanning, results or error, plus app settings.</summary>
public sealed partial class MainViewModel : ObservableObject
{
    private readonly IAppServices _services;

    public MainViewModel(IAppServices services)
    {
        _services = services;
        var settings = services.Settings.Load();
        Theme = settings.Theme;
        UseOnlineLookups = settings.UseOnlineLookups;
        CancelScanCommand = ScanCommand.CreateCancelCommand();
    }

    public string WindowTitle => _services.DemoSample is { } s ? $"GreyHaven - demo ({s})" : "GreyHaven";

    public IReadOnlyList<ThemeOption> ThemeOptions { get; } =
    [
        new(ThemeChoice.System, "Match Windows"),
        new(ThemeChoice.Light, "Light"),
        new(ThemeChoice.Dark, "Dark"),
    ];

    public string PrivacyText =>
        "GreyHaven reads device IDs and settings only. It never reads serial numbers, your name, your PC's name or network addresses.";

    public string OnlineLookupsText =>
        "Look up unknown devices online. Sends only device IDs (like pci:8086-2723) to linux-hardware.org, and checks the distros' websites for new releases.";

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

    partial void OnUseOnlineLookupsChanged(bool value) => SaveSettings();

    private void SaveSettings() =>
        _services.Settings.Save(new AppSettings { Theme = Theme, UseOnlineLookups = UseOnlineLookups });

    public System.Windows.Input.ICommand CancelScanCommand { get; }

    [RelayCommand(IncludeCancelCommand = false)]
    private async Task ScanAsync(CancellationToken cancellationToken)
    {
        Screen = Screen.Working;
        Results = null;
        ErrorMessage = null;
        ErrorDetails = null;
        ProgressText = null;
        try
        {
            StatusText = "Reading your PC's hardware...";
            var profile = await _services.ScanAsync(cancellationToken);

            StatusText = "Checking how each part works on Linux...";
            // Progress<T> posts back to the UI thread it was created on.
            var progress = new Progress<RatingProgress>(p =>
            {
                StatusText = p.Message + "...";
                ProgressText = p.Total is null ? null : $"{p.Current} of {p.Total}";
            });
            var report = await _services.RateAsync(profile, UseOnlineLookups, progress, cancellationToken);

            Results = new ResultsViewModel(report, _services);
            Screen = Screen.Results;
        }
        catch (OperationCanceledException)
        {
            Screen = Screen.Start;
            StatusText = "Scan cancelled.";
        }
        catch (Exception ex)
        {
            ErrorMessage = "Something went wrong while checking your PC. Nothing on your PC was changed. You can try again.";
            ErrorDetails = $"{ex.GetType().Name}: {ex.Message}";
            Screen = Screen.Error;
        }
        finally
        {
            ProgressText = null;
        }
    }

    [RelayCommand]
    private void StartOver()
    {
        Results = null;
        ErrorMessage = null;
        ErrorDetails = null;
        StatusText = "";
        Screen = Screen.Start;
    }
}
