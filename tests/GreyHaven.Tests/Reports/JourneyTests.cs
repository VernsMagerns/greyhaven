using GreyHaven.Compat;
using GreyHaven.Core.Compatibility;
using GreyHaven.Core.Hardware;
using GreyHaven.Core.Scanning;
using GreyHaven.Ratings;
using GreyHaven.Reports;

namespace GreyHaven.Tests.Reports;

/// <summary>The white-glove walkthrough (Part B): five phases, choices first, checks right away.</summary>
public class JourneyTests
{
    private static readonly Lazy<CompatibilityData> Data = new(() => CompatibilityData.Load());
    private const string RealPc = "ryzen-rtx2070s-desktop";

    private static async Task<CompatibilityReport> Rate(string sample, Func<HardwareProfile, HardwareProfile>? change = null)
    {
        var profile = await SampleProfiles.Open(sample).ScanAsync();
        if (change is not null) profile = change(profile);
        return await new RatingEngine(Data.Value).RateAsync(profile, new RatingOptions { UseOnlineLookups = false });
    }

    private static string Text(IEnumerable<Block> blocks) => MarkdownReportWriter.Write(new ReportDocument("t", blocks.ToList()));
    private static string Text(JourneyStep s) => Text(s.Body.Concat(s.Tasks.SelectMany(t => t.How)));

    [Fact]
    public async Task Real_pc_easy_path_has_no_startup_settings_step()
    {
        var steps = JourneyBuilder.Build((await Rate(RealPc)).WithInstall(InstallChoice.KeepWindows));
        Assert.Equal(["choose", "backup", "windows", "usb", "guide", "restart", "try", "install", "after"], steps.Select(s => s.Id));
        Assert.Equal(
            [JourneyPhase.GetReady, JourneyPhase.GetReady, JourneyPhase.GetReady, JourneyPhase.MakeUsb, JourneyPhase.TakeGuide, JourneyPhase.Restart, JourneyPhase.OnTheStick, JourneyPhase.OnTheStick, JourneyPhase.OnTheStick],
            steps.Select(s => s.Phase));
    }

    [Fact]
    public async Task Advanced_path_adds_the_makers_secure_boot_instruction()
    {
        var steps = JourneyBuilder.Build((await Rate(RealPc)).WithPath(PathKind.Advanced));
        var settings = steps.Single(s => s.Id == "startup-settings");
        var task = Assert.Single(settings.Tasks);
        Assert.Equal("secure-boot", task.Id);
        Assert.True(task.NeedsStartupSettings);
        var text = Text(settings);
        Assert.Contains("Delete", text);                             // MSI's setup key
        Assert.Contains(task.How.OfType<PictureGuide>().Single().Steps, st => st.Text == "Go to Settings > Security > Secure Boot.");
        Assert.Contains("msi.com", text);                            // the maker's guide for more detail
    }

    [Fact]
    public async Task Keep_windows_is_suggested_and_checked_right_away()
    {
        var r = await Rate(RealPc);
        Assert.Equal(InstallChoice.KeepWindows, JourneyBuilder.Suggested(r));
        var keep = JourneyBuilder.Build(r)[0].Choices.Single(c => c.Choice == InstallChoice.KeepWindows);
        Assert.True(keep.Recommended);
        Assert.Contains(keep.Checks, c => c.State == CheckState.Ok && c.Text.StartsWith("Enough room: 481 GB free"));
        Assert.Contains(keep.Checks, c => c.State == CheckState.Ok && c.Text.Contains("isn't encrypted"));
    }

    [Fact]
    public async Task Too_little_space_blocks_keeping_windows()
    {
        var r = await Rate(RealPc, p => p with { System = p.System with { SystemDriveFreeBytes = 30L * 1073741824 } });
        var keep = JourneyBuilder.Build(r)[0].Choices.Single(c => c.Choice == InstallChoice.KeepWindows);
        Assert.False(keep.Possible);
        Assert.Contains(keep.Checks, c => c.State == CheckState.Blocker && c.Text.Contains("only have 30 GB free"));
        Assert.Null(JourneyBuilder.Suggested(r));
    }

