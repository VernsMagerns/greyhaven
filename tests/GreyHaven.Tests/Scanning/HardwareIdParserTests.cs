using GreyHaven.Core.Hardware;
using GreyHaven.Core.Scanning;

namespace GreyHaven.Tests.Scanning;

public class HardwareIdParserTests
{
    [Fact]
    public void Pci_subsys_is_subdevice_then_subvendor()
    {
        var id = HardwareIdParser.ParseSingle(@"PCI\VEN_8086&DEV_2723&SUBSYS_00848086&REV_1A")!;

        Assert.Equal(DeviceBus.Pci, id.Bus);
        Assert.Equal("8086", id.Vendor);
        Assert.Equal("2723", id.Device);
        Assert.Equal("8086", id.SubVendor);
        Assert.Equal("0084", id.SubDevice);
        Assert.Equal("1a", id.Revision);
        Assert.Equal("pci:8086:2723", id.Key);
        Assert.Equal("pci:8086:2723:8086:0084", id.FullKey);
    }

    [Fact]
    public void Pci_parse_prefers_id_with_subsystem_and_picks_up_class_code()
    {
        var id = HardwareIdParser.Parse(
            [@"PCI\VEN_8086&DEV_A0F0&CC_028000", @"PCI\VEN_8086&DEV_A0F0&SUBSYS_00748086&REV_20"],
            [@"PCI\VEN_8086&DEV_A0F0", @"PCI\CC_0280", @"PCI\CC_028000"])!;

        Assert.Equal("0074", id.SubDevice);
        Assert.Equal("028000", id.ClassCode);
        Assert.Equal("02", id.BaseClass);
        Assert.Equal("0280", id.ClassAndSubclass);
    }

    [Fact]
    public void Usb_ids_and_interfaces()
    {
        var id = HardwareIdParser.ParseSingle(@"USB\VID_8087&PID_0026&REV_0002&MI_00")!;

        Assert.Equal(DeviceBus.Usb, id.Bus);
        Assert.Equal("8087:0026", id.VendorDevice);
        Assert.True(HardwareIdParser.IsUsbInterface([@"USB\VID_8087&PID_0026&REV_0002&MI_00"]));
        Assert.False(HardwareIdParser.IsUsbInterface([@"USB\VID_8087&PID_0026&REV_0002"]));
    }

    [Fact]
    public void HdAudio_subsys_is_subvendor_then_subdevice()
    {
        var id = HardwareIdParser.ParseSingle(@"HDAUDIO\FUNC_01&VEN_10EC&DEV_0287&SUBSYS_17AA22D8&REV_1000")!;

        Assert.Equal(DeviceBus.HdAudio, id.Bus);
        Assert.Equal("10ec:0287", id.VendorDevice);
        Assert.Equal("17aa", id.SubVendor);
        Assert.Equal("22d8", id.SubDevice);
    }

    [Theory]
    [InlineData(@"HID\VEN_06CB&DEV_CEC6&Col02", "06cb:cec6")]
    [InlineData(@"HID\VID_046D&PID_C52B&MI_00&Col01", "046d:c52b")]
    public void Hid_ids(string raw, string expected)
    {
        var id = HardwareIdParser.ParseSingle(raw)!;
        Assert.Equal(DeviceBus.Hid, id.Bus);
        Assert.Equal(expected, id.VendorDevice);
    }

    [Theory]
    [InlineData(@"ACPI\QCOM0C36", "QCOM0C36")]
    [InlineData(@"ACPI\VEN_QCOM&DEV_0C36", "QCOM0C36")]
    [InlineData(@"ACPI\SYN1E5A", "SYN1E5A")]
    [InlineData(@"acpi\pnp0c50", "PNP0C50")]
    public void Acpi_ids_are_normalised(string raw, string expected)
    {
        var id = HardwareIdParser.ParseSingle(raw)!;
        Assert.Equal(DeviceBus.Acpi, id.Bus);
        Assert.Equal(expected, id.VendorDevice);
        Assert.Equal($"acpi:{expected}", id.Key);
    }

    [Theory]
    [InlineData(@"ROOT\BasicRender")]
    [InlineData(@"SWD\GenericRaw")]
    [InlineData(@"BTHENUM\{0000111e-0000-1000-8000-00805f9b34fb}_VID&0002004c_PID&2006")]
    [InlineData(@"USB\ROOT_HUB30&VID8086&PIDA0ED&REV0020")]
    [InlineData(@"ACPI\FixedButton")]
    [InlineData("")]
    public void Unrated_buses_return_null(string raw)
    {
        Assert.Null(HardwareIdParser.ParseSingle(raw));
    }

    [Fact]
    public void Ids_are_lower_cased_for_matching()
    {
        var id = HardwareIdParser.ParseSingle(@"pci\ven_10DE&dev_2484")!;
        Assert.Equal("pci:10de:2484", id.Key);
    }
}
