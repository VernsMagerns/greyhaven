using System.Text.RegularExpressions;
using GreyHaven.Compat;
using GreyHaven.Core.Compatibility;
using GreyHaven.Core.Hardware;
using GreyHaven.Core.Scanning;
using GreyHaven.Presentation;
using GreyHaven.Presentation.Theming;
using GreyHaven.Presentation.ViewModels;
using GreyHaven.Ratings;

namespace GreyHaven.Tests.Presentation;

public class PaletteTests
{
    public static TheoryData<string> Themes => new() { "light", "dark" };
    private static Palette Get(string theme) => theme == "light" ? Palette.Light : Palette.Dark;

    [Theory]
    [MemberData(nameof(Themes))]
    public void All_text_meets_wcag_aa_on_every_surface(string theme)
    {
        var p = Get(theme);
        var failures = (from t in p.TextColours
                        from s in p.Surfaces
                        let ratio = Rgb.Contrast(t.Colour, s.Colour)
                        where ratio < 4.5
                        select $"{t.Name} on {s.Name}: {ratio:F2}").ToList();
        Assert.Empty(failures);
    }

    [Theory]
    [MemberData(nameof(Themes))]
    public void Accent_button_text_and_borders_meet_aa(string theme)
    {
        var p = Get(theme);
        Assert.True(Rgb.Contrast(p.OnAccent, p.Accent) >= 4.5);
        // Non-text UI parts (card borders) need 3:1 against what they sit on.
        Assert.True(Rgb.Contrast(p.CardBorder, p.Card) >= 3);
        Assert.True(Rgb.Contrast(p.CardBorder, p.Page) >= 3);
    }

    [Fact]
    public void Contrast_matches_wcag_reference_values()
    {
        Assert.Equal(21, Rgb.Contrast(Rgb.Parse("#000000"), Rgb.Parse("#FFFFFF")), 2);
        Assert.Equal(4.54, Rgb.Contrast(Rgb.Parse("#767676"), Rgb.Parse("#FFFFFF")), 2);
    }

    [Fact]
    public void Every_rating_has_text_icon_and_colour()
    {
        foreach (var r in Enum.GetValues<Rating>())
        {
            Assert.False(string.IsNullOrWhiteSpace(RatingDisplay.Label(r)));
            Assert.False(string.IsNullOrWhiteSpace(RatingDisplay.Glyph(r)));
            Assert.Contains(Palette.Light.TextColours, c => c.Name == RatingDisplay.ColourKey(r));
        }
        Assert.Equal(5, Enum.GetValues<Rating>().Select(RatingDisplay.Glyph).Distinct().Count());
    }
}

/// <summary>Fake services: canned profile and report, records opened links and copied text.</summary>
internal sealed class FakeServices : IAppServices
{
    private readonly Func<CancellationToken, Task<HardwareProfile>> _scan;
    private readonly Func<HardwareProfile, Task<CompatibilityReport>> _rate;

    public FakeServices(Func<CancellationToken, Task<HardwareProfile>> scan, Func<HardwareProfile, Task<CompatibilityReport>> rate, string? demo = null)
    {
        _scan = scan;
        _rate = rate;
        DemoSample = demo;
    }

    public string? DemoSample { get; }
    public List<string> Opened { get; } = [];
    public List<string> Copied { get; } = [];
    public bool? LastOnline { get; private set; }
    public MemorySettings Store { get; } = new();
    public ISettingsStore Settings => Store;

    public Task<HardwareProfile> ScanAsync(CancellationToken cancellationToken) => _scan(cancellationToken);

    public Task<CompatibilityReport> RateAsync(HardwareProfile profile, bool useOnlineLookups, IProgress<RatingProgress> progress, CancellationToken cancellationToken)
    {
        LastOnline = useOnlineLookups;
        return _rate(profile);
    }

    public void Open(string target) => Opened.Add(target);
    public void CopyToClipboard(string text) => Copied.Add(text);
    /// <summary>What the fake Save dialog returns; null = cancelled.</summary>
    public string? SaveLocation { get; set; }
    public string? SuggestedName { get; private set; }
    public List<string> Shown { get; } = [];
    public string? ChooseSaveLocation(string suggestedFileName) { SuggestedName = suggestedFileName; return SaveLocation; }
    public void ShowInFolder(string path) => Shown.Add(path);

