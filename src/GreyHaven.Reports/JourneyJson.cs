using GreyHaven.Core.Compatibility;
using GreyHaven.Core.Serialization;

namespace GreyHaven.Reports;

/// <summary>The walkthrough as JSON, so the browser design preview shows exactly the app's words.</summary>
public static class JourneyJson
{
    public static string Render(CompatibilityReport r) => GreyHavenJson.Serialize(new
    {
        SelectedInstall = r.SelectedInstall,
        SuggestedInstall = JourneyBuilder.Suggested(r),
        DistroId = r.SelectedDistroId,
        PhoneGuideFragment = r.Recommendation.Paths.Count > 0 ? JourneyBuilder.PhoneGuideFragment(r) : null,
        PhoneGuideUrl = JourneyBuilder.PhoneGuideUrl(r),
        Phases = Enum.GetValues<JourneyPhase>().Select(p => new { Phase = p, Name = JourneyBuilder.PhaseName(p) }),
        Steps = JourneyBuilder.Build(r),
        AppSummary = Core.Apps.AppWords.Summary(r.Apps),
        Apps = r.SelectedDistroId is not { } distro ? [] : r.Apps.Select(a => (object)new
        {
            a.Name,
            Category = Core.Apps.AppWords.Category(a.Category),
            a.Verdict,
            VerdictText = Core.Apps.AppWords.Verdict(a.Verdict),
            a.Summary,
            a.Layer,
            a.Confidence,
            a.WebUrl,
            a.Reason,
            a.MatchedBy,
            a.Sources,
            Linux = a.Linux is null ? null : new { a.Linux.Name, a.Linux.What, Get = Core.Apps.LinuxAppInstalls.For(a.Linux, distro) },
            Alternatives = a.Alternatives.Select(x => new
            {
                Name = x.App?.Name ?? x.WebName,
                What = x.App?.What,
                x.Closeness,
                x.Note,
                Get = x.App is null ? new Core.Apps.LinuxAppInstall("Open it in your web browser", x.WebUrl, null) : Core.Apps.LinuxAppInstalls.For(x.App, distro),
                Hardware = HardwareLines(r, x.Uses, distro),
            }),
            Hardware = HardwareLines(r, a.Uses, distro),
            MustHaveWarning = Core.Apps.AppWords.MustHaveWarning(a),
        }).ToList(),
    });

    /// <summary>"Camera (HD Pro Webcam C920): Works right away" for each part an app depends on.</summary>
    public static IReadOnlyList<string> HardwareLines(CompatibilityReport r, IEnumerable<Core.Hardware.ComponentCategory> uses, string distroId) =>
        uses.SelectMany(u => r.Components.Where(c => c.Component.Category == u)
                .Select(c => $"{Core.Hardware.Labels.Of(u)} ({c.DisplayName}): {Core.Hardware.PlainWords.Rating(c.For(distroId).Rating)}")
                .DefaultIfEmpty($"{Core.Hardware.Labels.Of(u)}: GreyHaven didn't find one plugged in. Plug it in and scan again to check it."))
            .ToList();
}
