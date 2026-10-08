using GreyHaven.Compat;
using GreyHaven.Core.Compatibility;
using GreyHaven.Core.Hardware;
using GreyHaven.Core.Scanning;
using GreyHaven.Ratings;
using GreyHaven.Reports;

namespace GreyHaven.Tests.Reports;

public class WalkthroughTests
{
    private static readonly Lazy<CompatibilityData> Data = new(() => CompatibilityData.Load());

    private static async Task<CompatibilityReport> Rate(string sample, Func<HardwareProfile, HardwareProfile>? change = null)
    {
        var profile = await SampleProfiles.Open(sample).ScanAsync();
        if (change is not null) profile = change(profile);
        return await new RatingEngine(Data.Value).RateAsync(profile, new RatingOptions { UseOnlineLookups = false });
    }

    private static string Text(IEnumerable<Block> blocks) => MarkdownReportWriter.Write(new ReportDocument("t", blocks.ToList()));
    private static string Text(WalkStep s) => Text(s.Body.Concat(s.Options.SelectMany(o => o.Steps)));

    [Fact]
    public async Task Nine_steps_in_order()
    {
        var steps = WalkthroughBuilder.Build(await Rate("nvidia-desktop"));
        Assert.Equal(["backup", "usb", "windows", "download", "write", "boot", "try", "install", "after"], steps.Select(s => s.Id));
        Assert.All(steps.Where(s => s.Id != "install"), s => Assert.False(string.IsNullOrWhiteSpace(s.Tip), $"{s.Id} needs a 'What could go wrong?' tip"));
    }

    [Fact]
    public async Task Windows_checklist_is_for_this_pc_and_path()
    {
        var easy = Text(WalkthroughBuilder.Build(await Rate("nvidia-desktop")).Single(s => s.Id == "windows"));
        var advanced = Text(WalkthroughBuilder.Build((await Rate("nvidia-desktop")).WithPath(PathKind.Advanced)).Single(s => s.Id == "windows"));

        Assert.Contains("Turn off Fast Startup", easy);              // the sample has Fast Startup on
        Assert.DoesNotContain("Turn off Secure Boot", easy);         // Ubuntu keeps Secure Boot on
        Assert.Contains("Turn off Secure Boot", advanced);
        Assert.Contains("https://www.msi.com/faq/faq-11370", advanced);
        Assert.Contains("https://www.msi.com/support", easy);         // BIOS updates: the motherboard maker's page
        Assert.DoesNotContain("BitLocker", easy);                     // not encrypted

        var vmd = Text(WalkthroughBuilder.Build(await Rate("intel-vmd-laptop")).Single(s => s.Id == "windows"));
        Assert.Contains("Change a storage setting", vmd);
    }

    [Fact]
    public async Task Boot_step_uses_the_makers_key_or_the_windows_way()
    {
        var msi = Text(WalkthroughBuilder.Build(await Rate("nvidia-desktop")).Single(s => s.Id == "boot"));
        Assert.Contains("startup menu key is: **F11**", msi);
        var unknown = Text(WalkthroughBuilder.Build(await Rate("intel-vmd-laptop")).Single(s => s.Id == "boot"));
        Assert.Contains("We don't know the startup menu key", unknown);
        Assert.Contains("Use a device", unknown);
    }

    [Theory]
    [InlineData(19045, "Settings > Update & Security > Recovery")] // Windows 10 22H2
    [InlineData(26100, "Settings > System > Recovery")]            // Windows 11 24H2
    public async Task Advanced_startup_path_matches_the_windows_version(int build, string path)
    {
        var r = await Rate("intel-vmd-laptop", p => p with { System = p.System with { Windows = p.System.Windows with { Build = build } } });
        var steps = WalkthroughBuilder.Build(r).Single(s => s.Id == "boot").Body.OfType<PictureGuide>().SelectMany(g => g.Steps);
        Assert.Contains(steps, st => st.Text.Contains(path, StringComparison.Ordinal));
    }

