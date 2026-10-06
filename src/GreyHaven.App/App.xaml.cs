using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Runtime.InteropServices;
using System.Windows;
using GreyHaven.App.Theming;
using GreyHaven.Compat;
using GreyHaven.Compat.Online;
using GreyHaven.Core.Scanning;
using GreyHaven.Presentation;
using GreyHaven.Presentation.ViewModels;
using GreyHaven.Scan.Windows;

namespace GreyHaven.App;

/// <summary>
/// Startup. Arguments:
///   --sample &lt;name&gt;   demo mode: use a bundled sample PC instead of scanning (see samples/).
/// </summary>
public partial class App : Application
{
    private ThemeManager? _themes;
    private HttpClient? _http;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        var sample = SampleArgument(e.Args);
        _http = GreyHavenHttp.Create(TimeSpan.FromSeconds(30));
        Func<IHardwareScanner> scanner = sample is null
            ? () => new LiveScanner(new WindowsRawHardwareSource())
            : () => SampleProfiles.Open(sample);

        var services = new AppServices(
            scanner,
            new Lazy<CompatibilityData>(() => CompatibilityData.Load()),
            _http,
            new JsonSettingsStore(JsonSettingsStore.DefaultPath),
            Open,
            CopyToClipboard,
            sample);

        var vm = new MainViewModel(services);
        _themes = new ThemeManager(this);
        _themes.Apply(vm.Theme);
        vm.ThemeChanged += (_, theme) => _themes.Apply(theme);

        var window = new MainWindow { DataContext = vm };
        window.SourceInitialized += (_, _) => _themes.ApplyTitleBar(window);
        MainWindow = window;
        window.Show();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _themes?.Dispose();
        _http?.Dispose();
        base.OnExit(e);
    }

    private static string? SampleArgument(string[] args)
    {
        var i = Array.IndexOf(args, "--sample");
        return i >= 0 && i + 1 < args.Length ? args[i + 1] : null;
    }

    /// <summary>
    /// Opens a link from GreyHaven's data or a guide shipped with the app. Only https links and
    /// files inside the app folder are opened, so data can never launch anything else.
    /// </summary>
    private static void Open(string target)
    {
        var isWeb = Uri.TryCreate(target, UriKind.Absolute, out var uri) && uri.Scheme == Uri.UriSchemeHttps;
        var appDir = Path.GetFullPath(AppContext.BaseDirectory);
        var isLocal = !isWeb && Path.IsPathRooted(target) && Path.GetFullPath(target).StartsWith(appDir, StringComparison.OrdinalIgnoreCase) && File.Exists(target);
        if (!isWeb && !isLocal) return;
        try
        {
            Process.Start(new ProcessStartInfo(target) { UseShellExecute = true });
        }
        catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or InvalidOperationException)
        {
            MessageBox.Show($"Windows couldn't open this link:\n{target}", "GreyHaven", MessageBoxButton.OK, MessageBoxImage.Information);
        }
    }

    private static void CopyToClipboard(string text)
    {
        // The clipboard can be briefly locked by another app; retry a few times.
        for (var attempt = 0; attempt < 5; attempt++)
        {
            try
            {
                Clipboard.SetText(text);
                return;
            }
            catch (COMException)
            {
                Thread.Sleep(50);
            }
        }
    }
}
