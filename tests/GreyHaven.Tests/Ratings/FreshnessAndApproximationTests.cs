using System.Net;
using GreyHaven.Compat.Data;
using GreyHaven.Compat.Kernel;
using GreyHaven.Compat.Online;
using GreyHaven.Core.Compatibility;
using GreyHaven.Core.Hardware;
using GreyHaven.Ratings;
using Microsoft.Extensions.Time.Testing;
using static GreyHaven.Tests.Ratings.TestData;

namespace GreyHaven.Tests.Ratings;

public class ApproximationTests
{
    private static readonly RatingOptions Offline = new() { UseOnlineLookups = false };
    private static readonly HardwareComponent Usb3 = Pci("8086:a0ed", "0c0330", ComponentCategory.UsbController);

    private static async Task<ComponentRating> Rate(GreyHaven.Compat.CompatibilityData data, string distro = "alpha")
    {
        var report = await new RatingEngine(data).RateAsync(Profile([Cpu(), Usb3]), Offline);
        return report.Components.Single(c => c.Component == Usb3).For(distro);
    }

    [Fact]
    public async Task Exact_data_keeps_high_confidence()
    {
        var r = await Rate(Build(alphaKernel: [("pci:v*d*sv*sd*bc0Csc03i30*", "xhci_pci")]));
        Assert.Equal(Confidence.High, r.Confidence);
        Assert.Null(r.ApproximationNote);
    }

    [Fact]
    public async Task Approximated_kernel_caps_confidence_at_medium()
    {
        var r = await Rate(Build(alphaKernel: [("pci:v*d*sv*sd*bc0Csc03i30*", "xhci_pci")], alphaKernelApproximate: true));
        Assert.Equal(Rating.WorksOutOfTheBox, r.Rating);
        Assert.Equal(Confidence.Medium, r.Confidence);
        Assert.Contains("upstream kernel", r.ApproximationNote);
    }

    [Fact]
    public async Task Borrowed_patterns_cap_confidence_at_medium()
    {
        var r = await Rate(Build(alphaKernel: [("pci:v*d*sv*sd*bc0Csc03i30*", "xhci_pci|borrowed:ref-7.0")]));
        Assert.Equal(Confidence.Medium, r.Confidence);
        Assert.Contains("general pattern from a newer kernel", r.ApproximationNote);
    }

    [Fact]
    public async Task Approximation_never_raises_confidence()
    {
        // uvcvideo has no module confidence, so it's medium already; approximation leaves it there.
        var cam = Usb("04f2:b6cb", ComponentCategory.Webcam, "0e0100");
        var data = Build(alphaKernel: [("usb:v*p*d*dc*dsc*dp*ic0Eisc01ip00in*", "uvcvideo")], alphaKernelApproximate: true);
        var report = await new RatingEngine(data).RateAsync(Profile([Cpu(), cam]), Offline);
        Assert.Equal(Confidence.Medium, report.Components.Single(c => c.Component == cam).For("alpha").Confidence);
    }

    [Fact]
    public void Provenance_survives_write_and_load()
    {
        var path = Path.Combine(Path.GetTempPath(), $"gh-prov-{Guid.NewGuid():N}.idx.gz");
        try
        {
            AliasIndex.Write(path, new Dictionary<string, string> { ["id"] = "t", ["kind"] = "kernel" },
            [
                new AliasEntry("pci:v*d*sv*sd*bc0Csc03i30*", "xhci_pci", "borrowed:ref"),
                new AliasEntry("pci:v00008086d00002723sv*sd*bc*sc*i*", "iwlwifi"),
            ]);
            var entries = AliasIndex.Load(path).Entries.ToList();
            Assert.Equal("borrowed:ref", entries.Single(e => e.Target == "xhci_pci").Provenance);
            Assert.Null(entries.Single(e => e.Target == "iwlwifi").Provenance);
        }
        finally
        {
            File.Delete(path);
        }
    }
}

public class FreshnessTests
{
    private static readonly DateOnly Built = new(2026, 10, 1);

    [Fact]
    public void Fresh_data_has_no_notice()
    {
        var f = Freshness.Evaluate(Built, Built.AddDays(30), new ReleaseCheckResult([], []));
        Assert.Equal(30, f.AgeDays);
        Assert.Null(f.Notice);
        Assert.True(f.ReleasesChecked);
    }

    [Fact]
    public void Data_older_than_90_days_asks_for_an_update()
    {
        Assert.Null(Freshness.Evaluate(Built, Built.AddDays(90), null).Notice);
        var f = Freshness.Evaluate(Built, Built.AddDays(91), null);
        Assert.Contains("1 October 2026", f.Notice);
        Assert.Contains("Check for a GreyHaven update", f.Notice);
        Assert.False(f.ReleasesChecked);
    }

