using GreyHaven.Compat.Data;
using GreyHaven.Core.Compatibility;
using GreyHaven.Core.Hardware;

namespace GreyHaven.Ratings;

/// <summary>
/// Picks the distro(s) to recommend and explains why. Order: highest score, then fewest
/// extra-driver steps, then fewest system changes (e.g. having to turn off Secure Boot), then
/// the catalog's tieBreakRank (Mint, Ubuntu, Pop!_OS).
///
/// If the best distro needs Secure Boot turned off (and Secure Boot is on, or can't be read),
/// the user gets two paths instead of one:
/// <list type="bullet">
///   <item><b>Easy</b> (default): the catalog's <c>secureBootEasyModeDistro</c> (Ubuntu), which keeps Secure Boot on.</item>
///   <item><b>Advanced</b>: the best distro, with a plain notice about turning Secure Boot off and a how-to link.</item>
/// </list>
/// Turning Secure Boot off is always instructions-only.
/// </summary>
public static class Recommender
{
    public static Recommendation Recommend(
        HardwareProfile profile,
        IReadOnlyList<RatedComponent> components,
        IReadOnlyList<DistroSummary> summaries,
        DistroCatalog catalog,
        SecureBootGuideCatalog guides)
    {
        var sb = profile.System.SecureBoot;
        var secureBootMayBeOn = sb is SecureBootState.Enabled or SecureBootState.Unknown;
        bool NeedsSecureBootOff(DistroSummary s) => secureBootMayBeOn && !catalog.Get(s.DistroId).SecureBoot.Supported;

        var ordered = summaries
            .OrderByDescending(s => s.Score.Score)
            .ThenBy(s => s.Score.ExtraDriverCount)
            .ThenBy(s => NeedsSecureBootOff(s) ? 1 : 0)
            .ThenBy(s => catalog.Get(s.DistroId).TieBreakRank)
            .ToList();
        var best = ordered[0];

        var cpu = components.FirstOrDefault(c => c.Component.Category == ComponentCategory.Cpu);
        if (cpu is not null && summaries.All(s => cpu.For(s.DistroId).Rating == Rating.Unsupported))
        {
            return new Recommendation
            {
                Paragraph = "GreyHaven can't recommend switching this PC to Linux Mint, Ubuntu or Pop!_OS: none of them offers a standard installer for its processor. " +
                            "Check your PC maker's support pages or the distro's own hardware lists before trying.",
            };
        }

        var easy = NeedsSecureBootOff(best) ? EasyCandidate(catalog, summaries, NeedsSecureBootOff) : null;
        if (easy is null)
        {
            var paragraph = SinglePath(profile, components, summaries, ordered, catalog);
            var requirements = NeedsSecureBootOff(best) ? [PreSwitchRequirement.TurnOffSecureBoot] : Array.Empty<PreSwitchRequirement>();
            return new Recommendation
            {
                Paragraph = paragraph,
                Paths = [MakePath(PathKind.Recommended, best, catalog, paragraph, requirements,
                    requirements.Length > 0 ? SecureBootNotice(profile, catalog.Get(best.DistroId)) : null,
                    requirements.Length > 0 ? guides.For(profile) : null)],
                DefaultPath = PathKind.Recommended,
            };
        }

        var bestInfo = catalog.Get(best.DistroId);
        var intro = sb == SecureBootState.Unknown
            ? $"{bestInfo.Name} has the best support for this PC's hardware, but it can't be installed while Secure Boot is on, and GreyHaven couldn't tell whether it is. Choose the path that suits you:"
            : $"{bestInfo.Name} has the best support for this PC's hardware, but it needs Secure Boot turned off. Choose the path that suits you:";

        return new Recommendation
        {
            Paragraph = intro,
            Paths =
            [
                MakePath(PathKind.Easy, easy, catalog, EasyParagraph(components, easy, best, catalog), [], null, null),
                MakePath(PathKind.Advanced, best, catalog, AdvancedParagraph(components, best, easy, catalog),
                    [PreSwitchRequirement.TurnOffSecureBoot], SecureBootNotice(profile, bestInfo), guides.For(profile)),
            ],
            DefaultPath = PathKind.Easy,
        };
    }

