using FlaUI.Core;
using FlaUI.Core.AutomationElements;
using FlaUI.Core.Capturing;
using FlaUI.Core.Definitions;
using FlaUI.Core.Tools;
using FlaUI.UIA3;

// UI smoke test for CI (Windows only). Launches GreyHaven in demo mode, walks the main flow,
// saves screenshots, and fails if any interactive control has no accessible name.
//
//   greyhaven-ui-smoke <path-to-GreyHaven.exe> <output-dir> [sample]

if (args.Length < 2)
{
    Console.Error.WriteLine("Usage: greyhaven-ui-smoke <GreyHaven.exe> <output-dir> [sample]");
    return 2;
}

var exe = Path.GetFullPath(args[0]);
var outDir = Directory.CreateDirectory(args[1]).FullName;
var sample = args.Length > 2 ? args[2] : "nvidia-desktop";
var problems = new List<string>();
var timeout = TimeSpan.FromSeconds(60);

using var app = Application.Launch(exe, $"--sample {sample}");
using var automation = new UIA3Automation();
try
{
    var window = app.GetMainWindow(automation, timeout) ?? throw new InvalidOperationException("Main window didn't appear.");
    window.SetForeground();

    AutomationElement Find(string name) =>
        Retry.WhileNull(() => window.FindFirstDescendant(cf => cf.ByName(name)), timeout).Result
        ?? throw new InvalidOperationException($"Couldn't find '{name}'.");

    void Shot(string file)
    {
        Thread.Sleep(500); // let layout and animations settle
        Capture.Element(window).ToFile(Path.Combine(outDir, file));
        Console.WriteLine($"saved {file}");
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

    // The walkthrough: step 1, step 3 (this PC's Windows checklist), step 8 (install choice).
    FindStarting(ControlType.Button, "What's next").AsButton().Invoke();
    Find("Back up your files");
    CheckNames("walkthrough step 1");
    Shot("06-walkthrough-step1-light.png");
    for (var i = 0; i < 2; i++) FindStarting(ControlType.Button, "Next:").AsButton().Invoke();
    Find("Get Windows ready");
    CheckNames("walkthrough step 3");
    Shot("07-walkthrough-step3-light.png");
    for (var i = 0; i < 5; i++) FindStarting(ControlType.Button, "Next:").AsButton().Invoke();
    Find("Choose how to install");
    FindStarting(ControlType.RadioButton, "Replace Windows").AsRadioButton().IsChecked = true;
    CheckNames("walkthrough step 8");
    Shot("08-walkthrough-install-replace-light.png");

    // Dark theme, on the walkthrough and back on the results.
    var theme = window.FindFirstDescendant(cf => cf.ByControlType(ControlType.ComboBox))?.AsComboBox()
                ?? throw new InvalidOperationException("Appearance box not found.");
    theme.Select("Dark");
    Shot("09-walkthrough-dark.png");
    FindStarting(ControlType.Button, "← Back to results").AsButton().Invoke();
    Find("Your PC is ready for Linux");
    Shot("05-results-dark.png");
    theme.Select("Match Windows");
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
