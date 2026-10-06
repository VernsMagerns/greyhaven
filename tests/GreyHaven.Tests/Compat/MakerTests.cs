using GreyHaven.Compat;
using GreyHaven.Core.Hardware;
using static GreyHaven.Tests.Ratings.TestData;

namespace GreyHaven.Tests.Compat;

/// <summary>Boot menu keys and support pages from the shipped data/makers.yaml.</summary>
public class MakerTests
{
    private static readonly Lazy<CompatibilityData> Data = new(() => CompatibilityData.Load());

    private static SystemInfo Pc(string? maker, string? board = null, FormFactor form = FormFactor.Desktop, string? model = null) =>
        Profile([], formFactor: form, manufacturer: maker, model: model).System with { BoardManufacturer = board };

    [Fact]
    public void Custom_built_pc_uses_the_motherboard_maker()
    {
        var m = Data.Value.Makers.For(Pc("System manufacturer", "Micro-Star International Co., Ltd."))!;
        Assert.Equal("MSI", m.Name);
        Assert.Equal("F11", m.BootMenuKey);
        Assert.True(m.FromMotherboard);
    }

    [Theory]
    [InlineData("Dell Inc.", FormFactor.Laptop, "F12")]
    [InlineData("HP", FormFactor.Laptop, "Esc, then F9")]
    [InlineData("ASUSTeK COMPUTER INC.", FormFactor.Laptop, "Esc")]
    [InlineData("ASUSTeK COMPUTER INC.", FormFactor.Desktop, "F8")]
    public void Pc_maker_gives_the_boot_menu_key(string maker, FormFactor form, string key)
    {
        var m = Data.Value.Makers.For(Pc(maker, form: form))!;
        Assert.Equal(key, m.BootMenuKey);
        Assert.False(m.FromMotherboard);
        Assert.StartsWith("https://", m.SupportUrl);
    }

    [Fact]
    public void Surface_needs_a_surface_model() =>
        Assert.Contains("volume-down", Data.Value.Makers.For(Pc("Microsoft Corporation", model: "Surface Laptop 5"))!.BootMenuKey);

    [Fact]
    public void Unknown_maker_has_no_entry() =>
        Assert.Null(Data.Value.Makers.For(Pc("To Be Filled By O.E.M.", "Some Board Co.")));

    [Fact]
    public void Every_edition_knows_its_installer_size() =>
        Assert.All(Data.Value.Distros.Distros.SelectMany(d => d.Editions), e => Assert.True(e.InstallerBytes > 1_000_000_000, $"{e.Id}: installerBytes"));

    [Theory]
    [InlineData(6482409472, "about 6 GB")]
    [InlineData(3934945280, "about 4 GB")]
    public void Sizes_read_like_windows_shows_them(long bytes, string expected) =>
        Assert.Equal(expected, PlainWords.AboutGb(bytes));
}
