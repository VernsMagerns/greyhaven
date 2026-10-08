using GreyHaven.Core;
using GreyHaven.Core.Compatibility;
using GreyHaven.Core.Hardware;
using static GreyHaven.Reports.Doc;

namespace GreyHaven.Reports;

/// <summary>
/// The five parts of switching. 1–4 happen in GreyHaven on Windows; 5 happens on the USB stick,
/// where the person follows the guide on their phone, on paper or on the Windows drive.
/// </summary>
public enum JourneyPhase { GetReady = 1, MakeUsb = 2, TakeGuide = 3, Restart = 4, OnTheStick = 5 }

/// <summary>One screen of the white-glove walkthrough.</summary>
/// <param name="Lead">One sentence under the title saying what this screen is for.</param>
/// <param name="Tip">The "What could go wrong?" tip, if any.</param>
public sealed record JourneyStep(string Id, JourneyPhase Phase, string Title, string Lead, IReadOnlyList<Block> Body, string? Tip)
{
    /// <summary>Only on "How do you want to install?".</summary>
    public IReadOnlyList<InstallChoiceOption> Choices { get; init; } = [];
    /// <summary>Only on "Get Windows ready": the things to do, each done by GreyHaven or by hand.</summary>
    public IReadOnlyList<PrepTask> Tasks { get; init; } = [];
    /// <summary>True when the person must tick every checklist item before going on (backup before erasing).</summary>
    public bool ChecklistRequired { get; init; }
    public UsbPlan? Usb { get; init; }
    public RestartPlan? Restart { get; init; }
    public GuideHandoff? Guide { get; init; }
}

public enum CheckState { Ok, Attention, Blocker }

/// <summary>A check run as soon as an install choice is picked, e.g. "481 GB free: enough".</summary>
public sealed record CheckLine(CheckState State, string Text);

/// <param name="Lines">One or two short lines for the choice card.</param>
/// <param name="Recommended">Shown as "Suggested" (keeping Windows, when it's possible).</param>
/// <param name="Possible">False when a check blocks it (e.g. too little free space).</param>
public sealed record InstallChoiceOption(InstallChoice Choice, string Title, IReadOnlyList<string> Lines, bool Recommended, bool Possible, IReadOnlyList<CheckLine> Checks);

/// <summary>Who does a task: GreyHaven with one click (asks first, can undo), or the person with instructions.</summary>
public enum PrepAction { OneClick, Manual }

/// <param name="Done">Already done on this PC (shown with a tick, nothing to do).</param>
/// <param name="ButtonLabel">For one-click tasks, e.g. "Turn off Fast Startup".</param>
/// <param name="Confirm">What GreyHaven will change, shown before it does it.</param>
/// <param name="Undo">How to undo it (a button for one-click tasks).</param>
/// <param name="NeedsStartupSettings">The task is done in the PC's startup settings (BIOS).</param>
public sealed record PrepTask(string Id, string Title, string Why, bool Done, PrepAction Action,
    string? ButtonLabel, string? Confirm, string? Undo, IReadOnlyList<Block> How, bool NeedsStartupSettings = false);

/// <summary>What "Make your USB stick" needs: the right file, how it's checked, the smallest stick that fits.</summary>
/// <param name="Verification">Plain words for how the download is checked for this distro.</param>
public sealed record UsbPlan(string DistroName, string EditionName, long? InstallerBytes, long MinimumStickBytes, string Verification, IReadOnlyList<Source> VerificationSources);

/// <param name="FinalChecks">Ticked before the restart button is enabled.</param>
/// <param name="AfterRestart">What the person will see, picture by picture.</param>
/// <param name="Fallback">If "Use a device" doesn't list the stick: the maker's startup menu key.</param>
/// <param name="Manual">If GreyHaven can't restart the PC: the same thing by hand, in this Windows version's Settings.</param>
public sealed record RestartPlan(IReadOnlyList<string> FinalChecks, string ButtonLabel, IReadOnlyList<Block> AfterRestart, IReadOnlyList<Block> Fallback, IReadOnlyList<Block> Manual);

/// <param name="PhoneUrl">The public phone guide (only once the repository is public), or null.</param>
/// <param name="OnWindowsDrive">Where the guide is also saved, if the live session can read it.</param>
public sealed record GuideHandoff(string? PhoneUrl, string? OnWindowsDrive);