    private static DistroSummary? EasyCandidate(DistroCatalog catalog, IReadOnlyList<DistroSummary> summaries, Func<DistroSummary, bool> needsOff)
    {
        var preferred = summaries.FirstOrDefault(s => s.DistroId == catalog.SecureBootEasyModeDistro);
        if (preferred is not null && !needsOff(preferred)) return preferred;
        return summaries
            .Where(s => !needsOff(s))
            .OrderByDescending(s => s.Score.Score)
            .ThenBy(s => catalog.Get(s.DistroId).TieBreakRank)
            .FirstOrDefault();
    }

    private static RecommendationPath MakePath(PathKind kind, DistroSummary s, DistroCatalog catalog, string paragraph,
        IReadOnlyList<PreSwitchRequirement> requirements, string? notice, GuideLink? guide)
    {
        var info = catalog.Get(s.DistroId);
        return new RecommendationPath
        {
            Kind = kind,
            DistroId = s.DistroId,
            EditionId = s.EditionId,
            EditionName = s.Edition,
            Title = kind switch
            {
                PathKind.Easy => $"Easy: {info.Name}",
                PathKind.Advanced => $"Advanced: {info.Name}",
                _ => info.Name,
            },
            Paragraph = paragraph,
            Requirements = requirements,
            SecureBootNotice = notice,
            SecureBootGuide = guide,
        };
    }

    // ---- Paragraphs --------------------------------------------------------------------------

    private static string SinglePath(HardwareProfile profile, IReadOnlyList<RatedComponent> components,
        IReadOnlyList<DistroSummary> summaries, IReadOnlyList<DistroSummary> ordered, DistroCatalog catalog)
    {
        var best = ordered[0];
        var bestInfo = catalog.Get(best.DistroId);
        var sentences = new List<string> { $"{bestInfo.Name} {bestInfo.Version} is the best fit for this PC, with a score of {best.Score.Score} out of 100 ({BandWord(best.Score.Band)})." };

        var runnerUp = ordered.Count > 1 ? ordered[1] : null;
        var better = runnerUp is null ? [] : BetterOn(components, best, runnerUp);
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
        AddCounts(sentences, best);
        if (!bestInfo.SecureBoot.Supported && profile.System.SecureBoot is SecureBootState.Enabled or SecureBootState.Unknown)
            sentences.Add($"You'll need to turn off Secure Boot in your PC's firmware settings before installing {bestInfo.Name}.");
        if (best.Score.CapReason is not null) sentences.Add(best.Score.CapReason);
        return string.Join(" ", sentences);
    }

    private static string EasyParagraph(IReadOnlyList<RatedComponent> components, DistroSummary easy, DistroSummary best, DistroCatalog catalog)
    {
        var info = catalog.Get(easy.DistroId);
        var bestName = catalog.Get(best.DistroId).Name;
        var sentences = new List<string>
        {
            $"{info.Name} {info.Version} keeps Secure Boot on, so you don't need to change any firmware settings.",
            easy.Score.Score == best.Score.Score
                ? $"It scores {easy.Score.Score} out of 100 ({BandWord(easy.Score.Band)}) on this PC, the same as {bestName}."
                : $"It scores {easy.Score.Score} out of 100 ({BandWord(easy.Score.Band)}) on this PC, compared with {best.Score.Score} for {bestName}.",
        };

        // Explain each extra step in plain words, using the fix's own steps.
        var extra = components.Where(c => c.For(easy.DistroId) is { Rating: Rating.WorksWithExtraDriver, Fix: not null }).ToList();
        foreach (var c in extra)
        {
            var fix = c.For(easy.DistroId).Fix!;
            var what = Prose(c.Component.Category);
            var steps = fix.Steps.Count > 0
                ? string.Join(" ", fix.Steps)
                : $"install the {fix.Driver} (the details below show the exact commands).";
            sentences.Add($"Your {what} needs one extra step after installing: {LowerFirst(steps)}");
        }
        if (extra.Any(c => c.For(easy.DistroId).Fix!.SecureBoot != SecureBootImpact.None))
            sentences.Add("Because Secure Boot stays on, you may be asked to create a one-time password while the driver installs. After restarting, a blue \"Enroll MOK\" screen appears: choose \"Enroll MOK\", then \"Continue\", and type that password. This is normal.");

        if (easy.Score.UnknownCount > 0)
            sentences.Add(easy.Score.UnknownCount == 1
                ? "One part couldn't be rated, so check it from a live USB before you switch."
                : $"{easy.Score.UnknownCount} parts couldn't be rated, so check them from a live USB before you switch.");
        if (easy.Score.CapReason is not null) sentences.Add(easy.Score.CapReason);
        return string.Join(" ", sentences);
    }

