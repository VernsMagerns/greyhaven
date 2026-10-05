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
    private static DistroSummary Summary(string id, int score, int extra = 0) => new()
    {
        DistroId = id, DisplayName = id, Edition = "e", KernelVersion = "k",
        Score = new ScoreResult { Score = score, Band = ScoreCalculator.BandOf(score), Coverage = 1, Incomplete = false, ExtraDriverCount = extra },
    };

    private static RatedComponent Component(ComponentCategory cat, Rating alpha, Rating beta) => new()
    {
        Component = new HardwareComponent { Category = cat, Name = cat.ToString() },
        Importance = Importance.Critical,
        Ratings =
        [
            new ComponentRating { DistroId = "alpha", Rating = alpha, Confidence = Confidence.High, Explanation = "" },
            new ComponentRating { DistroId = "beta", Rating = beta, Confidence = Confidence.High, Explanation = "" },
        ],
    };

    private static readonly GreyHaven.Compat.Data.DistroCatalog Catalog = Build().Distros;

    [Fact]
    public void Highest_score_wins_and_paragraph_explains_the_difference()
    {
        var rec = Recommender.Recommend(Profile([]), [Component(ComponentCategory.Gpu, Rating.WorksWithExtraDriver, Rating.WorksOutOfTheBox)],
            [Summary("alpha", 90, extra: 1), Summary("beta", 100)], Catalog);

        Assert.Equal("beta", rec.DistroId);
        Assert.Contains("Beta OS 2 is the best fit", rec.Paragraph);
        Assert.Contains("handles your graphics better than Alpha Linux", rec.Paragraph);
        Assert.Contains("Beta suits gamers.", rec.Paragraph);
    }

    [Fact]
    public void Tie_goes_to_fewer_extra_driver_steps()
    {
        var rec = Recommender.Recommend(Profile([]), [], [Summary("alpha", 95, extra: 2), Summary("beta", 95, extra: 0)], Catalog);
        Assert.Equal("beta", rec.DistroId);
    }

    [Fact]
    public void Tie_avoids_turning_off_secure_boot_then_uses_rank()
    {
        var sbOn = Profile([], SecureBootState.Enabled);
        Assert.Equal("alpha", Recommender.Recommend(sbOn, [], [Summary("beta", 95), Summary("alpha", 95)], Catalog).DistroId);

        var sbOff = Profile([], SecureBootState.Disabled);
        Assert.Equal("alpha", Recommender.Recommend(sbOff, [], [Summary("beta", 95), Summary("alpha", 95)], Catalog).DistroId); // rank 1
    }

    [Fact]
    public void Equal_ratings_say_so_and_use_audience()
    {
        var rec = Recommender.Recommend(Profile([]), [], [Summary("alpha", 100), Summary("beta", 100)], Catalog);
        Assert.Contains("rate the same", rec.Paragraph);
        Assert.Contains("alpha suits beginners", rec.Paragraph);
    }

    [Fact]
    public void Secure_boot_requirement_is_mentioned_for_the_pick()
    {
        var rec = Recommender.Recommend(Profile([], SecureBootState.Enabled), [Component(ComponentCategory.Gpu, Rating.Partial, Rating.WorksOutOfTheBox)],
            [Summary("alpha", 60), Summary("beta", 100)], Catalog);
        Assert.Equal("beta", rec.DistroId);
        Assert.Contains("turn off Secure Boot", rec.Paragraph);
    }

    [Fact]
    public void No_recommendation_when_no_distro_supports_the_cpu()
    {
        var cpu = Component(ComponentCategory.Cpu, Rating.Unsupported, Rating.Unsupported);
        var rec = Recommender.Recommend(Profile([], arch: CpuArchitecture.Arm64), [cpu], [Summary("alpha", 0), Summary("beta", 0)], Catalog);
        Assert.Null(rec.DistroId);
        Assert.Contains("can't recommend", rec.Paragraph);
    }

    [Theory]
    [InlineData(new[] { "a" }, "a")]
    [InlineData(new[] { "a", "b" }, "a and b")]
    [InlineData(new[] { "a", "b", "c" }, "a, b and c")]
    public void Lists_read_naturally(string[] items, string expected) => Assert.Equal(expected, Recommender.JoinList(items));
}
