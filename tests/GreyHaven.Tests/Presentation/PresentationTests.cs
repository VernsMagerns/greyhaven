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

    public sealed class MemorySettings : ISettingsStore
    {
        public AppSettings Saved { get; set; } = new();
        public int Saves { get; private set; }
        public AppSettings Load() => Saved;
        public void Save(AppSettings settings) { Saved = settings; Saves++; }
    }

    private static readonly Lazy<CompatibilityData> Data = new(() => CompatibilityData.Load());

    public static async Task<CompatibilityReport> RealReport(string sample)
    {
        var profile = await SampleProfiles.Open(sample).ScanAsync();
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
        Assert.Equal("Scan cancelled.", vm.StatusText);
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
        Assert.Equal(3, vm.ThemeOptions.Count);
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
    public async Task Nvidia_desktop_starts_on_the_easy_path()
    {
        var services = FakeServices.ForSample("nvidia-desktop");
        var vm = new ResultsViewModel(await FakeServices.RealReport("nvidia-desktop"), services);

        Assert.True(vm.HasPathChoice);
        Assert.Equal(PathKind.Easy, vm.Paths.Single(p => p.IsSelected).Kind);
        Assert.Equal("ubuntu", vm.SelectedDistroId);
        Assert.Equal("ubuntu", vm.ChosenDistroId);
        Assert.Equal("Keeps Secure Boot on", vm.Paths[0].Subtitle);
        Assert.Equal("Suggested for most people", vm.Paths[0].DefaultLabel);
        Assert.Contains("Ubuntu", vm.DistroName);
        Assert.Null(vm.ComparingText);
    }

    [Fact]
    public async Task Choosing_advanced_carries_through_to_report_score_and_components()
    {
        var services = FakeServices.ForSample("nvidia-desktop");
        var vm = new ResultsViewModel(await FakeServices.RealReport("nvidia-desktop"), services);
        var advanced = vm.Paths.Single(p => p.Kind == PathKind.Advanced);

        advanced.IsSelected = true;

        Assert.False(vm.Paths.Single(p => p.Kind == PathKind.Easy).IsSelected);
        Assert.Equal(PathKind.Advanced, vm.Report.SelectedPath);
        Assert.Equal("pop-os", vm.SelectedDistroId);
        Assert.Equal("100", vm.ScoreText);
        Assert.True(vm.Distros.Single(d => d.DistroId == "pop-os").IsChosen);
        Assert.Equal("You'll download: NVIDIA ISO", advanced.DownloadText);
        Assert.Equal("Needs Secure Boot turned off", advanced.Subtitle);
        Assert.Contains("turn it back on later", advanced.SecureBootNotice);
        // Graphics works out of the box on Pop!_OS's NVIDIA ISO, so nothing needs attention.
        Assert.DoesNotContain(vm.Groups, g => g.Heading == "Needs your attention");
    }

    [Fact]
    public async Task Secure_boot_guide_opens_the_bundled_copy_while_the_repo_is_unconfigured()
    {
        var services = FakeServices.ForSample("nvidia-desktop");
        var vm = new ResultsViewModel(await FakeServices.RealReport("nvidia-desktop"), services);
        var guide = vm.Paths.Single(p => p.Kind == PathKind.Advanced).Guide!;

        guide.OpenCommand.Execute(null);

        var opened = Assert.Single(services.Opened);
        Assert.EndsWith(Path.Combine("docs", "secure-boot.md"), opened);
        Assert.True(File.Exists(opened));
    }

    [Fact]
    public async Task Comparing_another_distro_keeps_the_chosen_path()
    {
        var vm = new ResultsViewModel(await FakeServices.RealReport("nvidia-desktop"), FakeServices.ForSample("nvidia-desktop"));

        vm.Distros.Single(d => d.DistroId == "mint").IsSelected = true;

        Assert.Equal("mint", vm.SelectedDistroId);
        Assert.Equal(PathKind.Easy, vm.Report.SelectedPath);
        Assert.Contains("You're comparing", vm.ComparingText);
        Assert.False(vm.Distros.Single(d => d.DistroId == "ubuntu").IsSelected);

        vm.ShowChosenDistroCommand.Execute(null);
        Assert.Equal("ubuntu", vm.SelectedDistroId);
        Assert.Null(vm.ComparingText);
    }

    [Fact]
    public async Task Components_needing_attention_come_first_with_fixes_one_click_away()
    {
        var services = FakeServices.ForSample("nvidia-desktop");
        var vm = new ResultsViewModel(await FakeServices.RealReport("nvidia-desktop"), services);

        Assert.Equal("Needs your attention", vm.Groups[0].Heading);
        var gpu = vm.Groups[0].Components.Single(c => c.Title == "Graphics");
        Assert.Equal("Works after an extra step", gpu.RatingLabel);
        Assert.Equal("ExtraStep", gpu.ColourKey);
        Assert.StartsWith("Graphics: Works after an extra step. Essential part.", gpu.AccessibleName);
        Assert.False(gpu.IsExpanded);
        Assert.True(gpu.HasFix);
        Assert.Contains("Additional Drivers", string.Join(" ", gpu.FixSteps));
        Assert.NotNull(gpu.SecureBootAdvice);
        Assert.Contains(gpu.TechnicalDetails, d => d.Label == "Device ID" && d.Value == "pci:10de:2484");
        Assert.Contains(gpu.TechnicalDetails, d => d.Label == "Rating" && d.Value == "Works with extra driver/config");

        gpu.FixCommands[0].CopyCommand.Execute(null);
        Assert.Equal(["sudo ubuntu-drivers install"], services.Copied);

        Assert.Equal("Works out of the box", vm.Groups[1].Heading);
    }

    [Fact]
    public async Task Sources_open_their_links()
    {
        var services = FakeServices.ForSample("broadcom-legacy-laptop");
        var vm = new ResultsViewModel(await FakeServices.RealReport("broadcom-legacy-laptop"), services);
        var wifi = vm.Groups.SelectMany(g => g.Components).Single(c => c.Title == "Wi-Fi");

        wifi.Sources.Single(s => s.IsLink).OpenCommand.Execute(null);
        Assert.Equal(["https://wiki.debian.org/wl"], services.Opened);
    }

    [Fact]
    public async Task Notices_include_demo_and_report_notes_once()
    {
        var report = await FakeServices.RealReport("intel-laptop");
        report = report with { DataNotes = ["Online lookups were incomplete: linux-hardware.org could not be reached."] };
        var vm = new ResultsViewModel(report, FakeServices.ForSample("intel-laptop", demo: "intel-laptop"));

        Assert.Contains(vm.Notices, n => n.Title == "Demo mode");
        Assert.Single(vm.Notices, n => n.Message.Contains("could not be reached"));
        Assert.StartsWith("Hardware data from ", vm.DataBuiltText);
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
        Assert.True(Assert.Single(vm.Paths).IsSelected);
        Assert.Equal("mint", vm.SelectedDistroId);
    }

    [Fact]
    public async Task Arm_pc_has_no_paths_and_a_warning()
    {
        var vm = new ResultsViewModel(await FakeServices.RealReport("snapdragon-arm-laptop"), FakeServices.ForSample("snapdragon-arm-laptop"));
        Assert.Contains(vm.Notices, n => n.Title == "This PC has an ARM processor" && n.Kind == NoticeKind.Warning);
        Assert.NotEmpty(vm.Groups);
    }

    [Theory]
    [InlineData(new[] { Rating.WorksOutOfTheBox }, "1 part works out of the box.")]
    [InlineData(new[] { Rating.WorksOutOfTheBox, Rating.WorksOutOfTheBox, Rating.WorksWithExtraDriver }, "2 parts work out of the box and 1 part needs an extra step.")]
    [InlineData(new[] { Rating.Partial, Rating.Unsupported, Rating.Unknown, Rating.Unknown }, "1 part partly works, 1 part doesn't work and 2 parts couldn't be checked.")]
    public void Summary_text_reads_naturally(Rating[] ratings, string expected) =>
        Assert.Equal(expected, ResultsViewModel.Summarise(ratings));
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

    [GeneratedRegex(@"<Button\b[^>]*>", RegexOptions.Singleline)]
    private static partial Regex ButtonRegex();

    [GeneratedRegex(@"<[\w:]+\b[^>]*FontFamily=""\{StaticResource IconFont\}""[^>]*>", RegexOptions.Singleline)]
    private static partial Regex IconUseRegex();
}
