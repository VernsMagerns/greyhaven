using System.Text.RegularExpressions;
using GreyHaven.Compat;
using GreyHaven.Compat.Data;
using GreyHaven.Compat.Kernel;
using GreyHaven.Compat.Online;
using GreyHaven.Compat.Rules;
using GreyHaven.Core.Compatibility;
using GreyHaven.Core.Hardware;

namespace GreyHaven.Ratings;

public sealed class RatingOptions
{
    /// <summary>
    /// Allow network access: linux-hardware.org for devices nothing else knows about, and the
    /// distros' release feeds for the data-freshness check.
    /// </summary>
    public bool UseOnlineLookups { get; init; } = true;
}

/// <summary>Progress for the UI: a plain-English message, optionally "n of total".</summary>
public sealed record RatingProgress(string Message, int? Current = null, int? Total = null);

/// <summary>
/// Rates every component for every distro. For each component and distro, the first source
/// that has something to say wins:
/// <list type="number">
///   <item>A curated device or vendor rule (data/rules) - hand-checked knowledge overrides raw data.</item>
///   <item>For fingerprint readers, the distro's libfprint supported/unsupported lists.</item>
///   <item>A kernel driver that claims this exact device ID.</item>
///   <item>A driver package in the distro's repositories that claims this exact device ID.</item>
///   <item>A curated class rule (e.g. "precision touchpads work").</item>
///   <item>A kernel driver that claims the whole device class (e.g. NVMe, USB webcams).</item>
///   <item>linux-hardware.org community probes (online, low confidence).</item>
///   <item>Otherwise: Unknown. GreyHaven never guesses.</item>
/// </list>
/// If the install ISO's kernel lacks support but the kernel installed with updates has it,
/// the rating becomes "works with extra driver/config: install updates".
/// </summary>
public sealed partial class RatingEngine(
    CompatibilityData data,
    ICommunityLookup? community = null,
    IDistroReleaseChecker? releases = null,
    TimeProvider? time = null)
{
    private readonly TimeProvider _time = time ?? TimeProvider.System;

    private const string LibfprintListUrl = "https://fprint.freedesktop.org/supported-devices.html";

    public async Task<CompatibilityReport> RateAsync(
        HardwareProfile profile,
        RatingOptions? options = null,
        IProgress<RatingProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        options ??= new RatingOptions();
        progress?.Report(new RatingProgress("Checking Linux support for each part"));
        var contexts = data.Distros.Distros.Select(d => CreateContext(d, profile)).ToList();
        var dataNotes = new List<string>();

        // 1. Offline evidence for every component and distro.
        var rows = profile.Components
            .Select(c => (Component: c, Ratings: contexts.Select(ctx => Evaluate(c, profile.System, ctx)).ToList()))
            .ToList();

        // 2. Online community data, only for devices nothing offline knows about.
        if (options.UseOnlineLookups && community is not null)
        {
            var unavailableNoted = false;
            var toLookUp = rows.Where(r => r.Component.Id is not null && r.Ratings.Any(x => x.Rating == Rating.Unknown)).ToList();
            for (var n = 0; n < toLookUp.Count; n++)
            {
                var row = toLookUp[n];
                progress?.Report(new RatingProgress("Checking the online Linux hardware database", n + 1, toLookUp.Count));
                var result = await community.LookupAsync(row.Component.Id!, cancellationToken).ConfigureAwait(false);
                if (result.Status == CommunityStatus.Unavailable)
                {
                    if (!unavailableNoted) dataNotes.Add($"Online lookups were incomplete: {result.Reason} Ratings use GreyHaven's built-in data only.");
                    unavailableNoted = true;
                    continue;
                }
                for (var i = 0; i < row.Ratings.Count; i++)
                    if (row.Ratings[i].Rating == Rating.Unknown)
                        row.Ratings[i] = ApplyCommunity(row.Ratings[i], result, contexts[i]);
            }
        }
        else if (!options.UseOnlineLookups)
        {
            dataNotes.Add("Online lookups were turned off. Ratings use GreyHaven's built-in data only.");
        }

        // 3. Data freshness (release feeds only when online lookups are allowed).
        ReleaseCheckResult? releaseCheck = null;
        if (options.UseOnlineLookups && releases is not null)
        {
            progress?.Report(new RatingProgress("Checking for new distro releases"));
            releaseCheck = await releases.CheckAsync(data.Distros, cancellationToken).ConfigureAwait(false);
        }
        var today = DateOnly.FromDateTime(_time.GetLocalNow().DateTime);
        var freshness = Freshness.Evaluate(data.Dataset.BuiltOnDate, today, releaseCheck);

        // 4. Caveats, importance, scores, recommendation.
        var caveats = FindCaveats(profile);
        var displayNames = ComponentNames.Distinct(rows.Select(r => r.Component).ToList());
        var rated = rows.Select((r, i) => new RatedComponent
        {
            Component = r.Component,
            DisplayName = displayNames[i],
            Importance = ImportanceRules.For(r.Component, profile),
            Ratings = r.Ratings,
            CaveatIds = caveats.Where(c => c.Components.Contains(r.Component)).Select(c => c.Caveat.Id).ToList(),
        }).ToList();

        var summaries = contexts.Select(ctx => Summarize(ctx, rated, profile)).ToList();
        // Approximate kernel data is explained on each affected rating (ApproximationNote), not here.

        var recommendation = Recommender.Recommend(profile, rated, summaries, data.Distros, data.SecureBootGuides);
        var caveatList = caveats.Select(c => c.Caveat).ToList();
        return new CompatibilityReport
        {
            Profile = profile,
            Distros = summaries,
            Components = rated,
            Recommendation = recommendation,
            SelectedPath = recommendation.DefaultPath,
            Caveats = caveatList,
            LiveUsbGuides = LiveUsbGuides.Build(profile, rated, summaries, caveatList, data),
            DataNotes = dataNotes.Distinct().ToList(),
            ContextNotes = WindowsNotes.For(profile.System, data.Windows, today),
            Maker = data.Makers.For(profile.System),
            Data = freshness,
            GeneratedAt = _time.GetLocalNow(),
        };
    }

    // ---- Per-distro context ---------------------------------------------------------------

    internal sealed record DistroContext(
        DistroInfo Distro,
        DistroEdition Edition,
        KernelInfo IsoKernel,
        AliasIndex IsoIndex,
        KernelInfo? UpdatesKernel,
        AliasIndex? UpdatesIndex,
        AliasIndex? Packages,
        FingerprintSet? Fingerprint,
        AliasIndex? FingerprintIndex,
        SecureBootState SecureBoot)
    {
        /// <summary>The same context but rating against the kernel installed with updates.</summary>
        public DistroContext WithUpdatesKernel() => this with { IsoKernel = UpdatesKernel!, IsoIndex = UpdatesIndex! };

        public bool HasNewerUpdatesKernel => UpdatesKernel is not null && UpdatesIndex is not null && UpdatesKernel != IsoKernel;
    }

    private DistroContext CreateContext(DistroInfo d, HardwareProfile profile)
    {
        var hasNvidia = profile.Components.Any(c => c.Category == ComponentCategory.Gpu && c.Id?.Vendor == "10de");
        var edition = d.Editions.FirstOrDefault(e => e.SelectWhen == "nvidiaGpu" && hasNvidia)
                      ?? d.Editions.First(e => e.SelectWhen is null);
        var iso = data.Distros.Kernels[edition.IsoKernel];
        var updates = d.UpdatesKernel is null ? null : data.Distros.Kernels[d.UpdatesKernel];
        var fp = d.FingerprintSet is null ? null : data.Distros.FingerprintSets[d.FingerprintSet];
        return new DistroContext(
            d, edition, iso, data.KernelIndexes[edition.IsoKernel],
            updates, d.UpdatesKernel is null ? null : data.KernelIndexes[d.UpdatesKernel],
            d.DriverPackageSet is null ? null : data.PackageIndexes[d.DriverPackageSet],
            fp, d.FingerprintSet is null ? null : data.FingerprintIndexes[d.FingerprintSet],
            profile.System.SecureBoot);
    }

    // ---- Evaluation -----------------------------------------------------------------------

    private ComponentRating Evaluate(HardwareComponent c, SystemInfo system, DistroContext ctx)
    {
        // The kernel and package indexes describe x64 builds. On an ARM PC they say nothing
        // about the ARM kernel, so only the CPU (which has curated ARM rules) can be rated.
        if (system.IsArm && c.Category != ComponentCategory.Cpu) return ArmUnknown(c, ctx);

        var result = EvaluateWith(c, system, ctx);
        if (result.Rating is Rating.WorksOutOfTheBox or Rating.WorksWithExtraDriver || !ctx.HasNewerUpdatesKernel)
            return result;

        var updated = EvaluateWith(c, system, ctx.WithUpdatesKernel());
        return updated.Rating == Rating.WorksOutOfTheBox ? AfterUpdates(c, updated, ctx) : result;
    }

    private ComponentRating EvaluateWith(HardwareComponent c, SystemInfo system, DistroContext ctx)
    {
        var rule = data.Rules.FindBest(c, system);
        if (rule is { Strength: >= RuleStrength.Vendor }) return FromRule(rule.Rule, c, ctx);

        if (c.Category == ComponentCategory.FingerprintReader && FromLibfprint(c, ctx) is { } fp) return fp;

        if (c.Id is not null)
        {
            var kernel = ctx.IsoIndex.Match(c.Id).Where(m => data.Modules.CountsAsDriver(m.Target)).ToList();
            var specific = kernel.Where(m => m.IsDeviceSpecific).ToList();
            if (specific.Count > 0) return FromKernel(c, specific, ctx, classWide: false);

            var packages = MatchingPackages(c.Id, ctx).Where(m => m.IsDeviceSpecific).ToList();
            if (packages.Count > 0) return FromPackage(packages, ctx);

            if (rule is not null) return FromRule(rule.Rule, c, ctx);
            if (kernel.Count > 0) return FromKernel(c, kernel, ctx, classWide: true);
        }
        else if (rule is not null)
        {
            return FromRule(rule.Rule, c, ctx);
        }

        return Unknown(c, ctx);
    }

    private List<AliasEntry> MatchingPackages(DeviceId id, DistroContext ctx) =>
        ctx.Packages?.Match(id)
            .Where(m => !data.Modules.IgnorePackages.Any(g => Glob.IsMatch(g, m.Target)))
            .ToList() ?? [];

    private ComponentRating FromRule(Rule rule, HardwareComponent c, DistroContext ctx)
    {
        var packages = c.Id is null ? [] : MatchingPackages(c.Id, ctx).Select(p => p.Target).Distinct().ToList();
        var outcome = rule.Ratings.First(o => Holds(o.When, ctx, packages));
        var rating = RuleSet.ParseRating(outcome.Rating);
        var fixData = outcome.Fix ?? (rating == Rating.WorksWithExtraDriver ? rule.Fix : null);

        return new ComponentRating
        {
            DistroId = ctx.Distro.Id,
            Rating = rating,
            Confidence = RuleSet.ParseConfidence(outcome.Confidence ?? rule.Confidence),
            Explanation = Fill(outcome.Explanation, ctx, packages),
            Summary = outcome.Summary is null ? null : Fill(outcome.Summary, ctx, packages),
            Sources = rule.Sources.Where(s => s.AppliesTo(ctx.Distro.Id)).Select(s => new Source(s.Title, s.Url, SourceKind.CuratedRule)).ToList(),
            Fix = fixData is null ? null : BuildFix(fixData, ctx) with { Id = rule.Id },
            LinuxDriver = fixData?.Driver,
        };
    }

    private static bool Holds(RuleCondition? when, DistroContext ctx, IReadOnlyList<string> packages)
    {
        if (when is null) return true;
        if (when.Distros.Count > 0 && !when.Distros.Contains(ctx.Distro.Id)) return false;
        if (when.KernelAtLeast is not null && CompareSeries(ctx.IsoKernel.Series, when.KernelAtLeast) < 0) return false;
        if (when.KernelBelow is not null && CompareSeries(ctx.IsoKernel.Series, when.KernelBelow) >= 0) return false;
        if (when.PreinstalledDriver is not null && !ctx.Edition.PreinstalledDrivers.Contains(when.PreinstalledDriver)) return false;
        if (when.DriverPackage is not null && !packages.Any(p => Glob.IsMatch(when.DriverPackage, p))) return false;
        if (when.OnlyDriverPackages is not null && (packages.Count == 0 || !packages.All(p => Glob.IsMatch(when.OnlyDriverPackages, p)))) return false;
        if (when.SecureBoot is { } sb && sb != (ctx.SecureBoot == SecureBootState.Enabled)) return false;
        return true;
    }

    private ComponentRating? FromLibfprint(HardwareComponent c, DistroContext ctx)
    {
        if (c.Id is not { Bus: DeviceBus.Usb } || ctx.FingerprintIndex is null) return null;
        // libfprint's hwdb patterns look like "usb:v27C6p633C*", so they match on vendor/product only.
        var hit = ctx.FingerprintIndex.Match(c.Id).FirstOrDefault();
        var version = ctx.Fingerprint!.Version;
        var source = new Source($"libfprint {version} supported and unsupported device lists", LibfprintListUrl, SourceKind.Libfprint);

        if (hit is null)
        {
            return new ComponentRating
            {
                DistroId = ctx.Distro.Id,
                Rating = Rating.Unknown,
                Confidence = Confidence.Low,
                Explanation = $"This fingerprint reader ({c.Id.VendorDevice}) isn't on the supported or known-unsupported lists of libfprint {version}, the fingerprint software {ctx.Distro.Name} uses. It most likely won't work, but GreyHaven can't confirm that.",
                Sources = [source],
            };
        }
        if (hit.Target == "libfprint:unsupported")
        {
            return new ComponentRating
            {
                DistroId = ctx.Distro.Id,
                Rating = Rating.Unsupported,
                Confidence = Confidence.High,
                Explanation = $"This fingerprint reader is on libfprint {version}'s list of known unsupported devices, so fingerprint login won't work on {ctx.Distro.Name}. You can still log in with your password.",
                Summary = "Fingerprint login won't work. You can still use your password.",
                Sources = [source],
            };
        }
        var driver = hit.Target["libfprint:".Length..];
        return new ComponentRating
        {
            DistroId = ctx.Distro.Id,
            Rating = Rating.WorksOutOfTheBox,
            Confidence = Confidence.Medium,
            Explanation = $"libfprint {version} in {ctx.Distro.Name} supports this fingerprint reader (its \"{driver}\" driver). Enrolling a finger still needs testing after you install.",
            Sources = [source],
            LinuxDriver = $"libfprint ({driver})",
        };
    }

    private ComponentRating FromKernel(HardwareComponent c, List<AliasEntry> matches, DistroContext ctx, bool classWide)
    {
        var modules = matches.Select(m => m.Target).Distinct().ToList();
        var module = modules[0];
        var confidence = modules.Select(m => data.Modules.Get(m)?.Confidence).FirstOrDefault(x => x is not null) is { } conf
            ? RuleSet.ParseConfidence(conf)
            : Confidence.Medium;
        var series = ctx.IsoKernel.Series;
        var approximation = ApproximationNote(ctx, matches);
        if (approximation is not null && confidence == Confidence.High) confidence = Confidence.Medium;
        var explanation = classWide
            ? $"{ctx.Distro.Name}'s Linux {series} kernel handles this as a standard {Labels.Of(c.Category).ToLowerInvariant()} using its \"{module}\" driver."
            : $"{ctx.Distro.Name}'s Linux {series} kernel includes the \"{module}\" driver, which lists this exact device.";
        return new ComponentRating
        {
            DistroId = ctx.Distro.Id,
            Rating = Rating.WorksOutOfTheBox,
            Confidence = confidence,
            Explanation = explanation,
            Sources = [KernelSource(ctx.IsoKernel, ctx.Distro, module)],
            LinuxDriver = string.Join(", ", modules),
            ApproximationNote = approximation,
        };
    }

    /// <summary>
    /// Explains why kernel evidence is approximate, or null if it isn't. Approximate evidence is
    /// capped at medium confidence.
    /// </summary>
    private static string? ApproximationNote(DistroContext ctx, IReadOnlyList<AliasEntry> matches)
    {
        var notes = new List<string>();
        if (ctx.IsoKernel.Approximation is not null)
            notes.Add($"{ctx.Distro.Name} doesn't publish this exact kernel package, so GreyHaven used the matching upstream kernel release ({ctx.IsoKernel.Series}).");
        if (matches.Count > 0 && matches.All(m => m.Provenance is not null))
            notes.Add($"Linux {ctx.IsoKernel.Series} doesn't list its built-in drivers' devices, so GreyHaven used this driver's general pattern from a newer kernel.");
        return notes.Count == 0 ? null : string.Join(" ", notes);
    }

    private static Source KernelSource(KernelInfo k, DistroInfo d, string module) =>
        new($"{d.Name} kernel {k.Version}, module \"{module}\" (device ID table)", k.Packages.FirstOrDefault(), SourceKind.KernelDriver);

    private static ComponentRating FromPackage(List<AliasEntry> matches, DistroContext ctx)
    {
        var package = BestPackage(matches.Select(m => m.Target));
        return new ComponentRating
        {
            DistroId = ctx.Distro.Id,
            Rating = Rating.WorksWithExtraDriver,
            Confidence = Confidence.Medium,
            Explanation = $"{ctx.Distro.Name}'s Linux kernel doesn't include a driver for this device, but its software repositories have one: the \"{package}\" package. It isn't installed automatically.",
            Sources = [new Source($"{ctx.Distro.Name} package index: \"{package}\" lists this device", null, SourceKind.DriverPackage)],
            Fix = new Fix
            {
                Driver = package,
                Commands = ["sudo apt update", $"sudo apt install {package}"],
                NeedsInternet = true,
                SecureBoot = SecureBootImpact.MokMayBeRequired,
                SecureBootAdvice = SecureBootAdvice(SecureBootImpact.MokMayBeRequired, ctx),
            },
            LinuxDriver = package,
        };
    }

    private static ComponentRating AfterUpdates(HardwareComponent c, ComponentRating updated, DistroContext ctx)
    {
        var iso = ctx.IsoKernel.Series;
        var upd = ctx.UpdatesKernel!.Series;
        var isNetwork = c.Category is ComponentCategory.Wifi or ComponentCategory.Ethernet or ComponentCategory.UsbNetwork;
        var isoApproximate = ctx.IsoKernel.Approximation is not null
            ? $"The \"no driver on the install USB\" part is based on approximate data: {ctx.Distro.Name} doesn't publish its exact install kernel package, so GreyHaven used the matching upstream release."
            : null;
        return updated with
        {
            Rating = Rating.WorksWithExtraDriver,
            Confidence = isoApproximate is not null && updated.Confidence == Confidence.High ? Confidence.Medium : updated.Confidence,
            ApproximationNote = isoApproximate ?? updated.ApproximationNote,
            Explanation = $"The {ctx.Distro.Name} install USB uses Linux {iso}, which doesn't support this device, but the Linux {upd} kernel that {ctx.Distro.Name} installs with system updates does{(updated.LinuxDriver is null ? "" : $" (\"{updated.LinuxDriver}\")")}. Install updates after setup, then restart.",
            Fix = new Fix
            {
                Driver = $"Linux {ctx.UpdatesKernel.Version} (system updates)",
                Steps = ["Connect to the internet.", "Install all available updates, then restart."],
                Commands = ["sudo apt update", "sudo apt full-upgrade"],
                NeedsInternet = true,
                Note = isNetwork ? "Because this is a network adapter, you'll need another way online for the update: a network cable or USB tethering from your phone." : null,
            },
        };
    }

    private static ComponentRating ArmUnknown(HardwareComponent c, DistroContext ctx) => new()
    {
        DistroId = ctx.Distro.Id,
        Rating = Rating.Unknown,
        Confidence = Confidence.Low,
        Explanation = $"This PC has an ARM processor. GreyHaven's driver lists come from {ctx.Distro.Name}'s 64-bit Intel/AMD (x64) kernel, so they can't tell whether this part works on ARM.",
    };

    private static ComponentRating Unknown(HardwareComponent c, DistroContext ctx)
    {
        var what = c.Id is null ? "this component" : $"this device ({c.Id.Key})";
        var sources = new List<Source>();
        if (c.Id is not null) sources.Add(new Source($"Searched: {ctx.Distro.Name} kernel {ctx.IsoKernel.Version} driver list", ctx.IsoKernel.Packages.FirstOrDefault(), SourceKind.KernelDriver));
        return new ComponentRating
        {
            DistroId = ctx.Distro.Id,
            Rating = Rating.Unknown,
            Confidence = Confidence.Low,
            Explanation = $"GreyHaven found no Linux driver for {what} in {ctx.Distro.Name}'s kernel, and no other data about it. That doesn't necessarily mean it won't work - test it with a live USB.",
            Sources = sources,
        };
    }

    // ---- Community data ---------------------------------------------------------------------

    /// <summary>Minimum community probes before LHDB data is allowed to produce a rating.</summary>
    public const int MinCommunitySample = 5;

    internal static ComponentRating ApplyCommunity(ComponentRating unknown, CommunityResult r, DistroContext ctx)
    {
        if (r.Status != CommunityStatus.Found || r.Url is null) return unknown;
        var source = new Source($"linux-hardware.org: {r.DeviceName ?? "device page"}", r.Url, SourceKind.LinuxHardwareDb);
        var withSource = unknown with { Sources = [.. unknown.Sources, source] };

        var bad = r.Failed + r.Malfunctioning;
        var decided = r.Works + bad;
        if (decided < MinCommunitySample) return withSource;

        var sample = $"{r.Works} of {decided} recent Linux computers in the Linux Hardware Database report it working";
        var share = (double)r.Works / decided;
        if (share >= 0.8)
        {
            return withSource with
            {
                Rating = Rating.WorksOutOfTheBox,
                Confidence = Confidence.Low,
                Explanation = $"{sample}. GreyHaven couldn't confirm this with {ctx.Distro.Name}'s own driver list, so treat it as a good sign rather than a guarantee.",
            };
        }
        return withSource with
        {
            Rating = Rating.Partial,
            Confidence = Confidence.Low,
            Explanation = $"Only {sample}; others report problems. GreyHaven couldn't confirm this with {ctx.Distro.Name}'s own driver list.",
        };
    }

    // ---- Fixes ------------------------------------------------------------------------------

    private static Fix BuildFix(RuleFix f, DistroContext ctx) => BuildFix(f, ctx.Distro, ctx.SecureBoot);

    internal static Fix BuildFix(RuleFix f, DistroInfo distro, SecureBootState secureBoot)
    {
        var impact = RuleSet.ParseSecureBoot(f.SecureBoot);
        return new Fix
        {
            Driver = f.Driver,
            Steps = f.Steps.GetValueOrDefault(distro.Id) ?? f.Steps.GetValueOrDefault("default") ?? [],
            Commands = f.Commands.GetValueOrDefault(distro.Id) ?? f.Commands.GetValueOrDefault("default") ?? [],
            Note = f.Note,
            NeedsInternet = f.NeedsInternet,
            SecureBoot = impact,
            SecureBootAdvice = SecureBootAdvice(impact, distro, secureBoot),
        };
    }

    private static string? SecureBootAdvice(SecureBootImpact impact, DistroContext ctx) =>
        SecureBootAdvice(impact, ctx.Distro, ctx.SecureBoot);

    /// <summary>Also used for the public phone guide, where the PC's Secure Boot state is unknown.</summary>
    internal static string? SecureBootAdvice(SecureBootImpact impact, DistroInfo distro, SecureBootState secureBoot)
    {
        if (impact == SecureBootImpact.None || secureBoot is SecureBootState.Disabled or SecureBootState.NotSupported) return null;
        if (!distro.SecureBoot.Supported)
            return $"{distro.Name} requires Secure Boot to be turned off before installing, so no extra Secure Boot step is needed for this driver.";

        // Plain words: the blue "Enroll MOK" screen scares people who aren't expecting it.
        var start = secureBoot == SecureBootState.Unknown ? "If Secure Boot is on, " : "Secure Boot is on, so ";
        return impact == SecureBootImpact.MokRequired
            ? start + "this driver needs your OK before it can run. While it installs, make up a password. After you restart, a blue screen appears. Choose \"Enroll MOK\", then \"Continue\", and type that password. This is normal."
            : start + "you may be asked to make up a password while the driver installs. After you restart, a blue screen appears. Choose \"Enroll MOK\", then \"Continue\", and type that password. This is normal.";
    }

    // ---- Caveats ----------------------------------------------------------------------------

    private List<(Caveat Caveat, List<HardwareComponent> Components)> FindCaveats(HardwareProfile profile)
    {
        var result = new List<(Caveat, List<HardwareComponent>)>();
        var gpus = profile.Components.Where(c => c.Category == ComponentCategory.Gpu).Sum(c => c.Count);
        foreach (var info in data.Caveats.Caveats)
        {
            var w = info.AppliesWhen;
            List<HardwareComponent> related = [];
            bool applies;
            if (w.Always) applies = true;
            else if (w.FormFactor is not null) applies = string.Equals(w.FormFactor, profile.System.FormFactor.ToString(), StringComparison.OrdinalIgnoreCase);
            else if (w.MinGpus is { } min)
            {
                applies = gpus >= min;
                related = profile.Components.Where(c => c.Category == ComponentCategory.Gpu).ToList();
            }
            else if (w.Category is not null)
            {
                related = profile.Components.Where(c => string.Equals(c.Category.ToString(), w.Category, StringComparison.OrdinalIgnoreCase)).ToList();
                applies = related.Count > 0;
            }
            else applies = false;

            if (applies)
                result.Add((new Caveat { Id = info.Id, Title = info.Title, Why = info.Why, WhatToTest = info.TestFor(profile.System.FormFactor.ToString()) }, related));
        }
        return result;
    }

    // ---- Summaries --------------------------------------------------------------------------

    private DistroSummary Summarize(DistroContext ctx, IReadOnlyList<RatedComponent> rated, HardwareProfile profile)
    {
        var items = rated
            .Where(r => ImportanceRules.CountsTowardScore(r.Component))
            .Select(r => (r.Importance, r.For(ctx.Distro.Id).Rating, Labels.Of(r.Component.Category)))
            .ToList();

        var notes = new List<string>();
        if (!ctx.Distro.SecureBoot.Supported && profile.System.SecureBoot == SecureBootState.Enabled)
            notes.Add($"Secure Boot is on. {ctx.Distro.SecureBoot.Note}");
        if (ctx.Edition.SelectWhen == "nvidiaGpu")
            notes.Add($"GreyHaven picked the {ctx.Edition.Name} because this PC has NVIDIA graphics.");
        if (profile.System.IsArm)
            notes.Add("This PC has an ARM processor; most of its parts can't be checked against this distro's x64 kernel.");

        return new DistroSummary
        {
            DistroId = ctx.Distro.Id,
            DisplayName = ctx.Distro.DisplayName,
            Edition = ctx.Edition.Name,
            EditionId = ctx.Edition.Id,
            KernelVersion = ctx.IsoKernel.Version,
            Score = ScoreCalculator.Calculate(items),
            Notes = notes,
        };
    }

    // ---- Helpers ----------------------------------------------------------------------------

    private string Fill(string template, DistroContext ctx, IReadOnlyList<string> packages)
    {
        var fpVersion = ctx.Fingerprint?.Version ?? "";
        return Collapse(template
            .Replace("{distro}", ctx.Distro.Name)
            .Replace("{kernel}", ctx.IsoKernel.Version)
            .Replace("{kernelSeries}", ctx.IsoKernel.Series)
            .Replace("{driverTool}", ctx.Distro.DriverTool ?? "its driver tool")
            .Replace("{edition}", ctx.Edition.Name)
            .Replace("{driverPackages}", packages.Count == 0 ? "none" : BestPackage(packages))
            .Replace("{libfprint}", fpVersion));
    }

    /// <summary>Picks the newest non-server package, e.g. "nvidia-driver-610" from 580/595/610 and -server variants.</summary>
    internal static string BestPackage(IEnumerable<string> packages)
    {
        var list = packages.Distinct().ToList();
        var preferred = list.Where(p => !p.Contains("-server", StringComparison.Ordinal) && !p.EndsWith("-open", StringComparison.Ordinal)).ToList();
        var pool = preferred.Count > 0 ? preferred : list;
        return pool.OrderByDescending(p => NumberRegex().Match(p) is { Success: true } m ? int.Parse(m.Value) : 0)
                   .ThenBy(p => p, StringComparer.Ordinal)
                   .First();
    }

    internal static int CompareSeries(string a, string b)
    {
        static (int, int) Parse(string s)
        {
            var parts = s.Split('.');
            return (int.Parse(parts[0]), parts.Length > 1 ? int.Parse(parts[1]) : 0);
        }
        return Parse(a).CompareTo(Parse(b));
    }

    private static string Collapse(string s) => WhitespaceRegex().Replace(s, " ").Trim();

    [GeneratedRegex(@"\d+")]
    private static partial Regex NumberRegex();

    [GeneratedRegex(@"\s+")]
    private static partial Regex WhitespaceRegex();
}
