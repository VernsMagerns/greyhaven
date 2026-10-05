using GreyHaven.Core.Hardware;
using GreyHaven.Core.Scanning;

namespace GreyHaven.Tests.Scanning;

public class ComponentClassifierTests
{
    private static ComponentCategory? Classify(string pnpClass, string[] hw, string[]? compat = null)
    {
        var dev = new RawPnpDevice { PnpClass = pnpClass, HardwareIds = hw, CompatibleIds = compat ?? [] };
        var id = HardwareIdParser.Parse(dev.HardwareIds, dev.CompatibleIds);
        Assert.NotNull(id);
        return ComponentClassifier.Classify(dev, id);
    }

    private static string[] Pci(string vd, string cc) => [$@"PCI\VEN_{vd[..4]}&DEV_{vd[5..]}&SUBSYS_00000000", $@"PCI\CC_{cc}"];

    [Theory]
    [InlineData("Display", "8086:9A49", "030000", ComponentCategory.Gpu)]
    [InlineData("Display", "10DE:2484", "030200", ComponentCategory.Gpu)]
    [InlineData("Net", "8086:2723", "028000", ComponentCategory.Wifi)]
    [InlineData("Net", "10EC:8125", "020000", ComponentCategory.Ethernet)]
    [InlineData("HDC", "1022:7901", "010601", ComponentCategory.StorageController)]
    [InlineData("SCSIAdapter", "8086:467F", "010400", ComponentCategory.StorageController)]
    [InlineData("SCSIAdapter", "144D:A80A", "010802", ComponentCategory.NvmeDrive)]
    [InlineData("MEDIA", "1022:1487", "040300", ComponentCategory.AudioController)]
    [InlineData("System", "8086:A0C8", "040100", ComponentCategory.AudioController)]
    [InlineData("System", "8086:465D", "048000", ComponentCategory.Webcam)] // Intel IPU6
    [InlineData("USB", "8086:A0ED", "0c0330", ComponentCategory.UsbController)]
    [InlineData("System", "8086:463E", "0c0340", ComponentCategory.Thunderbolt)]
    [InlineData("MTD", "10EC:5289", "ff0000", ComponentCategory.CardReader)]
    [InlineData("System", "8086:A0E0", "078000", ComponentCategory.Chipset)]
    public void Pci_devices(string pnpClass, string vd, string cc, ComponentCategory expected)
    {
        Assert.Equal(expected, Classify(pnpClass, Pci(vd, cc)));
    }

    [Theory]
    [InlineData("Bluetooth", "e0", ComponentCategory.Bluetooth)]
    [InlineData("Camera", "0e", ComponentCategory.Webcam)]
    [InlineData("Biometric", "ff", ComponentCategory.FingerprintReader)]
    [InlineData("Net", "ff", ComponentCategory.UsbNetwork)]
    [InlineData("HIDClass", "03", ComponentCategory.InputDevice)]
    [InlineData("USB", "e0", ComponentCategory.Bluetooth)] // no driver-specific class, use USB class
    public void Usb_devices(string pnpClass, string usbClass, ComponentCategory expected)
    {
        Assert.Equal(expected, Classify(pnpClass, [@"USB\VID_1234&PID_5678"], [$@"USB\Class_{usbClass}"]));
    }

    [Theory]
    [InlineData("08")] // mass storage
    [InlineData("09")] // hub
    public void Usb_sticks_and_hubs_are_ignored(string usbClass)
    {
        Assert.Null(Classify("USB", [@"USB\VID_0781&PID_5583"], [$@"USB\Class_{usbClass}"]));
    }

    [Fact]
    public void Driverless_fingerprint_reader_is_recognised_by_vendor()
    {
        Assert.Equal(ComponentCategory.FingerprintReader, Classify("", [@"USB\VID_27C6&PID_633C"]));
    }

    [Theory]
    [InlineData("HID_DEVICE_UP:000D_U:0005", ComponentCategory.Touchpad)]
    [InlineData("HID_DEVICE_UP:000D_U:0004", ComponentCategory.Touchscreen)]
    public void Hid_touch_devices_by_usage(string usage, ComponentCategory expected)
    {
        Assert.Equal(expected, Classify("HIDClass", [@"HID\VEN_06CB&DEV_CEC6&Col02"], [usage]));
    }

    [Fact]
    public void Other_hid_collections_are_ignored()
    {
        Assert.Null(Classify("Mouse", [@"HID\VEN_06CB&DEV_CEC6&Col01"], ["HID_DEVICE_SYSTEM_MOUSE"]));
    }

    [Fact]
    public void Hd_audio_codecs_and_hdmi_audio()
    {
        Assert.Equal(ComponentCategory.AudioCodec, Classify("MEDIA", [@"HDAUDIO\FUNC_01&VEN_10EC&DEV_0287&SUBSYS_17AA22D8"]));
        Assert.Equal(ComponentCategory.DisplayAudio, Classify("MEDIA", [@"HDAUDIO\FUNC_01&VEN_10DE&DEV_009A&SUBSYS_14623907"]));
        Assert.Equal(ComponentCategory.Other, Classify("MEDIA", [@"HDAUDIO\FUNC_02&VEN_14F1&DEV_2C06&SUBSYS_00000000"]));
    }

    [Theory]
    [InlineData(@"ACPI\SYN1E5A", "Mouse", ComponentCategory.Touchpad)]
    [InlineData(@"ACPI\VEN_QCOM&DEV_0C36", "Display", ComponentCategory.Gpu)]
    [InlineData(@"ACPI\OVTI01A0", "Camera", ComponentCategory.Webcam)]
    public void Acpi_devices_of_interest(string hw, string pnpClass, ComponentCategory expected)
    {
        Assert.Equal(expected, Classify(pnpClass, [hw]));
    }

    [Theory]
    [InlineData(@"ACPI\PNP0F13", "Mouse")] // generic PS/2 mouse, not a known touchpad maker
    [InlineData(@"ACPI\PNP0303", "Keyboard")]
    [InlineData(@"ACPI\ACPI0003", "Battery")]
    public void Acpi_platform_devices_are_ignored(string hw, string pnpClass)
    {
        Assert.Null(Classify(pnpClass, [hw]));
    }
}
