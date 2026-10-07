using GreyHaven.Core.Compatibility;
using GreyHaven.Core.Hardware;
using static GreyHaven.Reports.Doc;

namespace GreyHaven.Reports;

/// <summary>One screen of the "What's next" walkthrough.</summary>
/// <param name="Tip">The "What could go wrong?" tip, if any.</param>
/// <param name="Options">Only on the install step: keep Windows too, or replace it.</param>
public sealed record WalkStep(string Id, string Title, IReadOnlyList<Block> Body, string? Tip, IReadOnlyList<InstallOption> Options);

/// <summary>A way to install: alongside Windows or replacing it.</summary>
/// <param name="Lines">One or two short lines for the choice card.</param>
/// <param name="Possible">False when it can't be done as-is (e.g. too little free space).</param>
/// <param name="ConfirmText">Set when the user must tick a box before the steps are shown (erasing).</param>
/// <param name="Steps">What to do once chosen (or why it isn't possible).</param>
public sealed record InstallOption(string Id, string Title, IReadOnlyList<string> Lines, bool Possible, string? ConfirmText, IReadOnlyList<Block> Steps);

/// <summary>
/// The guided steps from Windows to Linux for the path the user chose, in beginner words: back
/// up, USB stick, this PC's Windows checklist, download, write the stick, start from it, try
/// Linux, install (alongside or replace), after installing. Shown one step per screen in the app
/// and all together in the saved report. Firmware and disk changes are instructions only.
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

    public static IReadOnlyList<WalkStep> Build(CompatibilityReport r)
    {
        var path = r.SelectedRecommendation ?? r.Recommendation.Paths.FirstOrDefault();
        if (path is null) return [];
        var guide = r.LiveUsbGuides.First(g => g.DistroId == path.DistroId);
        var name = PlainWords.ShortName(guide.DistroName);
        var size = guide.InstallerBytes is { } b ? PlainWords.AboutGb(b) : null;
        var s = r.Profile.System;

        return
        [
            Backup(),
            UsbStick(name, size),
            WindowsReady(r, path, name),
            Download(guide, name, size),
            WriteStick(guide),
            StartFromUsb(r, path, name),
            TryLinux(guide, name),
            Install(path, name, s),
            AfterInstalling(r, path, name),
        ];
    }

    private static WalkStep Step(string id, string title, IReadOnlyList<Block> body, string? tip = null, IReadOnlyList<InstallOption>? options = null) =>
        new(id, title, body, tip, options ?? []);

    internal static Block Checklist(params string[] items) => new ListBlock(ListKind.Checkboxes, items.Select(i => Line(i)).ToList());

    private static WalkStep Backup() => Step("backup", "Back up your files",
    [
        P("First, save a copy of anything you'd hate to lose."),
        Checklist("Photos, videos and music", "Documents and downloads", "Passwords saved in your web browser (or sign in to sync them)", "Game saves and app settings you care about"),
        new Details("Easy ways to back up",
        [
            P(new Strong("An external drive. "), new Text("Plug it in. Copy your Documents, Pictures, Videos, Music and Desktop folders onto it.")),
            P(new Strong("Cloud storage. "), new Text("OneDrive, Google Drive or Dropbox can keep a copy online.")),
            P("Then open a few files from the backup on another device, to make sure they work."),
        ]),
    ],
    "Trying Linux from a USB stick won't touch your files. Installing can, if you pick the wrong option. A backup means a mistake costs you nothing.");

    private static WalkStep UsbStick(string name, string? size) => Step("usb", "Get a USB stick",
    [
        P(new Text("You need a USB stick that holds "), new Strong("at least 8 GB"), new Text(size is null ? "." : $". The {name} installer file is {size}.")),
        new Callout(CalloutKind.Danger, "Everything on the stick will be erased", Line("Use an empty stick, or copy its files somewhere safe first.")),
        new PictureGuide([new PictureStep("Any USB stick of 8 GB or more will do.", "A USB stick with \"8 GB or more\" printed on it", "usb-stick")]),
    ],
    "Some cheap or old sticks fail while being written. If that happens, try a different stick.");

    private static WalkStep WindowsReady(CompatibilityReport r, RecommendationPath path, string name)
    {
        var s = r.Profile.System;
        var items = new List<Block> { P("These are just for this PC. Tick each one when it's done.") };
        var list = new List<IReadOnlyList<Inline>>();
        var how = new List<Block>();

        if (s.FastStartup == FastStartupState.Enabled)
        {
            list.Add([new Strong("Turn off Fast Startup. "), new Text("With it on, Windows never fully shuts down. Then Linux can't safely use your drive.")]);
            how.Add(new Details("How to turn off Fast Startup",
            [
                new ListBlock(ListKind.Numbered,
                [
                    Line("Open Control Panel, then Power Options."),
                    Line("Click \"Choose what the power buttons do\"."),
                    Line("Click \"Change settings that are currently unavailable\"."),
                    Line("Untick \"Turn on fast startup\", then click Save changes."),
                ]),
                P("A later version of GreyHaven will do this for you, with an undo button."),
            ]));
        }
        var bitLocker = s.BitLocker is not (BitLockerState.NotEncrypted or BitLockerState.Unknown);
        if (bitLocker)
        {
            list.Add([new Strong("Save your BitLocker key. "), new Text(PlainWords.BitLocker + " Windows may ask for its key after you change settings.")]);
            how.Add(new Details("How to save your BitLocker key",
                [P(new Text("Go to "), new Link("aka.ms/myrecoverykey", "https://aka.ms/myrecoverykey"), new Text(" and sign in. Write the key on paper."))]));
        }
        if (path.Requirements.Contains(PreSwitchRequirement.TurnOffSecureBoot))
        {
            list.Add([new Strong("Turn off Secure Boot. "), new Text($"{PlainWords.SecureBoot} {name} won't start while it's on.")]);
            var sb = new List<Block> { P("You change it in your PC's startup settings (sometimes called the BIOS).") };
            if (path.SecureBootGuide is { } g)
            {
                var inlines = new List<Inline> { new Text("Follow this guide: ") };
                inlines.Add(g.Url is not null ? new Link(g.Title, g.Url) : new Text($"{g.Title} ({g.LocalPath} in the GreyHaven folder)"));
                sb.Add(new Paragraph(inlines));
            }
            how.Add(new Details("How to turn off Secure Boot", sb));
        }
        if (s.StorageMode is StorageControllerMode.IntelVmd or StorageControllerMode.IntelRstRaid or StorageControllerMode.Raid)
        {
            list.Add([new Strong("Change a storage setting. "), new Text("Your drive is set up in a way Linux can't use. It needs to be switched to \"AHCI\".")]);
            how.Add(new Details("Why this needs care",
                [P("Switching it the wrong way can stop Windows from starting. GreyHaven will give you a careful, step-by-step guide for this in a later version. Until then, ask for help before you change it.")]));
        }
        var maker = r.Maker;
        list.Add([new Strong("Check for a BIOS update (optional). "), new Text($"{maker?.Name ?? "Your PC maker"} sometimes fixes startup problems with updates.")]);
        how.Add(new Details("How to check for a BIOS update", maker is not null
            ? [P(new Link($"Open {maker.Name}'s support page", maker.SupportUrl), new Text(maker.FromMotherboard ? " (the maker of your motherboard). " : ". "), new Text("Look up your model. If there's a BIOS update, follow their steps.")),
               P("Keep the PC plugged in. Never turn it off during an update.")]
            : [P("Search your PC maker's website for your model and \"BIOS update\". Keep the PC plugged in, and never turn it off during an update.")]));

        items.Add(new ListBlock(ListKind.Checkboxes, list));
        items.AddRange(how);
        return Step("windows", "Get Windows ready", items, bitLocker
            ? "If Windows asks for a BitLocker recovery key after you change a setting, type the key you saved. Then everything works as before."
            : "Can't find a setting? Menus look different on every PC. Follow your PC maker's guide, and skip the optional item if you're unsure.");
    }

    private static WalkStep Download(LiveUsbGuide g, string name, string? size)
    {
        var body = new List<Block>
        {
            P($"Next, you get the {name} installer file{(size is null ? "" : $" ({size})")}."),
            P("GreyHaven will download it for you and check it's genuine. That means nobody has changed it on the way to you."),
            new Callout(CalloutKind.Info, "Coming soon", Line("This GreyHaven test version can't download yet.")),
        };
        var manual = new List<Inline> { new Text("For now, download it yourself") };
        if (g.Website is not null) { manual.Add(new Text(" from ")); manual.Add(new Link(g.Website, g.Website)); }
        manual.Add(new Text($". Pick the {g.EditionName}."));
        body.Add(new Paragraph(manual));
        return Step("download", "Download the Linux installer file", body,
            "Slow internet? It can take a while. If the check ever fails, GreyHaven throws the file away and tries again. It never uses a bad file.");
    }

    private static WalkStep WriteStick(LiveUsbGuide g)
    {
        var body = new List<Block>
        {
            P("Plug in your USB stick. GreyHaven writes the installer file onto it. It takes about 10 minutes."),
            P("GreyHaven only lists USB sticks. It never shows your PC's own drives."),
            new Callout(CalloutKind.Info, "Coming soon", Line("This GreyHaven test version can't write USB sticks yet.")),
        };
        if (g.MakeUsbGuide is { } mk) body.Add(P(new Text("For now, follow this guide: "), SourceLink(mk), new Text(".")));
        return Step("write", "Put it on the USB stick", body,
            "Afterwards, Windows may say \"You need to format the disk\". Click Cancel. That's normal: Windows just can't read Linux sticks.");
    }

    private static WalkStep StartFromUsb(CompatibilityReport r, RecommendationPath path, string name)
    {
        var body = new List<Block> { P("Leave the USB stick plugged in.") };
        if (r.Maker is { } m)
        {
            body.Add(new KeyFact($"On {m.Name} {(m.FromMotherboard ? "motherboards" : "PCs")}, the startup menu key is", m.BootMenuKey));
            var isInstruction = m.BootMenuKey.Contains(' ', StringComparison.Ordinal) && m.BootMenuKey.Length > 12;
            body.Add(new PictureGuide(
            [
                new PictureStep("Restart your PC.", "The Windows restart button", "restart"),
                new PictureStep(isInstruction ? m.BootMenuKey + "." : $"As soon as the screen goes dark, press {m.BootMenuKey} again and again.", $"A keyboard with the {m.BootMenuKey} key marked", Illustrations.BootKeyId(m.BootMenuKey)),
                new PictureStep("Pick your USB stick from the list.", "A startup menu listing a USB stick", "boot-menu"),
            ]));
            if (m.BootMenuNote is not null) body.Add(P(m.BootMenuNote));
            body.Add(new Details("If that doesn't work", [AdvancedStartup(r)]));
        }
        else
        {
            body.Add(P("We don't know the startup menu key for this PC. This way always works:"));
            body.Add(AdvancedStartup(r));
        }
        var sb = path.Requirements.Contains(PreSwitchRequirement.TurnOffSecureBoot)
            ? " A red \"Secure Boot violation\" message means Secure Boot is still on (see step 3)." : "";
        return Step("boot", "Start your PC from the USB stick", body,
            "Windows starts as normal? You pressed the key too late. Try again, pressing it the moment the PC turns on. USB stick not listed? Try a USB port on the back of the PC." + sb);
    }

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

    internal static WalkStep TryLinux(LiveUsbGuide g, string name)
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
        return Step("try", "Try Linux without installing", body,
            "Everything is a bit slow, because it's running from the USB stick. That's normal. Something didn't work? Note it down. Your GreyHaven report can help find out why.");
    }

    private static WalkStep Install(RecommendationPath path, string name, SystemInfo s)
    {
        var free = s.SystemDriveFreeBytes;
        var need = LinuxMinimumBytes + WindowsKeepBytes;
        var enough = free is null || free >= need;
        var giveGb = free is { } f ? Math.Max(30, Math.Min(100, (int)((f - WindowsKeepBytes) / Gb - 10) / 10 * 10)) : 60;
        var pop = path.DistroId == "pop-os";
        var encrypted = s.BitLocker is not (BitLockerState.NotEncrypted or BitLockerState.Unknown);

        var alongsideLines = new List<string> { $"Each time your PC starts, you pick Windows or {name}." };
        alongsideLines.Add(free is { } fb
            ? enough ? $"You have {fb / Gb} GB free. Give {name} about {giveGb} GB." : $"You only have {fb / Gb} GB free. You need at least {need / Gb} GB."
            : $"You need at least {need / Gb} GB of free space.");

        IReadOnlyList<Block> alongsideSteps =
            !enough ? [new Callout(CalloutKind.Warning, "There isn't enough free space yet",
                Line($"Free up at least {(need - free!.Value + Gb - 1) / Gb} GB in Windows (Settings > System > Storage), then scan again. Or choose Replace Windows."))]
            : pop ? [new Callout(CalloutKind.Warning, "Pop!_OS can't set this up for you",
                Line("You would shrink Windows yourself, then use the installer's Custom option. That's tricky for a first try. Ubuntu and Linux Mint can do it for you.")),
                P(new Text("If you still want to: "), new Link("System76's support articles", "https://support.system76.com/"), new Text(" explain how."))]
            : [.. encrypted ? TurnOffEncryption(s) : [],
               new PictureGuide(InstallerSteps(path.DistroId, name, alongside: true, giveGb)),
               new Callout(CalloutKind.Tip, "What could go wrong?", Line("Plug in a laptop before you start. When the PC restarts, you'll see a menu. Pick Windows or Linux with the arrow keys, then press Enter."))];

        return Step("install", "Choose how to install",
        [
            P("Happy with how Linux worked? Now choose how to install it."),
            P("Installer screens change a little between versions. Follow the words on screen if they look different."),
        ],
        null,
        [
            new InstallOption("alongside", "Keep Windows too", alongsideLines, enough && !pop, null, alongsideSteps),
            new InstallOption("replace", "Replace Windows", [$"{name} uses the whole drive. Windows and everything on the drive is erased."], true,
                "I understand everything on this drive will be erased, and I've backed up my files.",
                [new Callout(CalloutKind.Danger, "This erases Windows and every file on this drive", Line("Your photos, documents, games and programs will be gone. Only continue if your backup is done.")),
                 new PictureGuide(InstallerSteps(path.DistroId, name, alongside: false, giveGb)),
                 new Callout(CalloutKind.Tip, "What could go wrong?", Line("Plug in a laptop before you start. If the power goes off while installing, start again from the USB stick."))]),
        ]);
    }

    /// <summary>
    /// Each distro's installer, step by step, matching the button labels on the real screens
    /// (checked against the official guides' screenshots; see assets/illustrations/catalog.json).
    /// </summary>
    internal static IReadOnlyList<PictureStep> InstallerSteps(string distroId, string name, bool alongside, int giveGb, string? opening = null)
    {
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
                        S($"Drag the divider to give Linux Mint about {giveGb} GB, then click Install Now.", "The divider between Windows and Linux Mint", "divider"),
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
                S("Tick both boxes for extra software, then click Next.", "Both boxes ticked on Optimise your computer", "third-party"),
                .. alongside
                    ? new[]
                    {
                        S($"Choose Install {name} alongside Windows Boot Manager, then click Next.", "Disk setup with the alongside option chosen", "alongside"),
                        S($"Drag the slider to give {name} about {giveGb} GB, then click Next.", "The slider between Windows and Ubuntu", "divider"),
                    }
                    : new[] { S($"Choose Erase disk and install {name}, then click Next.", "Disk setup with Erase disk chosen", "erase") },
                S("Type your name and a password, then click Next.", "The Create your account screen", "account"),
                S("Pick your time zone, then click Next.", "The time zone map", "timezone"),
                S(alongside ? "Check your choices, then click Install." : "Check it says Erase disk, then click Install.", "Review your choices with the Install button", "review"),
                S("When it's done, click Restart now, then take out the USB stick.", "The Restart now button", "restart"),
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

    internal static WalkStep AfterInstalling(CompatibilityReport r, RecommendationPath path, string name)
    {
        var id = path.DistroId;
        var updater = id switch { "mint" => "Update Manager", "pop-os" => "COSMIC Store", _ => "Software Updater" };
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
            if (fix.SecureBootAdvice is not null) body.Add(new Callout(CalloutKind.Info, "A blue screen after restarting is normal", Line(fix.SecureBootAdvice)));
        }
        body.Add(new Heading(3, "Where to get help"));
        if (Help.TryGetValue(id, out var help))
            body.Add(new ListBlock(ListKind.Bullets, help.Select(h => (IReadOnlyList<Inline>)[new Link(h.Title, h.Url)]).ToList()));
        body.Add(P("When you ask for help, share your GreyHaven report. It lists your parts without any personal details."));
        return Step("after", "After installing", body,
            "Screen stays black after the graphics step? Restart and wait a minute. Still black? Ask in the help forums and share your report. People see this a lot and can help quickly.");
    }
}
