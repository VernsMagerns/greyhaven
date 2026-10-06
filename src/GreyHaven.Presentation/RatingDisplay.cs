using GreyHaven.Core.Compatibility;
using GreyHaven.Core.Hardware;

namespace GreyHaven.Presentation;

/// <summary>
/// How a rating looks in the UI: a plain-English label, an icon and a colour key. All three are
/// always shown together, so a rating never depends on colour alone.
/// </summary>
public static class RatingDisplay
{
    /// <summary>Main-view wording: no jargon.</summary>
    public static string Label(Rating r) => Labels.Of(r);

    /// <summary>The official rating names, shown in technical details and reports.</summary>
    public static string OfficialName(Rating r) => Labels.Official(r);

    /// <summary>Segoe Fluent Icons / Segoe MDL2 Assets glyph (both fonts share these code points).</summary>
    public static string Glyph(Rating r) => r switch
    {
        Rating.WorksOutOfTheBox => "",     // CheckMark
        Rating.WorksWithExtraDriver => "", // Repair (wrench)
        Rating.Partial => "",              // Warning
        Rating.Unsupported => "",          // Cancel (X)
        _ => "",                           // Unknown (question mark)
    };

    /// <summary>Palette entry name for the rating colour, e.g. "Works".</summary>
    public static string ColourKey(Rating r) => r switch
    {
        Rating.WorksOutOfTheBox => "Works",
        Rating.WorksWithExtraDriver => "ExtraStep",
        Rating.Partial => "Partial",
        Rating.Unsupported => "Unsupported",
        _ => "Unknown",
    };

    public static string Confidence(Confidence c) => c switch
    {
        Core.Compatibility.Confidence.High => "High",
        Core.Compatibility.Confidence.Medium => "Medium",
        _ => "Low",
    };

    public static string Band(ScoreBand b) => b switch
    {
        ScoreBand.Great => "Great",
        ScoreBand.Good => "Good",
        ScoreBand.Fair => "Fair",
        _ => "Poor",
    };
}