/// <summary>
/// Builds the white-glove walkthrough for the chosen path and install choice:
/// <list type="number">
///   <item>Get ready: how to install (with checks), back up, get Windows ready, startup settings only if needed.</item>
///   <item>Make your USB stick: one step (download, check, write, check again).</item>
///   <item>Take the guide with you: phone, paper, Windows drive.</item>
///   <item>Restart into your USB stick: final checks and one button.</item>
///   <item>On the USB stick: try Linux, install, after installing (the phone/printed guide).</item>
/// </list>
/// Firmware and disk changes stay instructions only; GreyHaven only makes safe, undoable Windows changes.
/// </summary>
public static class JourneyBuilder
{
    /// <summary>The smallest stick the writer accepts, whatever the file size.</summary>
    public const long MinimumStickBytes = 8L * 1000 * 1000 * 1000;

    public const string GuideFileName = "GreyHaven guide.html";

    public static IReadOnlyList<JourneyStep> Build(CompatibilityReport r)
    {
        var path = r.SelectedRecommendation ?? r.Recommendation.Paths.FirstOrDefault();
        if (path is null) return [];
        var ctx = new Ctx(r, path);
        return
        [
            ChooseInstall(ctx),
            Backup(ctx),
            WindowsReady(ctx),
            .. StartupSettings(ctx) is { } settings ? [settings] : Array.Empty<JourneyStep>(),
            MakeUsb(ctx),
            TakeGuide(ctx),
            Restart(ctx),
            .. OnTheStick(r),
        ];
    }

    /// <summary>Phase 5 only: what goes in the phone and printed guide.</summary>
    public static IReadOnlyList<JourneyStep> OnTheStick(CompatibilityReport r)
    {
        var path = r.SelectedRecommendation ?? r.Recommendation.Paths.FirstOrDefault();
        if (path is null) return [];
        var ctx = new Ctx(r, path);
        return [TryLinux(ctx), Install(ctx), AfterInstalling(ctx)];
    }

    /// <summary>The choice GreyHaven suggests: keep Windows when that's possible, else nothing pre-picked.</summary>
    public static InstallChoice? Suggested(CompatibilityReport r)
    {
        var path = r.SelectedRecommendation ?? r.Recommendation.Paths.FirstOrDefault();
        return path is null ? null : KeepWindowsBlocker(new Ctx(r, path)) is null ? InstallChoice.KeepWindows : null;
    }

    private sealed class Ctx(CompatibilityReport r, RecommendationPath path)
    {
        public CompatibilityReport R { get; } = r;
        public RecommendationPath Path { get; } = path;
        public LiveUsbGuide Guide { get; } = r.LiveUsbGuides.First(g => g.DistroId == path.DistroId);
        public SystemInfo S => R.Profile.System;
        public string Name => PlainWords.ShortName(Guide.DistroName);
        public InstallChoice? Choice => R.SelectedInstall;
        public bool Keep => Choice == InstallChoice.KeepWindows;
        public bool Replace => Choice == InstallChoice.ReplaceWindows;
        public bool Encrypted => S.BitLocker is not (BitLockerState.NotEncrypted or BitLockerState.Unknown);
        public bool Laptop => S.FormFactor == FormFactor.Laptop;
        public bool Win11 => S.Windows.IsWindows11;
        public bool SecureBootOff => Path.Requirements.Contains(PreSwitchRequirement.TurnOffSecureBoot);
        public bool StorageNeedsChange => S.StorageMode is StorageControllerMode.IntelVmd or StorageControllerMode.IntelRstRaid or StorageControllerMode.Raid;
        public long Gb => WalkthroughBuilder.Gb;
        public long Need => WalkthroughBuilder.LinuxMinimumBytes + WalkthroughBuilder.WindowsKeepBytes;
        public int GiveGb => S.SystemDriveFreeBytes is { } f
            ? Math.Max(30, Math.Min(100, (int)((f - WalkthroughBuilder.WindowsKeepBytes) / Gb - 10) / 10 * 10))
            : 60;
    }

    private static JourneyStep Step(string id, JourneyPhase phase, string title, string lead, IReadOnlyList<Block> body, string? tip = null) =>
        new(id, phase, title, lead, body, tip);

    // ---- Phase 1: Get ready ------------------------------------------------------------------