    [Fact]
    public async Task Pop_os_cant_keep_windows_by_itself()
    {
        var r = (await Rate(RealPc)).WithPath(PathKind.Advanced);
        var keep = JourneyBuilder.Build(r)[0].Choices.Single(c => c.Choice == InstallChoice.KeepWindows);
        Assert.False(keep.Possible);
        Assert.Contains("pick Ubuntu", keep.Checks.Single(c => c.State == CheckState.Blocker).Text);
    }

    [Fact]
    public async Task Backup_is_required_only_when_replacing_windows()
    {
        var r = await Rate(RealPc);
        Assert.False(JourneyBuilder.Build(r.WithInstall(InstallChoice.KeepWindows)).Single(s => s.Id == "backup").ChecklistRequired);
        Assert.True(JourneyBuilder.Build(r.WithInstall(InstallChoice.ReplaceWindows)).Single(s => s.Id == "backup").ChecklistRequired);
    }

    [Fact]
    public async Task Windows_tasks_follow_the_pc_and_the_install_choice()
    {
        // The real PC: Fast Startup already off, not encrypted - nothing to do.
        var real = JourneyBuilder.Build(await Rate(RealPc)).Single(s => s.Id == "windows");
        Assert.All(real.Tasks, t => Assert.True(t.Done));
        Assert.Contains("nothing to change", Text(real));

        // Encrypted, keeping Windows: GreyHaven offers to turn encryption off (one click, can undo).
        var encrypted = await Rate(RealPc, p => p with { System = p.System with { BitLocker = BitLockerState.On, FastStartup = FastStartupState.Enabled } });
        var keep = JourneyBuilder.Build(encrypted.WithInstall(InstallChoice.KeepWindows)).Single(s => s.Id == "windows").Tasks;
        Assert.Equal(["fast-startup", "encryption-off"], keep.Select(t => t.Id));
        Assert.All(keep, t => Assert.Equal(PrepAction.OneClick, t.Action));
        Assert.All(keep, t => Assert.False(string.IsNullOrEmpty(t.Confirm)));
        Assert.All(keep, t => Assert.False(string.IsNullOrEmpty(t.Undo)));

        // Encrypted, replacing Windows: just save the recovery key.
        var replace = JourneyBuilder.Build(encrypted.WithInstall(InstallChoice.ReplaceWindows)).Single(s => s.Id == "windows").Tasks;
        Assert.Equal(["fast-startup", "recovery-key"], replace.Select(t => t.Id));
    }

    [Fact]
    public async Task Bios_update_is_an_optional_folded_extra_not_a_task()
    {
        var windows = JourneyBuilder.Build(await Rate(RealPc)).Single(s => s.Id == "windows");
        Assert.DoesNotContain(windows.Tasks, t => t.Title.Contains("BIOS"));
        Assert.Contains(windows.Body, b => b is Details { Summary: "Nice to have: check for a BIOS update" });
    }

    [Fact]
    public async Task Usb_step_says_how_the_download_is_checked_per_distro()
    {
        var r = await Rate(RealPc);
        Assert.Contains("Ubuntu's digital signature", JourneyBuilder.Build(r).Single(s => s.Id == "usb").Usb!.Verification);
        Assert.Contains("against the one System76 publishes", JourneyBuilder.Build(r.WithPath(PathKind.Advanced)).Single(s => s.Id == "usb").Usb!.Verification);
    }

    [Fact]
    public async Task Restart_checks_fit_the_pc_and_fall_back_to_the_makers_key()
    {
        var restart = JourneyBuilder.Build(await Rate(RealPc)).Single(s => s.Id == "restart").Restart!;
        Assert.DoesNotContain(restart.FinalChecks, c => c.Contains("laptop"));
        Assert.Contains(restart.Fallback, b => b is KeyFact { Value: "F11" });

        var laptop = JourneyBuilder.Build(await Rate("intel-laptop")).Single(s => s.Id == "restart").Restart!;
        Assert.Contains(laptop.FinalChecks, c => c.Contains("laptop is plugged in"));
    }

