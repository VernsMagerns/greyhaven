using GreyHaven.Compat;
using GreyHaven.Core.Compatibility;
using GreyHaven.Core.Hardware;
using GreyHaven.Core.Scanning;
using GreyHaven.Ratings;

namespace GreyHaven.Tests.Ratings;

/// <summary>
/// Checks the shipped data directory and the sample PCs end to end (offline). These pin down
/// behaviour that was verified by hand against official sources; if regenerating the indexes
/// changes an outcome, these tests say so.
/// </summary>
public class RealDataTests
{
    private static readonly Lazy<CompatibilityData> Data = new(() => CompatibilityData.Load());

    private static async Task<CompatibilityReport> Rate(string sample) =>
        await new RatingEngine(Data.Value).RateAsync(await SampleProfiles.Open(sample).ScanAsync(), new RatingOptions { UseOnlineLookups = false });

    private static ComponentRating Rating(CompatibilityReport r, ComponentCategory category, string distro) =>
        r.Components.First(c => c.Component.Category == category).For(distro);

    [Fact]
    public void Data_directory_loads_and_validates()
    {
        var d = Data.Value;
        Assert.Equal(["mint", "ubuntu", "pop-os"], d.Distros.Distros.Select(x => x.Id));
        Assert.All(d.KernelIndexes.Values, i => Assert.True(i.Count > 10_000, $"{i.Info.Id} looks truncated"));
        Assert.NotEmpty(d.Rules.Rules);
    }

    [Fact]
    public void Every_rule_cites_an_https_source()
    {
        Assert.All(Data.Value.Rules.Rules.SelectMany(r => r.Sources.Select(s => (r.Id, s.Url))),
            x => Assert.StartsWith("https://", x.Url));
    }

    [Theory]
    [MemberData(nameof(GreyHaven.Tests.Scanning.SampleProfileTests.SampleNames), MemberType = typeof(GreyHaven.Tests.Scanning.SampleProfileTests))]
    public async Task Every_sample_rates_every_component_on_every_distro(string sample)
    {
        var r = await Rate(sample);
        Assert.Equal(3, r.Distros.Count);
        Assert.All(r.Components, c => Assert.Equal(3, c.Ratings.Count));
        Assert.All(r.Components.SelectMany(c => c.Ratings), x => Assert.False(string.IsNullOrWhiteSpace(x.Explanation)));
        Assert.All(r.Components.SelectMany(c => c.Ratings).Where(x => x.Rating != Core.Compatibility.Rating.Unknown), x => Assert.NotEmpty(x.Sources));
        Assert.All(r.Components.SelectMany(c => c.Ratings).Where(x => x.Rating == Core.Compatibility.Rating.WorksWithExtraDriver), x => Assert.NotNull(x.Fix));
        Assert.False(string.IsNullOrWhiteSpace(r.Recommendation.Paragraph));
        Assert.Contains(r.Caveats, c => c.Id == "sleep");
    }

    [Theory]
    [MemberData(nameof(GreyHaven.Tests.Scanning.SampleProfileTests.SampleNames), MemberType = typeof(GreyHaven.Tests.Scanning.SampleProfileTests))]
    public async Task Sources_shown_for_a_distro_are_about_that_distro(string sample)
    {
        // Names that mark a source as being about one particular distro.
        var otherDistroWords = new Dictionary<string, string[]>
        {
            ["ubuntu"] = ["Mint", "System76", "Pop!_OS"],
            ["mint"] = ["System76", "Pop!_OS"],
            ["pop-os"] = ["Mint"],
        };
        var r = await Rate(sample);
        foreach (var c in r.Components)
            foreach (var rating in c.Ratings)
                foreach (var src in rating.Sources)
                    Assert.False(otherDistroWords[rating.DistroId].Any(w => src.Title.Contains(w, StringComparison.Ordinal)),
                        $"{sample}: {c.DisplayName} on {rating.DistroId} cites '{src.Title}'");
    }

    [Theory]
    [MemberData(nameof(GreyHaven.Tests.Scanning.SampleProfileTests.SampleNames), MemberType = typeof(GreyHaven.Tests.Scanning.SampleProfileTests))]
    public async Task Separate_parts_never_share_a_display_name(string sample)
    {
        var r = await Rate(sample);
        var dupes = r.Components.GroupBy(c => (c.Component.Category, c.DisplayName)).Where(g => g.Count() > 1).Select(g => g.Key.DisplayName);
        Assert.Empty(dupes);
    }