    private static string? KeepWindowsBlocker(Ctx c)
    {
        if (c.S.SystemDriveFreeBytes is { } free && free < c.Need)
            return $"You only have {free / c.Gb} GB free. You need at least {c.Need / c.Gb} GB. Free up space in Windows (Settings > System > Storage), then scan again.";
        if (c.Path.DistroId == "pop-os")
            return "Pop!_OS can't set this up by itself. Ubuntu and Linux Mint can. To keep Windows, go back and pick Ubuntu.";
        return null;
    }

    private static JourneyStep ChooseInstall(Ctx c)
    {
        var keepBlocker = KeepWindowsBlocker(c);
        var keepChecks = new List<CheckLine>();
        if (keepBlocker is not null) keepChecks.Add(new(CheckState.Blocker, keepBlocker));
        else if (c.S.SystemDriveFreeBytes is { } free) keepChecks.Add(new(CheckState.Ok, $"Enough room: {free / c.Gb} GB free. {c.Name} gets about {c.GiveGb} GB."));
        else keepChecks.Add(new(CheckState.Attention, $"We couldn't read your free space. You need at least {c.Need / c.Gb} GB."));
        keepChecks.Add(c.Encrypted
            ? new(CheckState.Attention, "Your drive is encrypted. Encryption must be turned off first. GreyHaven can do that for you in a moment.")
            : new(CheckState.Ok, "Your drive isn't encrypted, so the installer can make room."));
        keepChecks.Add(new(CheckState.Ok, "Your files and apps on Windows stay as they are."));

        var replaceChecks = new List<CheckLine>
        {
            new(CheckState.Attention, "Everything on the drive is erased: Windows, apps and files. You'll back up first."),
        };
        if (c.Encrypted) replaceChecks.Add(new(CheckState.Ok, "Your drive is encrypted. That's fine: you'll save the recovery key, just in case."));

        // Apps the person needs that won't work on Linux: keeping Windows keeps them.
        var problems = c.R.MustHaveProblems.Select(a => a.Name).ToList();
        if (problems.Count > 0)
        {
            var names = PlainWords.JoinAnd(problems);
            if (keepBlocker is null) keepChecks.Add(new(CheckState.Ok, $"You can still use {names} on Windows."));
            replaceChecks.Add(new(CheckState.Attention, $"{names} won't work on Linux, and replacing Windows removes {(problems.Count == 1 ? "it" : "them")}."));
        }

        var keepLines = new List<string> { $"Each time your PC starts, you pick Windows or {c.Name}." };
        var options = new List<InstallChoiceOption>
        {
            new(InstallChoice.KeepWindows, "Keep Windows too", keepLines, keepBlocker is null, keepBlocker is null, keepChecks),
            new(InstallChoice.ReplaceWindows, "Replace Windows", [$"{c.Name} uses the whole drive. Windows and everything on it is erased."], false, true, replaceChecks),
        };
        return Step("choose", JourneyPhase.GetReady, "How do you want to install Linux?",
            "This decides what you need to do next. You can still change your mind after trying Linux.",
            [P("Not sure? Keep Windows too. It's the safe choice for a first try, and you can remove Windows later.")])
            with { Choices = options };
    }

    private static JourneyStep Backup(Ctx c)
    {
        var items = new[] { "Photos, videos and music", "Documents and downloads", "Passwords saved in your web browser (or sign in to sync them)", "Game saves and app settings you care about" };
        var body = new List<Block>
        {
            c.Replace
                ? new Callout(CalloutKind.Danger, "You must back up first", Line($"Replacing Windows erases every file on this drive. Tick each item once it's saved somewhere else."))
                : P("Keeping Windows won't touch your files. A backup still means a mistake costs you nothing."),
            WalkthroughBuilder.Checklist(items),
            new Details("Easy ways to back up",
            [
                P(new Strong("An external drive. "), new Text("Plug it in. Copy your Documents, Pictures, Videos, Music and Desktop folders onto it.")),
                P(new Strong("Cloud storage. "), new Text("OneDrive, Google Drive or Dropbox can keep a copy online.")),
                P("Then open a few files from the backup on another device, to make sure they work."),
            ]),
        };
        return Step("backup", JourneyPhase.GetReady, "Back up your files",
            c.Replace ? "Required: replacing Windows erases this drive." : "Recommended, even when you keep Windows.",
            body, c.Replace ? "Not sure your backup worked? Open a few files from it on another device before you go on." : null)
            with { ChecklistRequired = c.Replace };
    }

