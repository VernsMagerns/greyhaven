using GreyHaven.Core.Compatibility;
using GreyHaven.Core.Hardware;
using static GreyHaven.Reports.Doc;

namespace GreyHaven.Reports;

/// <summary>
/// Shared step content for <see cref="JourneyBuilder"/> and <see cref="PublicGuideBuilder"/>: each
/// installer's screens, trying Linux, driver steps after installing, turning off encryption,
/// Windows' Advanced startup, and where to get help.
/// </summary>
public static class WalkthroughBuilder
{
    public const long LinuxMinimumBytes = 30L * Gb;
    public const long WindowsKeepBytes = 20L * Gb;
    internal const long Gb = 1073741824;

    internal static readonly IReadOnlyDictionary<string, (string Title, string Url)[]> Help = new Dictionary<string, (string, string)[]>
    {
        ["ubuntu"] = [("Ubuntu Discourse (questions and answers)", "https://discourse.ubuntu.com/"), ("Ask Ubuntu", "https://askubuntu.com/")],
        ["mint"] = [("Linux Mint forums", "https://forums.linuxmint.com/")],
        ["pop-os"] = [("System76 support articles", "https://support.system76.com/"), ("Pop!_OS chat", "https://chat.pop-os.org/")],
    };

    internal static Block Checklist(params string[] items) => new ListBlock(ListKind.Checkboxes, items.Select(i => Line(i)).ToList());

    internal static Block AdvancedStartup(CompatibilityReport r)
    {
        var win11 = r.Profile.System.Windows.IsWindows11;
        var settings = win11 ? "Settings > System > Recovery" : "Settings > Update & Security > Recovery";
        return new PictureGuide(
        [
            new PictureStep($"In Windows, open {settings}.", $"Windows {(win11 ? "11" : "10")} Recovery settings", win11 ? "recovery-11" : "recovery-10"),
            new PictureStep("Next to Advanced startup, click Restart now.", "The Restart now button", "advanced-startup"),
            new PictureStep("On the blue screen, choose Use a device, then your USB stick.", "The blue \"Use a device\" menu", "use-a-device"),
        ]);
    }

    internal static (IReadOnlyList<Block> Body, string Tip) TryLinux(LiveUsbGuide g, string name)
    {
        var body = new List<Block>
        {
            P(new Text("Choose "), new Strong($"Try {name}"), new Text(" when it asks. Nothing is installed, and your files aren't touched.")),
            P("Check these things work:"),
            new ListBlock(ListKind.Checkboxes, g.Checks.Select(c => (IReadOnlyList<Inline>)[new Strong(c.Title + ". "), new Text(c.Test)]).ToList()),
        };
        if (g.BeforeYouStart.Count > 0) body.Insert(0, new Callout(CalloutKind.Warning, "Before you start", Line(string.Join(" ", g.BeforeYouStart))));
        if (g.ExpectedInLiveSession.Count > 0) body.Add(P(string.Join(" ", g.ExpectedInLiveSession)));
        body.Add(P("Done? Shut down from the power menu, take out the stick, and Windows starts as before."));
        if (g.ShareCommands.Count > 0)
            body.Add(new Details("Optional: share your results to help others",
            [
                P("Open a terminal and type:"),
                new CommandBlock(g.ShareCommands),
                P(g.ShareNote ?? ""),
            ]));
        return (body,
            "Everything is a bit slow, because it's running from the USB stick. That's normal. Something didn't work? Note it down. Your GreyHaven report can help find out why.");
    }

