using System.Globalization;
using GreyHaven.Core;
using GreyHaven.Core.Compatibility;
using GreyHaven.Core.Hardware;
using static GreyHaven.Reports.Doc;

namespace GreyHaven.Reports;

/// <summary>
/// Turns a <see cref="CompatibilityReport"/> into the shareable report: the same content the
/// app shows, for the path the user chose, plus the live-USB test guide.
/// </summary>
public static class ReportBuilder
{
    private static readonly CultureInfo En = CultureInfo.InvariantCulture;

    /// <param name="distroId">Show details for this distro instead of the chosen path's.</param>
    public static ReportDocument Build(CompatibilityReport r, string? distroId = null)
    {
        var distro = DetailDistro(r, distroId);
        var blocks = new List<Block>();

        Header(r, blocks);
        Recommendation(r, blocks);
        Compare(r, blocks);
        Parts(r, distro, blocks);
        // No recommendation (e.g. an ARM PC): the downloads wouldn't suit this PC, so no guide.
        var guide = distroId is null && r.SelectedDistroId is null ? null : r.LiveUsbGuides.FirstOrDefault(g => g.DistroId == distro.DistroId);
        LiveUsb(r, guide, blocks);
        YourPc(r.Profile, blocks);
        Notes(r, blocks);
        Footer(blocks);

        return new ReportDocument("GreyHaven report", blocks);
    }

    /// <summary>The distro the details are for: the one asked for, else the chosen path's, else the best score.</summary>
    public static DistroSummary DetailDistro(CompatibilityReport r, string? distroId = null)
    {
        var id = distroId ?? r.SelectedDistroId;
        if (id is null) return r.Distros.OrderByDescending(d => d.Score.Score).First();
        return r.Distros.FirstOrDefault(d => d.DistroId == id)
            ?? throw new ArgumentException($"Unknown distro '{id}'. Choose one of: {string.Join(", ", r.Distros.Select(d => d.DistroId))}.", nameof(distroId));
    }

    // ---- Sections -------------------------------------------------------------------------

    private static void Header(CompatibilityReport r, List<Block> b)
    {
        b.Add(new Heading(1, "GreyHaven report"));
        var created = r.GeneratedAt.ToString("d MMMM yyyy, h:mm tt", En);
        b.Add(P($"Created {created} with GreyHaven {GreyHavenInfo.Version}. Hardware data from {r.Data.BuiltOn.ToString("d MMMM yyyy", En)}."));

        if (r.Profile.Source.StartsWith("sample:", StringComparison.Ordinal))
            b.Add(new Callout(CalloutKind.Info, "Demo report", Line($"This report is for a sample PC (\"{r.Profile.Source["sample:".Length..]}\"), not a real scan. {r.Profile.Description}".TrimEnd())));
        if (r.Data.Notice is { } stale)
            b.Add(new Callout(CalloutKind.Warning, "Check for a GreyHaven update", Line(stale)));
        foreach (var note in r.ContextNotes)
            b.Add(new Callout(CalloutKind.Info, "About Windows 10", [new Text(note.Text + " "), new Text("Source: "), SourceLink(note.Source)]));
    }

    private static void Recommendation(CompatibilityReport r, List<Block> b)
    {
        b.Add(new Heading(2, "What GreyHaven recommends"));
        b.Add(P(r.Recommendation.Paragraph));

        var paths = r.Recommendation.Paths;
        if (paths.Count > 1 && r.SelectedRecommendation is { } chosen)
            b.Add(P(new Text("This report follows the path you chose: "), new Strong(chosen.Title), new Text(".")));

        foreach (var path in paths)
        {
            var chosenMark = paths.Count > 1 && path.Kind == r.SelectedPath ? " (your choice)" : "";
            if (paths.Count > 1) b.Add(new Heading(3, path.Title + chosenMark));
            if (paths.Count > 1) b.Add(P(path.Paragraph));
            b.Add(P(new Strong("You'll download: "), new Text(WithEdition(DistroName(r, path.DistroId), path.EditionName) + ".")));
            if (path.SecureBootNotice is { } notice)
                b.Add(new Callout(CalloutKind.Warning, "Secure Boot", Line(notice)));
            if (path.SecureBootGuide is { } g)
                b.Add(P([new Strong("How to turn off Secure Boot: "), .. GuideInlines(g)]));
        }
    }