    public sealed class MemorySettings : ISettingsStore
    {
        public AppSettings Saved { get; set; } = new();
        public int Saves { get; private set; }
        public AppSettings Load() => Saved;
        public void Save(AppSettings settings) { Saved = settings; Saves++; }
    }

    private static readonly Lazy<CompatibilityData> Data = new(() => CompatibilityData.Load());

    public static async Task<CompatibilityReport> RealReport(string sample, Func<HardwareProfile, HardwareProfile>? change = null)
    {
        var profile = await SampleProfiles.Open(sample).ScanAsync();
        if (change is not null) profile = change(profile);
        return await new RatingEngine(Data.Value).RateAsync(profile, new RatingOptions { UseOnlineLookups = false });
    }

    public static FakeServices ForSample(string sample, string? demo = null) =>
        new(async _ => await SampleProfiles.Open(sample).ScanAsync(), _ => RealReport(sample), demo);
}

public class MainViewModelTests
{
    [Fact]
    public async Task Scan_goes_from_start_to_results()
    {
        var services = FakeServices.ForSample("nvidia-desktop");
        var vm = new MainViewModel(services);
        Assert.Equal(Screen.Start, vm.Screen);

        await vm.ScanCommand.ExecuteAsync(null);

        Assert.Equal(Screen.Results, vm.Screen);
        Assert.NotNull(vm.Results);
        Assert.True(services.LastOnline); // default setting
    }

    [Fact]
    public async Task Online_lookups_setting_is_passed_through_and_saved()
    {
        var services = FakeServices.ForSample("intel-laptop");
        var vm = new MainViewModel(services) { UseOnlineLookups = false };
        await vm.ScanCommand.ExecuteAsync(null);

        Assert.False(services.LastOnline);
        Assert.False(services.Store.Saved.UseOnlineLookups);
    }

    [Fact]
    public async Task Cancelling_returns_to_start_with_a_message()
    {
        var services = new FakeServices(
            async ct => { await Task.Delay(Timeout.Infinite, ct); return null!; },
            _ => throw new InvalidOperationException());
        var vm = new MainViewModel(services);

        var scan = vm.ScanCommand.ExecuteAsync(null);
        Assert.Equal(Screen.Working, vm.Screen);
        vm.CancelScanCommand.Execute(null);
        await scan;

        Assert.Equal(Screen.Start, vm.Screen);
        Assert.Equal("Stopped. Nothing was changed.", vm.StatusText);
    }

    [Fact]
    public async Task Errors_show_a_plain_message_and_technical_details()
    {
        var services = new FakeServices(_ => throw new UnauthorizedAccessException("WMI denied"), _ => throw new InvalidOperationException());
        var vm = new MainViewModel(services);
        await vm.ScanCommand.ExecuteAsync(null);

        Assert.Equal(Screen.Error, vm.Screen);
        Assert.Contains("Nothing on your PC was changed", vm.ErrorMessage);
        Assert.Contains("WMI denied", vm.ErrorDetails);

        vm.StartOverCommand.Execute(null);
        Assert.Equal(Screen.Start, vm.Screen);
    }

    [Fact]
    public void Theme_choice_is_saved_and_announced()
    {
        var services = FakeServices.ForSample("intel-laptop");
        var vm = new MainViewModel(services);
        ThemeChoice? applied = null;
        vm.ThemeChanged += (_, t) => applied = t;

        vm.Theme = ThemeChoice.Dark;

        Assert.Equal(ThemeChoice.Dark, applied);
        Assert.Equal(ThemeChoice.Dark, services.Store.Saved.Theme);
    }

    [Fact]
    public void Theme_toggle_flips_light_and_dark_and_names_what_it_does()
    {
        var vm = new MainViewModel(FakeServices.ForSample("intel-laptop")) { IsDarkNow = false };
        Assert.Equal("Switch to dark mode", vm.ThemeToggleName);
        vm.ToggleThemeCommand.Execute(null);
        Assert.Equal(ThemeChoice.Dark, vm.Theme);
        vm.IsDarkNow = true; // the app reports back
        Assert.Equal("Switch to light mode", vm.ThemeToggleName);
        vm.ToggleThemeCommand.Execute(null);
        Assert.Equal(ThemeChoice.Light, vm.Theme);
    }