    private static JourneyStep WindowsReady(Ctx c)
    {
        var tasks = new List<PrepTask>();
        var s = c.S;

        tasks.Add(new PrepTask("fast-startup", "Turn off Fast Startup",
            "With it on, Windows never fully shuts down, so Linux can't safely use your drive.",
            Done: s.FastStartup != FastStartupState.Enabled, PrepAction.OneClick,
            "Turn off Fast Startup", "GreyHaven will turn off Fast Startup. Windows may take a few seconds longer to start.",
            "Turn it back on any time, here or in Control Panel > Power Options.",
            [new Details("Or do it yourself",
            [
                new ListBlock(ListKind.Numbered,
                [
                    Line("Open Control Panel, then Power Options."),
                    Line("Click \"Choose what the power buttons do\"."),
                    Line("Click \"Change settings that are currently unavailable\"."),
                    Line("Untick \"Turn on fast startup\", then click Save changes."),
                ]),
            ])]));

        if (c.Encrypted && !c.Replace)
            tasks.Add(new PrepTask("encryption-off", "Turn off drive encryption",
                $"The installer can't make room for {c.Name} while your drive is encrypted.",
                Done: false, PrepAction.OneClick, "Turn off encryption",
                "GreyHaven will turn off BitLocker on your Windows drive. Your files stay the same. It runs in the background and can take an hour or more.",
                "Turn it back on any time from Control Panel > BitLocker Drive Encryption.",
                [new Details("Or do it yourself", WalkthroughBuilder.TurnOffEncryption(s).Skip(1).ToList())]));
        else if (c.Encrypted)
            tasks.Add(new PrepTask("recovery-key", "Save your recovery key",
                PlainWords.BitLocker + " If something goes wrong before Linux is installed, Windows may ask for this key.",
                Done: false, PrepAction.Manual, null, null, null,
                [P(new Text("Go to "), new Link("aka.ms/myrecoverykey", "https://aka.ms/myrecoverykey"), new Text(" and sign in. Write the key on paper or take a photo with your phone."))]));

        var body = new List<Block>();
        if (tasks.All(t => t.Done)) body.Add(P("Good news: Windows is already set up the right way. There's nothing to change."));
        if (c.R.Maker is { } m)
            body.Add(new Details("Nice to have: check for a BIOS update",
            [
                P($"{m.Name} sometimes fixes startup problems with updates. You don't need one to switch."),
                P(new Link($"Open {m.Name}'s support page", m.SupportUrl), new Text(m.FromMotherboard ? " (the maker of your motherboard)" : ""), new Text(". Look up your model and follow their steps.")),
                P("Keep the PC plugged in. Never turn it off during an update."),
            ]));
        return Step("windows", JourneyPhase.GetReady, "Get Windows ready",
            "GreyHaven does the safe changes for you. It asks first, and you can undo them.",
            body,
            c.Encrypted ? "If Windows ever asks for a BitLocker recovery key, type the key you saved. Then everything works as before." : null)
            with { Tasks = tasks };
    }

    /// <summary>Only when needed: Secure Boot off (Advanced path) or a storage setting Linux can't use.</summary>
    private static JourneyStep? StartupSettings(Ctx c)
    {
        if (!c.SecureBootOff && !c.StorageNeedsChange) return null;
        var tasks = new List<PrepTask>();
        var m = c.R.Maker;
        if (c.SecureBootOff)
        {
            var how = new List<Block>();
            if (m is { SetupKey: { } key, SecureBootPath: { } where })
            {
                how.Add(new KeyFact($"On {m.Name} {(m.FromMotherboard ? "motherboards" : "PCs")}, the startup settings key is", key));
                how.Add(new PictureGuide(
                [
                    new PictureStep($"Restart your PC. As it starts, press {key} again and again.", $"A keyboard with the {key} key marked", Illustrations.BootKeyId(key)),
                    new PictureStep($"Go to {where}.", $"{m.Name}'s startup settings with {where.Split(" > ").Last()} chosen", $"bios-{m.Name.ToLowerInvariant()}-secure-boot"),
                    new PictureStep("Set Secure Boot to Disabled. Press F10, then choose Yes to save and restart.", "Secure Boot set to Disabled", $"bios-{m.Name.ToLowerInvariant()}-secure-boot-off"),
                ]));
            }
            if (c.Path.SecureBootGuide is { } g)
                how.Add(P(new Text(how.Count > 0 ? "More detail: " : "Follow this guide: "),
                    g.Url is not null ? new Link(g.Title, g.Url) : new Text($"{g.Title} ({g.LocalPath} in the GreyHaven folder)")));
            tasks.Add(new PrepTask("secure-boot", "Turn off Secure Boot",
                $"{PlainWords.SecureBoot} {c.Name} won't start while it's on.",
                Done: false, PrepAction.Manual, null, null, "Turn it back on the same way, if you go back to only Windows.", how, NeedsStartupSettings: true));
        }
        if (c.StorageNeedsChange)
            tasks.Add(new PrepTask("storage-mode", "Change a storage setting",
                "Your drive is set up in a way Linux can't use. It needs to be switched to \"AHCI\".",
                Done: false, PrepAction.Manual, null, null, null,
                [P("Switching it the wrong way can stop Windows from starting. Ask for help before you change it. GreyHaven will have a careful, step-by-step guide for this later.")],
                NeedsStartupSettings: true));
        return Step("startup-settings", JourneyPhase.GetReady, "Change a startup setting",
            "This one is in your PC's startup settings (sometimes called the BIOS). GreyHaven can't change it for you.",
            [P("Do it now, then come back to GreyHaven. Your PC will restart into Windows when you save.")],
            c.Encrypted ? "Windows may ask for your BitLocker recovery key after this change. Type the key you saved." : "Can't find the setting? Menus differ a little between models. Look for the same words.")
            with { Tasks = tasks };
    }