    [Fact]
    public async Task Phone_guide_link_holds_only_choices_and_waits_for_the_public_repo()
    {
        var r = (await Rate(RealPc)).WithInstall(InstallChoice.KeepWindows);
        Assert.Null(JourneyBuilder.PhoneGuideUrl(r)); // the repository is still private
        Assert.Equal("v=1&d=ubuntu&e=desktop&i=keep&w=11&f=desktop&k=F11&x=gpu", JourneyBuilder.PhoneGuideFragment(r));
    }

    [Fact]
    public async Task Install_step_shows_only_the_chosen_way()
    {
        var r = await Rate(RealPc);
        var keep = Text(JourneyBuilder.Build(r.WithInstall(InstallChoice.KeepWindows)).Single(s => s.Id == "install"));
        var replace = Text(JourneyBuilder.Build(r.WithInstall(InstallChoice.ReplaceWindows)).Single(s => s.Id == "install"));
        Assert.Contains("alongside Windows Boot Manager", keep);
        Assert.DoesNotContain("Erase disk", keep);
        Assert.Contains("Erase disk", replace);
        Assert.DoesNotContain("again, like in step 6", keep); // you're already on the stick
        Assert.DoesNotContain("opens by itself. The installer opens by itself", keep);
    }

    [Fact]
    public async Task Phone_guide_has_the_three_steps_on_the_stick_and_enlargeable_pictures()
    {
        var r = (await Rate(RealPc)).WithInstall(InstallChoice.KeepWindows);
        var html = HtmlReportWriter.Write(PhoneGuideBuilder.Build(r));
        Assert.Contains("<h1>Your steps to Ubuntu</h1>", html);
        Assert.Contains("<h2>1. Try Linux</h2>", html);
        Assert.Contains("<h2>2. Install Ubuntu</h2>", html);
        Assert.Contains("<h2>3. After installing</h2>", html);
        Assert.Contains("You chose: Keep Windows too.", html);
        Assert.Contains("aria-label=\"Enlarge picture: ", html);
        Assert.Contains(">Close picture</a>", html);
        Assert.DoesNotContain("<script", html);
        Assert.DoesNotContain("pci:", html); // no part IDs in the take-along guide
    }

    [Fact]
    public async Task Journey_exports_as_json_with_block_types()
    {
        var json = JourneyJson.Render((await Rate(RealPc)).WithInstall(InstallChoice.KeepWindows));
        Assert.Contains("\"suggestedInstall\": \"keepWindows\"", json);
        Assert.Contains("\"type\": \"pictures\"", json);
        Assert.Contains("\"phase\": \"onTheStick\"", json);
    }

    [Fact]
    public void Qr_code_is_an_svg()
    {
        var svg = QrCode.Svg("https://example.org/guide/#v=1&d=ubuntu");
        Assert.StartsWith("<svg", svg);
        Assert.Contains("fill=\"#000000\"", svg);
    }

    [Theory]
    [MemberData(nameof(GreyHaven.Tests.Scanning.SampleProfileTests.SampleNames), MemberType = typeof(GreyHaven.Tests.Scanning.SampleProfileTests))]
    public async Task Every_picture_the_journey_uses_is_drawn(string sample)
    {
        var r = await Rate(sample);
        foreach (var path in r.Recommendation.Paths)
            foreach (InstallChoice? choice in new InstallChoice?[] { null, InstallChoice.KeepWindows, InstallChoice.ReplaceWindows })
            {
                var steps = JourneyBuilder.Build(r.WithPath(path.Kind).WithInstall(choice));
                IEnumerable<Block> All(IEnumerable<Block> bs) => bs.SelectMany(b => b is Details d ? All(d.Content).Prepend(b) : [b]);
                var blocks = All(steps.SelectMany(s => s.Body
                    .Concat(s.Tasks.SelectMany(t => t.How))
                    .Concat(s.Restart is { } rs ? rs.AfterRestart.Concat(rs.Fallback) : [])));
                var missing = blocks.OfType<PictureGuide>().SelectMany(g => g.Steps)
                    .Where(st => st.PictureId is not null && !Illustrations.Exists(st.PictureId))
                    .Select(st => st.PictureId).Distinct();
                Assert.Empty(missing);
            }
    }
}