    private static IEnumerable<Inline> GuideInlines(GuideLink g)
    {
        var basis = g.MatchedBy == "motherboard" ? " (chosen from your motherboard's maker)" : "";
        if (g.Url is not null) yield return new Link(g.Title, g.Url);
        else yield return new Text(g.Title);
        if (basis.Length > 0) yield return new Text(basis);
        if (g.LocalPath is not null)
            yield return new Text($". A copy comes with GreyHaven: {g.LocalPath.Replace('/', '\\')} in the GreyHaven folder.");
    }

    private static void Compare(CompatibilityReport r, List<Block> b)
    {
        b.Add(new Heading(2, "Compare distros"));
        var rows = r.Distros.Select(d =>
        {
            var s = d.Score;
            var score = $"{s.Score} out of 100 ({s.Band.ToString().ToLowerInvariant()})" + (s.Incomplete ? ", incomplete" : "");
            var notes = new List<string>();
            if (s.CapReason is not null) notes.Add(s.CapReason);
            notes.AddRange(d.Notes);
            return Cells(
                Line(d.DisplayName + (d.DistroId == r.SelectedDistroId ? " (your choice)" : "")),
                Line(score),
                Line($"{(s.Coverage * 100).ToString("0", En)}%"),
                Line(s.ExtraDriverCount == 0 ? "None" : s.ExtraDriverCount.ToString(En)),
                Line(d.Edition),
                Line(notes.Count == 0 ? "" : string.Join(" ", notes)));
        }).ToList();
        b.Add(new Table("Scores for each distro on this PC", ["Distro", "Score", "Parts checked", "Extra steps", "Download", "Notes"], rows));
    }

    private static void Parts(CompatibilityReport r, DistroSummary distro, List<Block> b)
    {
        var id = distro.DistroId;
        b.Add(new Heading(2, $"How each part works on {distro.DisplayName}"));
        b.Add(P("Parts labelled \"Essential\" are ones the PC can't be used properly without, and they count most toward the score. \"Important\" parts are ones you'd notice if they didn't work. Other parts count least."));

        var ordered = r.Components.OrderBy(c => c.Importance).ThenBy(c => c.Component.Category).ToList();
        var attention = ordered.Where(c => c.For(id).Rating != Rating.WorksOutOfTheBox).ToList();
        var fine = ordered.Where(c => c.For(id).Rating == Rating.WorksOutOfTheBox).ToList();

        if (attention.Count > 0)
        {
            b.Add(new Heading(3, "Needs your attention"));
            foreach (var c in attention) PartDetails(r, c, id, b);
        }

        if (fine.Count > 0)
        {
            b.Add(new Heading(3, "Works out of the box"));
            b.Add(new Table($"Parts that work out of the box on {distro.DisplayName}", ["Part", "Importance", "Rating"],
                fine.Select(c => Cells(Line(PartName(c)), Line(Labels.Of(c.Importance)), [new RatingTag(Rating.WorksOutOfTheBox)])).ToList()));
            b.Add(new Details("Sources and technical details for these parts", fine.SelectMany(c => TechnicalBlocks(c, c.For(id), withHeading: true)).ToList()));
        }

        b.Add(new Heading(3, "Every part on every distro"));
        b.Add(new Table("How each part works on each distro", ["Part", "Importance", .. r.Distros.Select(d => d.DisplayName)],
            ordered.Select(c => (IReadOnlyList<IReadOnlyList<Inline>>)[Line(PartName(c)), Line(Labels.Of(c.Importance)), .. r.Distros.Select(d => (IReadOnlyList<Inline>)[new RatingTag(c.For(d.DistroId).Rating)])]).ToList()));
    }

    private static void PartDetails(CompatibilityReport r, RatedComponent c, string distroId, List<Block> b)
    {
        var rating = c.For(distroId);
        b.Add(new Heading(4, PartName(c)));
        b.Add(P(new RatingTag(rating.Rating), new Text($" · {Labels.Of(c.Importance)} part")));
        b.Add(P(rating.Explanation));
        if (rating.ApproximationNote is not null) b.Add(P("Approximate data: " + rating.ApproximationNote));

        if (rating.Fix is { } fix)
        {
            b.Add(P(new Strong($"What to do: {fix.Driver}")));
            if (fix.Steps.Count > 0) b.Add(new ListBlock(ListKind.Numbered, fix.Steps.Select(s => Line(s)).ToList()));
            if (fix.Commands.Count > 0)
            {
                b.Add(P(fix.Steps.Count > 0 ? "Or type this in a terminal:" : "Type this in a terminal:"));
                b.Add(new CommandBlock(fix.Commands));
            }
            if (fix.Note is not null) b.Add(P(fix.Note));
            if (fix.SecureBootAdvice is not null) b.Add(new Callout(CalloutKind.Info, "Secure Boot", Line(fix.SecureBootAdvice)));
            if (fix.NeedsInternet) b.Add(P("This step needs an internet connection."));
        }

        var tests = r.Caveats.Where(cv => c.CaveatIds.Contains(cv.Id)).Select(cv => cv.Title).ToList();
        if (tests.Count > 0) b.Add(P($"Test with a live USB: {string.Join(", ", tests)}."));
        b.Add(new Details("Sources and technical details", TechnicalBlocks(c, rating, withHeading: false).ToList()));
    }

