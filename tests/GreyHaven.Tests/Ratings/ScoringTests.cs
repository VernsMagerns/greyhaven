using GreyHaven.Core.Compatibility;
using GreyHaven.Core.Hardware;
using GreyHaven.Ratings;
using static GreyHaven.Tests.Ratings.TestData;

namespace GreyHaven.Tests.Ratings;

public class ScoreCalculatorTests
{
    private static ScoreResult Score(params (Importance I, Rating R)[] items) =>
        ScoreCalculator.Calculate(items.Select((x, n) => (x.I, x.R, $"Part {n}")).ToList());

    [Fact]
    public void Everything_working_scores_100_great()
    {
        var s = Score((Importance.Critical, Rating.WorksOutOfTheBox), (Importance.Minor, Rating.WorksOutOfTheBox));
        Assert.Equal(100, s.Score);
        Assert.Equal(ScoreBand.Great, s.Band);
        Assert.Equal(1.0, s.Coverage);
        Assert.False(s.Incomplete);
        Assert.Null(s.CapReason);
    }

    [Fact]
    public void Weighted_mean_uses_points_and_weights()
    {
        // critical extra driver: 3 x 80 = 240; important partial: 1.5 x 40 = 60; minor OK: 1 x 100.
        // (240 + 60 + 100) / 5.5 = 72.7 -> 73
        var s = Score((Importance.Critical, Rating.WorksWithExtraDriver), (Importance.Important, Rating.Partial), (Importance.Minor, Rating.WorksOutOfTheBox));
        Assert.Equal(73, s.Score);
        Assert.Equal(ScoreBand.Good, s.Band);
        Assert.Equal(1, s.ExtraDriverCount);
    }

    [Fact]
    public void Critical_parts_weigh_more_than_minor_ones()
    {
        var criticalBroken = Score((Importance.Critical, Rating.Partial), (Importance.Minor, Rating.WorksOutOfTheBox));
        var minorBroken = Score((Importance.Critical, Rating.WorksOutOfTheBox), (Importance.Minor, Rating.Partial));
        Assert.True(criticalBroken.Score < minorBroken.Score);
    }

    [Fact]
    public void One_unsupported_critical_part_caps_the_score_at_40()
    {
        var s = Score(
            (Importance.Critical, Rating.Unsupported),
            (Importance.Critical, Rating.WorksOutOfTheBox), (Importance.Critical, Rating.WorksOutOfTheBox),
            (Importance.Important, Rating.WorksOutOfTheBox), (Importance.Minor, Rating.WorksOutOfTheBox));
        Assert.Equal(40, s.Score);
        Assert.Equal(ScoreBand.Poor, s.Band);
        Assert.Contains("Part 0", s.CapReason);
    }

    [Fact]
    public void Unsupported_minor_part_does_not_cap()
    {
        var s = Score((Importance.Critical, Rating.WorksOutOfTheBox), (Importance.Critical, Rating.WorksOutOfTheBox), (Importance.Minor, Rating.Unsupported));
        Assert.Equal(86, s.Score); // 600 / 7
        Assert.Null(s.CapReason);
    }

    [Fact]
    public void Unknown_is_excluded_from_the_mean_but_lowers_coverage()
    {
        var s = Score((Importance.Important, Rating.WorksOutOfTheBox), (Importance.Important, Rating.Unknown));
        Assert.Equal(100, s.Score);
        Assert.Equal(0.5, s.Coverage);
        Assert.Equal(1, s.UnknownCount);
        Assert.False(s.Incomplete); // not critical
    }

    [Fact]
    public void Unknown_critical_part_caps_at_70_and_marks_incomplete()
    {
        var s = Score((Importance.Critical, Rating.Unknown), (Importance.Critical, Rating.WorksOutOfTheBox));
        Assert.Equal(70, s.Score);
        Assert.True(s.Incomplete);
        Assert.Contains("couldn't be checked", s.CapReason);
    }