    // ---- Phase 2: Make your USB stick --------------------------------------------------------

    private static JourneyStep MakeUsb(Ctx c)
    {
        var g = c.Guide;
        var size = g.InstallerBytes is { } b ? PlainWords.AboutGb(b) : null;
        var (how, sources) = c.Path.DistroId switch
        {
            "pop-os" => ("GreyHaven checks the file's fingerprint (a SHA-256 checksum) against the one System76 publishes.",
                (IReadOnlyList<Source>)[new Source("System76 - Pop!_OS download information", "https://api.pop-os.org/builds/24.04/intel", SourceKind.Reference)]),
            "mint" => ("GreyHaven checks the file's fingerprint (a SHA-256 checksum) and Linux Mint's digital signature on the list of fingerprints.",
                [new Source("Linux Mint Installation Guide - Verify your ISO image", "https://linuxmint-installation-guide.readthedocs.io/en/latest/verify.html", SourceKind.Reference)]),
            _ => ("GreyHaven checks the file's fingerprint (a SHA-256 checksum) and Ubuntu's digital signature on the list of fingerprints.",
                [new Source("Ubuntu - How to verify your Ubuntu download", "https://ubuntu.com/tutorials/how-to-verify-ubuntu", SourceKind.Reference)]),
        };
        var stick = g.InstallerBytes is { } bytes && bytes + 500_000_000 > MinimumStickBytes ? 16 : 8;
        return Step("usb", JourneyPhase.MakeUsb, "Make your USB stick",
            $"GreyHaven gets {c.Name} ({g.EditionName}) and puts it on a USB stick for you.",
            [
                P(new Text("Plug in a USB stick that holds "), new Strong($"at least {stick} GB"), new Text(size is null ? "." : $". The {c.Name} file is {size}.")),
                new Callout(CalloutKind.Danger, "Everything on the stick will be erased", Line("Use an empty stick, or copy its files somewhere safe first. GreyHaven only lists USB sticks, never your PC's own drives.")),
                new ListBlock(ListKind.Numbered,
                [
                    Line("Download: GreyHaven gets the file from the official website."),
                    Line("Check: " + how),
                    Line("Write: GreyHaven copies it onto the stick."),
                    Line("Check again: GreyHaven reads the stick back to make sure every part was copied correctly."),
                ]),
            ],
            "Afterwards, Windows may say \"You need to format the disk\". Click Cancel. That's normal: Windows can't read Linux sticks.")
            with { Usb = new UsbPlan(g.DistroName, g.EditionName, g.InstallerBytes, MinimumStickBytes, how, sources) };
    }

    // ---- Phase 3: Take the guide with you ----------------------------------------------------