    [Fact]
    public void Newer_release_asks_for_an_update_even_when_data_is_new()
    {
        var newer = new NewerRelease("mint", "Linux Mint", "22.3", "23", null);
        var f = Freshness.Evaluate(Built, Built.AddDays(2), new ReleaseCheckResult([newer], []));
        Assert.Contains("Linux Mint release (23)", f.Notice);
        Assert.True(f.IsStale);
    }

    private sealed class FakeReleases(params NewerRelease[] newer) : IDistroReleaseChecker
    {
        public int Calls { get; private set; }

        public Task<ReleaseCheckResult> CheckAsync(DistroCatalog catalog, CancellationToken cancellationToken = default)
        {
            Calls++;
            return Task.FromResult(new ReleaseCheckResult(newer, []));
        }
    }

    [Fact]
    public async Task Engine_reports_build_date_and_only_checks_releases_when_online()
    {
        var releases = new FakeReleases(new NewerRelease("alpha", "Alpha Linux", "1", "2", null));
        var time = new FakeTimeProvider(new DateTimeOffset(2026, 10, 11, 0, 0, 0, TimeSpan.Zero));
        var engine = new RatingEngine(Build(builtOn: "2026-10-01"), community: null, releases, time);

        var offline = await engine.RateAsync(Profile([Cpu()]), new RatingOptions { UseOnlineLookups = false });
        Assert.Equal(new DateOnly(2026, 10, 1), offline.Data.BuiltOn);
        Assert.Equal(10, offline.Data.AgeDays);
        Assert.Null(offline.Data.Notice);
        Assert.Equal(0, releases.Calls);

        var online = await engine.RateAsync(Profile([Cpu()]), new RatingOptions { UseOnlineLookups = true });
        Assert.Equal(1, releases.Calls);
        Assert.Contains("Alpha Linux release (2)", online.Data.Notice);
    }
}

public class OnlineNoteTests
{
    private sealed class Unavailable : ICommunityLookup
    {
        public Task<CommunityResult> LookupAsync(DeviceId id, CancellationToken cancellationToken = default) =>
            Task.FromResult(new CommunityResult { Status = CommunityStatus.Unavailable, Reason = "linux-hardware.org could not be reached." });
    }

    [Fact]
    public async Task Unavailable_online_check_is_noted_once_per_report_not_per_component()
    {
        var unknowns = Enumerable.Range(1, 4).Select(i => Pci($"eeee:000{i}", "ff0000", ComponentCategory.Other)).ToList();
        var report = await new RatingEngine(Build(), new Unavailable()).RateAsync(Profile([Cpu(), .. unknowns]), new RatingOptions());

        Assert.Single(report.DataNotes, n => n.Contains("could not be reached"));
        Assert.All(report.Components.SelectMany(c => c.Ratings), r => Assert.DoesNotContain("could not be reached", r.Explanation));
    }
}

public class DistroReleaseCheckerTests
{
    [Fact]
    public void Ubuntu_meta_release_picks_latest_supported()
    {
        const string feed = """
            Dist: noble
            Name: Noble Numbat
            Version: 24.04.5 LTS
            Supported: 1

            Dist: resolute
            Name: Resolute Raccoon
            Version: 26.04.2 LTS
            Supported: 1

            Dist: future
            Version: 28.04 LTS
            Supported: 0
            """;
        Assert.Equal("26.04.2", DistroReleaseChecker.ParseUbuntuMetaRelease(feed));
    }

    [Fact]
    public void Directory_listing_versions()
    {
        const string html = """<a href="22.2/">22.2/</a> <a href="22.3/">22.3/</a> <a href="23/">23/</a> <a href="../">../</a>""";
        Assert.Equal(["22.2", "22.3", "23"], DistroReleaseChecker.ParseDirectoryVersions(html));
    }

    [Theory]
    [InlineData("23", "22.3", 1)]
    [InlineData("26.04.1 LTS", "26.04.1", 0)]
    [InlineData("26.04.2", "26.04.1 LTS", 1)]
    [InlineData("26.04", "26.04.1", -1)]
    [InlineData("22.10", "22.9", 1)]
    public void Versions_compare_numerically(string a, string b, int sign) =>
        Assert.Equal(sign, Math.Sign(DistroReleaseChecker.CompareVersions(a, b)));