    [Fact]
    public void Partial_critical_part_prevents_great()
    {
        // Uncapped: (3 x 40 + 20 x 100) / 23 = 92, which would be "Great".
        var items = new[] { (Importance.Critical, Rating.Partial) }
            .Concat(Enumerable.Repeat((Importance.Minor, Rating.WorksOutOfTheBox), 20))
            .ToArray();
        var s = Score(items);
        Assert.Equal(ScoreCalculator.PartialCriticalCap, s.Score);
        Assert.Equal(ScoreBand.Good, s.Band);
        Assert.Contains("only partly works", s.CapReason);
    }

    [Fact]
    public void Lowest_applicable_cap_wins_and_reason_is_always_given()
    {
        var s = Score((Importance.Critical, Rating.Unsupported), (Importance.Critical, Rating.Unknown), (Importance.Critical, Rating.Partial));
        Assert.Equal(20, s.Score); // unknown is excluded: (3 x 0 + 3 x 40) / 6 = 20, already below every cap
        Assert.Contains("isn't supported", s.CapReason);
    }

    [Fact]
    public void Nothing_known_scores_zero_poor_incomplete()
    {
        var s = Score((Importance.Critical, Rating.Unknown));
        Assert.Equal(0, s.Score);
        Assert.Equal(ScoreBand.Poor, s.Band);
        Assert.True(s.Incomplete);
    }

    [Theory]
    [InlineData(100, ScoreBand.Great)]
    [InlineData(90, ScoreBand.Great)]
    [InlineData(89, ScoreBand.Good)]
    [InlineData(70, ScoreBand.Good)]
    [InlineData(69, ScoreBand.Fair)]
    [InlineData(50, ScoreBand.Fair)]
    [InlineData(49, ScoreBand.Poor)]
    [InlineData(0, ScoreBand.Poor)]
    public void Bands(int score, ScoreBand band) => Assert.Equal(band, ScoreCalculator.BandOf(score));
}

public class ImportanceTests
{
    [Fact]
    public void Core_parts_are_critical()
    {
        var p = Profile([]);
        foreach (var cat in new[] { ComponentCategory.Cpu, ComponentCategory.Gpu, ComponentCategory.StorageController, ComponentCategory.NvmeDrive })
            Assert.Equal(Importance.Critical, ImportanceRules.For(new HardwareComponent { Category = cat, Name = "x" }, p));
    }

    [Fact]
    public void Wifi_is_critical_on_laptops_and_ethernet_on_desktops()
    {
        var wifi = Pci("8086:2723", "028000", ComponentCategory.Wifi);
        var eth = Pci("10ec:8125", "020000", ComponentCategory.Ethernet);

        var laptop = Profile([wifi, eth], formFactor: FormFactor.Laptop);
        Assert.Equal(Importance.Critical, ImportanceRules.For(wifi, laptop));
        Assert.Equal(Importance.Important, ImportanceRules.For(eth, laptop));

        var desktop = Profile([wifi, eth], formFactor: FormFactor.Desktop);
        Assert.Equal(Importance.Important, ImportanceRules.For(wifi, desktop));
        Assert.Equal(Importance.Critical, ImportanceRules.For(eth, desktop));

        var wifiOnlyDesktop = Profile([wifi], formFactor: FormFactor.Desktop);
        Assert.Equal(Importance.Critical, ImportanceRules.For(wifi, wifiOnlyDesktop));
    }

    [Fact]
    public void Chipset_and_input_devices_do_not_count_toward_score()
    {
        Assert.False(ImportanceRules.CountsTowardScore(new HardwareComponent { Category = ComponentCategory.Chipset, Name = "x" }));
        Assert.False(ImportanceRules.CountsTowardScore(new HardwareComponent { Category = ComponentCategory.InputDevice, Name = "x" }));
        Assert.True(ImportanceRules.CountsTowardScore(new HardwareComponent { Category = ComponentCategory.FingerprintReader, Name = "x" }));
    }
}