    private static JourneyStep TakeGuide(Ctx c)
    {
        var phone = PhoneGuideUrl(c.R);
        var onDrive = c.Encrypted || c.StorageNeedsChange ? null : $@"Documents\GreyHaven\{GuideFileName}";
        // Kept short on purpose: one thing to do, then carry on clicking Next.
        var body = new List<Block>
        {
            phone is not null
                ? P("Scan the code with your phone's camera. Your guide opens on your phone, so you can follow it after your PC restarts.")
                : P("Open the guide and print it, or keep it open on another device, so you can follow it after your PC restarts."),
            P(new Strong("Then click Next."), new Text(" GreyHaven still shows you what happens after the restart before you do it.")),
        };
        return Step("guide", JourneyPhase.TakeGuide, "Take the guide with you",
            "Put the next steps on your phone.",
            body, null)
            with { Guide = new GuideHandoff(phone, onDrive) };
    }

    /// <summary>
    /// The public phone guide's address for this report's choices: the general page for the
    /// distro, download and install choice, plus <see cref="GuideFragment"/> after "#" (which
    /// browsers never send to the website). Null until an install choice is made, or when there's
    /// no page for it (Pop!_OS next to Windows).
    /// </summary>
    public static string? PhoneGuideUrl(CompatibilityReport r)
    {
        var path = r.SelectedRecommendation ?? r.Recommendation.Paths.FirstOrDefault();
        if (path is null || r.SelectedInstall is not { } install) return null;
        if (install == InstallChoice.KeepWindows && !PhoneGuidePages.CanKeepWindows(path.DistroId)) return null;
        return GreyHavenInfo.PhoneGuideSite + PhoneGuidePages.PageName(path.DistroId, path.EditionId, install) + "#" + PhoneGuideFragment(r);
    }

    public static string PhoneGuideFragment(CompatibilityReport r)
    {
        var path = r.SelectedRecommendation ?? r.Recommendation.Paths.First();
        return GuideFragment.For(r, new Ctx(r, path).GiveGb);
    }

    // ---- Phase 4: Restart into your USB stick ------------------------------------------------

    private static JourneyStep Restart(Ctx c)
    {
        var fallback = new List<Block>();
        if (c.R.Maker is { } m)
        {
            fallback.Add(P("If your USB stick isn't in the list, restart and use your PC's startup menu instead."));
            fallback.Add(new KeyFact($"On {m.Name} {(m.FromMotherboard ? "motherboards" : "PCs")}, the startup menu key is", m.BootMenuKey));
            var isInstruction = m.BootMenuKey.Contains(' ', StringComparison.Ordinal) && m.BootMenuKey.Length > 12;
            fallback.Add(new PictureGuide(
            [
                new PictureStep(isInstruction ? m.BootMenuKey + "." : $"As soon as the screen goes dark, press {m.BootMenuKey} again and again.", $"A keyboard with the {m.BootMenuKey} key marked", Illustrations.BootKeyId(m.BootMenuKey)),
                new PictureStep("Pick your USB stick from the list.", "A startup menu listing a USB stick", "boot-menu"),
            ]));
            if (m.BootMenuNote is not null) fallback.Add(P(m.BootMenuNote));
        }
        else
        {
            fallback.Add(P("If your USB stick isn't in the list, try another USB port (one on the back of the PC), then restart again from GreyHaven."));
        }

        var checks = new List<string>
        {
            "I've saved my work and closed my other apps.",
            "My guide is on my phone, printed, or I know where to find it.",
            "The USB stick is plugged in.",
        };
        if (c.Laptop) checks.Add("My laptop is plugged in to charge.");

        var sb = c.SecureBootOff ? " A red \"Secure Boot violation\" message means Secure Boot is still on." : "";
        return Step("restart", JourneyPhase.Restart, "Restart into your USB stick",
            "One last check, then GreyHaven restarts your PC and helps it start from the stick.",
            [P("When you're ready, GreyHaven restarts your PC to a blue Windows menu. Pick your USB stick there.")],
            "Windows starts as normal instead? Open GreyHaven and press the button again, or use the startup menu key." + sb)
            with
            {
                Restart = new RestartPlan(checks, "Restart into your USB stick",
                [
                    new PictureGuide(
                    [
                        new PictureStep("Your PC restarts to a blue screen. Click Use a device.", "The blue \"Choose an option\" screen with Use a device", "use-a-device"),
                        new PictureStep("Click your USB stick. Its name often has \"USB\" or the stick's brand in it.", "The \"Use a device\" list with the USB stick", "use-a-device-list"),
                    ]),
                    P($"Your PC then starts from the stick. If a menu with {c.Name} appears, press Enter on the first choice. Then follow your guide."),
                ], fallback, [WalkthroughBuilder.AdvancedStartup(c.R)]),
            };
    }

