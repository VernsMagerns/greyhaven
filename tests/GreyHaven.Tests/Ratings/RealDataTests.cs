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
        Assert.Equal("mint", r.Recommendation.DistroId); // tie -> tieBreakRank
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