    [Fact]
    public void Demo_mode_is_visible_in_the_title()
    {
        Assert.Equal("GreyHaven - demo (nvidia-desktop)", new MainViewModel(FakeServices.ForSample("nvidia-desktop", demo: "nvidia-desktop")).WindowTitle);
        Assert.Equal("GreyHaven", new MainViewModel(FakeServices.ForSample("intel-laptop")).WindowTitle);
    }
}

public class ResultsViewModelTests
{
    [Fact]
    public async Task Nvidia_desktop_leads_with_a_plain_headline_and_the_easy_path()
    {
        var vm = new ResultsViewModel(await FakeServices.RealReport("nvidia-desktop"), FakeServices.ForSample("nvidia-desktop", demo: "nvidia-desktop"));

        Assert.Equal("Your PC is ready for Linux", vm.Headline);
        Assert.Equal("We suggest Ubuntu.", vm.Suggestion);
        Assert.Equal("98", vm.ScoreText);
        Assert.Equal("14 parts work right away. 1 part needs one extra step after you install.", vm.HeadlineSummary);
        Assert.Equal("System chipset: 2 parts, all fine. Show them", vm.ChipsetHeading); // not counted above
        Assert.Equal("Demo: this is a sample PC, not yours.", vm.DemoText);
        Assert.True(vm.HasPathChoice);
        Assert.Equal("Pick your version of Linux", vm.PathsHeading);
        var easy = vm.Paths.Single(p => p.IsSelected);
        Assert.Equal(PathKind.Easy, easy.Kind);
        Assert.Equal("Ubuntu", easy.Name);
        Assert.Equal("Easiest", easy.Tag);
        Assert.Equal("Suggested", easy.DefaultLabel);
        Assert.Equal(["Nothing to change on your PC first.", "Your graphics card needs one extra step after you install."], easy.Lines);
        Assert.Equal("Your PC's parts on Ubuntu", vm.PartsHeading);
        Assert.Null(vm.ComparingText);
    }

    [Fact]
    public async Task Top_of_results_is_calm_windows_10_note_collapsed_and_online_note_a_footnote()
    {
        var vm = new ResultsViewModel(await FakeServices.RealReport("nvidia-desktop"), FakeServices.ForSample("nvidia-desktop", demo: "nvidia-desktop"));
        Assert.Empty(vm.Notices); // no boxes: demo is a strip, Windows 10 is "Why switch now?", online check is a footnote
        Assert.StartsWith("Microsoft stopped supporting Windows 10", vm.WhySwitchText);
        Assert.Equal("https://www.microsoft.com/en-us/windows/end-of-support", vm.WhySwitchLink!.Target);
        Assert.EndsWith("The online check was off.", vm.Footnote);
    }

    [Fact]
    public async Task Choosing_advanced_updates_headline_parts_and_what_you_get()
    {
        var vm = new ResultsViewModel(await FakeServices.RealReport("nvidia-desktop"), FakeServices.ForSample("nvidia-desktop"));
        var advanced = vm.Paths.Single(p => p.Kind == PathKind.Advanced);

        advanced.IsSelected = true;

        Assert.False(vm.Paths.Single(p => p.Kind == PathKind.Easy).IsSelected);
        Assert.Equal(PathKind.Advanced, vm.Report.SelectedPath);
        Assert.Equal("We suggest Pop!_OS.", vm.Suggestion);
        Assert.Equal("100", vm.ScoreText);
        Assert.True(vm.Distros.Single(d => d.DistroId == "pop-os").IsChosen);
        Assert.Equal(["Your graphics card works right away.", "First, you turn off Secure Boot in your PC's startup settings."], advanced.Lines);
        Assert.Equal("The Pop!_OS installer file made for NVIDIA graphics (about 4 GB). GreyHaven will put it on a USB stick for you.", advanced.YoullGet);
        Assert.StartsWith("Secure Boot is a safety setting.", advanced.SecureBootText);
        Assert.Empty(vm.AttentionParts); // graphics works out of the box on the NVIDIA ISO
        Assert.Equal("All 15 parts work right away. Show them", vm.WorkingHeading);
    }

