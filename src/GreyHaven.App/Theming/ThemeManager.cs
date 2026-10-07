using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using GreyHaven.Presentation.Theming;
using Microsoft.Win32;

namespace GreyHaven.App.Theming;

/// <summary>
/// Applies light, dark or high-contrast theming. Uses the Fluent theme's resource dictionaries
/// directly (not the still-experimental Application.ThemeMode API), plus GreyHaven's own
/// palette (Presentation/Theming/Palette.cs, contrast-tested) as "GH.*" brushes.
/// Follows Windows' app theme and High Contrast setting unless the user picks Light or Dark.
/// </summary>
internal sealed class ThemeManager : IDisposable
{
    private const string FluentLight = "pack://application:,,,/PresentationFramework.Fluent;component/Themes/Fluent.Light.xaml";
    private const string FluentDark = "pack://application:,,,/PresentationFramework.Fluent;component/Themes/Fluent.Dark.xaml";
    private const string FluentHighContrast = "pack://application:,,,/PresentationFramework.Fluent;component/Themes/Fluent.HC.xaml";

    private readonly Application _app;
    private ThemeChoice _choice;

    /// <summary>
    /// App.xaml declares three merged dictionaries in this order: [0] Fluent theme, [1] palette
    /// (filled here), [2] GreyHaven styles. Slots 0 and 1 are replaced when the theme changes.
    /// </summary>
    public ThemeManager(Application app)
    {
        _app = app;
        if (_app.Resources.MergedDictionaries.Count < 3)
            throw new InvalidOperationException("App.xaml must merge the Fluent, palette and styles dictionaries.");
        SystemEvents.UserPreferenceChanged += OnUserPreferenceChanged;
        SystemParameters.StaticPropertyChanged += OnSystemParametersChanged;
    }

    public bool IsDark { get; private set; }

    public void Apply(ThemeChoice choice)
    {
        _choice = choice;
        var highContrast = SystemParameters.HighContrast;
        IsDark = choice switch
        {
            ThemeChoice.Light => false,
            ThemeChoice.Dark => true,
            _ => !SystemUsesLightTheme(),
        };

        var palette = new ResourceDictionary();
        // Note: Palette.Light/Dark are the "Market Day" brand colours (Presentation/Theming/Palette.cs).
        if (highContrast)
        {
            // High Contrast: use the user's system colours for everything; ratings keep icons and text.
            foreach (var (name, _) in Palette.Light.All)
                palette[$"GH.{name}"] = HighContrastBrush(name);
        }
        else
        {
            foreach (var (name, colour) in (IsDark ? Palette.Dark : Palette.Light).All)
            {
                var brush = new SolidColorBrush(Color.FromRgb(colour.R, colour.G, colour.B));
                brush.Freeze();
                palette[$"GH.{name}"] = brush;
            }
        }

        // Walkthrough pictures (GH.Ill.*): their own light/dark colours; hidden in High Contrast.
        foreach (var (name, (light, dark)) in IllustrationColours.All)
        {
            var brush = (SolidColorBrush)new BrushConverter().ConvertFromInvariantString(IsDark ? dark : light)!;
            brush.Freeze();
            palette[$"GH.Ill.{name}"] = brush;
        }

        // Decorative pictures use brand colours, so they're hidden in High Contrast.
        palette["GH.DecorVisibility"] = highContrast ? Visibility.Collapsed : Visibility.Visible;

        var dictionaries = _app.Resources.MergedDictionaries;
        dictionaries[0] = new ResourceDictionary { Source = new Uri(highContrast ? FluentHighContrast : IsDark ? FluentDark : FluentLight) };
        dictionaries[1] = palette;

        foreach (Window w in _app.Windows) ApplyTitleBar(w);
    }

    /// <summary>Dark title bar to match the dark theme (Windows 10 20H1+ and Windows 11).</summary>
    public void ApplyTitleBar(Window window)
    {
        var hwnd = new WindowInteropHelper(window).Handle;
        if (hwnd == IntPtr.Zero) return;
        var dark = IsDark && !SystemParameters.HighContrast ? 1 : 0;
        // Windows 11 and Windows 10 20H1+ use attribute 20; Windows 10 1809-1909 used 19.
        if (DwmSetWindowAttribute(hwnd, DwmwaUseImmersiveDarkMode, ref dark, sizeof(int)) != 0)
            _ = DwmSetWindowAttribute(hwnd, DwmwaUseImmersiveDarkModeBefore20H1, ref dark, sizeof(int));
    }

    private static Brush HighContrastBrush(string name) => name switch
    {
        "Page" or "Card" or "NoticeInfo" or "NoticeWarning" or "CommandBackground" => SystemColors.WindowBrush,
        "Accent" or "ExtraStep" => SystemColors.HotTrackBrush,
        "OnAccent" => SystemColors.HighlightTextBrush,
        "CardBorder" => SystemColors.WindowTextBrush,
        "Shadow" => Brushes.Transparent, // no decorative sticker shadows in High Contrast
        _ => SystemColors.WindowTextBrush,
    };

    /// <summary>Reads Windows' "Choose your app mode" setting. Defaults to light if it can't be read.</summary>
    private static bool SystemUsesLightTheme()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");
            return key?.GetValue("AppsUseLightTheme") is not int v || v != 0;
        }
        catch (Exception ex) when (ex is System.Security.SecurityException or UnauthorizedAccessException)
        {
            return true;
        }
    }

    private void OnUserPreferenceChanged(object sender, UserPreferenceChangedEventArgs e)
    {
        if (e.Category is UserPreferenceCategory.General or UserPreferenceCategory.Color or UserPreferenceCategory.Accessibility)
            _app.Dispatcher.BeginInvoke(() => Apply(_choice));
    }

    private void OnSystemParametersChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(SystemParameters.HighContrast))
            _app.Dispatcher.BeginInvoke(() => Apply(_choice));
    }

    public void Dispose()
    {
        SystemEvents.UserPreferenceChanged -= OnUserPreferenceChanged;
        SystemParameters.StaticPropertyChanged -= OnSystemParametersChanged;
    }

    private const int DwmwaUseImmersiveDarkMode = 20;
    private const int DwmwaUseImmersiveDarkModeBefore20H1 = 19;

    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attribute, ref int value, int size);
}
