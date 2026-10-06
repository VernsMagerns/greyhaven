using GreyHaven.Compat.Online;
using GreyHaven.Core.Compatibility;

namespace GreyHaven.Ratings;

/// <summary>Decides whether to tell the user to check for a GreyHaven update because the data may be out of date.</summary>
public static class Freshness
{
    public const int MaxAgeDays = 90;

    public static DataFreshness Evaluate(DateOnly builtOn, DateOnly today, ReleaseCheckResult? releases)
    {
        var age = Math.Max(0, today.DayNumber - builtOn.DayNumber);
        var newer = releases?.Newer ?? [];
        var reasons = new List<string>();
        if (age > MaxAgeDays)
            reasons.Add($"GreyHaven's hardware data is from {builtOn.ToString("d MMMM yyyy", System.Globalization.CultureInfo.InvariantCulture)} ({age} days ago).");
        foreach (var n in newer.GroupBy(n => n.DistroId).Select(g => g.First()))
            reasons.Add($"A newer {n.DistroName} release ({n.Found}) has come out since this data was made.");

        return new DataFreshness
        {
            BuiltOn = builtOn,
            AgeDays = age,
            NewerReleases = newer,
            ReleasesChecked = releases?.Complete ?? false,
            Notice = reasons.Count == 0
                ? null
                : string.Join(" ", reasons) + " Check for a GreyHaven update to get the most accurate results.",
        };
    }
}
