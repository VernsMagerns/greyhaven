using System.Globalization;
using GreyHaven.Core;
using GreyHaven.Core.Compatibility;
using GreyHaven.Core.Hardware;
using static GreyHaven.Reports.Doc;

namespace GreyHaven.Reports;

/// <summary>
/// Turns a <see cref="CompatibilityReport"/> into the shareable report, in the same plain words
/// and order as the app: headline and score, the version of Linux to use, the parts that need
/// attention, the step-by-step walkthrough (for printing or a phone while the PC restarts), and
/// the details one level down.
/// </summary>
public static class ReportBuilder
{
    private static readonly CultureInfo En = CultureInfo.InvariantCulture;

    /// <param name="distroId">Show part details for this distro instead of the chosen path's.</param>
    public static ReportDocument Build(CompatibilityReport r, string? distroId = null)
    {
        var distro = DetailDistro(r, distroId);
        var blocks = new List<Block>();

        Header(r, distro, blocks);
        YourLinux(r, blocks);
        Parts(r, distro, blocks);
        Apps(r, blocks);
        Steps(r, blocks);
        Compare(r, blocks);
        YourPc(r.Profile, blocks);
        Footer(r, blocks);

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

    public static string ShortName(CompatibilityReport r, string distroId) =>
        PlainWords.ShortName(r.LiveUsbGuides.FirstOrDefault(g => g.DistroId == distroId)?.DistroName ?? distroId);

    // ---- Sections -------------------------------------------------------------------------

    private static void Header(CompatibilityReport r, DistroSummary distro, List<Block> b)
    {
        b.Add(new Heading(1, "GreyHaven report"));
        b.Add(P($"Made on {r.GeneratedAt.ToString("d MMMM yyyy", En)} with GreyHaven {GreyHavenInfo.Version}."));
        if (r.Profile.Source.StartsWith("sample:", StringComparison.Ordinal))
            b.Add(new Callout(CalloutKind.Info, "Demo", Line("This is a sample PC, not a real scan.")));
        if (r.Data.Notice is { } stale)
            b.Add(new Callout(CalloutKind.Warning, "Check for a GreyHaven update", Line(stale)));

        var hasPath = r.Recommendation.Paths.Count > 0;
        b.Add(new ScoreHeadline(
            hasPath ? PlainWords.Headline(distro.Score.Score) : "We can't suggest a version of Linux yet",
            hasPath && r.SelectedDistroId is { } chosen ? $"We suggest {ShortName(r, chosen)}." : null,
            new PartsOverview(r, distro.DistroId).Summary,
            distro.Score.Score));
        if (r.Profile.System.IsArm)
            b.Add(new Callout(CalloutKind.Warning, "ARM chip", Line("This PC has an ARM chip. Linux support for these PCs is still limited.")));
        if (r.ContextNotes.FirstOrDefault() is { } why)
            b.Add(new Details("Why switch now?",
            [
                P("Microsoft stopped supporting Windows 10 in October 2025. Security updates now need a special sign-up. Those updates end in October 2027."),
                P(SourceLink(why.Source)),
            ]));
    }

    private static void YourLinux(CompatibilityReport r, List<Block> b)
    {
        var paths = r.Recommendation.Paths;
        if (paths.Count == 0) return;
        b.Add(new Heading(2, "Your version of Linux"));
        if (paths.Count > 1) b.Add(P(PlainWords.Distro + " Two fit this PC well."));
        foreach (var p in paths)
        {
            var chosen = p.Kind == r.SelectedPath;
            var name = ShortName(r, p.DistroId);
            var tag = p.Kind == PathKind.Easy ? "Easiest" : "Best fit";
            b.Add(new Heading(3, paths.Count > 1 ? $"{name}: {tag}{(chosen ? " (your choice)" : "")}" : name));
            foreach (var line in PathLines(r, p)) b.Add(P(line));
            var more = new List<Block> { P(p.Paragraph) };
            if (p.SecureBootNotice is not null) more.Add(P(new Strong("About Secure Boot. "), new Text($"{PlainWords.SecureBoot} {p.SecureBootNotice}")));
            if (p.SecureBootGuide is { } g)
                more.Add(P(new Text("How to turn off Secure Boot: "),
                    g.Url is not null ? new Link(g.Title, g.Url) : new Text($"{g.Title} ({g.LocalPath} in the GreyHaven folder)"),
                    new Text(g.MatchedBy == "motherboard" ? " (from your motherboard's maker)." : ".")));
            more.Add(P(new Strong("You'll get: "), new Text(YoullGet(r, p))));
            b.Add(new Details("Show more", more));
        }
    }

    /// <summary>One or two short lines per path, as on the app's choice cards.</summary>
    public static IReadOnlyList<string> PathLines(CompatibilityReport r, RecommendationPath p)
    {
        var secureBootOff = p.Requirements.Contains(PreSwitchRequirement.TurnOffSecureBoot);
        var lines = new List<string>();
        if (p.Kind == PathKind.Advanced)
        {
            var easy = r.Recommendation.Get(PathKind.Easy);
            var better = easy is null ? [] : BetterOn(r, p.DistroId, easy.DistroId);
            lines.Add(better.Count > 0 ? $"Your {Join(better)} {(better.Count == 1 ? "works" : "work")} right away." : "The best match for this PC's parts.");
        }
        else
        {
            if (!secureBootOff) lines.Add("Nothing to change on your PC first.");
            var extra = r.Components.Where(c => c.For(p.DistroId).Rating == Rating.WorksWithExtraDriver)
                .Select(c => PlainWords.Noun(c.Component.Category)).Distinct().ToList();
            if (extra.Count > 0) lines.Add($"Your {Join(extra)} {(extra.Count == 1 ? "needs" : "need")} one extra step after you install.");
        }
        if (secureBootOff) lines.Add("First, you turn off Secure Boot in your PC's startup settings.");
        return lines;
    }

    /// <summary>"The Pop!_OS installer file made for NVIDIA graphics (about 4 GB). GreyHaven will put it on a USB stick for you."</summary>
    public static string YoullGet(CompatibilityReport r, RecommendationPath p)
    {
        var guide = r.LiveUsbGuides.FirstOrDefault(g => g.DistroId == p.DistroId);
        var which = p.EditionId == "nvidia" ? " made for NVIDIA graphics" : "";
        var size = guide?.InstallerBytes is { } bytes ? $" ({PlainWords.AboutGb(bytes)})" : "";
        return $"The {ShortName(r, p.DistroId)} installer file{which}{size}. GreyHaven will put it on a USB stick for you.";
    }

    private static List<string> BetterOn(CompatibilityReport r, string a, string b)
    {
        static int Rank(Rating x) => x switch
        {
            Rating.WorksOutOfTheBox => 0, Rating.WorksWithExtraDriver => 1, Rating.Partial => 2, Rating.Unknown => 3, _ => 4,
        };
        return r.Components.Where(c => Rank(c.For(a).Rating) < Rank(c.For(b).Rating))
            .Select(c => PlainWords.Noun(c.Component.Category)).Distinct().ToList();
    }

    private static string Join(IReadOnlyList<string> items) =>
        items.Count < 2 ? string.Concat(items) : $"{string.Join(", ", items.Take(items.Count - 1))} and {items[^1]}";

    private static void Parts(CompatibilityReport r, DistroSummary distro, List<Block> b)
    {
        var id = distro.DistroId;
        var name = ShortName(r, id);
        b.Add(new Heading(2, $"Your PC's parts on {name}"));

        var parts = new PartsOverview(r, id);
        foreach (var c in parts.Attention)
        {
            var rating = c.For(id);
            b.Add(new Heading(3, PartName(c)));
            var line = new List<Inline> { new RatingTag(rating.Rating) };
            if (c.Importance != Importance.Minor) line.Add(new Text($" · {Labels.Of(c.Importance)} part"));
            b.Add(new Paragraph(line));
            b.Add(P(PlainWords.Summary(rating, name)));
            b.Add(new Details("Show more", PartMore(c, rating)));
        }

        if (parts.WorkingHeading is { } working)
            b.Add(new Details(working, PartList($"Parts that work right away on {name}", parts.Working, id)));
        if (parts.UncheckedHeading is { } unchecked_)
            b.Add(new Details(unchecked_, [P(PartsOverview.UncheckedAdvice), .. PartList($"Small parts we couldn't check on {name}", parts.UncheckedSmall, id)]));
        if (parts.ChipsetLine is { } chipset)
            b.Add(new Details(chipset, [P(PartsOverview.ChipsetAbout), .. PartList($"System chipset parts on {name}", parts.Chipset, id)]));

        if (r.Caveats.Count > 0)
            b.Add(new Details($"{r.Caveats.Count} {(r.Caveats.Count == 1 ? "thing" : "things")} to test before you install",
            [
                P($"We can't check {(r.Caveats.Count == 1 ? "this" : "these")} from Windows. Test {(r.Caveats.Count == 1 ? "it" : "them")} by trying Linux from a USB stick first (step 7 below)."),
                .. r.Caveats.Select(cv => P(new Strong(cv.Title + ". "), new Text(cv.WhatToTest))),
            ]));
    }

    private static List<Block> PartList(string caption, IReadOnlyList<RatedComponent> parts, string distroId) =>
    [
        new Table(caption, ["Part", "Rating"], parts.Select(c => Cells(Line(PartName(c)), [new RatingTag(c.For(distroId).Rating)])).ToList()),
        .. parts.SelectMany(c => TechnicalBlocks(c, c.For(distroId), withHeading: true)),
    ];

    private static List<Block> PartMore(RatedComponent c, ComponentRating rating)
    {
        var more = new List<Block> { P(rating.Explanation) };
        if (rating.ApproximationNote is not null) more.Add(P("Approximate data: " + rating.ApproximationNote));
        if (rating.Fix is { } fix)
        {
            more.Add(P(new Strong($"How to fix it: {fix.Driver}")));
            if (fix.Steps.Count > 0) more.Add(new ListBlock(ListKind.Numbered, fix.Steps.Select(s => Line(s)).ToList()));
            if (fix.Commands.Count > 0)
            {
                more.Add(P(fix.Steps.Count > 0 ? "Or type this in a terminal:" : "Type this in a terminal:"));
                more.Add(new CommandBlock(fix.Commands));
            }
            if (fix.Note is not null) more.Add(P(fix.Note));
            if (fix.SecureBootAdvice is not null) more.Add(new Callout(CalloutKind.Info, "Secure Boot", Line(fix.SecureBootAdvice)));
            if (fix.NeedsInternet) more.Add(P("This step needs an internet connection."));
        }
        more.AddRange(TechnicalBlocks(c, rating, withHeading: false));
        return more;
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

    /// <summary>
    /// The whole journey, part by part, as the app shows it. Interactive bits (GreyHaven's
    /// buttons) are described in words. Phase 5 is what the person follows on the USB stick.
    /// </summary>
    private static void Steps(CompatibilityReport r, List<Block> b)
    {
        var steps = JourneyBuilder.Build(r);
        if (steps.Count == 0) return;
        b.Add(new Heading(2, "Your steps to Linux"));
        b.Add(P("Keep this page on your phone, or print it, while your PC restarts."));
        foreach (var phase in steps.GroupBy(s => s.Phase))
        {
            b.Add(new Heading(3, $"Part {(int)phase.Key} of 5: {JourneyBuilder.PhaseName(phase.Key)}"));
            foreach (var s in phase)
            {
                b.Add(new Heading(4, s.Title));
                b.Add(P(new Strong(s.Lead)));
                foreach (var c in s.Choices)
                {
                    var chosen = r.SelectedInstall == c.Choice;
                    b.Add(P(new Strong(c.Title + (chosen ? " (your choice)" : c.Recommended ? " (suggested)" : "") + ". "), new Text(string.Join(" ", c.Lines))));
                    b.Add(new ListBlock(ListKind.Bullets, c.Checks.Select(k => Line((k.State switch { CheckState.Ok => "✓ ", CheckState.Attention => "! ", _ => "✗ " }) + k.Text)).ToList()));
                }
                b.AddRange(s.Body);
                foreach (var t in s.Tasks)
                {
                    b.Add(P(new Strong(t.Title + (t.Done ? " (already done)" : "") + ". "), new Text(t.Why)));
                    if (t.Done) continue;
                    if (t.Action == PrepAction.OneClick) b.Add(P($"GreyHaven can do this for you: \"{t.ButtonLabel}\". {t.Undo}"));
                    b.AddRange(t.How);
                }
                if (s.Usb is { } usb)
                    b.Add(P([new Text("How the download is checked: "), .. usb.VerificationSources.Select(SourceLink), new Text(".")]));
                if (s.Restart is { } rs)
                {
                    b.Add(new ListBlock(ListKind.Checkboxes, rs.FinalChecks.Select(Line).ToList()));
                    b.Add(P($"Then click \"{rs.ButtonLabel}\" in GreyHaven."));
                    b.AddRange(rs.AfterRestart);
                    b.Add(new Details("USB stick not in the list?", rs.Fallback));
                }
                if (s.Tip is not null) b.Add(new Callout(CalloutKind.Tip, "What could go wrong?", Line(s.Tip)));
            }
        }
    }

    /// <summary>Only when the person chose "Include my apps" (otherwise the list is removed before saving).</summary>
    private static void Apps(CompatibilityReport r, List<Block> b)
    {
        if (r.Apps.Count == 0) return;
        b.Add(new Heading(2, "Your apps"));
        b.Add(P(Core.Apps.AppWords.Summary(r.Apps)));
        foreach (var group in r.Apps.GroupBy(a => Core.Apps.AppWords.Category(a.Category)))
        {
            var rows = group.Select(a =>
            {
                var notes = new List<Inline> { new Text(a.Summary) };
                foreach (var alt in a.Alternatives)
                    notes.Add(new Text($" Instead: {alt.App?.Name ?? alt.WebName} ({alt.Closeness switch { "same" => "does the same job", "close" => "very close", _ => "does part of it" }})."));
                return Cells(Line(a.Name), Line(Core.Apps.AppWords.Verdict(a.Verdict)), notes);
            }).ToList();
            b.Add(new Table(group.Key, ["App", "On Linux", "Notes"], rows));
        }
    }

    private static void Compare(CompatibilityReport r, List<Block> b)
    {
        b.Add(new Heading(2, "Compare versions of Linux"));
        var rows = r.Distros.Select(d =>
        {
            var s = d.Score;
            var notes = new List<string>();
            if (s.CapReason is not null) notes.Add(s.CapReason);
            notes.AddRange(d.Notes);
            return Cells(
                Line(d.DisplayName + (d.DistroId == r.SelectedDistroId ? " (your pick)" : "")),
                Line($"{s.Score} out of 100{(s.Incomplete ? ", incomplete" : "")}"),
                Line(s.ExtraDriverCount == 0 ? "None" : s.ExtraDriverCount.ToString(En)),
                Line(notes.Count == 0 ? "" : string.Join(" ", notes)));
        }).ToList();
        b.Add(new Table("Scores for each version of Linux on this PC", ["Version of Linux", "Score", "Extra steps", "Notes"], rows));

        var ordered = r.Components.OrderBy(c => c.Importance).ThenBy(c => c.Component.Category).ToList();
        b.Add(new Details("Every part on every version",
        [
            new Table("How each part works on each version of Linux", ["Part", .. r.Distros.Select(d => ShortName(r, d.DistroId))],
                ordered.Select(c => (IReadOnlyList<IReadOnlyList<Inline>>)[Line(PartName(c)), .. r.Distros.Select(d => (IReadOnlyList<Inline>)[new RatingTag(c.For(d.DistroId).Rating)])]).ToList()),
        ]));
    }

    private static void YourPc(HardwareProfile p, List<Block> b)
    {
        var s = p.System;
        var rows = new List<(string, string?)>
        {
            ("PC", string.Join(" ", new[] { s.SystemManufacturer, s.SystemModel }.Where(x => x is not null))),
            ("Motherboard", s.BoardManufacturer),
            ("Type", Labels.Of(s.FormFactor)),
            ("Windows", s.Windows.FriendlyName),
            ("Free space on the Windows drive", s.SystemDriveFreeBytes is { } f && s.SystemDriveTotalBytes is { } t ? $"{f / 1073741824} GB of {t / 1073741824} GB" : null),
            ("Processor type", Labels.Of(s.Architecture)),
            ("Boot mode", Labels.Of(s.Firmware)),
            ("Secure Boot", Labels.Of(s.SecureBoot)),
            ("Storage mode", Labels.Of(s.StorageMode)),
            ("Fast Startup", Labels.Of(s.FastStartup)),
            ("BitLocker", Labels.Of(s.BitLocker)),
        };
        b.Add(new Details("Your PC's settings",
        [
            new Table("This PC's settings that matter for Linux", ["Setting", "Value"],
                rows.Where(x => !string.IsNullOrEmpty(x.Item2)).Select(x => Cells(Line(x.Item1), Line(x.Item2!))).ToList()),
        ]));
    }

    /// <summary>
    /// The small print about where the ratings came from, including how the online check went.
    /// Shared with the app, where it's a footnote rather than a box at the top.
    /// </summary>
    public static string Footnote(CompatibilityReport r)
    {
        var text = $"Checked with GreyHaven's built-in data from {r.Data.BuiltOn.ToString("d MMMM yyyy", En)}.";
        if (r.DataNotes.Any(n => n.Contains("turned off", StringComparison.Ordinal))) return text + " The online check was off.";
        var incomplete = r.DataNotes.FirstOrDefault(n => n.StartsWith("Online lookups were incomplete", StringComparison.Ordinal));
        return incomplete is null
            ? text
            : text + " The online check didn't finish, so a few parts may say \"Not sure\". " + incomplete["Online lookups were incomplete: ".Length..].Split(" Ratings use", 2)[0];
    }

    private static void Footer(CompatibilityReport r, List<Block> b)
    {
        b.Add(new Heading(2, "About this report"));
        b.Add(P(Footnote(r)));
        var other = r.Profile.Warnings.ToList();
        if (other.Count > 0) b.Add(new ListBlock(ListKind.Bullets, other.Select(n => Line(n)).ToList()));
        b.Add(P("This report has part ID numbers and Windows settings only. It has no serial numbers, names or network addresses, so it's safe to share when asking for help."));
        var made = new List<Inline> { new Text("Made with GreyHaven, which checks how well a PC will work with Linux.") };
        if (GreyHavenInfo.CanLinkToRepository)
        {
            made.Add(new Text(" "));
            made.Add(new Link("GreyHaven on GitHub", $"https://github.com/{GreyHavenInfo.GitHubRepository}"));
        }
        b.Add(new Paragraph(made));
    }

    private static string PartName(RatedComponent c)
    {
        var name = c.Component.Count > 1 ? $"{c.DisplayName} (×{c.Component.Count})" : c.DisplayName;
        return $"{Labels.Of(c.Component.Category)}: {name}";
    }
}
