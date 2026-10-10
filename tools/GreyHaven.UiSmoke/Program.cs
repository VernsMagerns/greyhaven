using FlaUI.Core;
using FlaUI.Core.AutomationElements;
using FlaUI.Core.Capturing;
using FlaUI.Core.Definitions;
using FlaUI.Core.Tools;
using FlaUI.UIA3;

// UI smoke test for CI (Windows only). Launches GreyHaven in demo mode, walks the main flow,
// saves screenshots, and fails if any interactive control has no accessible name.
//
//   greyhaven-ui-smoke <path-to-GreyHaven.exe> <output-dir> [sample] [--screenshots]
//
// --screenshots: hides the demo badge and sizes the window for the README's screenshots.

if (args.Length < 2)
{
    Console.Error.WriteLine("Usage: greyhaven-ui-smoke <GreyHaven.exe> <output-dir> [sample]");
    return 2;
}

var exe = Path.GetFullPath(args[0]);
var outDir = Directory.CreateDirectory(args[1]).FullName;
var sample = args.Length > 2 && !args[2].StartsWith("--", StringComparison.Ordinal) ? args[2] : "nvidia-desktop";
var screenshots = args.Contains("--screenshots");
var problems = new List<string>();
var timeout = TimeSpan.FromSeconds(60);