    [Theory]
    [MemberData(nameof(GreyHaven.Tests.Scanning.SampleProfileTests.SampleNames), MemberType = typeof(GreyHaven.Tests.Scanning.SampleProfileTests))]
    public async Task Reports_never_contain_placeholder_links(string sample)
    {
        // The full report includes every path's guide link, whichever path is selected.
        var json = Core.Serialization.GreyHavenJson.Serialize(await Rate(sample));
        Assert.DoesNotContain("OWNER/", json);
    }

    [Fact]
    public async Task General_secure_boot_guide_never_links_a_placeholder()
    {
        // A custom-built PC whose motherboard maker isn't recognised gets GreyHaven's own guide.
        var profile = await SampleProfiles.Open("nvidia-desktop").ScanAsync();
        profile = profile with { System = profile.System with { BoardManufacturer = "ASRock" } };
        var r = await new RatingEngine(Data.Value).RateAsync(profile, new RatingOptions { UseOnlineLookups = false });

        var guide = r.Recommendation.Get(PathKind.Advanced)!.SecureBootGuide!;
        Assert.False(guide.FromManufacturer);
        Assert.DoesNotContain("OWNER/", Core.Serialization.GreyHavenJson.Serialize(r));
    }

    [Fact]
    public async Task Nvidia_desktop_same_named_parts_are_told_apart()
    {
        var r = await Rate("nvidia-desktop");
        Assert.Equal(
            ["High Definition Audio Controller (AMD)", "High Definition Audio Controller (NVIDIA)"],
            r.Components.Where(c => c.Component.Category == ComponentCategory.AudioController).Select(c => c.DisplayName).Order());
        Assert.All(r.Components.Where(c => c.Component.Category == ComponentCategory.UsbController),
            c => Assert.Matches(@"\((1|2) of 2\)$", c.DisplayName));
    }

    [Fact]
    public async Task Nvidia_desktop()
    {
        var r = await Rate("nvidia-desktop");
        var mint = Rating(r, ComponentCategory.Gpu, "mint");
        Assert.Equal(Core.Compatibility.Rating.WorksWithExtraDriver, mint.Rating);
        Assert.Equal(["sudo ubuntu-drivers install"], mint.Fix!.Commands);
        Assert.NotNull(mint.Fix.SecureBootAdvice); // Secure Boot is on in this sample

        Assert.Equal(Core.Compatibility.Rating.WorksOutOfTheBox, Rating(r, ComponentCategory.Gpu, "pop-os").Rating);
        Assert.Equal("NVIDIA ISO", r.Distros.Single(d => d.DistroId == "pop-os").Edition);
        Assert.Contains(r.Distros.Single(d => d.DistroId == "pop-os").Notes, n => n.Contains("Secure boot must be disabled"));
        Assert.Equal(Core.Compatibility.Rating.WorksOutOfTheBox, Rating(r, ComponentCategory.Wifi, "mint").Rating);
        Assert.All(r.Components.Where(c => c.Component.Category == ComponentCategory.UsbController), c => Assert.Equal(Core.Compatibility.Rating.WorksOutOfTheBox, c.For("mint").Rating));
    }

    // ---- The maintainer's real PC (samples/ryzen-rtx2070s-desktop.raw.json, 7 October 2026) ----

    private const string RealPc = "ryzen-rtx2070s-desktop";

    [Fact]
    public async Task Real_pc_has_no_part_left_unchecked()
    {
        var r = await Rate(RealPc);
        Assert.Empty(r.Components.Where(c => c.Ratings.Any(x => x.Rating == Core.Compatibility.Rating.Unknown)).Select(c => c.DisplayName));
    }

    [Fact]
    public async Task Real_pc_leaves_out_software_and_placeholder_devices()
    {
        var keys = (await Rate(RealPc)).Components.Select(c => c.Component.Id?.Key).ToList();
        Assert.DoesNotContain("usb:0955:9000", keys); // NVIDIA Virtual Audio Device
        Assert.DoesNotContain("pci:1022:1485", keys); // AMD placeholder functions
        Assert.DoesNotContain("pci:1022:148a", keys);
    }