    [Fact]
    public async Task Secure_boot_guide_opens_the_bundled_copy()
    {
        var services = FakeServices.ForSample("nvidia-desktop");
        // An unrecognised PC and motherboard maker gets GreyHaven's own guide.
        var report = await FakeServices.RealReport("nvidia-desktop", p => p with { System = p.System with { BoardManufacturer = null } });
        var vm = new ResultsViewModel(report, services);
        var guide = vm.Paths.Single(p => p.Kind == PathKind.Advanced).Guide!;

        guide.OpenCommand.Execute(null);

        var opened = Assert.Single(services.Opened);
        Assert.EndsWith(Path.Combine("docs", "secure-boot.md"), opened);
        Assert.True(File.Exists(opened));
    }

    [Fact]
    public async Task Saving_the_report_follows_the_chosen_path_and_can_be_shown()
    {
        var services = FakeServices.ForSample("nvidia-desktop");
        var vm = new ResultsViewModel(await FakeServices.RealReport("nvidia-desktop"), services);
        vm.Paths.Single(p => p.Kind == PathKind.Advanced).IsSelected = true;
        var path = Path.Combine(Path.GetTempPath(), $"gh-{Guid.NewGuid():N}.md");
        services.SaveLocation = path;
        try
        {
            await vm.SaveReportCommand.ExecuteAsync(null);

            Assert.Matches(@"^greyhaven-report-\d{4}-\d{2}-\d{2}\.html$", services.SuggestedName);
            var saved = await File.ReadAllTextAsync(path);
            Assert.Contains("Your PC's parts on Pop!\\_OS", saved);
            Assert.Contains("Part 5 of 5: On the USB stick", saved);
            Assert.Equal($"Report saved: {Path.GetFileName(path)}", vm.SaveStatus);
            vm.ShowSavedReportCommand.Execute(null);
            Assert.Equal([path], services.Shown);
        }
        finally { File.Delete(path); }
    }

    [Fact]
    public async Task Cancelling_the_save_dialog_does_nothing()
    {
        var services = FakeServices.ForSample("nvidia-desktop");
        var vm = new ResultsViewModel(await FakeServices.RealReport("nvidia-desktop"), services);
        await vm.SaveReportCommand.ExecuteAsync(null);
        Assert.Null(vm.SaveStatus);
        Assert.Null(vm.SavedPath);
    }

    [Fact]
    public async Task A_failed_save_says_so_in_plain_words()
    {
        var services = FakeServices.ForSample("nvidia-desktop");
        var vm = new ResultsViewModel(await FakeServices.RealReport("nvidia-desktop"), services);
        services.SaveLocation = Path.Combine(Path.GetTempPath(), $"missing-{Guid.NewGuid():N}", "report.html");
        await vm.SaveReportCommand.ExecuteAsync(null);
        Assert.StartsWith("The report couldn't be saved there.", vm.SaveStatus);
        Assert.Null(vm.SavedPath);
    }

    [Fact]
    public async Task Comparing_another_distro_keeps_the_chosen_path_and_headline()
    {
        var vm = new ResultsViewModel(await FakeServices.RealReport("nvidia-desktop"), FakeServices.ForSample("nvidia-desktop"));

        vm.Distros.Single(d => d.DistroId == "mint").IsSelected = true;

        Assert.Equal("mint", vm.SelectedDistroId);
        Assert.Equal("Your PC's parts on Linux Mint", vm.PartsHeading);
        Assert.Equal(PathKind.Easy, vm.Report.SelectedPath);
        Assert.Equal("We suggest Ubuntu.", vm.Suggestion);
        Assert.Equal("You're looking at Linux Mint. Your pick is Ubuntu.", vm.ComparingText);
        Assert.False(vm.Distros.Single(d => d.DistroId == "ubuntu").IsSelected);

        vm.ShowChosenDistroCommand.Execute(null);
        Assert.Equal("ubuntu", vm.SelectedDistroId);
        Assert.Null(vm.ComparingText);
    }

