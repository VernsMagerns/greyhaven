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
    private static string Text(JourneyStep s) => Text(s.Body.Concat(s.Tasks.SelectMany(t => t.How)).Concat(s.Restart is { } r ? r.AfterRestart.Concat(r.Fallback).Concat(r.Manual) : []));
    private static JourneyStep Step(CompatibilityReport r, string id) => JourneyBuilder.Build(r).Single(s => s.Id == id);

    [Fact]
    public async Task Windows_tasks_are_for_this_pc_and_path()
    {
        var easy = Step(await Rate("nvidia-desktop"), "windows");
        Assert.Contains(easy.Tasks, t => t.Id == "fast-startup" && !t.Done);   // the sample has Fast Startup on
        Assert.Contains("https://www.msi.com/support", Text(easy));             // BIOS updates: the motherboard maker's page
        Assert.DoesNotContain(easy.Tasks, t => t.Id is "recovery-key" or "encryption-off"); // not encrypted
        Assert.DoesNotContain(JourneyBuilder.Build(await Rate("nvidia-desktop")), s => s.Id == "startup-settings"); // Ubuntu keeps Secure Boot on

        var advanced = Step((await Rate("nvidia-desktop")).WithPath(PathKind.Advanced), "startup-settings");
        Assert.Contains("https://www.msi.com/faq/faq-11370", Text(advanced));

        var vmd = Step(await Rate("intel-vmd-laptop"), "startup-settings");
        Assert.Contains(vmd.Tasks, t => t.Title == "Change a storage setting");
    }

    [Fact]
    public async Task Restart_falls_back_to_the_makers_key_and_has_no_key_when_unknown()
    {
        var msi = Step(await Rate("nvidia-desktop"), "restart").Restart!;
        Assert.Contains(msi.Fallback, b => b is KeyFact { Value: "F11" });
        var unknown = Step(await Rate("intel-vmd-laptop"), "restart").Restart!;
        Assert.DoesNotContain(unknown.Fallback, b => b is KeyFact);
        Assert.Contains(unknown.AfterRestart.OfType<PictureGuide>().Single().Steps, st => st.Text.Contains("Use a device"));
    }

    [Theory]
    [InlineData(19045, "Settings > Update & Security > Recovery")] // Windows 10 22H2
    [InlineData(26100, "Settings > System > Recovery")]            // Windows 11 24H2
    public async Task Manual_restart_path_matches_the_windows_version(int build, string path)
    {
        var r = await Rate("intel-vmd-laptop", p => p with { System = p.System with { Windows = p.System.Windows with { Build = build } } });
        var steps = Step(r, "restart").Restart!.Manual.OfType<PictureGuide>().SelectMany(g => g.Steps);
        Assert.Contains(steps, st => st.Text.Contains(path, StringComparison.Ordinal));
    }

    [Fact]
    public async Task After_installing_shows_the_driver_steps_as_pictures_only_where_needed()
    {
        var ubuntu = Step(await Rate("nvidia-desktop"), "after");
        Assert.Contains(ubuntu.Body, b => b is PictureGuide g && g.Steps.Any(st => st.PictureId == "ubuntu-gpu-1"));
        Assert.Contains("Set up your graphics card", Text(ubuntu));
        var pop = Step((await Rate("nvidia-desktop")).WithPath(PathKind.Advanced), "after");
        Assert.DoesNotContain("Set up your graphics card", Text(pop)); // the NVIDIA ISO has the driver
    }

    [Fact]
    public async Task Bitlocker_adds_the_tip_about_the_recovery_key()
    {
        var r = await Rate("nvidia-desktop", p => p with { System = p.System with { BitLocker = BitLockerState.On } });
        Assert.Contains("BitLocker recovery key", Step(r.WithInstall(InstallChoice.ReplaceWindows), "windows").Tip);
        Assert.Contains("aka.ms/myrecoverykey", Text(Step(r.WithInstall(InstallChoice.ReplaceWindows), "windows")));
    }

    [Theory]
    [InlineData("Professional", "BitLocker Drive Encryption")]
    [InlineData("Core", "Device encryption")]               // Windows Home
    public async Task Keeping_windows_with_encryption_on_shows_how_to_turn_it_off(string edition, string where)
    {
        var r = await Rate("nvidia-desktop", p => p with { System = p.System with { BitLocker = BitLockerState.On, Windows = p.System.Windows with { Edition = edition } } });
        var task = Step(r.WithInstall(InstallChoice.KeepWindows), "windows").Tasks.Single(t => t.Id == "encryption-off");
        var pictures = task.How.OfType<Details>().Single().Content.OfType<PictureGuide>().Single().Steps;
        Assert.Contains(pictures, st => st.Text.Contains(where, StringComparison.Ordinal));
    }

    [Fact]
    public async Task Installer_steps_use_each_installers_own_button_words()
    {
        var ubuntu = Text(Step((await Rate("nvidia-desktop")).WithInstall(InstallChoice.KeepWindows), "install"));
        Assert.Contains("click Restart now", ubuntu);
        Assert.Contains("Install Ubuntu alongside Windows Boot Manager", ubuntu);
        var mint = Text(Step((await Rate("intel-laptop")).WithInstall(InstallChoice.KeepWindows), "install"));
        Assert.Contains("click Continue", mint);
        Assert.Contains("click Restart Now", mint);
        var pop = Text(Step((await Rate("nvidia-desktop")).WithPath(PathKind.Advanced).WithInstall(InstallChoice.ReplaceWindows), "install"));
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
                        foreach (var install in new InstallChoice?[] { null, InstallChoice.KeepWindows, InstallChoice.ReplaceWindows })
                            foreach (var step in JourneyBuilder.Build(r.WithPath(path.Kind).WithInstall(install)))
                            {
                                IEnumerable<Block> all = step.Body.Concat(step.Tasks.SelectMany(t => t.How)).Concat(step.Restart is { } rs ? rs.AfterRestart.Concat(rs.Fallback).Concat(rs.Manual) : []);
                                all = all.SelectMany(b => b is Details d ? d.Content.Prepend(b) : [b]);
                                foreach (var pic in Pictures(all)) ids.Add(pic.PictureId!);
                            }
                }
        foreach (var maker in Data.Value.Makers.Makers)
            foreach (var key in new[] { maker.BootMenuKey, maker.BootMenuKeyLaptop, maker.BootMenuKeyDesktop, maker.SetupKey }.OfType<string>())
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