public class RecommenderTests
{
    private static DistroSummary Summary(string id, int score, int extra = 0, string edition = "std") => new()
    {
        DistroId = id, DisplayName = id, Edition = edition == "nv" ? "NVIDIA ISO" : "Standard", EditionId = edition, KernelVersion = "k",
        Score = new ScoreResult { Score = score, Band = ScoreCalculator.BandOf(score), Coverage = 1, Incomplete = false, ExtraDriverCount = extra },
    };

    private static RatedComponent Component(ComponentCategory cat, Rating alpha, Rating beta, Fix? alphaFix = null) => new()
    {
        Component = new HardwareComponent { Category = cat, Name = cat.ToString() },
        Importance = Importance.Critical,
        Ratings =
        [
            new ComponentRating { DistroId = "alpha", Rating = alpha, Confidence = Confidence.High, Explanation = "", Fix = alphaFix },
            new ComponentRating { DistroId = "beta", Rating = beta, Confidence = Confidence.High, Explanation = "" },
        ],
    };

    private static readonly GreyHaven.Compat.CompatibilityData Data = Build();

    private static Recommendation Recommend(HardwareProfile profile, IReadOnlyList<RatedComponent> components, params DistroSummary[] summaries) =>
        Recommender.Recommend(profile, components, summaries, Data.Distros, Data.SecureBootGuides);

    // beta doesn't support Secure Boot; alpha does and is the catalog's easy-mode distro.

    [Fact]
    public void Single_path_when_the_best_distro_keeps_secure_boot()
    {
        var rec = Recommend(Profile([], SecureBootState.Enabled), [Component(ComponentCategory.Gpu, Rating.WorksOutOfTheBox, Rating.WorksWithExtraDriver)],
            Summary("alpha", 100), Summary("beta", 90, extra: 1));

        var path = Assert.Single(rec.Paths);
        Assert.Equal(PathKind.Recommended, path.Kind);
        Assert.Equal(PathKind.Recommended, rec.DefaultPath);
        Assert.Equal("alpha", path.DistroId);
        Assert.Empty(path.Requirements);
        Assert.Contains("handles your graphics better than Beta OS", rec.Paragraph);
    }

    [Fact]
    public void Single_path_without_requirement_when_secure_boot_is_already_off()
    {
        var rec = Recommend(Profile([], SecureBootState.Disabled), [Component(ComponentCategory.Gpu, Rating.WorksWithExtraDriver, Rating.WorksOutOfTheBox)],
            Summary("alpha", 98, extra: 1), Summary("beta", 100));

        var path = Assert.Single(rec.Paths);
        Assert.Equal("beta", path.DistroId);
        Assert.Empty(path.Requirements);
        Assert.Null(path.SecureBootNotice);
    }

    [Fact]
    public void Two_paths_when_the_best_distro_needs_secure_boot_off()
    {
        var fix = new Fix { Driver = "NVIDIA", Steps = ["Open Driver Tool and pick the recommended driver."], SecureBoot = SecureBootImpact.MokMayBeRequired };
        var rec = Recommend(Profile([], SecureBootState.Enabled, bitLocker: BitLockerState.On),
            [Component(ComponentCategory.Gpu, Rating.WorksWithExtraDriver, Rating.WorksOutOfTheBox, fix)],
            Summary("alpha", 98, extra: 1), Summary("beta", 100, edition: "nv"));

        Assert.Equal(PathKind.Easy, rec.DefaultPath);
        Assert.Contains("needs Secure Boot turned off", rec.Paragraph);

        var easy = rec.Get(PathKind.Easy)!;
        Assert.Equal("alpha", easy.DistroId);
        Assert.Empty(easy.Requirements);
        Assert.Contains("keeps Secure Boot on", easy.Paragraph);
        Assert.Contains("open Driver Tool and pick the recommended driver.", easy.Paragraph); // the fix's own step, in plain words
        Assert.Contains("Enroll MOK", easy.Paragraph);

        var advanced = rec.Get(PathKind.Advanced)!;
        Assert.Equal("beta", advanced.DistroId);
        Assert.Equal("nv", advanced.EditionId);
        Assert.Equal([PreSwitchRequirement.TurnOffSecureBoot], advanced.Requirements);
        Assert.Contains("turn it back on later", advanced.SecureBootNotice);
        Assert.Contains("BitLocker recovery key", advanced.SecureBootNotice);
        Assert.Contains("won't change this setting for you", advanced.SecureBootNotice);
        Assert.NotNull(advanced.SecureBootGuide);
    }