using var app = Application.Launch(exe, $"--sample {sample}" + (screenshots ? " --screenshots" : ""));
using var automation = new UIA3Automation();
try
{
    var window = app.GetMainWindow(automation, timeout) ?? throw new InvalidOperationException("Main window didn't appear.");
    window.SetForeground();
    // Fit the window on the screen (CI screens can be small), top left, so captures are whole.
    var desk = automation.GetDesktop().BoundingRectangle;
    if (window.Patterns.Transform.IsSupported)
    {
        var t = window.Patterns.Transform.Pattern;
        t.Move(desk.Left, desk.Top);
        t.Resize(Math.Min(screenshots ? 1180 : 1000, desk.Width), Math.Min(screenshots ? 860 : 840, desk.Height - 40));
    }
    Console.WriteLine($"screen {desk.Width}x{desk.Height}, window {window.BoundingRectangle.Width}x{window.BoundingRectangle.Height}");

    AutomationElement Find(string name) =>
        Retry.WhileNull(() => window.FindFirstDescendant(cf => cf.ByName(name)), timeout).Result
        ?? throw new InvalidOperationException($"Couldn't find '{name}'.");

    void Shot(string file)
    {
        Thread.Sleep(500); // let layout and animations settle
        Capture.Element(window).ToFile(Path.Combine(outDir, file));
        Console.WriteLine($"saved {file}");
    }

    static void ScrollTo(AutomationElement e)
    {
        try { if (e.Patterns.ScrollItem.IsSupported) e.Patterns.ScrollItem.Pattern.ScrollIntoView(); }
        catch { /* best effort: only affects what the screenshot shows */ }
    }

    // Scrolls the page down a line at a time until the element sits near the top of the window,
    // so what follows it is in the screenshot.
    void ScrollNearTop(AutomationElement e, int below = 140)
    {
        var scroller = window.FindAllDescendants().FirstOrDefault(x => { try { return x.Patterns.Scroll.IsSupported && x.Patterns.Scroll.Pattern.VerticallyScrollable; } catch { return false; } });
        if (scroller is null) return;
        var scroll = scroller.Patterns.Scroll.Pattern;
        for (var i = 0; i < 400 && e.BoundingRectangle.Top > window.BoundingRectangle.Top + below; i++)
        {
            if (scroll.VerticalScrollPercent >= 100) break;
            scroll.Scroll(ScrollAmount.NoAmount, ScrollAmount.SmallIncrement);
        }
        for (var i = 0; i < 400 && e.BoundingRectangle.Top < window.BoundingRectangle.Top + below - 40; i++)
        {
            if (scroll.VerticalScrollPercent <= 0) break;
            scroll.Scroll(ScrollAmount.NoAmount, ScrollAmount.SmallDecrement);
        }
    }

    static bool IsScrollBarPart(AutomationElement e)
    {
        try { return e.Parent?.ControlType == ControlType.ScrollBar; }
        catch { return false; }
    }

    void CheckNames(string screen)
    {
        var interactive = new[] { ControlType.Button, ControlType.RadioButton, ControlType.CheckBox, ControlType.ComboBox, ControlType.Hyperlink, ControlType.Edit, ControlType.Group };
        foreach (var e in window.FindAllDescendants())
        {
            ControlType type;
            try { type = e.ControlType; } catch { continue; }
            if (!interactive.Contains(type) || !e.IsEnabled) continue;
            if (type == ControlType.Group && !e.Patterns.ExpandCollapse.IsSupported) continue; // only expanders
            // WPF's own scrollbar parts (PageUp/PageDown/LineUp...) are unnamed buttons inside a
            // ScrollBar; screen readers announce the scrollbar, not these parts.
            if (IsScrollBarPart(e)) continue;
            if (string.IsNullOrWhiteSpace(e.Name)) problems.Add($"{screen}: {type} without an accessible name (AutomationId '{e.AutomationId}')");
        }
    }

    AutomationElement FindStarting(ControlType type, string prefix) =>
        Retry.WhileNull(() => window.FindAllDescendants(cf => cf.ByControlType(type))
            .FirstOrDefault(e => (e.Name ?? "").StartsWith(prefix, StringComparison.Ordinal)), timeout).Result
        ?? throw new InvalidOperationException($"Couldn't find a {type} starting '{prefix}'.");

    // Start screen: turn online lookups off so CI never depends on the network.
    Find("Will Linux work on your PC?");
    // Start in light mode, whatever an earlier run saved.
    if (window.FindFirstDescendant(cf => cf.ByControlType(ControlType.Button).And(cf.ByName("Switch to light mode"))) is { } toLight)
        toLight.AsButton().Invoke();
    var online = window.FindFirstDescendant(cf => cf.ByControlType(ControlType.CheckBox))?.AsCheckBox();
    if (online?.IsChecked == true) online.Toggle();
    CheckNames("start");
    Shot("01-start-light.png");

    Find("Scan my PC").AsButton().Invoke();
    Find("Your PC is ready for Linux");
    CheckNames("results");
    Shot("02-results-light.png");

    // Advanced path (offered when the best distro needs Secure Boot off), then back to Easy.
    FindStarting(ControlType.RadioButton, "Pop!_OS, Best fit").AsRadioButton().IsChecked = true;
    Shot("03-results-advanced-light.png");
    FindStarting(ControlType.RadioButton, "Ubuntu, Easiest").AsRadioButton().IsChecked = true;

    // "Show more" on the part that needs attention.
    FindStarting(ControlType.Group, "Show more about Graphics").Patterns.ExpandCollapse.Pattern.Expand();
    Shot("04-part-show-more-light.png");

    // Your apps (the demo sample's made-up list), with VALORANT marked as needed.
    var appsGroup = FindStarting(ControlType.Group, "Show your");
    appsGroup.Patterns.ExpandCollapse.Pattern.Expand();
    var valorant = FindStarting(ControlType.CheckBox, "I need VALORANT").AsCheckBox();
    valorant.IsChecked = true;
    ScrollNearTop(FindStarting(ControlType.Group, "Show your"), below: 230);
    CheckNames("results with apps");
    Shot("04b-results-apps-light.png");

    // Check a game: type a name, results appear as you type.
    var gameBox = Find("Game name").AsTextBox();
    ScrollTo(gameBox);
    gameBox.Text = "elden ring";
    var elden = Retry.WhileNull(() => window.FindFirstDescendant(cf => cf.ByControlType(ControlType.Text).And(cf.ByName("ELDEN RING"))), timeout).Result
                ?? throw new InvalidOperationException("Check a game found nothing for 'elden ring'.");
    ScrollNearTop(gameBox, below: 230);      // the "Check a game" heading and line above the box too
    CheckNames("results: check a game");
    Shot("04c-results-check-a-game-light.png");
    appsGroup.Patterns.ExpandCollapse.Pattern.Collapse();

    // The walkthrough, from the button under the headline. Demo mode: nothing is written or restarted.
    FindStarting(ControlType.Button, "Show me how to switch").AsButton().Invoke();
    Find("How do you want to install Linux?");
    CheckNames("walkthrough: install choice");
    Shot("06-walkthrough-choose-light.png");
    void Next() => FindStarting(ControlType.Button, "Next:").AsButton().Invoke();
    Next();
    Find("Back up your files");
    Next();
    Find("Get Windows ready");
    FindStarting(ControlType.Button, "Turn off Fast Startup").AsButton().Invoke();
    Find("Yes, do it");
    CheckNames("walkthrough: Windows tasks");
    Shot("07-walkthrough-windows-confirm-light.png");
    Find("Yes, do it").AsButton().Invoke();
    Find("✓ Done.");
    Next();
    Find("Make your USB stick");
    FindStarting(ControlType.CheckBox, "I understand everything on the").AsCheckBox().IsChecked = true;
    Find("Make my USB stick").AsButton().Invoke();
    Find("✓ Your USB stick is ready.");
    CheckNames("walkthrough: USB stick");
    Shot("07b-walkthrough-usb-done-light.png");
    Next();
    Find("Take the guide with you");
    Find("QR code that opens your guide on a phone");
    CheckNames("walkthrough: guide");
    Shot("08-walkthrough-guide-light.png");
    Next();
    Find("Restart into your USB stick");
    foreach (var c in window.FindAllDescendants(cf => cf.ByControlType(ControlType.CheckBox)).Select(e => e.AsCheckBox()))
        if (c.IsChecked != true) c.IsChecked = true;
    FindStarting(ControlType.Button, "Restart into your USB stick").AsButton().Invoke();
    FindStarting(ControlType.Text, "Demo: your PC would restart now");
    CheckNames("walkthrough: restart");
    Shot("08b-walkthrough-restart-light.png");

    // A picture, enlarged, then closed with Escape.
    FindStarting(ControlType.Button, "Enlarge picture:").AsButton().Invoke();
    // It's a dialog owned by the main window, so it's one of the window's modal windows.
    var picture = Retry.WhileNull(() => window.ModalWindows.FirstOrDefault(w => w.Title == "Picture"), timeout).Result
                  ?? throw new InvalidOperationException("The enlarged picture didn't open.");
    Thread.Sleep(500);
    Capture.Element(picture).ToFile(Path.Combine(outDir, "08c-picture-enlarged-light.png"));
    Console.WriteLine("saved 08c-picture-enlarged-light.png");
    FlaUI.Core.Input.Keyboard.Type(FlaUI.Core.WindowsAPI.VirtualKeyShort.ESCAPE);
    if (!Retry.WhileTrue(() => window.ModalWindows.Any(w => w.Title == "Picture"), timeout).Success)
        problems.Add("Escape didn't close the enlarged picture.");

    Next();
    Find("After you restart");
    CheckNames("walkthrough: after you restart");

    // Dark theme, on the walkthrough and back on the results.
    FindStarting(ControlType.Button, "Switch to dark mode").AsButton().Invoke();
    Shot("09-walkthrough-after-restart-dark.png");
    FindStarting(ControlType.Button, "← Back to results").AsButton().Invoke();
    Find("Your PC is ready for Linux");
    Shot("05-results-dark.png");
    FindStarting(ControlType.Button, "Switch to light mode").AsButton().Invoke();
}
catch (Exception ex)
{
    problems.Add($"Flow failed: {ex.Message}");
    try { Capture.Screen().ToFile(Path.Combine(outDir, "failure.png")); } catch { /* best effort */ }
}
finally
{
    app.Close();
}

foreach (var p in problems) Console.Error.WriteLine(p);
Console.WriteLine(problems.Count == 0 ? "UI smoke test passed." : $"{problems.Count} problem(s).");
return problems.Count == 0 ? 0 : 1;
