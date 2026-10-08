using GreyHaven.Compat;
using GreyHaven.Core.Apps;
using GreyHaven.Core.Compatibility;
using GreyHaven.Core.Scanning;
using GreyHaven.Ratings;
using GreyHaven.Reports;

namespace GreyHaven.Tests.Ratings;

public class AppTests
{
    private static readonly Lazy<CompatibilityData> Data = new(() => CompatibilityData.Load());

    private static RatedApp Rate(string name, AppSource source = AppSource.Installed, string? id = null) =>
        AppRater.Rate(new InstalledApp(name, null, source, id), Data.Value.Apps);

    [Theory]
    [InlineData("Mozilla Firefox (x64 en-US)", "mozilla firefox")]
    [InlineData("Adobe Photoshop 2025", "adobe photoshop 2025")]
    [InlineData("NVIDIA App 11.0.5.420", "nvidia app")]
    [InlineData("Notepad++ (64-bit x64)", "notepad++")]
    [InlineData("Microsoft® Teams™", "microsoft teams")]
    [InlineData("Audacity 3.7.5", "audacity")]
    public void Names_are_tidied_for_matching(string name, string key) => Assert.Equal(key, AppNames.Normalize(name));

    [Theory]
    [InlineData("Microsoft Visual C++ 2015-2022 Redistributable (x64) - 14.40.33810")]
    [InlineData("NVIDIA Graphics Driver 581.42")]
    [InlineData("Microsoft Edge WebView2 Runtime")]
    [InlineData("Update for Windows 10 for x64-based Systems (KB5001716)")]
    [InlineData("Realtek High Definition Audio Driver")]
    public void Runtimes_drivers_and_updates_are_left_out(string name) =>
        Assert.True(AppInventory.IsIgnored(new RawApp { Name = name }));

    [Fact]
    public void Hidden_entries_updates_and_windows_store_parts_are_left_out()
    {
        Assert.True(AppInventory.IsIgnored(new RawApp { Name = "Something", SystemComponent = true }));
        Assert.True(AppInventory.IsIgnored(new RawApp { Name = "Something", HasParent = true }));
        Assert.True(AppInventory.IsIgnored(new RawApp { Name = "Something", ReleaseType = "Security Update" }));
        Assert.True(AppInventory.IsIgnored(new RawApp { Name = "Calculator", Source = AppSource.Store, Id = "Microsoft.WindowsCalculator" }));
        Assert.True(AppInventory.IsIgnored(new RawApp { Name = "VCLibs", Source = AppSource.Store, Id = "Microsoft.VCLibs.140.00" }));
        Assert.False(AppInventory.IsIgnored(new RawApp { Name = "Xbox", Source = AppSource.Store, Id = "Microsoft.GamingApp" }));
        Assert.False(AppInventory.IsIgnored(new RawApp { Name = "WhatsApp", Source = AppSource.Store, Id = "5319275A.WhatsAppDesktop" }));
        Assert.False(AppInventory.IsIgnored(new RawApp { Name = "Discord" }));
    }

    [Fact]
    public void Steam_games_listed_twice_appear_once()
    {
        var apps = AppInventory.Build(
        [
            new RawApp { Name = "ELDEN RING", Source = AppSource.Steam, Id = "1245620" },
            new RawApp { Name = "ELDEN RING", Source = AppSource.Steam, Id = "1245620" },
            new RawApp { Name = "Elden Ring" },
        ]);
        var app = Assert.Single(apps);
        Assert.Equal(AppSource.Steam, app.Source);
    }

    [Fact]
    public void Steam_files_are_read()
    {
        const string vdf = """
            "libraryfolders"
            {
                "0" { "path" "C:\\Program Files (x86)\\Steam" "apps" { "228980" "1" } }
                "1" { "path" "D:\\SteamLibrary" }
            }
            """;
        Assert.Equal([@"C:\Program Files (x86)\Steam", @"D:\SteamLibrary"], SteamFiles.LibraryFolders(vdf));
        var m = SteamFiles.Manifest("\"AppState\"\n{\n\t\"appid\"\t\t\"1245620\"\n\t\"name\"\t\t\"ELDEN RING\"\n}");
        Assert.Equal(("1245620", "ELDEN RING"), m);
        Assert.True(SteamFiles.IsTool("228980", "Steamworks Common Redistributables"));
        Assert.True(SteamFiles.IsTool("2348590", "Proton 8.0"));
    }

    // ---- The maintainer's examples ----------------------------------------------------------

    [Fact]
    public void Valorant_wont_work_because_of_its_anti_cheat()
    {
        var r = Rate("VALORANT");
        Assert.Equal(AppVerdict.WontWork, r.Verdict);
        Assert.Equal("anti-cheat", r.Reason);
        Assert.Contains("Vanguard", r.Summary);
        Assert.Equal("https://areweanticheatyet.com/game/valorant", r.Sources.Single().Url);
    }

    [Fact]
    public void FluidVox_has_OpenWhispr_as_its_alternative()
    {
        var r = Rate("FluidVox");
        Assert.Equal(AppVerdict.WontWork, r.Verdict);
        var alt = Assert.Single(r.Alternatives);
        Assert.Equal("OpenWhispr", alt.App!.Name);
        Assert.Equal("close", alt.Closeness);
        Assert.StartsWith("https://github.com/OpenWhispr/openwhispr", alt.App.Website);
    }