    [Fact]
    public void Unknown_secure_boot_also_offers_both_paths_and_says_so()
    {
        var rec = Recommend(Profile([], SecureBootState.Unknown), [], Summary("alpha", 90), Summary("beta", 100));
        Assert.Equal(2, rec.Paths.Count);
        Assert.Contains("couldn't tell", rec.Paragraph);
        Assert.Contains("couldn't tell", rec.Get(PathKind.Advanced)!.SecureBootNotice);
    }

    [Theory]
    [InlineData("Dell Inc.", "XPS 13", "Dell guide", true)]
    [InlineData("LENOVO", "ThinkPad X1 Carbon", "Lenovo Think guide", true)]
    [InlineData("LENOVO", "IdeaPad 5", "General guide", false)]  // Lenovo guide only covers Think models
    [InlineData("System manufacturer", "System Product Name", "General guide", false)]
    [InlineData(null, null, "General guide", false)]
    public void Secure_boot_guide_uses_the_pc_makers_instructions_when_known(string? maker, string? model, string title, bool fromMaker)
    {
        var rec = Recommend(Profile([], SecureBootState.Enabled, manufacturer: maker, model: model), [], Summary("alpha", 90), Summary("beta", 100));
        var guide = rec.Get(PathKind.Advanced)!.SecureBootGuide!;
        Assert.Equal(title, guide.Title);
        Assert.Equal(fromMaker, guide.FromManufacturer);
        if (!fromMaker) Assert.Equal("docs/secure-boot.md", guide.LocalPath);
    }

    [Fact]
    public void Tie_goes_to_fewer_extra_driver_steps()
    {
        var rec = Recommend(Profile([], SecureBootState.Disabled), [], Summary("alpha", 95, extra: 2), Summary("beta", 95, extra: 0));
        Assert.Equal("beta", rec.Paths.Single().DistroId);
    }

    [Fact]
    public void Tie_avoids_turning_off_secure_boot_then_uses_rank()
    {
        Assert.Equal("alpha", Recommend(Profile([], SecureBootState.Enabled), [], Summary("beta", 95), Summary("alpha", 95)).Paths.Single().DistroId);
        Assert.Equal("alpha", Recommend(Profile([], SecureBootState.Disabled), [], Summary("beta", 95), Summary("alpha", 95)).Paths.Single().DistroId); // rank 1
    }

    [Fact]
    public void Equal_ratings_say_so_and_use_audience()
    {
        var rec = Recommend(Profile([]), [], Summary("alpha", 100), Summary("beta", 100));
        Assert.Contains("rate the same", rec.Paragraph);
        Assert.Contains("alpha suits beginners", rec.Paragraph);
    }

    [Fact]
    public void No_recommendation_when_no_distro_supports_the_cpu()
    {
        var cpu = Component(ComponentCategory.Cpu, Rating.Unsupported, Rating.Unsupported);
        var rec = Recommend(Profile([], arch: CpuArchitecture.Arm64), [cpu], Summary("alpha", 0), Summary("beta", 0));
        Assert.Empty(rec.Paths);
        Assert.Null(rec.DefaultPath);
        Assert.Contains("can't recommend", rec.Paragraph);
    }

    [Theory]
    [InlineData(new[] { "a" }, "a")]
    [InlineData(new[] { "a", "b" }, "a and b")]
    [InlineData(new[] { "a", "b", "c" }, "a, b and c")]
    public void Lists_read_naturally(string[] items, string expected) => Assert.Equal(expected, Recommender.JoinList(items));
}