    private static string AdvancedParagraph(IReadOnlyList<RatedComponent> components, DistroSummary best, DistroSummary easy, DistroCatalog catalog)
    {
        var info = catalog.Get(best.DistroId);
        var easyName = catalog.Get(easy.DistroId).Name;
        var sentences = new List<string> { $"{info.Name} {info.Version} scores {best.Score.Score} out of 100 ({BandWord(best.Score.Band)}) on this PC." };

        var better = BetterOn(components, best, easy);
        var edition = info.Editions.First(e => e.Id == best.EditionId);
        var preinstalledReason = better.Count > 0 && edition.PreinstalledDrivers.Count > 0;
        if (better.Count > 0)
        {
            sentences.Add(preinstalledReason
                ? $"It handles your {JoinList(better)} better than {easyName}, because its {edition.Name} comes with the driver already installed."
                : $"It handles your {JoinList(better)} better than {easyName}.");
        }
        // The audience sentence can repeat the preinstalled-driver point; skip it then.
        if (info.Audience is not null && !preinstalledReason) sentences.Add(info.Audience);
        AddCounts(sentences, best);
        if (best.Score.CapReason is not null) sentences.Add(best.Score.CapReason);
        return string.Join(" ", sentences);
    }

    /// <summary>What turning Secure Boot off means, in plain words. Never an automatic change.</summary>
    internal static string SecureBootNotice(HardwareProfile profile, DistroInfo distro)
    {
        var sentences = new List<string>();
        sentences.Add(profile.System.SecureBoot == SecureBootState.Unknown
            ? $"GreyHaven couldn't tell whether Secure Boot is on. If it is, you'll need to turn it off before installing {distro.Name}."
            : $"You'll need to turn off Secure Boot before installing {distro.Name}.");
        sentences.Add("Secure Boot is a firmware setting that only lets approved software start your PC; with it off, the PC is a little less protected against rare, low-level malware.");
        sentences.Add($"You can turn it back on later, but {distro.Name} won't start while it's on, so only do that if you go back to using Windows alone.");
        if (profile.System.BitLocker is BitLockerState.On or BitLockerState.Encrypting or BitLockerState.Suspended or BitLockerState.Locked or BitLockerState.Unknown)
            sentences.Add("Save your BitLocker recovery key first: Windows may ask for it after you change firmware settings.");
        sentences.Add("GreyHaven won't change this setting for you; follow the guide.");
        return string.Join(" ", sentences);
    }

    private static void AddCounts(List<string> sentences, DistroSummary s)
    {
        if (s.Score.ExtraDriverCount > 0)
            sentences.Add(s.Score.ExtraDriverCount == 1
                ? "One part needs an extra step after installing; the details below show exactly what to do."
                : $"{s.Score.ExtraDriverCount} parts need an extra step after installing; the details below show exactly what to do.");
        if (s.Score.UnknownCount > 0)
            sentences.Add(s.Score.UnknownCount == 1
                ? "One part couldn't be rated, so check it from a live USB before you switch."
                : $"{s.Score.UnknownCount} parts couldn't be rated, so check them from a live USB before you switch.");
    }

    private static List<string> BetterOn(IReadOnlyList<RatedComponent> components, DistroSummary a, DistroSummary b) =>
        components
            .Where(c => Rank(c.For(a.DistroId).Rating) < Rank(c.For(b.DistroId).Rating))
            .Select(c => Prose(c.Component.Category))
            .Distinct()
            .ToList();

    private static int Rank(Rating r) => r switch
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
