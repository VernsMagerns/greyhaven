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
        Phases = Enum.GetValues<JourneyPhase>().Select(p => new { Phase = p, Name = JourneyBuilder.PhaseName(p) }),
        Steps = JourneyBuilder.Build(r),
    });
}
