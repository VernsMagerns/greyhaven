using System.Text.Json;
using GreyHaven.Compat;
using GreyHaven.Compat.Data;
using GreyHaven.Core.Compatibility;
using GreyHaven.Core.Hardware;
using GreyHaven.Core.Scanning;
using GreyHaven.Presentation.Theming;
using GreyHaven.Ratings;
using GreyHaven.Reports;

namespace GreyHaven.Tests.Reports;

public class ReportExportTests
{
    private static readonly Lazy<CompatibilityData> Data = new(() => CompatibilityData.Load());

    private static async Task<CompatibilityReport> Rate(string sample, Func<HardwareProfile, HardwareProfile>? change = null)
    {
        var profile = await SampleProfiles.Open(sample).ScanAsync();
        if (change is not null) profile = change(profile);
        return await new RatingEngine(Data.Value).RateAsync(profile, new RatingOptions { UseOnlineLookups = false });
    }

    public static TheoryData<string, ReportFormat> SamplesAndFormats()
    {
        var data = new TheoryData<string, ReportFormat>();
        foreach (var s in SampleProfiles.List())
            foreach (var f in Enum.GetValues<ReportFormat>())
                data.Add(s, f);
        return data;
    }

    [Theory]
    [MemberData(nameof(SamplesAndFormats))]
    public async Task Every_sample_exports_in_every_format_without_markers_or_placeholders(string sample, ReportFormat format)
    {
        var text = ReportExporter.Render(await Rate(sample), format);
        Assert.False(string.IsNullOrWhiteSpace(text));
        Assert.DoesNotContain("OWNER/", text);
        if (format != ReportFormat.Json)
        {
            Assert.DoesNotContain("!!", text);
            Assert.Contains("Your steps to Linux", text);
            Assert.Contains("s parts on ", text); // "Your PC's parts on" (the apostrophe is encoded in HTML)
        }
    }

    [Theory]
    [InlineData(ReportFormat.Markdown)]
    [InlineData(ReportFormat.Html)]
    public async Task Nvidia_desktop_report_matches_the_cli_fixes(ReportFormat format)
    {
        var text = ReportExporter.Render(await Rate("nvidia-desktop"), format);

        // Same-named parts are told apart.
        Assert.Contains("High Definition Audio Controller (AMD)", text);
        Assert.Contains("High Definition Audio Controller (NVIDIA)", text);
        Assert.Contains("(1 of 2)", text);
        Assert.Contains("(2 of 2)", text);
        // Details are for Ubuntu (the default Easy path) and cite only Ubuntu's sources.
        Assert.Contains("Ubuntu Desktop docs - Install NVIDIA drivers", text);
        Assert.DoesNotContain("Linux Mint 22.3 release notes", text);
        Assert.DoesNotContain("System76 - Install the System76 driver", text);
        // Motherboard maker's Secure Boot guide, and the Windows 10 note with Microsoft's page.
        Assert.Contains("https://www.msi.com/faq/faq-11370", text);
        Assert.Contains("https://www.microsoft.com/en-us/windows/end-of-support", text);
    }

    [Fact]
    public async Task Choosing_another_path_changes_the_details_and_live_usb_guide()
    {
        var r = (await Rate("nvidia-desktop")).WithPath(PathKind.Advanced);
        var md = ReportExporter.Render(r, ReportFormat.Markdown);
        Assert.Contains("Your PC's parts on Pop!\\_OS", md);
        Assert.Contains("Pop!\\_OS (NVIDIA ISO)", md);
        Assert.Contains("turn Secure Boot off first", md);
        Assert.Contains("System76 - Pop!\\_OS download information", md);
    }

    [Fact]
    public async Task Show_more_is_a_red_link_that_uses_windows_colours_in_high_contrast()
    {
        var html = ReportExporter.Render(await Rate("nvidia-desktop"), ReportFormat.Html);
        Assert.Contains("summary{cursor:pointer;color:var(--accent)", html);
        Assert.Contains("summary:focus-visible", html);
        Assert.Contains("summary{color:LinkText}", html); // inside the forced-colors block
        Assert.Contains("<summary>Why switch now?</summary>", ReportExporter.Render(await Rate("broadcom-legacy-laptop"), ReportFormat.Html)); // Windows 10
    }

    [Fact]
    public async Task Each_linux_choice_has_sourced_pros_and_cons_and_other_versions_are_mentioned()
    {
        var r = await Rate("nvidia-desktop");
        Assert.All(r.Distros, d =>
        {
            Assert.NotNull(d.About);
            Assert.NotEmpty(d.About!.Pros);
            Assert.NotEmpty(d.About.Cons);
            Assert.All(d.About.Sources, s => Assert.StartsWith("https://", s.Url));
        });
        var md = ReportExporter.Render(r, ReportFormat.Markdown);
        Assert.Contains("About Ubuntu: pros and cons", md);
        Assert.Contains("There are many more versions of Linux", md);
        Assert.Contains("https://distrowatch.com/", md);
    }