    private static IEnumerable<Block> TechnicalBlocks(RatedComponent c, ComponentRating rating, bool withHeading)
    {
        if (withHeading) yield return P(new Strong(PartName(c)));
        if (rating.Sources.Count > 0)
            yield return new ListBlock(ListKind.Bullets, rating.Sources.Select(s => (IReadOnlyList<Inline>)[new Text("Source: "), SourceLink(s)]).ToList());

        var facts = new List<string> { $"Rating: {Labels.Official(rating.Rating)}", $"Confidence: {rating.Confidence.ToString().ToLowerInvariant()}{(rating.IsWeakData ? " (limited data)" : "")}" };
        var comp = c.Component;
        if (comp.Id is { } id)
        {
            facts.Add($"Device ID: {id.FullKey}");
            if (id.ClassCode is not null) facts.Add($"PCI class: {id.ClassCode}");
        }
        if (comp.Cpu is { } cpu) facts.Add($"Processor: {cpu.Vendor}, family {cpu.Family}, model {cpu.Model}");
        if (rating.LinuxDriver is not null) facts.Add($"Linux driver: {rating.LinuxDriver}");
        if (comp.WindowsDriver is not null) facts.Add($"Windows driver: {comp.WindowsDriver}");
        if (comp.Count > 1) facts.Add($"Identical devices: {comp.Count}");
        yield return P(string.Join(" · ", facts));
    }

    private static void LiveUsb(CompatibilityReport r, LiveUsbGuide? g, List<Block> b)
    {
        b.Add(new Heading(2, "Test with a live USB before installing"));
        if (g is null)
        {
            b.Add(P("GreyHaven can't recommend a distro for this PC, so there's no live-USB guide."));
            return;
        }
        b.Add(P($"A live USB runs {g.DistroName} from a USB stick without installing anything or changing your files. It's the best way to check the things GreyHaven can't see from Windows. It takes about 30 minutes, plus the download."));

        b.Add(new Heading(3, "1. Make the USB stick"));
        var make = new List<Inline> { new Text($"Download {WithEdition(g.DistroName, g.EditionName)}") };
        if (g.Website is not null) { make.Add(new Text(" from ")); make.Add(new Link(g.Website, g.Website)); }
        make.Add(new Text(". Write it to a USB stick of 8 GB or larger; everything on the stick will be erased."));
        if (g.MakeUsbGuide is { } mk) { make.Add(new Text(" Step-by-step: ")); make.Add(SourceLink(mk)); make.Add(new Text(".")); }
        b.Add(new Paragraph(make));
        b.Add(P("A later version of GreyHaven will download, check and write the USB stick for you."));

        if (g.BeforeYouStart.Count > 0)
        {
            b.Add(new Heading(3, "2. Before you start"));
            b.Add(new ListBlock(ListKind.Bullets, g.BeforeYouStart.Select(s => Line(s)).ToList()));
        }

        var n = g.BeforeYouStart.Count > 0 ? 3 : 2;
        b.Add(new Heading(3, $"{n}. Start the live session"));
        var settingsPath = r.Profile.System.Windows.IsWindows11
            ? "Settings > System > Recovery"
            : "Settings > Update & Security > Recovery";
        var start = new List<IReadOnlyList<Inline>>
        {
            Line("Plug the USB stick into the PC."),
            Line($"In Windows, open {settingsPath}. Next to \"Advanced startup\", choose \"Restart now\"."),
            Line("When the blue menu appears, choose \"Use a device\", then your USB stick. (Or restart and press your PC's boot menu key while it starts, often F12, F11, F10, F8 or Esc.)"),
            Line($"When {g.DistroName} asks, choose to try it without installing."),
        };
        b.Add(new ListBlock(ListKind.Numbered, start));
        if (g.TryItGuide is { } tryIt) b.Add(P(new Text("More detail: "), SourceLink(tryIt), new Text(".")));

        b.Add(new Heading(3, $"{n + 1}. Check these"));
        b.Add(new ListBlock(ListKind.Checkboxes, g.Checks.Select(c => (IReadOnlyList<Inline>)[new Strong(c.Title + ": "), new Text(c.Test)]).ToList()));
        if (g.ExpectedInLiveSession.Count > 0)
            b.Add(new ListBlock(ListKind.Bullets, g.ExpectedInLiveSession.Select(s => Line(s)).ToList()));

        if (g.ShareCommands.Count > 0)
        {
            b.Add(new Heading(3, $"{n + 2}. Optional: share your results"));
            var share = new List<Inline> { new Text("Help other people (and GreyHaven's data) by sharing a hardware report from the live session. Open a terminal and type:") };
            b.Add(new Paragraph(share));
            b.Add(new CommandBlock(g.ShareCommands));
            var note = new List<Inline>();
            if (g.ShareNote is not null) note.Add(new Text(g.ShareNote + " "));
            if (g.ShareSource is { } src) { note.Add(new Text("More about it: ")); note.Add(SourceLink(src)); note.Add(new Text(".")); }
            if (note.Count > 0) b.Add(new Paragraph(note));
        }

        b.Add(new Heading(3, "Going back to Windows"));
        b.Add(P("Shut down from the live session's power menu, remove the USB stick and start the PC as usual. Nothing was installed. If you turned Secure Boot off and decide not to switch, you can turn it back on."));
    }

