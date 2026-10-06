using GreyHaven.Compat.Data;
using GreyHaven.Compat.Rules;
using GreyHaven.Core.Hardware;
using static GreyHaven.Tests.Ratings.TestData;

namespace GreyHaven.Tests.Compat;

public class RuleSetTests
{
    private static readonly SystemInfo X64 = Profile([]).System;
    private static RuleOutcome Ok => new() { Rating = "worksOutOfTheBox", Explanation = "ok" };

    [Fact]
    public void More_specific_rules_win()
    {
        var device = Pci("10de:2484", "030000", ComponentCategory.Gpu, "1462:3907");
        var set = new RuleSet(
        [
            Rule("category", new RuleMatch { Category = "gpu" }, Ok),
            Rule("vendor", new RuleMatch { Vendor = "pci:10de" }, Ok),
            Rule("vendor-class", new RuleMatch { Vendor = "pci:10de", ClassCode = "03" }, Ok),
            Rule("id", new RuleMatch { Ids = ["pci:10de:2484"] }, Ok),
            Rule("id-subsystem", new RuleMatch { Ids = ["pci:10de:2484:1462:3907"] }, Ok),
        ], ["alpha"]);

        var best = set.FindBest(device, X64)!;
        Assert.Equal("id-subsystem", best.Rule.Id);
        Assert.Equal(RuleStrength.Device, best.Strength);
    }

    [Theory]
    [InlineData(20, RuleStrength.Class)]
    [InlineData(50, RuleStrength.Vendor)]
    [InlineData(60, RuleStrength.Vendor)]
    [InlineData(90, RuleStrength.Device)]
    public void Strength_from_specificity(int specificity, RuleStrength expected) =>
        Assert.Equal(expected, new RuleMatchResult(new Rule(), specificity).Strength);

    [Fact]
    public void Exclude_ids_remove_a_match()
    {
        var m = new RuleMatch { Ids = ["pci:14e4:4365"], ExcludeIds = ["pci:14e4:4365:14e4:4365"] };
        Assert.True(RuleSet.Specificity(m, Pci("14e4:4365", "028000", ComponentCategory.Wifi, "1043:85d1"), X64) > 0);
        Assert.Equal(0, RuleSet.Specificity(m, Pci("14e4:4365", "028000", ComponentCategory.Wifi, "14e4:4365"), X64));
    }

    [Fact]
    public void All_conditions_must_hold()
    {
        var m = new RuleMatch { Vendor = "pci:8086", ClassCode = "0104" };
        Assert.True(RuleSet.Specificity(m, Pci("8086:282a", "010400", ComponentCategory.StorageController), X64) > 0);
        Assert.Equal(0, RuleSet.Specificity(m, Pci("8086:282a", "010601", ComponentCategory.StorageController), X64));
        Assert.Equal(0, RuleSet.Specificity(m, Pci("1022:7905", "010400", ComponentCategory.StorageController), X64));
    }

    [Fact]
    public void Cpu_rules_match_architecture_and_vendor()
    {
        var m = new RuleMatch { Cpu = new CpuMatch { Architecture = "x64", Vendors = ["GenuineIntel"] } };
        Assert.True(RuleSet.Specificity(m, Cpu("GenuineIntel"), X64) > 0);
        Assert.Equal(0, RuleSet.Specificity(m, Cpu("AuthenticAMD"), X64));
        Assert.Equal(0, RuleSet.Specificity(m, Cpu("GenuineIntel"), Profile([], arch: CpuArchitecture.Arm64).System));
    }

    public static TheoryData<Rule, string> InvalidRules => new()
    {
        { Rule("empty", new RuleMatch(), Ok), "would match every device" },
        { Rule("cat", new RuleMatch { Category = "graphics" }, Ok), "unknown category" },
        { Rule("id", new RuleMatch { Ids = ["PCI:8086:2723"] }, Ok), "lower case" },
        { Rule("rating", new RuleMatch { Category = "gpu" }, new RuleOutcome { Rating = "great", Explanation = "x" }), "unknown rating" },
        { Rule("when", new RuleMatch { Category = "gpu" }, new RuleOutcome { Rating = "partial", Explanation = "x", When = new RuleCondition { Distros = ["alpha"] } }), "last rating must have no 'when'" },
        { Rule("distro", new RuleMatch { Category = "gpu" }, new RuleOutcome { Rating = "partial", Explanation = "x", When = new RuleCondition { Distros = ["nope"] } }, Ok), "unknown distro 'nope'" },
        { Rule("fix", new RuleMatch { Category = "gpu" }, new RuleOutcome { Rating = "worksWithExtraDriver", Explanation = "x" }), "needs a fix" },
        { Rule("explain", new RuleMatch { Category = "gpu" }, new RuleOutcome { Rating = "partial", Explanation = " " }), "needs an explanation" },
        { new Rule { Id = "nosource", Match = new RuleMatch { Category = "gpu" }, Ratings = [Ok] }, "at least one source" },
        { WithSources("srcdistro", new SourceLink { Title = "t", Url = "https://x", Distros = ["nope"] }), "unknown distro 'nope' in a source" },
        { WithSources("srcgap", new SourceLink { Title = "t", Url = "https://x", Distros = ["alpha"] }), "no source applies to 'beta'" },
    };

    private static Rule WithSources(string id, params SourceLink[] sources)
    {
        var rule = Rule(id, new RuleMatch { Category = "gpu" }, Ok);
        rule.Sources = [.. sources];
        return rule;
    }

    [Theory]
    [MemberData(nameof(InvalidRules))]
    public void Invalid_rules_are_rejected_with_a_helpful_message(Rule rule, string message)
    {
        var ex = Assert.Throws<InvalidDataException>(() => new RuleSet([rule], ["alpha", "beta"]));
        Assert.Contains(message, ex.Message);
    }

    [Fact]
    public void Duplicate_ids_are_rejected()
    {
        var ex = Assert.Throws<InvalidDataException>(() => new RuleSet(
            [Rule("same", new RuleMatch { Category = "gpu" }, Ok), Rule("same", new RuleMatch { Category = "wifi" }, Ok)], ["alpha"]));
        Assert.Contains("duplicate id", ex.Message);
    }
}