    [Fact]
    public async Task Real_pc_parts_that_used_to_say_not_sure()
    {
        var r = await Rate(RealPc);
        var typeC = r.Components.Single(c => c.Component.Id?.Key == "pci:10de:1ad9");
        Assert.All(typeC.Ratings, x => Assert.Equal(Core.Compatibility.Rating.WorksOutOfTheBox, x.Rating));
        Assert.Equal(ComponentCategory.Chipset, typeC.Component.Category);

        var scarlett = r.Components.Single(c => c.Component.Id?.Key == "usb:1235:8211");
        Assert.Equal(ComponentCategory.AudioController, scarlett.Component.Category);
        Assert.All(scarlett.Ratings, x => Assert.Equal(Core.Compatibility.Rating.WorksOutOfTheBox, x.Rating));
        Assert.Contains("Scarlett2", scarlett.For("ubuntu").Explanation);
    }

    [Fact]
    public async Task Real_pc_matches_what_the_tester_saw()
    {
        var r = await Rate(RealPc);
        Assert.Equal(FormFactor.Desktop, r.Profile.System.FormFactor);
        Assert.True(r.Profile.System.Windows.IsWindows11); // registry says "Windows 10 Pro"; build 26200 says 11
        Assert.Empty(r.ContextNotes); // so no Windows 10 end-of-support note
        Assert.Equal("ubuntu", r.SelectedDistroId);
        Assert.Equal(98, r.Distros.Single(d => d.DistroId == "ubuntu").Score.Score);
        Assert.Equal(Core.Compatibility.Rating.WorksWithExtraDriver, Rating(r, ComponentCategory.Gpu, "ubuntu").Rating);
        Assert.Contains("msi.com", r.Recommendation.Get(PathKind.Advanced)!.SecureBootGuide!.Url);

        var parts = new GreyHaven.Reports.PartsOverview(r, "ubuntu");
        Assert.Equal("System chipset: 18 parts, all fine", parts.ChipsetLine);
        Assert.Equal("15 parts work right away. 1 part needs one extra step after you install.", parts.Summary);

        var sleep = r.LiveUsbGuides[0].Checks.Single(c => c.Title == "Sleep and wake").Test;
        Assert.DoesNotContain("lid", sleep);
        Assert.DoesNotContain("touchpad", sleep);
    }

    // ---- Parts from a real scan (Windows 11, MSI board, Ryzen 5 3600, RTX 2070 SUPER) ----

    private static async Task<RatedComponent> RateDevices(params RawPnpDevice[] devices)
    {
        var raw = new RawSnapshot { CapturedAt = DateTimeOffset.UnixEpoch, System = new RawSystemData(), Devices = devices };
        var r = await new RatingEngine(Data.Value).RateAsync(ProfileBuilder.Build(raw, "test"), new RatingOptions { UseOnlineLookups = false });
        return Assert.Single(r.Components, c => c.Component.Category != ComponentCategory.Cpu);
    }

    [Fact]
    public async Task Nvidia_usb_c_port_controller_works()
    {
        var c = await RateDevices(new RawPnpDevice
        {
            Name = "NVIDIA USB Type-C Port Policy Controller", PnpClass = "System", Present = true,
            HardwareIds = [@"PCI\VEN_10DE&DEV_1AD9&SUBSYS_C7571462&REV_A1", @"PCI\VEN_10DE&DEV_1AD9&SUBSYS_C7571462", @"PCI\VEN_10DE&DEV_1AD9&CC_0C8000", @"PCI\VEN_10DE&DEV_1AD9&CC_0C80"],
            CompatibleIds = [@"PCI\VEN_10DE&DEV_1AD9&REV_A1", @"PCI\VEN_10DE&DEV_1AD9", @"PCI\VEN_10DE&CC_0C8000", @"PCI\VEN_10DE&CC_0C80", @"PCI\VEN_10DE", @"PCI\CC_0C8000", @"PCI\CC_0C80"],
        });
        Assert.All(c.Ratings, x => Assert.Equal(Core.Compatibility.Rating.WorksOutOfTheBox, x.Rating));
        Assert.Contains("i2c_nvidia_gpu", c.For("ubuntu").LinuxDriver);
    }

    [Fact]
    public async Task Scarlett_solo_works_even_when_focusrites_driver_hides_the_usb_class()
    {
        var c = await RateDevices(new RawPnpDevice
        {
            Name = "Scarlett Solo USB", PnpClass = "USB", Present = true,
            HardwareIds = [@"USB\VID_1235&PID_8211&REV_0626", @"USB\VID_1235&PID_8211"],
            CompatibleIds = [@"USB\DevClass_EF&SubClass_02&Prot_01", @"USB\COMPOSITE"],
        });
        Assert.Equal(ComponentCategory.AudioController, c.Component.Category);
        Assert.All(c.Ratings, x => Assert.Equal(Core.Compatibility.Rating.WorksOutOfTheBox, x.Rating));
    }