    private static void YourPc(HardwareProfile p, List<Block> b)
    {
        var s = p.System;
        b.Add(new Heading(2, "Your PC"));
        var rows = new List<(string, string?)>
        {
            ("PC", string.Join(" ", new[] { s.SystemManufacturer, s.SystemModel }.Where(x => x is not null))),
            ("Motherboard", s.BoardManufacturer),
            ("Type", Labels.Of(s.FormFactor)),
            ("Windows", s.Windows.FriendlyName),
            ("Processor type", Labels.Of(s.Architecture)),
            ("Boot mode", Labels.Of(s.Firmware)),
            ("Secure Boot", Labels.Of(s.SecureBoot)),
            ("Storage mode", Labels.Of(s.StorageMode)),
            ("Fast Startup", Labels.Of(s.FastStartup)),
            ("BitLocker", Labels.Of(s.BitLocker)),
        };
        b.Add(new Table("This PC's settings that matter for Linux", ["Setting", "Value"],
            rows.Where(x => !string.IsNullOrEmpty(x.Item2)).Select(x => Cells(Line(x.Item1), Line(x.Item2!))).ToList()));
    }

    private static void Notes(CompatibilityReport r, List<Block> b)
    {
        var notes = r.Profile.Warnings.Concat(r.DataNotes).ToList();
        if (notes.Count == 0) return;
        b.Add(new Heading(2, "Notes"));
        b.Add(new ListBlock(ListKind.Bullets, notes.Select(n => Line(n)).ToList()));
    }

    private static void Footer(List<Block> b)
    {
        b.Add(new Heading(2, "About this report"));
        b.Add(P("This report contains device IDs and Windows settings only. It has no serial numbers, user names, PC name or network (MAC) addresses, so it's safe to share when asking for help."));
        var made = new List<Inline> { new Text("Made with GreyHaven, which checks how well a PC will work on Linux.") };
        if (GreyHavenInfo.IsGitHubRepositoryConfigured)
        {
            made.Add(new Text(" "));
            made.Add(new Link("GreyHaven on GitHub", $"https://github.com/{GreyHavenInfo.GitHubRepository}"));
        }
        b.Add(new Paragraph(made));
    }

    // ---- Helpers --------------------------------------------------------------------------

    private static string PartName(RatedComponent c)
    {
        var name = c.Component.Count > 1 ? $"{c.DisplayName} (×{c.Component.Count})" : c.DisplayName;
        return $"{Labels.Of(c.Component.Category)}: {name}";
    }

    /// <summary>"Ubuntu 26.04.1 LTS Desktop" stays as is; "Pop!_OS 24.04 COSMIC" + "NVIDIA ISO" becomes "Pop!_OS 24.04 COSMIC (NVIDIA ISO)".</summary>
    private static string WithEdition(string name, string edition) =>
        name.EndsWith(edition, StringComparison.Ordinal) ? name : $"{name} ({edition})";

    private static string DistroName(CompatibilityReport r, string distroId) =>
        r.Distros.FirstOrDefault(d => d.DistroId == distroId)?.DisplayName ?? distroId;
}