    [Fact]
    public void Canon_EOS_Utility_points_to_OBS_with_a_capture_card()
    {
        var r = Rate("EOS Utility 3");
        Assert.Equal(AppVerdict.WontWork, r.Verdict);
        var obs = r.Alternatives.Single(a => a.App?.Id == "obs-studio");
        Assert.Contains("capture card", obs.Note);
        Assert.Equal([Core.Hardware.ComponentCategory.Webcam], obs.Uses);
        Assert.Contains(r.Alternatives, a => a.App?.Id == "entangle");
    }

    [Fact]
    public void OBS_works_and_depends_on_the_camera_or_capture_card()
    {
        var r = Rate("OBS Studio");
        Assert.Equal(AppVerdict.Works, r.Verdict);
        Assert.Equal("com.obsproject.Studio", r.Linux!.Flathub);
        Assert.Equal([Core.Hardware.ComponentCategory.Webcam], r.Uses);
    }

    // ---- Games ---------------------------------------------------------------------------------

    [Fact]
    public void Games_use_anti_cheat_data_then_ProtonDB_then_say_not_sure()
    {
        Assert.Equal(AppVerdict.WontWork, Rate("Apex Legends", AppSource.Steam, "1172470").Verdict);                 // anti-cheat: Denied
        Assert.Equal(AppVerdict.WorksWithLayer, Rate("ELDEN RING", AppSource.Steam, "1245620").Verdict);             // anti-cheat: Supported
        var cyberpunk = Rate("Cyberpunk 2077", AppSource.Steam, "1091500");                                         // ProtonDB
        Assert.Equal((AppVerdict.WorksWithLayer, "Proton", Confidence.High), (cyberpunk.Verdict, cyberpunk.Layer, cyberpunk.Confidence));
        Assert.Contains("of 782 recent Linux players", cyberpunk.Summary);
        Assert.Equal("protondb", cyberpunk.MatchedBy);
        Assert.Equal(AppVerdict.NotSure, Rate("Half-Life 2: Episode One", AppSource.Steam, "380").Verdict);         // too few reports
        Assert.Equal(AppVerdict.WontWork, Rate("Fortnite", AppSource.Epic, "fn").Verdict);                           // by Epic namespace
    }

    [Fact]
    public void Unknown_apps_are_not_sure_never_guessed()
    {
        var r = Rate("Some App Nobody Has Heard Of");
        Assert.Equal(AppVerdict.NotSure, r.Verdict);
        Assert.Equal("none", r.MatchedBy);
    }

    // ---- The whole flow, and privacy ------------------------------------------------------------

    [Fact]
    public async Task Sample_apps_are_filtered_rated_and_summarised()
    {
        var profile = await SampleProfiles.Open("nvidia-desktop").ScanAsync();
        Assert.DoesNotContain(profile.Apps, a => a.Name.Contains("Redistributable") || a.Name.Contains("Driver") || a.Name == "Calculator");
        var r = await new RatingEngine(Data.Value).RateAsync(profile, new RatingOptions { UseOnlineLookups = false });
        Assert.Equal(profile.Apps.Count, r.Apps.Count);
        Assert.Equal("9 apps work. 10 have a website or alternative. 4 apps won't work. We don't know 2 apps yet.", AppWords.Summary(r.Apps));
    }

    [Fact]
    public async Task Saved_reports_leave_apps_out_unless_asked()
    {
        var profile = await SampleProfiles.Open("nvidia-desktop").ScanAsync();
        var r = await new RatingEngine(Data.Value).RateAsync(profile, new RatingOptions { UseOnlineLookups = false });
        var dir = Path.Combine(Path.GetTempPath(), $"gh-apps-{Guid.NewGuid():N}");
        Directory.CreateDirectory(dir);
        try
        {
            foreach (var ext in new[] { ".json", ".html", ".md" })
            {
                var file = Path.Combine(dir, "report" + ext);
                await ReportExporter.SaveAsync(r, file);
                Assert.DoesNotContain("OpenWhispr", await File.ReadAllTextAsync(file)); // only in the app ratings
            }
            var with = Path.Combine(dir, "with.json");
            await ReportExporter.SaveAsync(r, with, includeApps: true);
            Assert.Contains("OpenWhispr", await File.ReadAllTextAsync(with));
        }
        finally { Directory.Delete(dir, true); }
    }

    [Fact]
    public void Every_alternative_and_linux_app_says_where_to_get_it()
    {
        var apps = Data.Value.Apps;
        Assert.True(apps.WindowsApps.Count >= 60, $"{apps.WindowsApps.Count} Windows apps");
        Assert.All(apps.LinuxApps.Values, l => Assert.True(l.Flathub is not null || l.Snap is not null || l.Apt is not null || l.Website is not null || l.Web is not null, l.Id));
        Assert.All(apps.WindowsApps, w => Assert.StartsWith("https://", w.Source!.Url));
        Assert.True(apps.AntiCheat.Count > 1000);
        Assert.True(apps.ProtonDb.Count > 10_000);
    }
}
