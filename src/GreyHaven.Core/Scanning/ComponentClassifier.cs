using GreyHaven.Core.Hardware;

namespace GreyHaven.Core.Scanning;

/// <summary>
/// Decides what kind of component a PnP device is, from its bus, class codes and Windows setup
/// class. Returns null for devices GreyHaven doesn't report (hubs, USB sticks, HID collections
/// that aren't touch devices, most ACPI platform devices).
/// </summary>
public static class ComponentClassifier
{
    // HID usage page 0x0D (Digitizer): usage 0x05 = touch pad, 0x04 = touch screen.
    private const string HidTouchpadUsage = "HID_DEVICE_UP:000D_U:0005";
    private const string HidTouchscreenUsage = "HID_DEVICE_UP:000D_U:0004";

    public static ComponentCategory? Classify(RawPnpDevice device, DeviceId id)
    {
        var cls = device.PnpClass?.Trim().ToLowerInvariant() ?? "";
        return id.Bus switch
        {
            DeviceBus.Pci => ClassifyPci(cls, id),
            DeviceBus.Usb => ClassifyUsb(cls, id, device),
            DeviceBus.HdAudio => ClassifyHdAudio(id, device),
            DeviceBus.Hid => ClassifyHid(device),
            DeviceBus.Acpi => ClassifyAcpi(cls, id),
            _ => null,
        };
    }

    private static ComponentCategory ClassifyPci(string cls, DeviceId id)
    {
        if (cls is "camera" or "image") return ComponentCategory.Webcam;
        if (cls == "biometric") return ComponentCategory.FingerprintReader;
        if (id.Vendor == "8086" && KnownDeviceIds.IntelIpuDevices.Contains(id.Device)) return ComponentCategory.Webcam;
        if (cls is "sdhost" or "mtd") return ComponentCategory.CardReader;

        // Programming interface 0x40 on a USB controller = USB4 host interface (Thunderbolt-class).
        if (id.ClassCode == "0c0340") return ComponentCategory.Thunderbolt;

        return id.ClassAndSubclass switch
        {
            "0280" => ComponentCategory.Wifi,
            "0108" => ComponentCategory.NvmeDrive,
            "0401" or "0403" => ComponentCategory.AudioController,
            "0480" when cls == "media" => ComponentCategory.AudioController,
            "0c03" => ComponentCategory.UsbController,
            "0c0a" => ComponentCategory.Thunderbolt,
            "0805" => ComponentCategory.CardReader,
            "0d11" => ComponentCategory.Bluetooth,
            _ => id.BaseClass switch
            {
                "03" => ComponentCategory.Gpu,
                "02" => ComponentCategory.Ethernet,
                "01" => ComponentCategory.StorageController,
                null when cls == "display" => ComponentCategory.Gpu,
                null when cls == "net" => ComponentCategory.Ethernet,
                null when cls is "scsiadapter" or "hdc" => ComponentCategory.StorageController,
                _ => ComponentCategory.Chipset,
            },
        };
    }

    private static ComponentCategory? ClassifyUsb(string cls, DeviceId id, RawPnpDevice device)
    {
        switch (cls)
        {
            case "bluetooth": return ComponentCategory.Bluetooth;
            case "camera" or "image": return ComponentCategory.Webcam;
            case "biometric": return ComponentCategory.FingerprintReader;
            case "net": return ComponentCategory.UsbNetwork;
            case "diskdrive" or "wpd" or "printer" or "printqueue" or "volume": return null;
        }

        if (KnownDeviceIds.FingerprintOnlyUsbVendors.Contains(id.Vendor)) return ComponentCategory.FingerprintReader;

        return HardwareIdParser.FindUsbClass(device.CompatibleIds) switch
        {
            "e0" => ComponentCategory.Bluetooth,
            "0e" => ComponentCategory.Webcam,
            "01" => ComponentCategory.AudioController,
            "03" => ComponentCategory.InputDevice,
            "08" or "09" => null, // mass storage, hubs
            _ => cls switch
            {
                "media" => ComponentCategory.AudioController,
                "mouse" or "keyboard" or "hidclass" => ComponentCategory.InputDevice,
                _ => ComponentCategory.Other,
            },
        };
    }

    private static ComponentCategory? ClassifyHdAudio(DeviceId id, RawPnpDevice device)
    {
        if (HardwareIdParser.FindHdAudioFunction(device.HardwareIds) == "02") return ComponentCategory.Other; // modem
        return KnownDeviceIds.DisplayAudioCodecVendors.Contains(id.Vendor)
            ? ComponentCategory.DisplayAudio
            : ComponentCategory.AudioCodec;
    }

    private static ComponentCategory? ClassifyHid(RawPnpDevice device)
    {
        var ids = device.HardwareIds.Concat(device.CompatibleIds);
        foreach (var id in ids)
        {
            if (id.Equals(HidTouchpadUsage, StringComparison.OrdinalIgnoreCase)) return ComponentCategory.Touchpad;
            if (id.Equals(HidTouchscreenUsage, StringComparison.OrdinalIgnoreCase)) return ComponentCategory.Touchscreen;
        }
        return null;
    }

    // ACPI devices are mostly platform plumbing; only report the classes that matter for Linux
    // support. On ARM PCs (e.g. Snapdragon) the GPU, audio and camera are ACPI-enumerated.
    private static ComponentCategory? ClassifyAcpi(string cls, DeviceId id) => cls switch
    {
        "mouse" when KnownDeviceIds.AcpiTouchpadPrefixes.Any(p => id.Vendor.StartsWith(p, StringComparison.OrdinalIgnoreCase))
            => ComponentCategory.Touchpad,
        "display" => ComponentCategory.Gpu,
        "net" => ComponentCategory.Wifi,
        "bluetooth" => ComponentCategory.Bluetooth,
        "camera" or "image" => ComponentCategory.Webcam,
        "biometric" => ComponentCategory.FingerprintReader,
        "media" => ComponentCategory.AudioController,
        _ => null,
    };
}
