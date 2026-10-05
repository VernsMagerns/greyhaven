using System.Reflection;
using GreyHaven.Core.Hardware;
using GreyHaven.Core.Scanning;
using GreyHaven.Core.Serialization;

namespace GreyHaven.Tests.Scanning;

/// <summary>The bundled samples are the app's demo data; make sure they stay valid and meaningful.</summary>
public class SampleProfileTests
{
    private static Task<HardwareProfile> Load(string name) => SampleProfiles.Open(name).ScanAsync();

    [Fact]
    public void All_expected_samples_are_present()
    {
        Assert.Equal(
            ["broadcom-legacy-laptop", "intel-laptop", "intel-vmd-laptop", "nvidia-desktop", "snapdragon-arm-laptop"],
            SampleProfiles.List());
    }

    [Theory]
    [MemberData(nameof(SampleNames))]
    public async Task Every_sample_builds_with_ids_on_every_device(string name)
    {
        var p = await Load(name);

        Assert.Equal($"sample:{name}", p.Source);
        Assert.False(string.IsNullOrWhiteSpace(p.Description));
        Assert.Contains(p.Components, c => c.Category == ComponentCategory.Cpu);
        Assert.Contains(p.Components, c => c.Category == ComponentCategory.Gpu);
        Assert.All(p.Components.Where(c => c.Category != ComponentCategory.Cpu), c => Assert.NotNull(c.Id));
        // Noise entries (root hubs, software devices, PS/2 keyboard, USB stick) must be filtered out.
        Assert.DoesNotContain(p.Components, c => c.Name.Contains("Root Hub") || c.Name.Contains("Mass Storage") || c.Name.Contains("PS/2 Keyboard"));
    }

    public static TheoryData<string> SampleNames => new(SampleProfiles.List());

    [Fact]
    public async Task Intel_laptop()
    {
        var p = await Load("intel-laptop");
        Assert.Equal(StorageControllerMode.Nvme, p.System.StorageMode);
        Assert.Equal(BitLockerState.On, p.System.BitLocker);
        Assert.True(p.System.Windows.IsWindows11);
        Assert.Single(p.Components, c => c.Category == ComponentCategory.Bluetooth); // interface entry merged
        Assert.Single(p.Components, c => c.Category == ComponentCategory.Touchpad);
        Assert.Single(p.Components, c => c.Category == ComponentCategory.FingerprintReader);
    }

    [Fact]
    public async Task Nvidia_desktop()
    {
        var p = await Load("nvidia-desktop");
        Assert.Equal(FormFactor.Desktop, p.System.FormFactor);
        Assert.Equal(SecureBootState.Enabled, p.System.SecureBoot);
        Assert.Equal("10de", Assert.Single(p.Components, c => c.Category == ComponentCategory.Gpu).Id!.Vendor);
        Assert.Equal(ComponentCategory.Webcam, Assert.Single(p.Components, c => c.Id?.Key == "usb:046d:082d").Category);
    }

    [Fact]
    public async Task Vmd_laptop()
    {
        var p = await Load("intel-vmd-laptop");
        Assert.Equal(StorageControllerMode.IntelVmd, p.System.StorageMode);
        Assert.Equal(BitLockerState.WaitingForActivation, p.System.BitLocker);
        var fp = Assert.Single(p.Components, c => c.Category == ComponentCategory.FingerprintReader);
        Assert.Equal(28, fp.WindowsProblemCode);
        Assert.Equal(2, p.Components.Count(c => c.Category == ComponentCategory.Webcam)); // IPU6 + sensor
    }

    [Fact]
    public async Task Legacy_broadcom_laptop()
    {
        var p = await Load("broadcom-legacy-laptop");
        Assert.Equal(FirmwareMode.LegacyBios, p.System.Firmware);
        Assert.Equal(SecureBootState.NotSupported, p.System.SecureBoot);
        Assert.Equal("pci:14e4:4365", Assert.Single(p.Components, c => c.Category == ComponentCategory.Wifi).Id!.Key);
        Assert.Equal(DeviceBus.Acpi, Assert.Single(p.Components, c => c.Category == ComponentCategory.Touchpad).Id!.Bus);
    }

    [Fact]
    public async Task Snapdragon_laptop()
    {
        var p = await Load("snapdragon-arm-laptop");
        Assert.True(p.System.IsArm);
        Assert.Contains(p.Warnings, w => w.Contains("ARM"));
        Assert.Equal(DeviceBus.Acpi, Assert.Single(p.Components, c => c.Category == ComponentCategory.Gpu).Id!.Bus);
    }

    [Theory]
    [MemberData(nameof(SampleNames))]
    public async Task Profile_json_round_trips(string name)
    {
        var p = await Load(name);
        var json = GreyHavenJson.Serialize(p);
        var back = GreyHavenJson.Deserialize<HardwareProfile>(json)!;

        Assert.Equal(json, GreyHavenJson.Serialize(back));
        Assert.Contains("\"category\": \"cpu\"", json); // enums serialise as camelCase strings
    }

    /// <summary>
    /// Guards the privacy promise: the raw snapshot and profile models must never grow a field
    /// for serial numbers, instance paths, MAC addresses, host or user names.
    /// </summary>
    [Theory]
    [InlineData(typeof(RawSnapshot))]
    [InlineData(typeof(HardwareProfile))]
    public void Models_have_no_identifying_fields(Type root)
    {
        string[] forbidden = ["serial", "instance", "deviceid", "pnpdeviceid", "mac", "hostname", "computername", "username", "uuid", "processorid"];
        var seen = new HashSet<Type>();
        var offenders = new List<string>();

        void Walk(Type t)
        {
            if (!seen.Add(t) || t.Namespace?.StartsWith("GreyHaven") != true) return;
            foreach (var prop in t.GetProperties(BindingFlags.Public | BindingFlags.Instance))
            {
                var n = prop.Name.ToLowerInvariant();
                if (forbidden.Any(f => n == f || n.Contains(f) && f.Length > 3)) offenders.Add($"{t.Name}.{prop.Name}");
                var pt = prop.PropertyType;
                Walk(Nullable.GetUnderlyingType(pt) ?? pt);
                foreach (var arg in pt.GetGenericArguments()) Walk(arg);
            }
        }

        Walk(root);
        Assert.Empty(offenders);
    }
}
