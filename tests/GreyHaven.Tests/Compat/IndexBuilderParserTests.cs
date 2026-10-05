using GreyHaven.IndexBuilder;

namespace GreyHaven.Tests.Compat;

public class IndexBuilderParserTests
{
    [Fact]
    public void Package_modaliases_are_split_per_pattern_and_keyed_by_package()
    {
        const string index = """
            Package: nvidia-driver-610
            Version: 610.10-0ubuntu1
            Modaliases: nvidia(pci:v000010DEd00002484sv*sd*bc03sc*i*, pci:v000010DEd00002486sv*sd*bc03sc*i*)

            Package: unrelated
            Version: 1.0

            Package: two-modules
            Modaliases: a(usb:v1234p5678d*dc*dsc*dp*ic*isc*ip*in*), b(pci:v00001234d00005678sv*sd*bc*sc*i*)
            """;
        var entries = PackageIndexReader.ReadModaliases(new StringReader(index)).ToList();

        Assert.Equal(4, entries.Count);
        Assert.Equal(2, entries.Count(e => e.Target == "nvidia-driver-610"));
        Assert.Contains(entries, e => e.Target == "two-modules" && e.Pattern.StartsWith("usb:v1234p5678"));
        Assert.DoesNotContain(entries, e => e.Target == "unrelated");
    }

    [Fact]
    public void Libfprint_hwdb_sections_become_driver_or_unsupported_targets()
    {
        const string hwdb = """
            # SPDX-License-Identifier: LGPL-2.1-or-later
            # This file has been generated using fprint-list-udev-hwdb with all drivers enabled

            # Supported by libfprint driver goodixmoc
            usb:v27C6p633C*
            usb:v27C6p63AC*
             ID_AUTOSUSPEND=1
             ID_PERSIST=0

            # Known unsupported devices
            usb:v04F3p0C4C*
             ID_AUTOSUSPEND=1
            """;
        var entries = FingerprintHwdbReader.Read(new StringReader(hwdb)).ToList();

        Assert.Equal(3, entries.Count);
        Assert.Equal("libfprint:goodixmoc", entries[0].Target);
        Assert.Equal("usb:v27C6p633C*", entries[0].Pattern);
        Assert.Equal(FingerprintHwdbReader.Unsupported, entries[2].Target);
    }
}
