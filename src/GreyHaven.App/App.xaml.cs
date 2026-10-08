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

        var data = new Lazy<CompatibilityData>(() => CompatibilityData.Load());
        var services = new AppServices(
            scanner,
            data,
            _http,
            new JsonSettingsStore(JsonSettingsStore.DefaultPath),
            Open,
            CopyToClipboard,
            ChooseSaveLocation,
            ShowInFolder,
            sample);

        Infrastructure.BlockView.Open = Open;
        Infrastructure.BlockView.Copy = CopyToClipboard;

        // Demo mode (--sample) always uses DemoSwitchServices inside MainViewModel: nothing is ever written.
        var vm = new MainViewModel(services, new Infrastructure.WindowsSwitchServices(data));
        _themes = new ThemeManager(this);
        _themes.Apply(vm.Theme);
        vm.IsDarkNow = _themes.IsDark;
        vm.ThemeChanged += (_, theme) => { _themes.Apply(theme); vm.IsDarkNow = _themes.IsDark; };

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

    private static string? ChooseSaveLocation(string suggestedFileName)
    {
        var dialog = new Microsoft.Win32.SaveFileDialog
        {
            Title = "Save your GreyHaven report",
            FileName = Path.GetFileNameWithoutExtension(suggestedFileName),
            Filter = "Web page (*.html)|*.html|Markdown (*.md)|*.md|JSON, for other programs (*.json)|*.json",
            DefaultExt = ".html",
            AddExtension = true,
            InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
        };
        return dialog.ShowDialog(Current.MainWindow) == true ? dialog.FileName : null;
    }

    /// <summary>Opens File Explorer with the saved report selected. Only for files GreyHaven just wrote.</summary>
    private static void ShowInFolder(string path)
    {
        if (!File.Exists(path)) return;
        try
        {
            // Windows file names can't contain quotes, so quoting the path is safe.
            Process.Start(new ProcessStartInfo("explorer.exe", $"/select,\"{path}\"") { UseShellExecute = false });
        }
        catch (System.ComponentModel.Win32Exception) { /* Explorer unavailable; the status text still shows the file name */ }
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