    [Fact]
    public async Task Class_compliant_usb_audio_works_through_the_standard_driver()
    {
        var c = await RateDevices(
            new RawPnpDevice { Name = "USB Audio Device", PnpClass = "MEDIA", Present = true, HardwareIds = [@"USB\VID_FFF0&PID_0001&REV_0100"] },
            new RawPnpDevice { Name = "Speakers", PnpClass = "MEDIA", Present = true, HardwareIds = [@"USB\VID_FFF0&PID_0001&REV_0100&MI_00"], CompatibleIds = [@"USB\Class_01&SubClass_01&Prot_20"] });
        Assert.All(c.Ratings, x => Assert.Equal(Core.Compatibility.Rating.WorksOutOfTheBox, x.Rating));
        Assert.Contains("snd_usb_audio", c.For("ubuntu").LinuxDriver);
    }

    // ---- Secure Boot paths (nvidia-desktop: Pop!_OS scores best but needs Secure Boot off) ----

    [Fact]
    public async Task Nvidia_desktop_easy_path_is_default_and_keeps_secure_boot_on()
    {
        var r = await Rate("nvidia-desktop");

        Assert.Equal(2, r.Recommendation.Paths.Count);
        Assert.Equal(PathKind.Easy, r.SelectedPath);
        var easy = r.SelectedRecommendation!;
        Assert.Equal("ubuntu", easy.DistroId);

        // Live USB creator default ISO
        Assert.Equal("desktop", easy.EditionId);
        // Pre-switch checklist: nothing to change in the firmware
        Assert.DoesNotContain(PreSwitchRequirement.TurnOffSecureBoot, easy.Requirements);
        Assert.Null(easy.SecureBootNotice);
        // Fixes follow the chosen distro: the NVIDIA driver step, in plain words, with MOK advice
        Assert.Contains("Additional Drivers", easy.Paragraph);
        Assert.Contains("Enroll MOK", easy.Paragraph);
        var gpu = Rating(r, ComponentCategory.Gpu, r.SelectedDistroId!);
        Assert.Equal(Core.Compatibility.Rating.WorksWithExtraDriver, gpu.Rating);
        Assert.Equal(["sudo ubuntu-drivers install"], gpu.Fix!.Commands);
        Assert.NotNull(gpu.Fix.SecureBootAdvice);
    }

    [Fact]
    public async Task Nvidia_desktop_advanced_path_offers_pop_with_secure_boot_notice()
    {
        var r = (await Rate("nvidia-desktop")).WithPath(PathKind.Advanced);
        var advanced = r.SelectedRecommendation!;

        Assert.Equal("pop-os", r.SelectedDistroId);
        // Live USB creator default ISO: the NVIDIA build
        Assert.Equal("nvidia", advanced.EditionId);
        Assert.Equal("NVIDIA ISO", advanced.EditionName);
        // Pre-switch checklist gets an instructions-only Secure Boot item
        Assert.Equal([PreSwitchRequirement.TurnOffSecureBoot], advanced.Requirements);
        Assert.Contains("turn it back on later", advanced.SecureBootNotice);
        Assert.Contains("won't change this setting for you", advanced.SecureBootNotice);
        // The PC maker ("System manufacturer") isn't recognised, so the motherboard maker's guide is linked.
        Assert.True(advanced.SecureBootGuide!.FromManufacturer);
        Assert.Equal("motherboard", advanced.SecureBootGuide.MatchedBy);
        Assert.Contains("msi.com", advanced.SecureBootGuide.Url);
        // Fixes follow the chosen distro: the GPU needs nothing on the NVIDIA ISO
        Assert.Equal(Core.Compatibility.Rating.WorksOutOfTheBox, Rating(r, ComponentCategory.Gpu, r.SelectedDistroId!).Rating);
    }

    [Fact]
    public async Task Selected_path_is_carried_into_the_json_export()
    {
        var r = (await Rate("nvidia-desktop")).WithPath(PathKind.Advanced);
        var json = GreyHaven.Core.Serialization.GreyHavenJson.Serialize(r);

        Assert.Contains("\"selectedPath\": \"advanced\"", json);
        Assert.Contains("\"selectedDistroId\": \"pop-os\"", json);
        Assert.Contains("turnOffSecureBoot", json);
    }

