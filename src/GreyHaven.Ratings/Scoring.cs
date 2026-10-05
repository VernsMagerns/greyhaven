using GreyHaven.Core.Compatibility;
using GreyHaven.Core.Hardware;

namespace GreyHaven.Ratings;

/// <summary>Which components matter most. Documented in docs/scoring.md.</summary>
public static class ImportanceRules
{
    public static Importance For(HardwareComponent c, HardwareProfile profile)
    {
        var laptop = profile.System.FormFactor == FormFactor.Laptop;
        var hasEthernet = profile.Components.Any(x => x.Category == ComponentCategory.Ethernet);
        var hasWifi = profile.Components.Any(x => x.Category == ComponentCategory.Wifi);

        return c.Category switch
        {
            ComponentCategory.Cpu or ComponentCategory.Gpu or ComponentCategory.StorageController or ComponentCategory.NvmeDrive
                => Importance.Critical,
            // The connection you'll actually use is critical; a second one is merely important.
            ComponentCategory.Wifi => laptop || !hasEthernet ? Importance.Critical : Importance.Important,
            ComponentCategory.Ethernet => !laptop || !hasWifi ? Importance.Critical : Importance.Important,
            ComponentCategory.AudioController or ComponentCategory.AudioCodec or ComponentCategory.Bluetooth or
            ComponentCategory.Touchpad or ComponentCategory.Webcam or ComponentCategory.UsbController or ComponentCategory.UsbNetwork
                => Importance.Important,
            _ => Importance.Minor,
        };
    }

    /// <summary>
    /// Chipset plumbing and external input devices are rated and shown, but left out of the
    /// score so dozens of trivially-supported parts can't dilute the components that matter.
    /// </summary>
    public static bool CountsTowardScore(HardwareComponent c) =>
        c.Category is not (ComponentCategory.Chipset or ComponentCategory.InputDevice or ComponentCategory.Other);
}

/// <summary>
/// Computes the 0–100 summary score for one distro. Formula (docs/scoring.md):
/// weighted mean of points (out of the box 100, extra driver 80, partial 40, unsupported 0)
/// with weights critical 3, important 1.5, minor 1. Unknown ratings are left out of the mean
/// and reduce coverage instead. Caps: any unsupported critical component → at most 40; any
/// partial critical → at most 79; any unknown critical → at most 70 and marked incomplete.
/// </summary>
public static class ScoreCalculator
{
    public const int UnsupportedCriticalCap = 40;
    public const int PartialCriticalCap = 79;
    public const int UnknownCriticalCap = 70;

    public static double Weight(Importance i) => i switch
    {
        Importance.Critical => 3.0,
        Importance.Important => 1.5,
        _ => 1.0,
    };

    public static int? Points(Rating r) => r switch
    {
        Rating.WorksOutOfTheBox => 100,
        Rating.WorksWithExtraDriver => 80,
        Rating.Partial => 40,
        Rating.Unsupported => 0,
        _ => null,
    };

    public static ScoreBand BandOf(int score) => score switch
    {
        >= 90 => ScoreBand.Great,
        >= 70 => ScoreBand.Good,
        >= 50 => ScoreBand.Fair,
        _ => ScoreBand.Poor,
    };

    /// <param name="items">Each scored component's importance and its rating on this distro.</param>
    /// <param name="labels">Display names of the components, for the cap reason.</param>
    public static ScoreResult Calculate(IReadOnlyList<(Importance Importance, Rating Rating, string Label)> items)
    {
        double total = 0, known = 0, sum = 0;
        foreach (var (importance, rating, _) in items)
        {
            var w = Weight(importance);
            total += w;
            if (Points(rating) is { } p)
            {
                known += w;
                sum += w * p;
            }
        }

        var score = known > 0 ? (int)Math.Round(sum / known, MidpointRounding.AwayFromZero) : 0;
        string? capReason = null;

        string Names(Rating r) => string.Join(", ", items.Where(i => i.Importance == Importance.Critical && i.Rating == r).Select(i => i.Label).Distinct());

        var unsupportedCritical = items.Any(i => i.Importance == Importance.Critical && i.Rating == Rating.Unsupported);
        var partialCritical = items.Any(i => i.Importance == Importance.Critical && i.Rating == Rating.Partial);
        var unknownCritical = items.Any(i => i.Importance == Importance.Critical && i.Rating == Rating.Unknown);

        // Every applicable cap limits the score; the lowest one explains it. The reason is shown
        // even when the score was already below the cap, so a low score always says why.
        var caps = new List<(int Cap, string Reason)>();
        if (unsupportedCritical) caps.Add((UnsupportedCriticalCap, $"Limited to {UnsupportedCriticalCap} because an essential part isn't supported: {Names(Rating.Unsupported)}."));
        if (unknownCritical) caps.Add((UnknownCriticalCap, $"Limited to {UnknownCriticalCap} because an essential part couldn't be checked: {Names(Rating.Unknown)}."));
        if (partialCritical) caps.Add((PartialCriticalCap, $"Limited to {PartialCriticalCap} because an essential part only partly works: {Names(Rating.Partial)}."));
        if (caps.Count > 0)
        {
            var lowest = caps.MinBy(c => c.Cap);
            score = Math.Min(score, lowest.Cap);
            capReason = lowest.Reason;
        }

        return new ScoreResult
        {
            Score = score,
            Band = known > 0 ? BandOf(score) : ScoreBand.Poor,
            Coverage = total > 0 ? Math.Round(known / total, 3) : 0,
            Incomplete = unknownCritical || known == 0,
            CapReason = capReason,
            ExtraDriverCount = items.Count(i => i.Rating == Rating.WorksWithExtraDriver),
            UnknownCount = items.Count(i => i.Rating == Rating.Unknown),
        };
    }
}