    [Fact]
    public async Task Parts_needing_attention_show_one_plain_line_and_details_behind_show_more()
    {
        var services = FakeServices.ForSample("nvidia-desktop");
        var vm = new ResultsViewModel(await FakeServices.RealReport("nvidia-desktop"), services);

        var gpu = Assert.Single(vm.AttentionParts);
        Assert.Equal("Graphics", gpu.Title);
        Assert.Equal("Needs one extra step", gpu.RatingLabel);
        Assert.Equal("Works after you add NVIDIA's driver. It takes a few clicks once Ubuntu is installed.", gpu.Summary);
        Assert.Equal("ExtraStep", gpu.ColourKey);
        Assert.StartsWith("Graphics: Needs one extra step. Essential part.", gpu.AccessibleName);
        Assert.False(gpu.IsExpanded);
        Assert.Equal("How to fix it: NVIDIA proprietary driver", gpu.FixTitle);
        Assert.Contains("Additional Drivers", string.Join(" ", gpu.FixSteps));
        Assert.Contains("Enroll MOK", gpu.SecureBootAdvice);
        Assert.Contains(gpu.TechnicalDetails, d => d.Label == "Device ID" && d.Value == "pci:10de:2484");

        gpu.FixCommands[0].CopyCommand.Execute(null);
        Assert.Equal(["sudo ubuntu-drivers install"], services.Copied);

        Assert.Equal(14, vm.WorkingParts.Count);
        Assert.Equal("14 parts work right away. Show them", vm.WorkingHeading);
    }

    [Fact]
    public async Task Sources_open_their_links()
    {
        var services = FakeServices.ForSample("broadcom-legacy-laptop");
        var vm = new ResultsViewModel(await FakeServices.RealReport("broadcom-legacy-laptop"), services);
        var wifi = vm.AttentionParts.Concat(vm.WorkingParts).Single(c => c.Title == "Wi-Fi");

        wifi.Sources.Single(s => s.IsLink).OpenCommand.Execute(null);
        Assert.Equal(["https://wiki.debian.org/wl"], services.Opened);
    }

    [Fact]
    public async Task Online_check_notes_are_a_footnote_not_a_box()
    {
        var report = await FakeServices.RealReport("intel-laptop");
        report = report with { DataNotes = ["Online lookups were incomplete: linux-hardware.org could not be reached. Ratings use GreyHaven's built-in data only."] };
        var vm = new ResultsViewModel(report, FakeServices.ForSample("intel-laptop"));

        Assert.DoesNotContain(vm.Notices, n => n.Message.Contains("could not be reached"));
        Assert.StartsWith("Checked with GreyHaven's built-in data from ", vm.Footnote);
        Assert.EndsWith("The online check didn't finish, so a few parts may say \"Not sure\". linux-hardware.org could not be reached.", vm.Footnote);
    }

    [Fact]
    public async Task Small_parts_nobody_could_check_are_folded_away_calmly()
    {
        var report = await FakeServices.RealReport("intel-laptop");
        var minor = report.Components.First(c => c.Importance == Importance.Minor && c.Component.Category != ComponentCategory.Chipset);
        report = report with
        {
            Components = report.Components.Select(c => c != minor ? c : c with
            {
                Ratings = c.Ratings.Select(x => x with { Rating = Rating.Unknown }).ToList(),
            }).ToList(),
        };
        var vm = new ResultsViewModel(report, FakeServices.ForSample("intel-laptop"));

        Assert.DoesNotContain(vm.AttentionParts, p => p.Name == minor.DisplayName);
        Assert.Equal("1 small part couldn't be checked. It rarely matters. Show it", vm.UncheckedHeading);
        Assert.EndsWith("1 small part couldn't be checked, which rarely matters.", vm.HeadlineSummary);
        Assert.DoesNotContain("We couldn't check", vm.HeadlineSummary);
    }

    [Fact]
    public async Task Stale_data_shows_an_update_notice()
    {
        var report = await FakeServices.RealReport("intel-laptop");
        report = report with { Data = report.Data with { Notice = "GreyHaven's hardware data is from 1 January 2026. Check for a GreyHaven update." } };
        var vm = new ResultsViewModel(report, FakeServices.ForSample("intel-laptop"));

        var notice = Assert.Single(vm.Notices, n => n.Title == "Check for a GreyHaven update");
        Assert.Equal(NoticeKind.Warning, notice.Kind);
    }

