using GreyHaven.Compat.Online;
using GreyHaven.Compat.Rules;
using GreyHaven.Core.Compatibility;
using GreyHaven.Core.Hardware;
using GreyHaven.Ratings;
using static GreyHaven.Tests.Ratings.TestData;

namespace GreyHaven.Tests.Ratings;

public class RatingEngineTests
{
    private static readonly RatingOptions Offline = new() { UseOnlineLookups = false };

    private static async Task<RatedComponent> RateOne(GreyHaven.Compat.CompatibilityData data, HardwareComponent c,
        SecureBootState sb = SecureBootState.Disabled, ICommunityLookup? community = null, RatingOptions? options = null)
    {
        var report = await new RatingEngine(data, community).RateAsync(Profile([Cpu(), c], sb), options ?? Offline);
        return report.Components.Single(x => x.Component == c);
    }

    // ---- Kernel evidence ----

    [Fact]
    public async Task Device_specific_kernel_driver_means_works_out_of_the_box()
    {
        var data = Build(alphaKernel: [("pci:v00008086d00002723sv*sd*bc*sc*i*", "iwlwifi")]);
        var r = (await RateOne(data, Pci("8086:2723", "028000", ComponentCategory.Wifi))).For("alpha");

        Assert.Equal(Rating.WorksOutOfTheBox, r.Rating);
        Assert.Equal(Confidence.Medium, r.Confidence);
        Assert.Equal("iwlwifi", r.LinuxDriver);
        Assert.Contains("iwlwifi", r.Explanation);
        Assert.Contains("6.10", r.Explanation);
        Assert.Equal(SourceKind.KernelDriver, Assert.Single(r.Sources).Kind);
        Assert.Null(r.Fix);
    }

    [Fact]
    public async Task Subsystem_specific_pattern_only_matches_that_subsystem()
    {
        var data = Build(alphaKernel: [("pci:v000014E4d00004365sv000014E4sd00004365bc02sc80i*", "brcmfmac")]);

        var other = (await RateOne(data, Pci("14e4:4365", "028000", ComponentCategory.Wifi, "1043:85d1"))).For("alpha");
        var same = (await RateOne(data, Pci("14e4:4365", "028000", ComponentCategory.Wifi, "14e4:4365"))).For("alpha");

        Assert.Equal(Rating.Unknown, other.Rating);
        Assert.Equal(Rating.WorksOutOfTheBox, same.Rating);
    }

    [Fact]
    public async Task Bus_drivers_do_not_count_as_support()
    {
        var data = Build(alphaKernel: [("pci:v000014E4d00004365sv00001043sd000085D1bc*sc*i*", "bcma")]);
        var r = (await RateOne(data, Pci("14e4:4365", "028000", ComponentCategory.Wifi, "1043:85d1"))).For("alpha");
        Assert.Equal(Rating.Unknown, r.Rating);
    }

    [Fact]
    public async Task Class_wide_driver_uses_module_confidence_and_says_standard()
    {
        var data = Build(alphaKernel: [("pci:v*d*sv*sd*bc0Csc03i30*", "xhci_pci")]);
        var r = (await RateOne(data, Pci("1022:149c", "0c0330", ComponentCategory.UsbController))).For("alpha");

        Assert.Equal(Rating.WorksOutOfTheBox, r.Rating);
        Assert.Equal(Confidence.High, r.Confidence);
        Assert.Contains("standard", r.Explanation);
    }

    [Fact]
    public async Task Usb_interface_class_driver_needs_known_interface_class()
    {
        var data = Build(alphaKernel: [("usb:v*p*d*dc*dsc*dp*ic0Eisc01ip00in*", "uvcvideo")]);

        var withClass = (await RateOne(data, Usb("04f2:b6cb", ComponentCategory.Webcam, "0e0100"))).For("alpha");
        var withoutClass = (await RateOne(data, Usb("04f2:b6cb", ComponentCategory.Webcam))).For("alpha");

        Assert.Equal(Rating.WorksOutOfTheBox, withClass.Rating);
        Assert.Equal(Rating.Unknown, withoutClass.Rating); // conservative: unknown fields never match specific values
    }

    // ---- Updates kernel ----