    [Fact]
    public async Task Chipset_parts_are_one_line_and_not_counted()
    {
        var md = ReportExporter.Render(await Rate("nvidia-desktop"), ReportFormat.Markdown);
        Assert.Contains("System chipset: 2 parts, all fine", md);
        Assert.Contains("14 parts work right away. 1 part needs one extra step", md);
    }

    [Fact]
    public async Task Arm_pcs_are_told_to_download_the_arm64_version()
    {
        var md = ReportExporter.Render(await Rate("snapdragon-arm-laptop"), ReportFormat.Markdown);
        Assert.Contains("This PC has an ARM processor. Download Ubuntu's ARM64 version", md);
    }

    [Fact]
    public async Task Html_is_self_contained_and_escapes_device_names()
    {
        var r = await Rate("nvidia-desktop", p => p with
        {
            Components = [.. p.Components.Select(c => c.Category == ComponentCategory.Webcam ? c with { Name = "<script>alert(1)</script> & \"cam\"" } : c)],
        });
        var html = ReportExporter.Render(r, ReportFormat.Html);

        Assert.StartsWith("<!DOCTYPE html>", html);
        Assert.Contains("<html lang=\"en\">", html);
        Assert.DoesNotContain("<script", html);
        Assert.Contains("&lt;script&gt;alert(1)&lt;/script&gt; &amp; &quot;cam&quot;", html);
        Assert.DoesNotContain("<link", html);   // no external stylesheets
        Assert.DoesNotContain("src=", html);    // no external images or scripts
        Assert.Contains("prefers-color-scheme: dark", html);
        Assert.Contains("<caption>", html);
        Assert.Contains("type=\"checkbox\"", html);
        // Rating symbols are hidden from screen readers; the words carry the meaning.
        Assert.Contains("<span aria-hidden=\"true\">✓</span> Works right away", html);
    }

    [Fact]
    public async Task Markdown_escapes_device_names()
    {
        var r = await Rate("nvidia-desktop", p => p with
        {
            Components = [.. p.Components.Select(c => c.Category == ComponentCategory.Webcam ? c with { Name = "Cam | *bold* [x](y)" } : c)],
        });
        var md = ReportExporter.Render(r, ReportFormat.Markdown);
        Assert.Contains(@"Cam \| \*bold\* \[x\](y)", md);
    }

    [Fact]
    public void Html_colours_are_the_apps_tested_palette()
    {
        var c = HtmlReportWriter.Colours;
        void Same(string key, Rgb light, Rgb dark)
        {
            Assert.Equal(light.ToString(), c[key].Light);
            Assert.Equal(dark.ToString(), c[key].Dark);
        }
        Palette L = Palette.Light, D = Palette.Dark;
        Same("page", L.Card, D.Card);
        Same("text", L.Text, D.Text);
        Same("text-secondary", L.TextSecondary, D.TextSecondary);
        Same("accent", L.Accent, D.Accent);
        Same("works", L.Works, D.Works);
        Same("extra", L.ExtraStep, D.ExtraStep);
        Same("partial", L.Partial, D.Partial);
        Same("unsupported", L.Unsupported, D.Unsupported);
        Same("unknown", L.Unknown, D.Unknown);
        Same("notice-info", L.NoticeInfo, D.NoticeInfo);
        Same("notice-warning", L.NoticeWarning, D.NoticeWarning);
        Same("command", L.CommandBackground, D.CommandBackground);
    }

    [Fact]
    public async Task Json_export_has_an_envelope_and_the_full_report()
    {
        var json = ReportExporter.Render((await Rate("nvidia-desktop")).WithPath(PathKind.Advanced), ReportFormat.Json);
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;
        Assert.Equal("greyhaven-report", root.GetProperty("format").GetString());
        Assert.Equal(ReportExporter.JsonFormatVersion, root.GetProperty("formatVersion").GetInt32());
        var report = root.GetProperty("report");
        Assert.Equal("advanced", report.GetProperty("selectedPath").GetString());
        Assert.Equal(3, report.GetProperty("liveUsbGuides").GetArrayLength());
        Assert.Contains(report.GetProperty("components").EnumerateArray(), c => c.GetProperty("displayName").GetString() == "High Definition Audio Controller (NVIDIA)");
    }

    [Theory]
    [InlineData("report.md", ReportFormat.Markdown)]
    [InlineData("REPORT.HTML", ReportFormat.Html)]
    [InlineData("r.htm", ReportFormat.Html)]
    [InlineData("r.json", ReportFormat.Json)]
    public void Format_follows_the_file_name(string path, ReportFormat expected) =>
        Assert.Equal(expected, ReportExporter.FormatFromPath(path));