    [Fact]
    public async Task Single_path_has_no_choice_but_is_selected()
    {
        var vm = new ResultsViewModel(await FakeServices.RealReport("intel-laptop"), FakeServices.ForSample("intel-laptop"));
        Assert.False(vm.HasPathChoice);
        Assert.Equal("Your version of Linux", vm.PathsHeading);
        Assert.True(Assert.Single(vm.Paths).IsSelected);
        Assert.Equal("mint", vm.SelectedDistroId);
    }

    [Fact]
    public async Task Arm_pc_gets_a_warning()
    {
        var vm = new ResultsViewModel(await FakeServices.RealReport("snapdragon-arm-laptop"), FakeServices.ForSample("snapdragon-arm-laptop"));
        Assert.Contains(vm.Notices, n => n.Title == "This PC has an ARM chip" && n.Kind == NoticeKind.Warning);
        Assert.NotEmpty(vm.AttentionParts);
    }
}

public class WalkthroughViewModelTests
{
    private static async Task<(MainViewModel Main, FakeServices Services)> AtResults(string sample)
    {
        var services = FakeServices.ForSample(sample);
        var main = new MainViewModel(services, new FakeSwitch());
        await main.ScanCommand.ExecuteAsync(null);
        return (main, services);
    }

    [Fact]
    public async Task Whats_next_opens_the_journey_and_back_returns_to_results()
    {
        var (main, _) = await AtResults("nvidia-desktop");
        main.ShowWalkthroughCommand.Execute(null);

        Assert.Equal(Screen.Walkthrough, main.Screen);
        var w = main.Walkthrough!;
        Assert.Equal("How do you want to install Linux?", w.Title);
        Assert.Equal("Next: Back up your files →", w.NextLabel);
        Assert.False(w.CanGoBack);
        Assert.Equal(["now", "next", "next", "next", "next"], w.Phases.Select(p => p.State));

        w.NextCommand.Execute(null);
        Assert.Equal("Back up your files", w.Title);
        Assert.Equal("Get ready: Back up your files", w.Announcement);

        w.BackToResultsCommand.Execute(null);
        Assert.Equal(Screen.Results, main.Screen);
    }

    [Fact]
    public async Task Journey_follows_the_chosen_path()
    {
        var (main, _) = await AtResults("nvidia-desktop");
        main.Results!.Paths.Single(p => p.Kind == PathKind.Advanced).IsSelected = true;
        main.ShowWalkthroughCommand.Execute(null);
        var w = main.Walkthrough!;
        Assert.Null(w.Report.SelectedInstall);                                     // Pop!_OS can't keep Windows: nothing pre-picked
        Assert.False(w.CanGoOn);
        w.Choices.Single(c => c.Choice == InstallChoice.ReplaceWindows).IsSelected = true;
        w.Go(3);
        Assert.Equal("Change a startup setting", w.Title);
        Assert.Contains(w.Tasks, t => t.Title == "Turn off Secure Boot");
    }

    [Fact]
    public async Task The_install_choice_is_carried_into_the_saved_report()
    {
        var (main, services) = await AtResults("nvidia-desktop");
        main.ShowWalkthroughCommand.Execute(null);
        main.Walkthrough!.Choices.Single(c => c.Choice == InstallChoice.ReplaceWindows).IsSelected = true;
        Assert.Equal(InstallChoice.ReplaceWindows, main.Results!.Report.SelectedInstall);
    }
}

