using GreyHaven.Compat.Data;
using GreyHaven.Core.Compatibility;
using GreyHaven.Core.Hardware;

namespace GreyHaven.Ratings;

/// <summary>
/// Picks the distro to recommend and explains why in one paragraph. Order: highest score, then
/// fewest extra-driver steps, then fewest system changes (e.g. having to turn off Secure Boot),
/// then the catalog's tieBreakRank (Mint, Ubuntu, Pop!_OS).
/// </summary>
public static class Recommender
{
    public static Recommendation Recommend(
        HardwareProfile profile,
        IReadOnlyList<RatedComponent> components,
        IReadOnlyList<DistroSummary> summaries,
        DistroCatalog catalog)
    {
        bool NeedsSecureBootOff(DistroSummary s) =>
            !catalog.Get(s.DistroId).SecureBoot.Supported && profile.System.SecureBoot == SecureBootState.Enabled;

        var ordered = summaries
            .OrderByDescending(s => s.Score.Score)
            .ThenBy(s => s.Score.ExtraDriverCount)
            .ThenBy(s => NeedsSecureBootOff(s) ? 1 : 0)
            .ThenBy(s => catalog.Get(s.DistroId).TieBreakRank)
            .ToList();
        var best = ordered[0];
        var bestInfo = catalog.Get(best.DistroId);

        var cpu = components.FirstOrDefault(c => c.Component.Category == ComponentCategory.Cpu);
        if (cpu is not null && summaries.All(s => cpu.For(s.DistroId).Rating == Rating.Unsupported))
        {
            return new Recommendation
            {
                DistroId = null,
                Paragraph = "GreyHaven can't recommend switching this PC to Linux Mint, Ubuntu or Pop!_OS: none of them offers a standard installer for its processor. " +
                            "Check your PC maker's support pages or the distro's own hardware lists before trying.",
            };
        }

        var sentences = new List<string>
        {
            $"{bestInfo.Name} {bestInfo.Version} is the best fit for this PC, with a score of {best.Score.Score} out of 100 ({BandWord(best.Score.Band)}).",
        };

        var runnerUp = ordered.Count > 1 ? ordered[1] : null;
        var better = runnerUp is null
            ? []
            : components
                .Where(c => Rank(c.For(best.DistroId).Rating) < Rank(c.For(runnerUp.DistroId).Rating))
                .Select(c => Prose(c.Component.Category))
                .Distinct()
                .ToList();

        if (runnerUp is not null && better.Count > 0)
        {
            sentences.Add($"It handles your {JoinList(better)} better than {catalog.Get(runnerUp.DistroId).Name}.");
            if (bestInfo.Audience is not null) sentences.Add(bestInfo.Audience);
        }
        else if (summaries.Select(s => s.Score.Score).Distinct().Count() == 1)
        {
            sentences.Add($"All {summaries.Count} distros rate the same on this PC, so GreyHaven suggests {bestInfo.Name}: {LowerFirst(bestInfo.Audience ?? "it is a solid general-purpose choice.")}");
        }
        else if (bestInfo.Audience is not null)
        {
            sentences.Add(bestInfo.Audience);
        }

        if (profile.System.IsArm)
            sentences.Add($"Because this PC has an ARM processor, only {bestInfo.Name}'s ARM64 version could work, and support depends on your exact model, so try it from a live USB before changing anything.");
        if (best.Score.ExtraDriverCount > 0)
            sentences.Add(best.Score.ExtraDriverCount == 1
                ? "One part needs an extra step after installing; the details below show exactly what to do."
                : $"{best.Score.ExtraDriverCount} parts need an extra step after installing; the details below show exactly what to do.");
        if (best.Score.UnknownCount > 0)
            sentences.Add(best.Score.UnknownCount == 1
                ? "One part couldn't be rated, so check it from a live USB before you switch."
                : $"{best.Score.UnknownCount} parts couldn't be rated, so check them from a live USB before you switch.");
        if (NeedsSecureBootOff(best))
            sentences.Add($"You'll need to turn off Secure Boot in your PC's firmware settings before installing {bestInfo.Name}.");
        if (best.Score.CapReason is not null)
            sentences.Add(best.Score.CapReason);

        return new Recommendation { DistroId = best.DistroId, Paragraph = string.Join(" ", sentences) };
    }

    private static int Rank(Core.Compatibility.Rating r) => r switch
    {
        Rating.WorksOutOfTheBox => 0,
        Rating.WorksWithExtraDriver => 1,
        Rating.Partial => 2,
        Rating.Unknown => 3,
        _ => 4,
    };

    private static string BandWord(ScoreBand b) => b switch
    {
        ScoreBand.Great => "great",
        ScoreBand.Good => "good",
        ScoreBand.Fair => "fair",
        _ => "poor",
    };

    /// <summary>Category names as they read in a sentence ("your graphics", "your Wi-Fi").</summary>
    internal static string Prose(ComponentCategory c) => c switch
    {
        ComponentCategory.Cpu => "processor",
        ComponentCategory.Gpu => "graphics",
        ComponentCategory.Wifi => "Wi-Fi",
        ComponentCategory.Ethernet => "wired network",
        ComponentCategory.UsbNetwork => "USB network adapter",
        ComponentCategory.Bluetooth => "Bluetooth",
        ComponentCategory.AudioController or ComponentCategory.AudioCodec => "sound",
        ComponentCategory.DisplayAudio => "HDMI audio",
        ComponentCategory.Webcam => "camera",
        ComponentCategory.NvmeDrive => "SSD",
        ComponentCategory.Thunderbolt => "Thunderbolt",
        ComponentCategory.UsbController => "USB ports",
        _ => LowerFirst(Labels.Of(c)),
    };

    internal static string JoinList(IReadOnlyList<string> items) => items.Count switch
    {
        0 => "",
        1 => items[0],
        2 => $"{items[0]} and {items[1]}",
        _ => string.Join(", ", items.Take(items.Count - 1)) + " and " + items[^1],
    };

    private static string LowerFirst(string s) => s.Length > 1 && char.IsUpper(s[0]) && char.IsLower(s[1]) ? char.ToLowerInvariant(s[0]) + s[1..] : s;
}