    [Fact]
    public void Unknown_extension_has_no_format() => Assert.Null(ReportExporter.FormatFromPath("report.txt"));

    [Fact]
    public async Task Saving_writes_utf8_without_a_byte_order_mark()
    {
        var path = Path.Combine(Path.GetTempPath(), $"gh-{Guid.NewGuid():N}.html");
        try
        {
            await ReportExporter.SaveAsync(await Rate("intel-laptop"), path);
            var bytes = await File.ReadAllBytesAsync(path);
            Assert.NotEqual(0xEF, bytes[0]);
            Assert.Contains("<!DOCTYPE html>", System.Text.Encoding.UTF8.GetString(bytes));
        }
        finally { File.Delete(path); }
    }
}

public class LiveUsbGuideTests
{
    private static readonly Lazy<CompatibilityData> Data = new(() => CompatibilityData.Load());

    private static async Task<CompatibilityReport> Rate(string sample, Func<HardwareProfile, HardwareProfile>? change = null)
    {
        var profile = await SampleProfiles.Open(sample).ScanAsync();
        if (change is not null) profile = change(profile);
        return await new RatingEngine(Data.Value).RateAsync(profile, new RatingOptions { UseOnlineLookups = false });
    }

    [Fact]
    public async Task Checks_follow_the_parts_this_pc_has()
    {
        var g = (await Rate("nvidia-desktop")).SelectedLiveUsbGuide!;
        var titles = g.Checks.Select(c => c.Title).ToList();
        Assert.Contains("Wi-Fi", titles);
        Assert.Contains("Wired network", titles);
        Assert.Contains("Camera", titles);
        Assert.Contains("Sleep and wake", titles);   // from caveats.yaml
        Assert.DoesNotContain("Touchpad", titles);    // a desktop without one
        Assert.DoesNotContain("Battery life", titles);
    }

    [Fact]
    public async Task Parts_needing_an_extra_step_are_expected_not_to_fully_work_live()
    {
        var r = await Rate("nvidia-desktop");
        var ubuntu = r.LiveUsbGuides.Single(g => g.DistroId == "ubuntu");
        var pop = r.LiveUsbGuides.Single(g => g.DistroId == "pop-os");
        Assert.Contains(ubuntu.ExpectedInLiveSession, s => s.Contains("NVIDIA GeForce RTX 3070"));
        Assert.Empty(pop.ExpectedInLiveSession); // the NVIDIA ISO has the driver
        Assert.Equal("NVIDIA ISO", pop.EditionName);
    }

    [Fact]
    public async Task Before_you_start_covers_secure_boot_and_bitlocker()
    {
        var r = await Rate("nvidia-desktop", p => p with { System = p.System with { BitLocker = BitLockerState.On } });
        var pop = r.LiveUsbGuides.Single(g => g.DistroId == "pop-os");
        var ubuntu = r.LiveUsbGuides.Single(g => g.DistroId == "ubuntu");
        Assert.Contains(pop.BeforeYouStart, s => s.Contains("Secure Boot"));
        Assert.DoesNotContain(ubuntu.BeforeYouStart, s => s.Contains("Secure Boot is on")); // Ubuntu supports Secure Boot
        Assert.Contains(ubuntu.BeforeYouStart, s => s.Contains("aka.ms/myrecoverykey"));
    }

    [Fact]
    public async Task Hw_probe_commands_are_per_distro()
    {
        var r = await Rate("intel-laptop");
        Assert.Equal("sudo add-apt-repository universe", r.LiveUsbGuides.Single(g => g.DistroId == "ubuntu").ShareCommands[0]);
        Assert.Equal("sudo apt update", r.LiveUsbGuides.Single(g => g.DistroId == "mint").ShareCommands[0]);
        Assert.All(r.LiveUsbGuides, g => Assert.Equal("sudo -E hw-probe -all -upload", g.ShareCommands[^1]));
    }

    [Fact]
    public void Every_distro_links_its_own_usb_guide()
    {
        foreach (var d in Data.Value.Distros.Distros)
        {
            Assert.NotNull(d.LiveUsb.MakeUsb);
            Assert.StartsWith("https://", d.LiveUsb.MakeUsb!.Url);
        }
    }

    [Fact]
    public void Unknown_categories_in_live_usb_data_are_rejected()
    {
        var catalog = new LiveUsbCatalog
        {
            Checks = [new LiveUsbCheckInfo { Id = "x", Categories = ["graphics"], Title = "t", Test = "t" }],
            HwProbe = new HwProbeInfo { Commands = new() { ["default"] = ["x"] } },
        };
        var ex = Assert.Throws<InvalidDataException>(catalog.Validate);
        Assert.Contains("unknown category 'graphics'", ex.Message);
    }
}