/// <summary>
/// Static checks on the WPF views, so UI rules hold even though the UI can't run on Linux.
/// </summary>
public partial class XamlLintTests
{
    private static string RepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "GreyHaven.slnx"))) dir = dir.Parent;
        return dir?.FullName ?? throw new DirectoryNotFoundException("Repository root not found.");
    }

    private static IEnumerable<(string File, string Xaml)> Views() =>
        Directory.GetFiles(Path.Combine(RepoRoot(), "src", "GreyHaven.App"), "*.xaml", SearchOption.AllDirectories)
            .Where(f => !f.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}") && !f.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}"))
            .Select(f => (Path.GetFileName(f), File.ReadAllText(f)));

    [Fact]
    public void Views_exist() => Assert.True(Views().Count() >= 6);

    [Fact]
    public void No_eyebrow_labels()
    {
        // Small all-caps kicker text above headings is not allowed; lead with the heading.
        var offenders = Views()
            .SelectMany(v => TextLiteralRegex().Matches(v.Xaml).Select(m => (v.File, Text: m.Groups[1].Value)))
            .Where(x => x.Text.Count(char.IsLetter) >= 3 && x.Text.Where(char.IsLetter).All(char.IsUpper))
            .Select(x => $"{x.File}: \"{x.Text}\"")
            .ToList();
        Assert.Empty(offenders);

        Assert.All(Views(), v => Assert.DoesNotContain("CharacterCasing", v.Xaml));
        Assert.All(Views(), v => Assert.DoesNotContain("Typography.Capitals", v.Xaml));
    }

    [Fact]
    public void Every_button_has_an_accessible_name()
    {
        var offenders = new List<string>();
        foreach (var (file, xaml) in Views())
        {
            foreach (Match m in ButtonRegex().Matches(xaml))
            {
                var tag = m.Value;
                var named = tag.Contains("Content=") || tag.Contains("AutomationProperties.Name=") || tag.Contains("Style=\"{StaticResource LinkButton}\"");
                if (!named) offenders.Add($"{file}: {tag[..Math.Min(80, tag.Length)]}");
            }
        }
        Assert.Empty(offenders);
    }

    [Fact]
    public void Content_presenters_dont_bind_their_own_visibility()
    {
        // A ContentPresenter's DataContext is its Content, so Visibility="{Binding X}" next to
        // Content="{Binding X}" looks up X on the content, fails, and stays Visible: an empty,
        // unnamed control appears. Put the Visibility binding on a wrapper instead.
        var offenders = Views()
            .SelectMany(v => ContentPresenterRegex().Matches(v.Xaml).Select(m => (v.File, Tag: m.Value)))
            .Where(x => x.Tag.Contains("Content=\"{Binding ") && x.Tag.Contains("Visibility=\"{Binding"))
            .Select(x => $"{x.File}: {x.Tag[..Math.Min(100, x.Tag.Length)]}")
            .ToList();
        Assert.Empty(offenders);
    }

    [Fact]
    public void Icon_glyphs_are_hidden_from_screen_readers()
    {
        // Any element using the icon font must be DecorativeText (paired with a visible text label).
        var offenders = Views()
            .SelectMany(v => IconUseRegex().Matches(v.Xaml).Select(m => (v.File, m.Value)))
            .Where(x => !x.Value.StartsWith("<infra:DecorativeText", StringComparison.Ordinal))
            .Select(x => $"{x.File}: {x.Value[..Math.Min(80, x.Value.Length)]}")
            .ToList();
        Assert.Empty(offenders);
    }

    [Fact]
    public void Headings_set_a_heading_level()
    {
        var styles = File.ReadAllText(Path.Combine(RepoRoot(), "src", "GreyHaven.App", "Theming", "Styles.xaml"));
        foreach (var level in new[] { "Level1", "Level2", "Level3" })
            Assert.Contains($"\"AutomationProperties.HeadingLevel\" Value=\"{level}\"", styles);
    }

    [GeneratedRegex(@"(?:Text|Content|Header)=""([^""{]+)""")]
    private static partial Regex TextLiteralRegex();

    [GeneratedRegex(@"<Button(?![.\w])[^>]*>", RegexOptions.Singleline)] // <Button ...>, not <Button.Style>
    private static partial Regex ButtonRegex();

    [GeneratedRegex(@"<ContentPresenter\b[^>]*>", RegexOptions.Singleline)]
    private static partial Regex ContentPresenterRegex();

    [GeneratedRegex(@"<[\w:]+\b[^>]*FontFamily=""\{StaticResource IconFont\}""[^>]*>", RegexOptions.Singleline)]
    private static partial Regex IconUseRegex();
}