    // ---- Phase 5: On the USB stick (the phone/printed guide) ---------------------------------

    private static JourneyStep TryLinux(Ctx c)
    {
        var old = WalkthroughBuilder.TryLinux(c.Guide, c.Name);
        var body = old.Body.Select(b => b is Paragraph { Content: [Text { Value: var t }] } && t.StartsWith("Done?", StringComparison.Ordinal)
            ? P(c.Choice is null
                ? "Happy with how it works? Go on to the next step. Not happy? Shut down from the power menu, take out the stick, and Windows starts as before."
                : "Happy with how it works? Go on to Install. Not happy? Shut down from the power menu, take out the stick, and Windows starts as before. Nothing has changed.")
            : b).ToList();
        return Step("try", JourneyPhase.OnTheStick, "Try Linux", "See how it works on your PC. Nothing is installed yet.", body, old.Tip);
    }

    private static JourneyStep Install(Ctx c)
    {
        var body = new List<Block>();
        string lead;
        if (c.Choice is null)
        {
            lead = "You'll choose to keep Windows or replace it in GreyHaven first.";
            body.Add(P("Go back to GreyHaven's first step to choose."));
        }
        else if (c.Keep)
        {
            lead = $"Put {c.Name} next to Windows. Each time your PC starts, you pick one.";
            if (KeepWindowsBlocker(c) is { } blocker)
                body.Add(new Callout(CalloutKind.Warning, "This can't be done yet", Line(blocker)));
            else
            {
                body.Add(new PictureGuide(WalkthroughBuilder.InstallerSteps(c.Path.DistroId, c.Name, alongside: true, c.GiveGb, Opening(c))));
                body.Add(P("When your PC restarts, you'll see a menu. Pick Windows or Linux with the arrow keys, then press Enter."));
            }
        }
        else
        {
            lead = $"Put {c.Name} on the whole drive. This erases Windows and your files.";
            body.Add(new Callout(CalloutKind.Danger, "This erases Windows and every file on this drive", Line("Only continue if your backup is done.")));
            body.Add(new PictureGuide(WalkthroughBuilder.InstallerSteps(c.Path.DistroId, c.Name, alongside: false, c.GiveGb, Opening(c))));
        }
        body.Add(P("Installer screens change a little between versions. Follow the words on screen if they look different."));
        return Step("install", JourneyPhase.OnTheStick, $"Install {c.Name}", lead, body,
            c.Laptop ? "Plug in your laptop before you start. If the power goes off while installing, start again from the USB stick." : "If the power goes off while installing, start again from the USB stick.");
    }

    /// <summary>
    /// How to open the installer from the session you're already in. Restarting from the stick
    /// always works (the installer then opens by itself), so it's the backup.
    /// </summary>
    private static string Opening(Ctx c) => c.Path.DistroId == "mint"
        ? "" // Linux Mint's step already says to double-click the desktop icon
        : "Open the installer from the desktop (its name starts with \"Install\"). Or restart from the stick: it opens by itself.";

    private static JourneyStep AfterInstalling(Ctx c)
    {
        var old = WalkthroughBuilder.AfterInstalling(c.R, c.Path, c.Name);
        var body = old.Body.ToList();
        var help = body.FindIndex(b => b is Heading { Text: "Where to get help" });
        body.InsertRange(help < 0 ? body.Count : help, InstallYourApps(c.R, c.Path.DistroId));
        return Step("after", JourneyPhase.OnTheStick, "After installing", $"A few last things to make {c.Name} yours.", body, old.Tip);
    }