    [Fact]
    public async Task Driver_only_in_updated_kernel_means_extra_step()
    {
        var data = Build(
            alphaKernel: [],
            alphaUpdates: [("pci:v000010ECd00008852sv*sd*bc*sc*i*", "rtw89_8852be")],
            betaKernel: [("pci:v000010ECd00008852sv*sd*bc*sc*i*", "rtw89_8852be")]);
        var rated = await RateOne(data, Pci("10ec:8852", "028000", ComponentCategory.Wifi));

        var alpha = rated.For("alpha");
        Assert.Equal(Rating.WorksWithExtraDriver, alpha.Rating);
        Assert.Contains("6.10", alpha.Explanation);
        Assert.Contains("7.0", alpha.Explanation);
        Assert.Contains("sudo apt full-upgrade", alpha.Fix!.Commands);
        Assert.NotNull(alpha.Fix.Note); // network adapter: needs another way online
        Assert.Equal(Rating.WorksOutOfTheBox, rated.For("beta").Rating);
    }

    // ---- Driver packages ----

    [Fact]
    public async Task Driver_package_means_extra_driver_with_install_command()
    {
        var data = Build(packages: [("pci:v00001234d00005678sv*sd*bc*sc*i*", "vendor-driver-dkms"), ("pci:v00001234d00005678sv*sd*bc*sc*i*", "oem-ignored-meta")]);
        var r = (await RateOne(data, Pci("1234:5678", "028000", ComponentCategory.Wifi))).For("alpha");

        Assert.Equal(Rating.WorksWithExtraDriver, r.Rating);
        Assert.Equal(["sudo apt update", "sudo apt install vendor-driver-dkms"], r.Fix!.Commands);
        Assert.True(r.Fix.NeedsInternet);
    }

    // ---- Curated rules ----

    private static RuleOutcome Outcome(string rating, string explanation, RuleCondition? when = null) =>
        new() { Rating = rating, Explanation = explanation, When = when };

    [Fact]
    public async Task Device_rule_overrides_kernel_match()
    {
        var rule = Rule("bad-chip", new RuleMatch { Ids = ["pci:8086:2723"] }, Outcome("partial", "Known issues on {distro}."));
        var data = Build(alphaKernel: [("pci:v00008086d00002723sv*sd*bc*sc*i*", "iwlwifi")], rules: [rule]);
        var r = (await RateOne(data, Pci("8086:2723", "028000", ComponentCategory.Wifi))).For("alpha");

        Assert.Equal(Rating.Partial, r.Rating);
        Assert.Equal("Known issues on Alpha Linux.", r.Explanation);
        Assert.Equal(Confidence.High, r.Confidence);
        Assert.Equal(SourceKind.CuratedRule, Assert.Single(r.Sources).Kind);
    }

    [Fact]
    public async Task Class_rule_loses_to_device_specific_kernel_match_but_beats_nothing()
    {
        var rule = Rule("codec-generic", new RuleMatch { Category = "audioCodec" }, Outcome("partial", "Generic codec."));
        var data = Build(alphaKernel: [("hdaudio:v10EC0287r*a01*", "snd_hda_codec_realtek")], rules: [rule]);

        var known = new HardwareComponent { Category = ComponentCategory.AudioCodec, Name = "c", Id = new DeviceId(DeviceBus.HdAudio, "10ec", "0287") };
        var unknown = new HardwareComponent { Category = ComponentCategory.AudioCodec, Name = "c", Id = new DeviceId(DeviceBus.HdAudio, "14f1", "1234") };

        Assert.Equal(Rating.WorksOutOfTheBox, (await RateOne(data, known)).For("alpha").Rating);
        Assert.Equal(Rating.Partial, (await RateOne(data, unknown)).For("alpha").Rating);
    }

