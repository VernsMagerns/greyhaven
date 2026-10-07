using GreyHaven.Core.Compatibility;
using GreyHaven.Core.Hardware;
using static GreyHaven.Reports.Doc;

namespace GreyHaven.Reports;

/// <summary>
/// The guide people take with them when GreyHaven restarts the PC: trying Linux, installing it
/// and setting it up, for their choices only. Short enough for a phone; prints well too.
/// It holds no part IDs or other details about the PC beyond what the steps need.
/// </summary>
public static class PhoneGuideBuilder
{
    public static ReportDocument Build(CompatibilityReport r)
    {
        var path = r.SelectedRecommendation ?? r.Recommendation.Paths.FirstOrDefault();
        if (path is null) return new ReportDocument("GreyHaven guide", [P("GreyHaven couldn't suggest a version of Linux for this PC.")]);
        var name = PlainWords.ShortName(r.LiveUsbGuides.First(g => g.DistroId == path.DistroId).DistroName);
        var choice = r.SelectedInstall switch
        {
            InstallChoice.KeepWindows => "Keep Windows too",
            InstallChoice.ReplaceWindows => "Replace Windows",
            _ => null,
        };

        var b = new List<Block>
        {
            new Heading(1, $"Your steps to {name}"),
            P(choice is null ? $"From GreyHaven, for your PC." : $"From GreyHaven, for your PC. You chose: {choice}."),
        };
        if (r.Maker is { } m)
            b.Add(new Details("PC didn't start from the USB stick?",
            [
                P("Restart and press the startup menu key again and again as the PC starts. Then pick your USB stick."),
                new KeyFact($"On {m.Name} {(m.FromMotherboard ? "motherboards" : "PCs")}, the startup menu key is", m.BootMenuKey),
            ]));

        var steps = JourneyBuilder.OnTheStick(r);
        for (var i = 0; i < steps.Count; i++)
        {
            var s = steps[i];
            b.Add(new Heading(2, $"{i + 1}. {s.Title}"));
            b.Add(P(new Strong(s.Lead)));
            b.AddRange(s.Body);
            if (s.Tip is not null) b.Add(new Callout(CalloutKind.Tip, "What could go wrong?", Line(s.Tip)));
        }
        return new ReportDocument($"Your steps to {name} - GreyHaven", b);
    }
}
