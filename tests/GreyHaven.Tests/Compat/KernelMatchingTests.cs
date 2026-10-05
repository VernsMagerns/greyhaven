using GreyHaven.Compat.Kernel;
using GreyHaven.Core.Hardware;

namespace GreyHaven.Tests.Compat;

public class GlobTests
{
    [Theory]
    [InlineData("abc", "abc", true)]
    [InlineData("a*c", "abbbc", true)]
    [InlineData("a*c", "ab", false)]
    [InlineData("a?c", "abc", true)]
    [InlineData("a?c", "ac", false)]
    [InlineData("*", "", true)]
    [InlineData("a[0-2]c", "a1c", true)]
    [InlineData("a[0-2]c", "a3c", false)]
    [InlineData("a[!0-2]c", "a3c", true)]
    [InlineData("a[^0-2]c", "a1c", false)]
    [InlineData("a[xyz]", "ay", true)]
    [InlineData("a[", "a[", false)] // unterminated class never matches
    [InlineData("pci:v00008086d00002723sv*sd*bc*sc*i*", "pci:v00008086d00002723sv00008086sd00000084bc02sc80i00", true)]
    [InlineData("usb:v0BDAp*d0[0-1]*dc*", "usb:v0BDAp5634d0100dcEF", true)]
    [InlineData("usb:v0BDAp*d0[0-1]*dc*", "usb:v0BDAp5634d0200dcEF", false)]
    public void Matches_like_fnmatch(string pattern, string text, bool expected) =>
        Assert.Equal(expected, Glob.IsMatch(pattern, text));
}

public class ModaliasTests
{
    [Fact]
    public void Pci_modalias_has_all_fields()
    {
        var id = new DeviceId(DeviceBus.Pci, "8086", "2723", "8086", "0084", ClassCode: "028000");
        Assert.Equal("pci:v00008086d00002723sv00008086sd00000084bc02sc80i00", Modalias.For(id));
    }

    [Fact]
    public void Unknown_fields_are_filled_with_Z_so_only_wildcards_match()
    {
        var id = new DeviceId(DeviceBus.Pci, "8086", "2723");
        var m = Modalias.For(id)!;
        Assert.Equal("pci:v00008086d00002723svZZZZZZZZsdZZZZZZZZbcZZscZZiZZ", m);
        Assert.True(Glob.IsMatch("pci:v00008086d00002723sv*sd*bc*sc*i*", m));
        Assert.False(Glob.IsMatch("pci:v00008086d00002723sv*sd*bc02sc*i*", m));
    }

    [Fact]
    public void Usb_modalias_includes_interface_class_and_revision()
    {
        var id = new DeviceId(DeviceBus.Usb, "8087", "0026", Revision: "0002", InterfaceClass: "e00101");
        Assert.Equal("usb:v8087p0026d0002dcZZdscZZdpZZicE0isc01ip01inZZ", Modalias.For(id));
    }

    [Fact]
    public void Other_buses()
    {
        Assert.Equal("hdaudio:v10EC0287rZZZZZZZZa01", Modalias.For(new DeviceId(DeviceBus.HdAudio, "10ec", "0287")));
        Assert.Equal("hid:bZZZZgZZZZv000006CBp0000CEC6", Modalias.For(new DeviceId(DeviceBus.Hid, "06cb", "cec6")));
        Assert.Equal("acpi:OVTI01A0:", Modalias.For(new DeviceId(DeviceBus.Acpi, "OVTI01A0", "")));
        Assert.True(Glob.IsMatch("acpi*:OVTI01A0:*", Modalias.For(new DeviceId(DeviceBus.Acpi, "OVTI01A0", ""))!));
    }
}

public class AliasIndexTests
{
    [Theory]
    [InlineData("pci:v00008086d00002723sv*sd*bc*sc*i*", true)]
    [InlineData("pci:v000010DEd*sv*sd*bc03sc*i*", false)]
    [InlineData("pci:v*d*sv*sd*bc0Csc03i30*", false)]
    [InlineData("usb:v8087p0026d*dc*dsc*dp*ic*isc*ip*in*", true)]
    [InlineData("usb:v*p*d*dc*dsc*dp*ic0Eisc01ip00in*", false)]
    [InlineData("hdaudio:v10EC0287r*a01*", true)]
    [InlineData("hdaudio:v10EC*r*a01*", false)]
    [InlineData("hid:b0018g*v000006CBp0000CEC6", true)]
    [InlineData("hid:b*g0004v*p*", false)]
    [InlineData("acpi*:OVTI01A0:*", true)]
    [InlineData("acpi*:*:*", false)]
    public void Device_specific_patterns_are_recognised(string pattern, bool specific) =>
        Assert.Equal(specific, new AliasEntry(pattern, "m").IsDeviceSpecific);

    [Fact]
    public void Write_then_load_round_trips_sorted_and_deduplicated()
    {
        var path = Path.Combine(Path.GetTempPath(), $"gh-test-{Guid.NewGuid():N}.idx.gz");
        try
        {
            AliasIndex.Write(path, new Dictionary<string, string> { ["id"] = "t", ["kind"] = "kernel", ["kernel"] = "7.0" },
            [
                new AliasEntry("usb:v8087p0026d*dc*dsc*dp*ic*isc*ip*in*", "btusb"),
                new AliasEntry("pci:v00008086d00002723sv*sd*bc*sc*i*", "iwlwifi"),
                new AliasEntry("pci:v00008086d00002723sv*sd*bc*sc*i*", "iwlwifi"),
                new AliasEntry("of:N*T*Cfoo", "ignored_bus"),
            ]);
            var index = AliasIndex.Load(path);

            Assert.Equal("t", index.Info.Id);
            Assert.Equal("7.0", index.Info.Get("kernel"));
            Assert.Equal(2, index.Count);
            Assert.Equal("iwlwifi", Assert.Single(index.Match(new DeviceId(DeviceBus.Pci, "8086", "2723"))).Target);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void Loading_a_non_index_file_fails_clearly()
    {
        using var ms = new MemoryStream();
        using (var gz = new System.IO.Compression.GZipStream(ms, System.IO.Compression.CompressionLevel.Fastest, leaveOpen: true))
            gz.Write("not an index\n"u8);
        ms.Position = 0;
        Assert.Throws<InvalidDataException>(() => AliasIndex.Load(ms));
    }
}