    [Fact]
    public async Task Rule_conditions_select_outcome_per_distro_and_kernel()
    {
        var rule = Rule("new-wifi", new RuleMatch { Ids = ["pci:aaaa:0001"] },
            Outcome("worksOutOfTheBox", "Supported from 7.0.", new RuleCondition { KernelAtLeast = "7.0" }),
            Outcome("unsupported", "Too old."));
        var data = Build(rules: [rule]);
        var rated = await RateOne(data, Pci("aaaa:0001", "028000", ComponentCategory.Wifi));

        // alpha's ISO kernel (6.10) is too old, but the kernel it installs with updates (7.0) qualifies.
        var alpha = rated.For("alpha");
        Assert.Equal(Rating.WorksWithExtraDriver, alpha.Rating);
        Assert.Contains("system updates", alpha.Explanation);
        Assert.DoesNotContain("(\"\")", alpha.Explanation);
        Assert.Equal(Rating.WorksOutOfTheBox, rated.For("beta").Rating);
    }

    [Fact]
    public async Task Rule_kernel_condition_without_newer_updates_kernel_stays_as_rated()
    {
        var rule = Rule("future-wifi", new RuleMatch { Ids = ["pci:aaaa:0002"] },
            Outcome("worksOutOfTheBox", "Supported from 7.1.", new RuleCondition { KernelAtLeast = "7.1" }),
            Outcome("unsupported", "Too old."));
        var rated = await RateOne(Build(rules: [rule]), Pci("aaaa:0002", "028000", ComponentCategory.Wifi));

        Assert.Equal(Rating.Unsupported, rated.For("alpha").Rating);
        Assert.Equal(Rating.Unsupported, rated.For("beta").Rating);
    }

    [Fact]
    public async Task Nvidia_edition_is_selected_and_preinstalled_driver_condition_holds()
    {
        var rule = Rule("nvidia", new RuleMatch { Vendor = "pci:10de", ClassCode = "03" },
            Outcome("worksOutOfTheBox", "{edition} has it.", new RuleCondition { PreinstalledDriver = "nvidia" }),
            Outcome("worksWithExtraDriver", "Install {driverPackages} with {driverTool}.", new RuleCondition { DriverPackage = "nvidia-driver-*" }),
            Outcome("partial", "Basic driver only."));
        rule.Fix = new RuleFix { Driver = "NVIDIA", SecureBoot = "mokMayBeRequired", Commands = new() { ["default"] = ["sudo ubuntu-drivers install"] } };
        var data = Build(rules: [rule], packages:
        [
            ("pci:v000010DEd00002484sv*sd*bc03sc*i*", "nvidia-driver-580"),
            ("pci:v000010DEd00002484sv*sd*bc03sc*i*", "nvidia-driver-610"),
            ("pci:v000010DEd00002484sv*sd*bc03sc*i*", "nvidia-driver-610-server"),
        ]);
        var gpu = Pci("10de:2484", "030000", ComponentCategory.Gpu);

        var report = await new RatingEngine(data).RateAsync(Profile([Cpu(), gpu], SecureBootState.Enabled), Offline);
        var rated = report.Components.Single(c => c.Component == gpu);

        var beta = rated.For("beta");
        Assert.Equal(Rating.WorksOutOfTheBox, beta.Rating);
        Assert.Equal("NVIDIA ISO has it.", beta.Explanation);
        Assert.Equal("NVIDIA ISO", report.Distros.Single(d => d.DistroId == "beta").Edition);

        var alpha = rated.For("alpha");
        Assert.Equal(Rating.WorksWithExtraDriver, alpha.Rating);
        Assert.Equal("Install nvidia-driver-610 with Driver Tool.", alpha.Explanation);
        Assert.Equal(["sudo ubuntu-drivers install"], alpha.Fix!.Commands);
        Assert.Contains("Enroll MOK", alpha.Fix.SecureBootAdvice); // Secure Boot is on
    }

    [Fact]
    public async Task Secure_boot_advice_depends_on_state_and_distro()
    {
        var rule = Rule("dkms-wifi", new RuleMatch { Ids = ["pci:bbbb:0001"] }, Outcome("worksWithExtraDriver", "Needs wl."));
        rule.Fix = new RuleFix { Driver = "wl", SecureBoot = "mokRequired" };
        var data = Build(rules: [rule]);
        var wifi = Pci("bbbb:0001", "028000", ComponentCategory.Wifi);

        var on = await RateOne(data, wifi, SecureBootState.Enabled);
        var off = await RateOne(data, wifi, SecureBootState.Disabled);

        Assert.Contains("must be approved", on.For("alpha").Fix!.SecureBootAdvice);
        Assert.Contains("turned off", on.For("beta").Fix!.SecureBootAdvice); // beta doesn't support Secure Boot
        Assert.Null(off.For("alpha").Fix!.SecureBootAdvice);
    }

