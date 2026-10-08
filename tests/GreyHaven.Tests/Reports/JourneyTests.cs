using GreyHaven.Compat;
using GreyHaven.Core.Compatibility;
using GreyHaven.Core.Hardware;
using GreyHaven.Core.Scanning;
using GreyHaven.Ratings;
using GreyHaven.Reports;
using PhoneGuideSite = GreyHaven.Ratings.PhoneGuideSite;

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
    public async Task Phone_guide_link_is_the_hosted_page_plus_choices_after_the_hash()
    {
        var r = (await Rate(RealPc)).WithInstall(InstallChoice.KeepWindows);
        Assert.Equal("https://greyhaven-guide.pages.dev/ubuntu-desktop-keep#v=1&m=MSI&k=F11&f=desktop&g=100&x=nvidia-gpu", JourneyBuilder.PhoneGuideUrl(r));
        Assert.Null(JourneyBuilder.PhoneGuideUrl(r.WithInstall(null)));                                          // nothing chosen yet
        Assert.Null(JourneyBuilder.PhoneGuideUrl(r.WithPath(PathKind.Advanced)));                                 // Pop!_OS can't keep Windows
        // Pop!_OS's NVIDIA download already has the driver, so there's no extra step (no "x").
        Assert.Equal("https://greyhaven-guide.pages.dev/pop-os-nvidia-replace#v=1&m=MSI&k=F11&s=Delete&sb=Settings%20%3E%20Security%20%3E%20Secure%20Boot&f=desktop",
            JourneyBuilder.PhoneGuideUrl(r.WithPath(PathKind.Advanced).WithInstall(InstallChoice.ReplaceWindows)));
    }

    public static TheoryData<string> Samples => GreyHaven.Tests.Scanning.SampleProfileTests.SampleNames;

    /// <summary>The QR code must always open the hosted guide: never localhost or a home-network address.</summary>
    [Theory]
    [MemberData(nameof(Samples))]
    public async Task Qr_code_always_points_to_the_hosted_guide(string sample)
    {
        var r = await Rate(sample);
        var site = new Uri(GreyHaven.Core.GreyHavenInfo.PhoneGuideSite);
        Assert.True(IsPublicHttps(site), $"PhoneGuideSite must be a public https address: {site}");
        foreach (var path in r.Recommendation.Paths)
            foreach (var choice in new[] { InstallChoice.KeepWindows, InstallChoice.ReplaceWindows })
            {
                var report = r.WithPath(path.Kind).WithInstall(choice);
                if (JourneyBuilder.PhoneGuideUrl(report) is not { } url) continue;
                var uri = new Uri(url);
                Assert.True(IsPublicHttps(uri), url);
                Assert.Equal(site.Host, uri.Host);
                Assert.Equal(url, JourneyBuilder.Build(report).Single(s => s.Id == "guide").Guide!.PhoneUrl);
            }
    }

    private static bool IsPublicHttps(Uri u)
    {
        if (u.Scheme != "https" || u.IsLoopback || u.Host is "localhost" || u.Host.EndsWith(".local", StringComparison.OrdinalIgnoreCase)) return false;
        if (!System.Net.IPAddress.TryParse(u.Host, out var ip)) return true;
        var b = ip.GetAddressBytes();
        return !(b[0] == 10 || b[0] == 127 || (b[0] == 172 && b[1] is >= 16 and <= 31) || (b[0] == 192 && b[1] == 168) || (b[0] == 169 && b[1] == 254) || ip.IsIPv6LinkLocal || ip.IsIPv6SiteLocal);
    }

    [Theory]
    [MemberData(nameof(Samples))]
    public async Task Fragment_has_only_known_fields_and_nothing_identifying(string sample)
    {
        var r = await Rate(sample);
        foreach (var path in r.Recommendation.Paths)
        {
            var fragment = JourneyBuilder.PhoneGuideFragment(r.WithPath(path.Kind).WithInstall(InstallChoice.ReplaceWindows));
            foreach (var kv in fragment.Split('&'))
            {
                var (k, v) = (kv[..kv.IndexOf('=')], Uri.UnescapeDataString(kv[(kv.IndexOf('=') + 1)..]));
                Assert.True(GuideFragment.IsValid(k, v), $"{sample}: {kv}");
            }
            Assert.DoesNotContain("pci", fragment);
            Assert.DoesNotContain("usb", fragment, StringComparison.OrdinalIgnoreCase);
            if (r.Profile.System.SystemModel is { Length: > 2 } model) Assert.DoesNotContain(Uri.EscapeDataString(model), fragment);
            Assert.True(fragment.Length < 300);
        }
    }

    [Fact]
    public void Fragment_rejects_anything_outside_the_known_fields()
    {
        Assert.False(GuideFragment.IsValid("apps", "Steam"));
        Assert.False(GuideFragment.IsValid("k", "<script>"));
        Assert.False(GuideFragment.IsValid("x", "pci:10de:1e84"));
        Assert.False(GuideFragment.IsValid("f", "tablet"));
        Assert.True(GuideFragment.IsValid("sb", "Settings > Security > Secure Boot"));
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
    public void Public_site_is_general_safe_and_locked_down()
    {
        var dir = Path.Combine(Path.GetTempPath(), $"gh-site-{Guid.NewGuid():N}");
        try
        {
            var pages = PhoneGuideSite.Pages(Data.Value);
            Assert.Equal(["mint-cinnamon-keep.html", "mint-cinnamon-replace.html", "ubuntu-desktop-keep.html", "ubuntu-desktop-replace.html", "pop-os-intel-replace.html", "pop-os-nvidia-replace.html"],
                pages.Select(p => p.FileName));
            PhoneGuideSiteWriter.Write(dir, pages);
            var script = GuideFragment.Script();
            foreach (var p in pages)
            {
                var html = File.ReadAllText(Path.Combine(dir, p.FileName));
                Assert.Contains(HtmlReportWriter.ContentSecurityPolicy(script), html);
                Assert.Contains("<script>" + script + "</script>", html);
                Assert.Single(System.Text.RegularExpressions.Regex.Matches(html, "<script"));
                Assert.DoesNotContain("pci:", html);
                Assert.DoesNotContain("MSI", html);              // no maker names written into the page
                Assert.DoesNotContain("Install your apps</h3>\n<ul", html); // never an app list
                Assert.Contains("data-fill=\"k\"", html);
            }
            Assert.DoesNotContain("innerHTML", script);
            Assert.Contains("textContent", script);
            Assert.Contains("Referrer-Policy: no-referrer", File.ReadAllText(Path.Combine(dir, "_headers")));
            // Pop!_OS's NVIDIA download already has the driver; its Intel/AMD download doesn't.
            Assert.DoesNotContain(pages.Single(p => p.FileName == "pop-os-nvidia-replace.html").Fixes, f => f.Id == "nvidia-gpu");
            Assert.Contains(pages.Single(p => p.FileName == "pop-os-intel-replace.html").Fixes, f => f.Id == "nvidia-gpu");
            Assert.Contains(pages[0].LaptopChecks, c => c.Title == "Touchpad");
            Assert.DoesNotContain(pages[0].Checks, c => c.Title == "Touchpad");
            Assert.DoesNotContain("touchpad", pages[0].DesktopChecks.Single().Test, StringComparison.OrdinalIgnoreCase);
        }
        finally { if (Directory.Exists(dir)) Directory.Delete(dir, true); }
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
