using GreyHaven.Compat;
using GreyHaven.Core;
using GreyHaven.Core.Hardware;
using static GreyHaven.Tests.Ratings.TestData;

namespace GreyHaven.Tests.Compat;

/// <summary>Which "turn Secure Boot off" guide a PC gets, using the shipped data/secure-boot-guides.yaml.</summary>
public class SecureBootGuideTests
{
    private static readonly Lazy<CompatibilityData> Data = new(() => CompatibilityData.Load());

    private static HardwareProfile Pc(string? maker, string? board, string cpuVendor = "AuthenticAMD", string? model = null)
    {
        var p = Profile([Cpu(cpuVendor)], manufacturer: maker, model: model);
        return p with { System = p.System with { BoardManufacturer = board } };
    }

    [Theory]
    [InlineData("Micro-Star International Co., Ltd.", "AuthenticAMD", "msi.com")]
    [InlineData("MSI", "GenuineIntel", "msi.com")]
    [InlineData("ASUSTeK COMPUTER INC.", "GenuineIntel", "asus.com")]
    [InlineData("Gigabyte Technology Co., Ltd.", "AuthenticAMD", "gigabyte.com")]
    public void Custom_built_pcs_get_the_motherboard_makers_guide(string board, string cpu, string host)
    {
        var g = Data.Value.SecureBootGuides.For(Pc("System manufacturer", board, cpu));
        Assert.True(g.FromManufacturer);
        Assert.Equal("motherboard", g.MatchedBy);
        Assert.Contains(host, g.Url);
    }

    [Theory]
    [InlineData("Gigabyte Technology Co., Ltd.", "GenuineIntel")] // GIGABYTE's guide is for AMD boards only
    [InlineData("ASRock", "AuthenticAMD")]                        // no official general guide
    [InlineData(null, "AuthenticAMD")]
    public void Without_an_official_guide_the_general_guide_is_used(string? board, string cpu)
    {
        var g = Data.Value.SecureBootGuides.For(Pc("To Be Filled By O.E.M.", board, cpu));
        Assert.False(g.FromManufacturer);
        Assert.Equal("docs/secure-boot.md", g.LocalPath);
    }

    [Fact]
    public void The_pc_maker_wins_over_the_motherboard_maker()
    {
        var g = Data.Value.SecureBootGuides.For(Pc("Dell Inc.", "Micro-Star International Co., Ltd."));
        Assert.Contains("dell.com", g.Url);
        Assert.Null(g.MatchedBy);
    }

    [Fact]
    public void The_general_guide_never_links_a_placeholder_address()
    {
        var g = Data.Value.SecureBootGuides.For(Pc(null, null));
        if (GreyHavenInfo.IsGitHubRepositoryConfigured)
            Assert.Equal($"https://github.com/{GreyHavenInfo.GitHubRepository}/blob/main/docs/secure-boot.md", g.Url);
        else
            Assert.Null(g.Url);
    }

    [Fact]
    public void Every_guide_is_https()
    {
        var all = Data.Value.SecureBootGuides.Guides.Concat(Data.Value.SecureBootGuides.Motherboards).ToList();
        Assert.All(all, g => Assert.StartsWith("https://", g.Url));
        Assert.All(all, g => Assert.False(string.IsNullOrWhiteSpace(g.Checked), $"{g.Title}: say how the link was checked"));
    }
}