    [Fact]
    public async Task Fix_is_not_attached_to_non_driver_outcomes()
    {
        var rule = Rule("r", new RuleMatch { Ids = ["pci:cccc:0001"] }, Outcome("partial", "Partly."));
        rule.Fix = new RuleFix { Driver = "x" };
        var r = (await RateOne(Build(rules: [rule]), Pci("cccc:0001", "028000", ComponentCategory.Wifi))).For("alpha");
        Assert.Null(r.Fix);
    }

    // ---- Fingerprint readers ----

    [Theory]
    [InlineData("libfprint:goodixmoc", Rating.WorksOutOfTheBox)]
    [InlineData("libfprint:unsupported", Rating.Unsupported)]
    public async Task Fingerprint_readers_use_libfprint_lists(string target, Rating expected)
    {
        var data = Build(fingerprint: [("usb:v27C6p633C*", target)]);
        var rated = await RateOne(data, Usb("27c6:633c", ComponentCategory.FingerprintReader));

        Assert.Equal(expected, rated.For("alpha").Rating);
        Assert.Equal(SourceKind.Libfprint, rated.For("alpha").Sources.Single().Kind);
        Assert.Contains("fp", rated.CaveatIds);
    }

    [Fact]
    public async Task Fingerprint_reader_missing_from_libfprint_is_unknown()
    {
        var r = (await RateOne(Build(), Usb("27c6:ffff", ComponentCategory.FingerprintReader))).For("alpha");
        Assert.Equal(Rating.Unknown, r.Rating);
        Assert.Contains("isn't on the supported", r.Explanation);
    }

    // ---- Unknown, ARM ----

    [Fact]
    public async Task No_data_means_unknown_with_low_confidence()
    {
        var r = (await RateOne(Build(), Pci("dddd:0001", "ff0000", ComponentCategory.Other))).For("alpha");
        Assert.Equal(Rating.Unknown, r.Rating);
        Assert.Equal(Confidence.Low, r.Confidence);
        Assert.True(r.IsWeakData);
    }

    [Fact]
    public async Task Arm_pcs_do_not_use_x64_driver_lists()
    {
        var data = Build(alphaKernel: [("pci:v00008086d00002723sv*sd*bc*sc*i*", "iwlwifi")]);
        var wifi = Pci("8086:2723", "028000", ComponentCategory.Wifi);
        var report = await new RatingEngine(data).RateAsync(Profile([Cpu("Qualcomm"), wifi], arch: CpuArchitecture.Arm64), Offline);

        var r = report.Components.Single(c => c.Component == wifi).For("alpha");
        Assert.Equal(Rating.Unknown, r.Rating);
        Assert.Contains("ARM", r.Explanation);
    }

    // ---- Community data ----

    private sealed class FakeCommunity(CommunityResult result) : ICommunityLookup
    {
        public int Calls { get; private set; }

        public Task<CommunityResult> LookupAsync(DeviceId id, CancellationToken cancellationToken = default)
        {
            Calls++;
            return Task.FromResult(result);
        }
    }

    private static CommunityResult Found(int works, int failed) => new()
    {
        Status = CommunityStatus.Found, Url = "https://linux-hardware.org/?id=pci:eeee-0001", DeviceName = "Mystery", Works = works, Failed = failed,
    };

    [Fact]
    public async Task Mostly_working_community_reports_give_low_confidence_works()
    {
        var community = new FakeCommunity(Found(works: 9, failed: 1));
        var r = (await RateOne(Build(), Pci("eeee:0001", "ff0000", ComponentCategory.Other), community: community, options: new RatingOptions())).For("alpha");

        Assert.Equal(Rating.WorksOutOfTheBox, r.Rating);
        Assert.Equal(Confidence.Low, r.Confidence);
        Assert.Contains(r.Sources, s => s.Kind == SourceKind.LinuxHardwareDb);
    }

