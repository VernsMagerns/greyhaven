using GreyHaven.Core.Hardware;
using static GreyHaven.Tests.Ratings.TestData;

namespace GreyHaven.Tests.Scanning;

public class ComponentNamesTests
{
    [Fact]
    public void Unique_names_are_unchanged()
    {
        var names = ComponentNames.Distinct([
            Pci("8086:2723", "028000", ComponentCategory.Wifi, name: "Intel Wi-Fi"),
            Pci("10ec:8125", "020000", ComponentCategory.Ethernet, name: "Realtek 2.5GbE"),
        ]);
        Assert.Equal(["Intel Wi-Fi", "Realtek 2.5GbE"], names);
    }

    [Fact]
    public void Same_name_different_makers_get_the_maker()
    {
        var names = ComponentNames.Distinct([
            Pci("1022:1487", "040300", ComponentCategory.AudioController, name: "HD Audio Controller"),
            Pci("10de:228b", "040300", ComponentCategory.AudioController, name: "HD Audio Controller"),
        ]);
        Assert.Equal(["HD Audio Controller (AMD)", "HD Audio Controller (NVIDIA)"], names);
    }

    [Fact]
    public void Same_name_same_maker_get_numbered()
    {
        var names = ComponentNames.Distinct([
            Pci("1022:149c", "0c0330", ComponentCategory.UsbController, name: "USB controller"),
            Pci("8086:2723", "028000", ComponentCategory.Wifi, name: "USB controller"), // different kind: unaffected
            Pci("1022:43ee", "0c0330", ComponentCategory.UsbController, name: "USB controller"),
        ]);
        Assert.Equal(["USB controller (1 of 2)", "USB controller", "USB controller (2 of 2)"], names);
    }

    [Fact]
    public void Unknown_maker_falls_back_to_numbers()
    {
        var names = ComponentNames.Distinct([
            Pci("1022:1487", "040300", ComponentCategory.AudioController, name: "Audio"),
            Pci("abcd:0001", "040300", ComponentCategory.AudioController, name: "Audio"),
        ]);
        Assert.Equal(["Audio (1 of 2)", "Audio (2 of 2)"], names);
    }
}