    [Fact]
    public async Task Nvidia_desktop_with_secure_boot_off_gets_one_path()
    {
        var profile = await SampleProfiles.Open("nvidia-desktop").ScanAsync();
        profile = profile with { System = profile.System with { SecureBoot = SecureBootState.Disabled } };
        var r = await new RatingEngine(Data.Value).RateAsync(profile, new RatingOptions { UseOnlineLookups = false });

        var path = Assert.Single(r.Recommendation.Paths);
        Assert.Equal("pop-os", path.DistroId);
        Assert.Empty(path.Requirements);
    }

    [Fact]
    public async Task Choosing_a_path_the_report_does_not_have_fails()
    {
        var r = await Rate("intel-laptop");
        Assert.Throws<ArgumentException>(() => r.WithPath(PathKind.Advanced));
    }

    [Fact]
    public void Secure_boot_guides_are_https_and_fallback_guide_ships()
    {
        Assert.All(Data.Value.SecureBootGuides.Guides, g => Assert.StartsWith("https://", g.Url));
        Assert.True(File.Exists(Path.Combine(AppContext.BaseDirectory, Data.Value.SecureBootGuides.Fallback.Path)));
    }

    [Fact]
    public async Task Approximate_kernel_data_is_never_high_confidence()
    {
        var r = await Rate("intel-laptop");
        var pop = r.Components.Select(c => c.For("pop-os")).Where(x => x.ApproximationNote is not null).ToList();
        Assert.NotEmpty(pop); // Pop!_OS's ISO kernel index is approximated
        Assert.All(pop, x => Assert.NotEqual(Confidence.High, x.Confidence));

        // Mint's 6.14 kernel borrows class-wide patterns for built-in drivers (e.g. USB controllers).
        var mintUsb = r.Components.First(c => c.Component.Category == ComponentCategory.UsbController).For("mint");
        Assert.NotNull(mintUsb.ApproximationNote);
        Assert.Equal(Confidence.Medium, mintUsb.Confidence);

        // Ubuntu's index is exact: no notes.
        Assert.All(r.Components, c => Assert.Null(c.For("ubuntu").ApproximationNote));
    }

    [Fact]
    public void Dataset_build_date_is_set()
    {
        Assert.True(Data.Value.Dataset.BuiltOnDate > new DateOnly(2026, 1, 1));
    }

    [Fact]
    public async Task Broadcom_laptop_needs_wl_with_distro_specific_package()
    {
        var r = await Rate("broadcom-legacy-laptop");
        Assert.Contains("sudo apt install broadcom-sta-dkms", Rating(r, ComponentCategory.Wifi, "ubuntu").Fix!.Commands);
        Assert.Contains("sudo apt install bcmwl-kernel-source", Rating(r, ComponentCategory.Wifi, "mint").Fix!.Commands);
        Assert.Null(Rating(r, ComponentCategory.Wifi, "mint").Fix!.SecureBootAdvice); // legacy BIOS: no Secure Boot
        Assert.Equal(Core.Compatibility.Rating.WorksOutOfTheBox, Rating(r, ComponentCategory.Touchpad, "mint").Rating);
    }

    [Fact]
    public async Task Vmd_laptop_storage_is_partial_and_caps_score()
    {
        var r = await Rate("intel-vmd-laptop");
        Assert.All(r.Distros, d => Assert.True(d.Score.Score <= ScoreCalculator.PartialCriticalCap));
        Assert.Equal(Core.Compatibility.Rating.Partial, Rating(r, ComponentCategory.StorageController, "ubuntu").Rating);
        Assert.Equal(Core.Compatibility.Rating.WorksOutOfTheBox, Rating(r, ComponentCategory.FingerprintReader, "ubuntu").Rating); // libfprint goodixmoc
        Assert.Contains(r.Caveats, c => c.Id == "fingerprint");
    }

    [Fact]
    public async Task Intel_laptop_is_fully_supported()
    {
        var r = await Rate("intel-laptop");
        Assert.All(r.Distros, d => Assert.Equal(100, d.Score.Score));
        Assert.Equal("mint", r.SelectedDistroId); // tie -> tieBreakRank
    }