    private sealed class Handler(Func<string, (HttpStatusCode, string)> respond) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var (code, body) = respond(request.RequestUri!.ToString());
            return Task.FromResult(new HttpResponseMessage(code) { Content = new StringContent(body) });
        }
    }

    private static DistroCatalog Catalog() => new()
    {
        Distros =
        [
            new DistroInfo { Id = "ubuntu", Name = "Ubuntu", Version = "26.04.1 LTS", ReleaseCheck = new ReleaseCheck { Kind = "ubuntuMetaRelease", Url = "https://u.test/meta" } },
            new DistroInfo { Id = "mint", Name = "Linux Mint", Version = "22.3", ReleaseCheck = new ReleaseCheck { Kind = "directoryListing", Url = "https://m.test/stable/" } },
            new DistroInfo
            {
                Id = "pop-os", Name = "Pop!_OS", Version = "24.04", ReleaseCheck = new ReleaseCheck { Kind = "popApi", Url = "https://p.test/builds/" },
                Editions =
                [
                    new DistroEdition { Id = "intel", Name = "Intel/AMD ISO", IsoBuild = 20 },
                    new DistroEdition { Id = "nvidia", Name = "NVIDIA ISO", IsoBuild = 28, SelectWhen = "nvidiaGpu" },
                ],
            },
        ],
    };

    [Fact]
    public async Task Finds_newer_releases_from_each_source()
    {
        var handler = new Handler(url => url switch
        {
            "https://u.test/meta" => (HttpStatusCode.OK, "Version: 26.04.1 LTS\nSupported: 1\n"),
            "https://m.test/stable/" => (HttpStatusCode.OK, """<a href="22.3/">22.3/</a><a href="23/">23/</a>"""),
            "https://p.test/builds/24.04/intel" => (HttpStatusCode.OK, """{"version":"24.04","build":"21"}"""),
            "https://p.test/builds/24.04/nvidia" => (HttpStatusCode.OK, """{"version":"24.04","build":"28"}"""),
            "https://p.test/builds/26.04/intel" => (HttpStatusCode.OK, """{"version":"26.04","build":"3"}"""),
            _ => (HttpStatusCode.OK, ""),
        });
        var result = await new DistroReleaseChecker(new HttpClient(handler)).CheckAsync(Catalog());

        Assert.True(result.Complete);
        Assert.DoesNotContain(result.Newer, n => n.DistroId == "ubuntu");
        Assert.Contains(result.Newer, n => n.DistroId == "mint" && n.Found == "23");
        Assert.Contains(result.Newer, n => n.DistroId == "pop-os" && n.Found.Contains("build 21"));
        Assert.DoesNotContain(result.Newer, n => n.Found.Contains("build 28"));
        Assert.Contains(result.Newer, n => n.DistroId == "pop-os" && n.Found == "26.04");
    }

    [Fact]
    public async Task Network_errors_are_reported_not_thrown()
    {
        var handler = new Handler(_ => throw new HttpRequestException("offline"));
        var result = await new DistroReleaseChecker(new HttpClient(handler)).CheckAsync(Catalog());
        Assert.False(result.Complete);
        Assert.Equal(3, result.Errors.Count);
        Assert.Empty(result.Newer);
    }
}

public class WindowsNotesTests
{
    private static readonly GreyHaven.Compat.Data.WindowsLifecycle Lifecycle = new()
    {
        Windows10 = new() { EndOfSupport = "2025-10-14", EsuEnds = "2027-10-12", Source = new() { Title = "Microsoft", Url = "https://www.microsoft.com/en-us/windows/end-of-support" } },
    };

    private static SystemInfo Windows(int build, string edition = "Professional") =>
        TestData.Profile([]).System with { Windows = new WindowsVersion("Windows 10 Pro", "22H2", build, 1, edition) };

    [Fact]
    public void Windows_10_gets_a_calm_note_with_the_esu_date_and_source()
    {
        var note = Assert.Single(WindowsNotes.For(Windows(19045), Lifecycle, new DateOnly(2026, 10, 5)));
        Assert.Contains("14 October 2025", note.Text);
        Assert.Contains("Extended Security Updates program, which runs until 12 October 2027", note.Text);
        Assert.Contains("keeps working", note.Text);
        Assert.Equal("https://www.microsoft.com/en-us/windows/end-of-support", note.Source.Url);
    }

    [Fact]
    public void After_esu_ends_the_note_says_so()
    {
        var note = Assert.Single(WindowsNotes.For(Windows(19045), Lifecycle, new DateOnly(2027, 11, 1)));
        Assert.Contains("ended on 12 October 2027", note.Text);
    }

    [Theory]
    [InlineData(22631, "Professional")] // Windows 11
    [InlineData(19044, "EnterpriseS")]  // Windows 10 LTSC: separate support schedule
    [InlineData(17763, "IoTEnterpriseS")]
    public void No_note_for_windows_11_or_ltsc(int build, string edition) =>
        Assert.Empty(WindowsNotes.For(Windows(build, edition), Lifecycle, new DateOnly(2026, 10, 5)));

    [Fact]
    public void No_note_before_end_of_support() =>
        Assert.Empty(WindowsNotes.For(Windows(19045), Lifecycle, new DateOnly(2025, 6, 1)));
}
