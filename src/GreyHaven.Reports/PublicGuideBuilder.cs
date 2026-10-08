using GreyHaven.Core;
using GreyHaven.Core.Compatibility;
using static GreyHaven.Reports.Doc;

namespace GreyHaven.Reports;

/// <summary>
/// The public phone guide: what the QR code opens. Built from a <see cref="PhoneGuidePage"/>
/// (data files only), so it's the same for everyone who picked the same distro, download and
/// install choice. Personal touches come only from the address after "#" (<see cref="GuideFragment"/>).
/// The person's app list never goes here; it's in their printed or saved copy.
/// </summary>
public static class PublicGuideBuilder
{
    public static ReportDocument Build(PhoneGuidePage page)
    {
        var name = page.DistroName;
        var keep = page.Install == InstallChoice.KeepWindows;
        var pop = page.DistroId == "pop-os";
        var b = new List<Block>
        {
            new Heading(1, $"Your steps to {name}"),
            P($"From GreyHaven. For the {page.EditionName}, {(keep ? "keeping Windows too" : "replacing Windows")}."),
            new Details("PC didn't start from the USB stick?",
            [
                P(new Text("Restart and press "), new Fill("k", "your PC's startup menu key (often F12, F11, F8 or Esc)"), new Text(" again and again as the PC starts. Then pick your USB stick.")),
            ]),
        };
        if (pop)
            b.Add(new Details("A red \"Secure Boot violation\" message?",
            [
                P(new Text("Secure Boot is still on. Restart and press "), new Fill("s", "your startup settings key (often Delete or F2)"),
                  new Text(" as the PC starts. Go to "), new Fill("sb", "the Secure Boot setting"), new Text(", set Secure Boot to Disabled, then save and restart.")),
            ]));

        // 1. Try Linux
        b.Add(new Heading(2, "1. Try Linux"));
        b.Add(P(new Strong("See how it works on your PC. Nothing is installed yet.")));
        b.Add(P(new Text("Choose "), new Strong($"Try {name}"), new Text(" when it asks. Nothing is installed, and your files aren't touched.")));
        b.Add(P("Check these things work. Skip any your PC doesn't have:"));
        b.Add(Checks(page.Checks));
        b.Add(new OnlyIf("f:laptop", [new Heading(3, "On a laptop"), Checks(page.LaptopChecks)]));
        b.Add(new OnlyIf("f:desktop", [new Heading(3, "On a desktop"), Checks(page.DesktopChecks)]));
        if (page.Fixes.Count > 0)
            b.Add(P("If GreyHaven said a part needs an extra step after installing, it may not work fully yet. That's expected."));
        b.Add(P($"Happy with how it works? Go on to Install. Not happy? Shut down from the power menu, take out the stick, and Windows starts as before. Nothing has changed."));
        b.Add(Tip("Everything is a bit slow, because it's running from the USB stick. That's normal. Something didn't work? Note it down. Your GreyHaven report can help find out why."));

        // 2. Install
        b.Add(new Heading(2, $"2. Install {name}"));
        b.Add(P(new Strong(keep ? $"Put {name} next to Windows. Each time your PC starts, you pick one." : $"Put {name} on the whole drive. This erases Windows and your files.")));
        if (keep)
            b.Add(new OnlyIf("has:g", [P(new Text($"GreyHaven suggests giving {name} about "), new Fill("g", "30"), new Text(" GB."))]));
        else
            b.Add(new Callout(CalloutKind.Danger, "This erases Windows and every file on this drive", Line("Only continue if your backup is done.")));
        var opening = page.DistroId == "mint" ? "" : "Open the installer from the desktop (its name starts with \"Install\"). Or restart from the stick: it opens by itself.";
        b.Add(new PictureGuide(WalkthroughBuilder.InstallerSteps(page.DistroId, name, keep, giveGb: null, opening)));
        if (keep) b.Add(P("When your PC restarts, you'll see a menu. Pick Windows or Linux with the arrow keys, then press Enter."));
        b.Add(P("Installer screens change a little between versions. Follow the words on screen if they look different."));
        b.Add(new OnlyIf("f:laptop", [Tip("Plug in your laptop before you start. If the power goes off while installing, start again from the USB stick.")]));

        // 3. After installing
        b.Add(new Heading(2, "3. After installing"));
        b.Add(P(new Strong($"A few last things to make {name} yours.")));
        b.Add(new Heading(3, "Install updates"));
        b.Add(P(new Text("Open "), new Strong(WalkthroughBuilder.Updater(page.DistroId)), new Text(" and install everything it offers. Then restart.")));
        foreach (var f in page.Fixes)
        {
            var more = new List<Block>
            {
                new Heading(3, $"If GreyHaven said your {f.Title} needs an extra step"),
                P("Follow the pictures. Don't download drivers from websites: that can break your setup."),
            };
            var category = f.Id.Contains("gpu", StringComparison.Ordinal) ? "gpu" : f.Id.Contains("wifi", StringComparison.Ordinal) ? "wifi" : null;
            if (f.Fix.Steps.Count > 0)
                more.Add(new PictureGuide(f.Fix.Steps.Select((st, i) =>
                {
                    var id = category is null ? null : $"{page.DistroId}-{category}-{i + 1}";
                    return new PictureStep(st, st, id is not null && Illustrations.Exists(id) ? id : null);
                }).ToList()));
            if (f.Fix.Commands.Count > 0)
            {
                more.Add(P(f.Fix.Steps.Count > 0 ? "Or type this in a terminal:" : "Type this in a terminal:"));
                more.Add(new CommandBlock(f.Fix.Commands));
            }
            if (f.Fix.SecureBootAdvice is not null) more.Add(new Callout(CalloutKind.Info, "If a blue screen appears after restarting", Line(f.Fix.SecureBootAdvice)));
            b.Add(new OnlyIf($"x:{f.Id}", more));
        }
        b.Add(new Heading(3, "Install your apps"));
        b.Add(P("Your list of apps, with where to get each one, is in the printed or saved copy of your guide from GreyHaven. It isn't on this page, to keep it private."));
        if (keep) b.AddRange(JourneyBuilder.KeepingWindows(name, new Fill("k", "your PC's startup menu key")));
        b.Add(new Heading(3, "Where to get help"));
        if (WalkthroughBuilder.Help.TryGetValue(page.DistroId, out var help))
            b.Add(new ListBlock(ListKind.Bullets, help.Select(h => (IReadOnlyList<Inline>)[new Link(h.Title, h.Url)]).ToList()));
        b.Add(P("When you ask for help, share your GreyHaven report. It lists your parts without any personal details."));
        b.Add(P($"This page is the same for everyone who picked {name} ({page.EditionName}) and chose to {(keep ? "keep Windows" : "replace Windows")}. Anything personal on it comes from the part of the address after \"#\", which your browser keeps to itself. GreyHaven {GreyHavenInfo.Version}."));
        return new ReportDocument($"Your steps to {name} - GreyHaven", b);
    }

    private static ListBlock Checks(IReadOnlyList<LiveUsbCheck> checks) =>
        new(ListKind.Checkboxes, checks.Select(c => (IReadOnlyList<Inline>)[new Strong(c.Title + ". "), new Text(c.Test)]).ToList());

    private static Callout Tip(string text) => new(CalloutKind.Tip, "What could go wrong?", Line(text));
}