    [Fact]
    public async Task Arm_laptop_is_poor_everywhere()
    {
        var r = await Rate("snapdragon-arm-laptop");
        Assert.All(r.Distros, d => Assert.Equal(ScoreBand.Poor, d.Score.Band));
        Assert.Equal(Core.Compatibility.Rating.Unsupported, Rating(r, ComponentCategory.Cpu, "mint").Rating);
        Assert.Equal(Core.Compatibility.Rating.Partial, Rating(r, ComponentCategory.Cpu, "ubuntu").Rating);
        Assert.Equal(Core.Compatibility.Rating.Unknown, Rating(r, ComponentCategory.Wifi, "ubuntu").Rating);
        Assert.Contains("ARM", r.Recommendation.Paragraph);
    }
}

public class PlainWordsTests
{
    [Fact]
    public void Unchecked_small_parts_are_mentioned_calmly()
    {
        var s = PlainWords.PartsSummary([(Core.Compatibility.Rating.WorksOutOfTheBox, Importance.Critical), (Core.Compatibility.Rating.Unknown, Importance.Minor), (Core.Compatibility.Rating.Unknown, Importance.Minor)]);
        Assert.Equal("1 part works right away. 2 small parts couldn't be checked, which rarely matters.", s);
    }

    [Fact]
    public void Unchecked_important_parts_say_to_test_them()
    {
        var s = PlainWords.PartsSummary([(Core.Compatibility.Rating.Unknown, Importance.Important), (Core.Compatibility.Rating.Unknown, Importance.Minor)]);
        Assert.Equal("We couldn't check 1 important part. Test it from the USB stick. 1 small part couldn't be checked, which rarely matters.", s);
    }

    [Theory]
    [InlineData(FormFactor.Desktop, false)]
    [InlineData(FormFactor.Laptop, true)]
    public async Task Sleep_test_mentions_the_lid_and_touchpad_only_on_laptops(FormFactor formFactor, bool laptopWords)
    {
        var profile = await SampleProfiles.Open("nvidia-desktop").ScanAsync();
        profile = profile with { System = profile.System with { FormFactor = formFactor } };
        var r = await new RatingEngine(Data.Value).RateAsync(profile, new RatingOptions { UseOnlineLookups = false });

        var sleep = r.LiveUsbGuides[0].Checks.Single(c => c.Title == "Sleep and wake").Test;
        Assert.Equal(laptopWords, sleep.Contains("lid"));
        Assert.Equal(laptopWords, sleep.Contains("touchpad"));
    }

    private static readonly Lazy<GreyHaven.Compat.CompatibilityData> Data = new(() => GreyHaven.Compat.CompatibilityData.Load());

    [Fact]
    public void Every_rule_outcome_that_isnt_works_has_a_short_summary()
    {
        var missing = Data.Value.Rules.Rules
            .SelectMany(r => r.Ratings.Select(o => (r.Id, o)))
            .Where(x => x.o.Rating != "worksOutOfTheBox" && string.IsNullOrWhiteSpace(x.o.Summary))
            .Select(x => x.Id)
            .ToList();
        Assert.Empty(missing);
    }

    [Fact]
    public void Summaries_are_short_and_plain()
    {
        foreach (var o in Data.Value.Rules.Rules.SelectMany(r => r.Ratings).Where(o => o.Summary is not null))
        {
            var sentences = o.Summary!.Split(['.', '!', '?'], StringSplitOptions.RemoveEmptyEntries).Where(s => s.Trim().Length > 0).ToList();
            Assert.True(sentences.Count <= 2, $"'{o.Summary}': at most two sentences");
            Assert.All(sentences, s => Assert.True(s.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length <= 14, $"'{s.Trim()}': keep sentences under 15 words"));
        }
    }

    [Theory]
    [InlineData(98, "Your PC is ready for Linux")]
    [InlineData(79, "Your PC should work well with Linux")]
    [InlineData(55, "Linux will work, with a few problems")]
    [InlineData(20, "Linux may not work well on this PC")]
    public void Headline_follows_the_score(int score, string expected) =>
        Assert.Equal(expected, GreyHaven.Core.Hardware.PlainWords.Headline(score));

    [Fact]
    public void Short_names_drop_the_version() =>
        Assert.Equal(["Ubuntu", "Pop!_OS", "Linux Mint"], new[] { "Ubuntu 26.04.1 LTS", "Pop!_OS 24.04", "Linux Mint 22.3" }.Select(GreyHaven.Core.Hardware.PlainWords.ShortName));
}