    [Fact]
    public async Task Install_alongside_needs_enough_free_space()
    {
        var roomy = WalkthroughBuilder.Build(await Rate("nvidia-desktop")).Single(s => s.Id == "install");
        var alongside = roomy.Options.Single(o => o.Id == "alongside");
        Assert.True(alongside.Possible);
        Assert.Contains("You have 412 GB free. Give Ubuntu about 100 GB.", alongside.Lines);

        var tight = WalkthroughBuilder.Build(await Rate("intel-vmd-laptop")).Single(s => s.Id == "install").Options.Single(o => o.Id == "alongside");
        Assert.False(tight.Possible);
        Assert.Contains("You only have 38 GB free. You need at least 50 GB.", tight.Lines);
    }

    [Fact]
    public async Task Replacing_windows_needs_a_confirmation_and_says_everything_is_erased()
    {
        var replace = WalkthroughBuilder.Build(await Rate("nvidia-desktop")).Single(s => s.Id == "install").Options.Single(o => o.Id == "replace");
        Assert.Contains("erased", replace.ConfirmText);
        Assert.Contains(replace.Steps, b => b is Callout { Kind: CalloutKind.Danger });
    }

    [Fact]
    public async Task Pop_os_alongside_windows_is_not_offered_as_easy()
    {
        var pop = WalkthroughBuilder.Build((await Rate("nvidia-desktop")).WithPath(PathKind.Advanced)).Single(s => s.Id == "install");
        var alongside = pop.Options.Single(o => o.Id == "alongside");
        Assert.False(alongside.Possible);
        Assert.Contains("can't set this up for you", Text(alongside.Steps));
    }

    [Fact]
    public async Task After_installing_shows_the_driver_steps_as_pictures_only_where_needed()
    {
        var ubuntu = WalkthroughBuilder.Build(await Rate("nvidia-desktop")).Single(s => s.Id == "after");
        Assert.Contains(ubuntu.Body, b => b is PictureGuide);
        Assert.Contains("Set up your graphics card", Text(ubuntu));
        var pop = WalkthroughBuilder.Build((await Rate("nvidia-desktop")).WithPath(PathKind.Advanced)).Single(s => s.Id == "after");
        Assert.DoesNotContain(pop.Body, b => b is PictureGuide); // the NVIDIA ISO has the driver
    }

    [Fact]
    public async Task Bitlocker_adds_the_recovery_key_item_and_tip()
    {
        var r = await Rate("nvidia-desktop", p => p with { System = p.System with { BitLocker = BitLockerState.On } });
        var step = WalkthroughBuilder.Build(r).Single(s => s.Id == "windows");
        Assert.Contains("aka.ms/myrecoverykey", Text(step));
        Assert.Contains("BitLocker recovery key", step.Tip);
    }

    [Theory]
    [InlineData("Professional", "BitLocker Drive Encryption")]
    [InlineData("Core", "Device encryption")]               // Windows Home
    public async Task Keeping_windows_with_encryption_on_turns_it_off_first(string edition, string where)
    {
        var r = await Rate("nvidia-desktop", p => p with { System = p.System with { BitLocker = BitLockerState.On, Windows = p.System.Windows with { Edition = edition } } });
        var alongside = WalkthroughBuilder.Build(r).Single(s => s.Id == "install").Options.Single(o => o.Id == "alongside");
        Assert.Contains(alongside.Steps, b => b is Callout { Title: "First, turn off drive encryption" });
        Assert.Contains(alongside.Steps.OfType<PictureGuide>().First().Steps, st => st.Text.Contains(where, StringComparison.Ordinal));
    }

    [Fact]
    public async Task Installer_steps_use_each_installers_own_button_words()
    {
        var ubuntu = Text(WalkthroughBuilder.Build(await Rate("nvidia-desktop")).Single(s => s.Id == "install"));
        Assert.Contains("click Restart now", ubuntu);
        Assert.Contains("Install Ubuntu alongside Windows Boot Manager", ubuntu);
        var mint = Text(WalkthroughBuilder.Build(await Rate("intel-laptop")).Single(s => s.Id == "install"));
        Assert.Contains("click Continue", mint);
        Assert.Contains("click Restart Now", mint);
        var pop = Text(WalkthroughBuilder.Build((await Rate("nvidia-desktop")).WithPath(PathKind.Advanced)).Single(s => s.Id == "install"));
        Assert.Contains("click Erase and Install", pop);
        Assert.Contains("click Restart Device", pop);
    }