    /// <summary>
    /// The person's own list of Linux apps to install, with where to get each on their distro, and
    /// one optional block of commands that installs them all. Private: only in the app and the
    /// printed or saved copy, never on the public phone guide.
    /// </summary>
    public static IReadOnlyList<Block> InstallYourApps(CompatibilityReport r, string distroId)
    {
        var blocks = new List<Block> { new Heading(3, "Install your apps") };
        var picks = new List<(Core.Apps.LinuxApp App, string For)>();
        var web = new List<(string Name, string Url)>();
        foreach (var a in r.Apps)
        {
            switch (a.Verdict)
            {
                case Core.Apps.AppVerdict.Works or Core.Apps.AppVerdict.WorksWithLayer when a.Linux is not null:
                    picks.Add((a.Linux, a.Name));
                    break;
                case Core.Apps.AppVerdict.UseWeb when a.WebUrl is not null:
                    web.Add((a.Name, a.WebUrl));
                    if (a.Alternatives.FirstOrDefault(x => x.App is not null) is { App: { } webAlt }) picks.Add((webAlt, a.Name));
                    break;
                case Core.Apps.AppVerdict.WontWork when a.Alternatives.FirstOrDefault() is { } alt:
                    if (alt.App is not null) picks.Add((alt.App, a.Name));
                    else if (alt.WebUrl is not null) web.Add((alt.WebName ?? a.Name, alt.WebUrl));
                    break;
            }
        }
        var apps = picks.GroupBy(p => p.App.Id).Select(g => (App: g.First().App, For: string.Join(", ", g.Select(x => x.For).Distinct()))).ToList();
        if (apps.Count == 0 && web.Count == 0)
        {
            blocks.Add(P("GreyHaven didn't find any apps to set up. You can find more in " + Core.Apps.LinuxAppInstalls.StoreName(distroId) + "."));
            return blocks;
        }

        var items = new List<IReadOnlyList<Inline>>();
        var installs = apps.Select(x => (x.App, x.For, Get: Core.Apps.LinuxAppInstalls.For(x.App, distroId))).ToList();
        foreach (var (app, forApp, get) in installs)
        {
            var line = new List<Inline> { new Strong(app.Name), new Text(app.Name == forApp ? ": " : $" (instead of {forApp}): ") };
            line.Add(get.Link is { } link ? new Link(get.Where, link) : new Text(get.Where));
            line.Add(new Text("."));
            items.Add(line);
        }
        foreach (var (name, url) in web)
            items.Add([new Strong(name), new Text(": "), new Link("open the website", url), new Text(". Add it to your browser's bookmarks.")]);
        blocks.Add(P($"Tick each one once it's set up. In {Core.Apps.LinuxAppInstalls.StoreName(distroId)}, search for the app's name and click Install."));
        blocks.Add(new ListBlock(ListKind.Checkboxes, items));

        // One step, for people happy to use a terminal: each line is readable before it's run.
        var commands = new List<string>();
        var flatpaks = installs.Where(i => i.Get.Command?.StartsWith("flatpak install", StringComparison.Ordinal) == true).Select(i => i.App.Flathub!).ToList();
        var snaps = installs.Where(i => i.Get.Command?.StartsWith("sudo snap install", StringComparison.Ordinal) == true && !i.App.Classic).Select(i => i.App.Snap!).ToList();
        var classic = installs.Where(i => i.Get.Command?.StartsWith("sudo snap install", StringComparison.Ordinal) == true && i.App.Classic).Select(i => i.Get.Command!).ToList();
        var apts = installs.Where(i => i.Get.Command?.StartsWith("sudo apt install", StringComparison.Ordinal) == true).Select(i => i.App.Apt!).ToList();
        if (apts.Count > 0) commands.Add($"sudo apt install -y {string.Join(" ", apts)}");
        if (flatpaks.Count > 0)
        {
            // Ubuntu doesn't include Flatpak; these are flathub.org/setup/Ubuntu's own steps.
            if (distroId == "ubuntu")
            {
                commands.Add("sudo apt install -y flatpak");
                commands.Add("sudo flatpak remote-add --if-not-exists flathub https://dl.flathub.org/repo/flathub.flatpakrepo");
            }
            commands.Add($"flatpak install -y flathub {string.Join(" ", flatpaks)}");
        }
        if (snaps.Count > 0) commands.Add($"sudo snap install {string.Join(" ", snaps)}");
        commands.AddRange(classic);
        if (commands.Count > 0)
            blocks.Add(new Details("Or install them all at once",
            [
                P("Open a terminal, paste these lines and press Enter. They only install the apps above, from the official app stores. Apps that come from a website aren't included."),
                new CommandBlock(commands),
            ]));
        return blocks;
    }

    /// <summary>Plain names for the phases, for the progress bar.</summary>
    public static string PhaseName(JourneyPhase p) => p switch
    {
        JourneyPhase.GetReady => "Get ready",
        JourneyPhase.MakeUsb => "Make your USB stick",
        JourneyPhase.TakeGuide => "Take your guide",
        JourneyPhase.Restart => "Restart",
        _ => "On the USB stick",
    };
}