    /// <summary>
    /// Each distro's installer, step by step, matching the button labels on the real screens
    /// (checked against the official guides' screenshots; see assets/illustrations/catalog.json).
    /// </summary>
    internal static IReadOnlyList<PictureStep> InstallerSteps(string distroId, string name, bool alongside, int? giveGb, string? opening = null)
    {
        // Without this PC's free space (the public phone guide), give the minimum and say more is better.
        var give = giveGb is { } g ? $"about {g} GB" : "at least 30 GB (more if you have room)";
        PictureStep S(string text, string caption, string id) => new(text, caption, $"{distroId}-{id}");
        var again = opening ?? "Start from the USB stick again, like in step 6.";
        return distroId switch
        {
            // Pop!_OS has no automatic "alongside Windows" option, so only a clean install is offered.
            "pop-os" =>
            [
                S(opening is not null ? $"{opening} Pick your language." : $"{again} The installer opens by itself. Pick your language.", "The Pop!_OS installer's language list", "language"),
                S("Pick your keyboard layout.", "The keyboard layout list", "keyboard"),
                S("Choose Clean Install, then click Clean Install at the bottom right.", "Clean Install chosen, and the Clean Install button", "clean-install"),
                S("Pick your drive, then click Erase and Install.", "The drive list and the Erase and Install button", "erase"),
                S("Type your name, then a password.", "The user account screen", "account"),
                S("When it asks about encryption, Don't Encrypt is fine.", "The Drive Encryption screen with Don't Encrypt", "encryption"),
                S("When it's done, click Restart Device, then take out the USB stick.", "The Restart Device button", "restart"),
            ],
            "mint" =>
            [
                S(opening is not null ? "Double-click Install Linux Mint on the desktop." : $"{again} Then double-click Install Linux Mint on the desktop.", "The Install Linux Mint icon on the desktop", "install-icon"),
                S("Pick your language, then click Continue.", "The language list and the Continue button", "language"),
                S("Pick your keyboard layout, then click Continue.", "The keyboard layout screen", "keyboard"),
                S("Connect to Wi-Fi, then click Continue.", "The wireless network list", "wifi"),
                S("Tick Install multimedia codecs, then click Continue.", "The multimedia codecs box, ticked", "codecs"),
                .. alongside
                    ? new[]
                    {
                        S("Choose Install Linux Mint alongside Windows Boot Manager, then click Continue.", "Installation type with the alongside option chosen", "alongside"),
                        S($"Drag the divider to give Linux Mint {give}, then click Install Now.", "The divider between Windows and Linux Mint", "divider"),
                    }
                    : new[]
                    {
                        S("Choose Erase disk and install Linux Mint, then click Continue.", "Installation type with Erase disk chosen", "erase"),
                        S("Check it's the right disk, then click Continue to write the changes.", "The Write the changes to disks? box", "erase-check"),
                    },
                S("Pick your time zone, then click Continue.", "The Where are you? map", "timezone"),
                S("Type your name and a password, then click Continue.", "The Who are you? screen", "account"),
                S("When it's done, click Restart Now, then take out the USB stick.", "The Installation Complete box with Restart Now", "restart"),
            ],
            _ =>
            [
                S(opening is not null ? $"{opening} Pick your language, then click Next." : $"{again} The installer opens by itself. Pick your language, then click Next.", "The language list and the Next button", "language"),
                S("Click Next on Accessibility. Pick your keyboard, then click Next.", "The keyboard layout screen", "keyboard"),
                S("Connect to the internet, then click Next.", "The Connect to the internet screen", "internet"),
                S($"Choose Install {name}, then click Next.", $"Install {name} chosen", "try-install"),
                S("Choose Interactive installation, then click Next.", "Interactive installation chosen", "interactive"),
                S("Keep Default selection, then click Next.", "Default selection chosen", "apps"),
                S("Tick both boxes (graphics and Wi-Fi drivers, and media formats), then click Next.", "Both boxes ticked on Optimise your computer", "third-party"),
                .. alongside
                    ? new[]
                    {
                        S($"Choose Install {name} alongside Windows Boot Manager, then click Next.", "Disk setup with the alongside option chosen", "alongside"),
                        S($"Drag the divider to give {name} {give}, then click Next.", "The divider between Windows and Ubuntu", "divider"),
                    }
                    : new[] { S($"Choose Erase disk and install {name}, then click Next.", "Disk setup with Erase disk chosen", "erase") },
                S("Type your name and a password, then click Next.", "The Create your account screen", "account"),
                S("Pick your time zone, then click Next.", "The time zone map", "timezone"),
                S(alongside ? "Check your choices, then click Install." : "Check it says Erase disk, then click Install.", "Review your choices with the Install button", "review"),
                S("When it's done, click Restart now. When the screen says to remove the installation medium, take out the USB stick and press Enter.", "The Restart now button", "restart"),
            ],
        };
    }