    [Fact]
    public async Task Saved_report_includes_every_step()
    {
        var html = ReportExporter.Render(await Rate("nvidia-desktop"), ReportFormat.Html);
        Assert.Contains("Part 1 of 5: Get ready", html);
        Assert.Contains("How do you want to install Linux?", html);
        Assert.Contains("Part 5 of 5: On the USB stick", html);
        Assert.Contains("After installing", html);
        Assert.Contains("What could go wrong?", html);
    }
}

public class IllustrationTests
{
    private static readonly Lazy<CompatibilityData> Data = new(() => CompatibilityData.Load());

    private static async Task<CompatibilityReport> Rate(string sample, Func<HardwareProfile, HardwareProfile>? change = null)
    {
        var profile = await SampleProfiles.Open(sample).ScanAsync();
        if (change is not null) profile = change(profile);
        return await new RatingEngine(Data.Value).RateAsync(profile, new RatingOptions { UseOnlineLookups = false });
    }

    private static IEnumerable<PictureStep> Pictures(IEnumerable<Block> blocks) => blocks.SelectMany(b => b switch
    {
        PictureGuide g => g.Steps,
        Details d => Pictures(d.Content),
        _ => [],
    });

    /// <summary>Every picture any walkthrough can ask for has been drawn: every sample, path, install choice, encryption and Windows version.</summary>
    [Fact]
    public async Task Every_walkthrough_picture_exists()
    {
        var ids = new HashSet<string>();
        foreach (var sample in SampleProfiles.List())
            foreach (var bitLocker in new[] { BitLockerState.NotEncrypted, BitLockerState.On })
                foreach (var (build, edition) in new[] { (19045, "Professional"), (26100, "Core") })
                {
                    var r = await Rate(sample, p => p with { System = p.System with { BitLocker = bitLocker, Windows = p.System.Windows with { Build = build, Edition = edition } } });
                    foreach (var path in r.Recommendation.Paths)
                        foreach (var step in WalkthroughBuilder.Build(r.WithPath(path.Kind)))
                            foreach (var pic in Pictures(step.Body.Concat(step.Options.SelectMany(o => o.Steps))))
                                ids.Add(pic.PictureId!);
                }
        foreach (var maker in Data.Value.Makers.Makers)
            foreach (var key in new[] { maker.BootMenuKey, maker.BootMenuKeyLaptop, maker.BootMenuKeyDesktop }.OfType<string>())
                ids.Add(Illustrations.BootKeyId(key));

        Assert.All(ids, id => Assert.True(Illustrations.Exists(id), $"No picture for '{id}'"));
        Assert.All(ids, id => Assert.False(string.IsNullOrWhiteSpace(Illustrations.Alt(id)), $"'{id}' needs alt text"));
    }

    [Fact]
    public void Pictures_use_no_logos_and_have_titles()
    {
        foreach (var id in Illustrations.Ids)
        {
            var svg = Illustrations.Svg(id)!;
            Assert.Contains("<title>", svg);
            Assert.Contains("role=\"img\"", svg);
            Assert.DoesNotContain("<image", svg); // drawn shapes and text only: no embedded logos or photos
            Assert.Contains("prefers-color-scheme: dark", svg);
        }
    }

    [Fact]
    public async Task Report_embeds_pictures_with_captions()
    {
        var html = ReportExporter.Render(await Rate("nvidia-desktop"), ReportFormat.Html);
        Assert.Contains("<div class=\"pic\"><svg", html);
        Assert.Contains("<figcaption>", html);
        Assert.DoesNotContain("Picture coming soon", html);
    }
}
