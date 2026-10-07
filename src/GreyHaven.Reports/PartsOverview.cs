using GreyHaven.Core.Compatibility;
using GreyHaven.Core.Hardware;

namespace GreyHaven.Reports;

/// <summary>
/// How the results group a PC's parts for one distro, shared by the app and the saved report:
/// <list type="bullet">
///   <item>Parts that need attention, one by one.</item>
///   <item>Parts that work right away, folded away.</item>
///   <item>Small parts nobody could check (not essential or important), folded away with a calm line.</item>
///   <item>The system chipset (bridges, controllers on the motherboard) as one line. These aren't
///     counted in the summary, since dozens of them would drown out the parts people care about.</item>
/// </list>
/// </summary>
public sealed class PartsOverview
{
    public IReadOnlyList<RatedComponent> Attention { get; }
    public IReadOnlyList<RatedComponent> Working { get; }
    public IReadOnlyList<RatedComponent> UncheckedSmall { get; }
    public IReadOnlyList<RatedComponent> Chipset { get; }

    private readonly string _distroId;

    public PartsOverview(CompatibilityReport report, string distroId)
    {
        _distroId = distroId;
        var ordered = report.Components.OrderBy(c => c.Importance).ThenBy(c => c.Component.Category).ToList();
        Rating R(RatedComponent c) => c.For(distroId).Rating;
        bool IsChipset(RatedComponent c) => c.Component.Category == ComponentCategory.Chipset;
        bool Quiet(RatedComponent c) => R(c) is Rating.WorksOutOfTheBox or Rating.Unknown;

        // A chipset part that's known to have a problem still gets its own line.
        Chipset = ordered.Where(c => IsChipset(c) && Quiet(c)).ToList();
        var rest = ordered.Where(c => !Chipset.Contains(c)).ToList();
        Working = rest.Where(c => R(c) == Rating.WorksOutOfTheBox).ToList();
        UncheckedSmall = rest.Where(c => R(c) == Rating.Unknown && c.Importance == Importance.Minor).ToList();
        Attention = rest.Where(c => R(c) != Rating.WorksOutOfTheBox && !UncheckedSmall.Contains(c)).ToList();
    }

    /// <summary>"16 parts work right away. 1 part needs one extra step after you install." Chipset parts aren't counted.</summary>
    public string Summary => PlainWords.PartsSummary(
        Working.Concat(Attention).Concat(UncheckedSmall).Select(c => (c.For(_distroId).Rating, c.Importance)).ToList());

    /// <summary>Expander heading for the working parts, or null if there are none.</summary>
    public string? WorkingHeading => Working.Count == 0 ? null
        : $"{(Attention.Count + UncheckedSmall.Count == 0 ? "All " : "")}{Working.Count} {(Working.Count == 1 ? "part works" : "parts work")} right away. Show them";

    /// <summary>"2 small parts couldn't be checked. They rarely matter. Show them", or null.</summary>
    public string? UncheckedHeading => UncheckedSmall.Count == 0 ? null
        : $"{UncheckedSmall.Count} small {(UncheckedSmall.Count == 1 ? "part" : "parts")} couldn't be checked. {(UncheckedSmall.Count == 1 ? "It rarely matters" : "They rarely matter")}. Show {(UncheckedSmall.Count == 1 ? "it" : "them")}";

    /// <summary>What to do about small unchecked parts, shown when they're expanded.</summary>
    public const string UncheckedAdvice =
        "These aren't parts you need to use your PC. If you use one of them, try it when you test Linux from the USB stick.";

    /// <summary>"System chipset: 21 parts, all fine", or null if the PC has none listed.</summary>
    public string? ChipsetLine
    {
        get
        {
            if (Chipset.Count == 0) return null;
            var unknown = Chipset.Count(c => c.For(_distroId).Rating == Rating.Unknown);
            var parts = $"{Chipset.Count} {(Chipset.Count == 1 ? "part" : "parts")}";
            return unknown == 0
                ? $"System chipset: {parts}, all fine"
                : $"System chipset: {parts}. {Chipset.Count - unknown} fine, {unknown} not checked (these rarely matter)";
        }
    }

    /// <summary>One sentence explaining the chipset line.</summary>
    public const string ChipsetAbout =
        "The chipset is the wiring on your PC's main board that connects everything else. Linux handles these parts by itself.";
}