    /// <summary>Installers can't resize a drive that BitLocker (or Device encryption) protects, so it's turned off first.</summary>
    internal static IReadOnlyList<Block> TurnOffEncryption(SystemInfo s)
    {
        var home = s.Windows.Edition is { } e && e.StartsWith("Core", StringComparison.Ordinal);
        var win11 = s.Windows.IsWindows11;
        IReadOnlyList<PictureStep> steps = home
            ? [
                new($"In Windows, open {(win11 ? "Settings > Privacy & security > Device encryption" : "Settings > Update & Security > Device encryption")}.", "The Device encryption page in Settings", win11 ? "device-encryption-11" : "device-encryption-10"),
                new("Turn Device encryption off, then click Turn off.", "The Device encryption switch turned off", "device-encryption-off"),
                new("Wait until it says it's off. It can take an hour or more.", "Device encryption turning off", "encryption-wait"),
              ]
            : [
                new("In Windows, open Control Panel, then BitLocker Drive Encryption.", "The BitLocker Drive Encryption page", "bitlocker-panel"),
                new("Click Turn off BitLocker, then Turn off BitLocker again.", "The Turn off BitLocker button", "bitlocker-turn-off"),
                new("Wait until it's done. It can take an hour or more.", "The Decrypting progress bar", "encryption-wait"),
              ];
        return
        [
            new Callout(CalloutKind.Warning, "First, turn off drive encryption", Line("The installer can't make room for Linux while your drive is encrypted. Keep your recovery key safe.")),
            new PictureGuide(steps),
        ];
    }

    internal static string Updater(string distroId) => distroId switch { "mint" => "Update Manager", "pop-os" => "COSMIC Store", _ => "Software Updater" };

    internal static (IReadOnlyList<Block> Body, string Tip) AfterInstalling(CompatibilityReport r, RecommendationPath path, string name)
    {
        var id = path.DistroId;
        var updater = Updater(id);
        var body = new List<Block>
        {
            P($"Welcome to {name}! A few last things."),
            new Heading(3, "1. Install updates"),
            P(new Text("Open "), new Strong(updater), new Text(" and install everything it offers. Then restart.")),
        };
        var n = 2;
        foreach (var c in r.Components)
        {
            var rating = c.For(id);
            if (rating.Rating == Rating.WorksOutOfTheBox || rating.Fix is not { } fix) continue;
            body.Add(new Heading(3, $"{n++}. Set up your {PlainWords.Noun(c.Component.Category)}"));
            body.Add(P($"{c.DisplayName} needs one extra step. Follow the pictures. Don't download drivers from websites: that can break your setup."));
            if (fix.Steps.Count > 0)
                body.Add(new PictureGuide(fix.Steps.Select((st, i) => new PictureStep(st, st, $"{id}-{c.Component.Category.ToString().ToLowerInvariant()}-{i + 1}")).ToList()));
            if (fix.SecureBootAdvice is not null) body.Add(new Callout(CalloutKind.Info, "If a blue screen appears after restarting", Line(fix.SecureBootAdvice)));
        }
        body.Add(new Heading(3, "Where to get help"));
        if (Help.TryGetValue(id, out var help))
            body.Add(new ListBlock(ListKind.Bullets, help.Select(h => (IReadOnlyList<Inline>)[new Link(h.Title, h.Url)]).ToList()));
        body.Add(P("When you ask for help, share your GreyHaven report. It lists your parts without any personal details."));
        return (body,
            "Screen stays black after the graphics step? Restart and wait a minute. Still black? Ask in the help forums and share your report. People see this a lot and can help quickly.");
    }
}