    [Fact]
    public async Task Mixed_community_reports_give_partial()
    {
        var r = (await RateOne(Build(), Pci("eeee:0001", "ff0000", ComponentCategory.Other), community: new FakeCommunity(Found(2, 4)), options: new RatingOptions())).For("alpha");
        Assert.Equal(Rating.Partial, r.Rating);
    }

    [Fact]
    public async Task Too_few_community_reports_stay_unknown_but_link_the_page()
    {
        var r = (await RateOne(Build(), Pci("eeee:0001", "ff0000", ComponentCategory.Other), community: new FakeCommunity(Found(2, 0)), options: new RatingOptions())).For("alpha");
        Assert.Equal(Rating.Unknown, r.Rating);
        Assert.Contains(r.Sources, s => s.Kind == SourceKind.LinuxHardwareDb);
    }

    [Fact]
    public async Task Community_is_only_asked_about_unknown_devices()
    {
        var community = new FakeCommunity(Found(9, 0));
        var data = Build(alphaKernel: [("pci:v00008086d00002723sv*sd*bc*sc*i*", "iwlwifi")]);
        await RateOne(data, Pci("8086:2723", "028000", ComponentCategory.Wifi), community: community, options: new RatingOptions());
        Assert.Equal(0, community.Calls);
    }

    [Fact]
    public async Task Unavailable_community_data_adds_a_clear_note()
    {
        var community = new FakeCommunity(new CommunityResult { Status = CommunityStatus.Unavailable, Reason = "You may be offline." });
        var data = Build();
        var report = await new RatingEngine(data, community).RateAsync(Profile([Cpu(), Pci("eeee:0001", "ff0000", ComponentCategory.Other)]), new RatingOptions());

        Assert.Contains(report.DataNotes, n => n.Contains("You may be offline."));
    }

    [Fact]
    public async Task Offline_option_skips_community_and_says_so()
    {
        var community = new FakeCommunity(Found(9, 0));
        var report = await new RatingEngine(Build(), community).RateAsync(Profile([Cpu(), Pci("eeee:0001", "ff0000", ComponentCategory.Other)]), Offline);

        Assert.Equal(0, community.Calls);
        Assert.Contains(report.DataNotes, n => n.Contains("turned off"));
    }

    // ---- Caveats and summaries ----

    [Fact]
    public async Task Caveats_follow_the_hardware()
    {
        var gpus = new[] { Pci("8086:9a49", "030000", ComponentCategory.Gpu), Pci("10de:25a2", "030200", ComponentCategory.Gpu) };
        var report = await new RatingEngine(Build()).RateAsync(Profile([Cpu(), .. gpus], formFactor: FormFactor.Laptop), Offline);

        Assert.Equal(["sleep", "battery", "hybrid"], report.Caveats.Select(c => c.Id));
        Assert.All(report.Components.Where(c => c.Component.Category == ComponentCategory.Gpu), c => Assert.Contains("hybrid", c.CaveatIds));
    }

    [Fact]
    public async Task Distro_summary_notes_secure_boot_requirement()
    {
        var report = await new RatingEngine(Build()).RateAsync(Profile([Cpu()], SecureBootState.Enabled), Offline);
        Assert.Contains(report.Distros.Single(d => d.DistroId == "beta").Notes, n => n.Contains("Secure Boot off"));
        Assert.DoesNotContain(report.Distros.Single(d => d.DistroId == "alpha").Notes, n => n.Contains("Secure Boot"));
    }

    [Theory]
    [InlineData("6.10", "6.9", 1)]
    [InlineData("7.0", "6.18", 1)]
    [InlineData("6.2", "6.10", -1)]
    [InlineData("7.0", "7.0", 0)]
    public void Kernel_series_compare_numerically(string a, string b, int sign)
    {
        Assert.Equal(sign, Math.Sign(RatingEngine.CompareSeries(a, b)));
    }

    [Fact]
    public void Best_package_prefers_newest_non_server()
    {
        Assert.Equal("nvidia-driver-610", RatingEngine.BestPackage(["nvidia-driver-580", "nvidia-driver-610-server", "nvidia-driver-610", "nvidia-driver-610-open"]));
    }
}
