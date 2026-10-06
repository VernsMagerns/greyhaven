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

    void CheckNames(string screen)
    {
        var interactive = new[] { ControlType.Button, ControlType.RadioButton, ControlType.CheckBox, ControlType.ComboBox, ControlType.Hyperlink, ControlType.Edit, ControlType.Group };
        foreach (var e in window.FindAllDescendants())
        {
            ControlType type;
            try { type = e.ControlType; } catch { continue; }
            if (!interactive.Contains(type) || !e.IsEnabled) continue;
            if (type == ControlType.Group && !e.Patterns.ExpandCollapse.IsSupported) continue; // only expanders
            if (string.IsNullOrWhiteSpace(e.Name)) problems.Add($"{screen}: {type} without an accessible name (AutomationId '{e.AutomationId}')");
        }
    }

    // Start screen: turn online lookups off so CI never depends on the network.
    Find("Will this PC work well with Linux?");
    var online = window.FindFirstDescendant(cf => cf.ByControlType(ControlType.CheckBox))?.AsCheckBox();
    if (online?.IsChecked == true) online.Toggle();
    CheckNames("start");
    Shot("01-start-light.png");

    Find("Scan my PC").AsButton().Invoke();
    Find("Your results");
    CheckNames("results");
    Shot("02-results-light.png");

    // Advanced path (only offered when the best distro needs Secure Boot off).
    var advanced = window.FindFirstDescendant(cf => cf.ByControlType(ControlType.RadioButton).And(cf.ByName("Advanced: Pop!_OS. Needs Secure Boot turned off.")));
    if (advanced is not null)
    {
        advanced.AsRadioButton().IsChecked = true;
        Shot("03-results-advanced-light.png");
        Find("Easy: Ubuntu. Keeps Secure Boot on. Suggested for most people.").AsRadioButton().IsChecked = true;
    }

    // Expand the first component that needs attention.
    var firstExpander = Retry.WhileNull(() => window.FindFirstDescendant(cf => cf.ByControlType(ControlType.Group).And(cf.ByName("Graphics: Works after an extra step. Essential part. NVIDIA GeForce RTX 3070"))), TimeSpan.FromSeconds(5)).Result;
    firstExpander?.Patterns.ExpandCollapse.Pattern.Expand();
    Shot("04-component-expanded-light.png");

    // Dark theme.
    var theme = window.FindFirstDescendant(cf => cf.ByControlType(ControlType.ComboBox))?.AsComboBox()
                ?? throw new InvalidOperationException("Appearance box not found.");
    theme.Select("Dark");
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
