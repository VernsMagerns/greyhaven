using GreyHaven.Compat.Data;
using GreyHaven.Core.Compatibility;
using GreyHaven.Core.Hardware;

namespace GreyHaven.Compat.Rules;

/// <summary>How specific a rule match is. Device- and vendor-level rules override kernel data; class rules don't.</summary>
public enum RuleStrength { Class, Vendor, Device }

public sealed record RuleMatchResult(Rule Rule, int Specificity)
{
    public RuleStrength Strength => Specificity switch
    {
        >= 80 => RuleStrength.Device,
        >= 50 => RuleStrength.Vendor,
        _ => RuleStrength.Class,
    };
}

/// <summary>All curated rules, loaded and validated.</summary>
public sealed class RuleSet
{
    private static readonly string[] ValidRatings = ["worksOutOfTheBox", "worksWithExtraDriver", "partial", "unsupported", "unknown"];
    private static readonly string[] ValidConfidence = ["high", "medium", "low"];
    private static readonly string[] ValidSecureBoot = ["none", "mokMayBeRequired", "mokRequired"];

    public IReadOnlyList<Rule> Rules { get; }

    public RuleSet(IReadOnlyList<Rule> rules, IReadOnlyCollection<string> distroIds)
    {
        Validate(rules, distroIds);
        Rules = rules;
    }

    public static RuleSet LoadDirectory(string directory, IReadOnlyCollection<string> distroIds)
    {
        var rules = new List<Rule>();
        foreach (var file in Directory.GetFiles(directory, "*.yaml").Order(StringComparer.Ordinal))
        {
            foreach (var r in Yaml.Load<RuleFile>(file).Rules)
            {
                r.File = Path.GetFileName(file);
                rules.Add(r);
            }
        }
        return new RuleSet(rules, distroIds);
    }

    /// <summary>The most specific rule that matches the component, or null.</summary>
    public RuleMatchResult? FindBest(HardwareComponent component, SystemInfo system)
    {
        RuleMatchResult? best = null;
        foreach (var rule in Rules)
        {
            var s = Specificity(rule.Match, component, system);
            if (s > 0 && (best is null || s > best.Specificity)) best = new RuleMatchResult(rule, s);
        }
        return best;
    }

    /// <summary>0 = no match; higher = more specific.</summary>
    public static int Specificity(RuleMatch m, HardwareComponent c, SystemInfo system)
    {
        var id = c.Id;
        var score = 0;

        if (m.Category is not null)
        {
            if (!string.Equals(m.Category, Camel(c.Category.ToString()), StringComparison.Ordinal)) return 0;
            score = Math.Max(score, 20);
        }

        if (m.Cpu is not null)
        {
            if (c.Category != ComponentCategory.Cpu || c.Cpu is null) return 0;
            if (m.Cpu.Architecture is not null && !string.Equals(m.Cpu.Architecture, Camel(system.Architecture.ToString()), StringComparison.Ordinal)) return 0;
            if (m.Cpu.Vendors.Count > 0 && !m.Cpu.Vendors.Contains(c.Cpu.Vendor, StringComparer.OrdinalIgnoreCase)) return 0;
            score = Math.Max(score, m.Cpu.Vendors.Count > 0 ? 60 : 30);
        }

        if (m.Bus is not null)
        {
            if (id is null || !string.Equals(m.Bus, Camel(id.Bus.ToString()), StringComparison.Ordinal)) return 0;
            score = Math.Max(score, 20);
        }

        if (m.ClassCode is not null)
        {
            if (id?.ClassCode is null || !id.ClassCode.StartsWith(m.ClassCode, StringComparison.OrdinalIgnoreCase)) return 0;
            score = Math.Max(score, 20);
        }

        if (m.Vendor is not null)
        {
            if (id is null || !string.Equals(m.Vendor, $"{Camel(id.Bus.ToString()).ToLowerInvariant()}:{id.Vendor}", StringComparison.OrdinalIgnoreCase)) return 0;
            score = Math.Max(score, m.ClassCode is null ? 50 : 60);
        }

        if (m.Ids.Count > 0)
        {
            if (id is null) return 0;
            var full = m.Ids.Any(k => string.Equals(k, id.FullKey, StringComparison.OrdinalIgnoreCase));
            var plain = m.Ids.Any(k => string.Equals(k, id.Key, StringComparison.OrdinalIgnoreCase));
            if (!full && !plain) return 0;
            score = Math.Max(score, full && id.HasSubsystem ? 100 : 90);
        }

        if (id is not null && m.ExcludeIds.Any(k => string.Equals(k, id.FullKey, StringComparison.OrdinalIgnoreCase) || string.Equals(k, id.Key, StringComparison.OrdinalIgnoreCase)))
            return 0;

        return score;
    }

    public static Rating ParseRating(string s) => s switch
    {
        "worksOutOfTheBox" => Rating.WorksOutOfTheBox,
        "worksWithExtraDriver" => Rating.WorksWithExtraDriver,
        "partial" => Rating.Partial,
        "unsupported" => Rating.Unsupported,
        _ => Rating.Unknown,
    };

    public static Confidence ParseConfidence(string s) => s switch
    {
        "high" => Confidence.High,
        "medium" => Confidence.Medium,
        _ => Confidence.Low,
    };

    public static SecureBootImpact ParseSecureBoot(string s) => s switch
    {
        "mokRequired" => SecureBootImpact.MokRequired,
        "mokMayBeRequired" => SecureBootImpact.MokMayBeRequired,
        _ => SecureBootImpact.None,
    };

    private static string Camel(string s) => s.Length == 0 ? s : char.ToLowerInvariant(s[0]) + s[1..];

    private static void Validate(IReadOnlyList<Rule> rules, IReadOnlyCollection<string> distroIds)
    {
        var categories = Enum.GetNames<ComponentCategory>().Select(Camel).ToHashSet();
        var buses = Enum.GetNames<DeviceBus>().Select(Camel).ToHashSet();
        var errors = new List<string>();
        var seen = new HashSet<string>();

        foreach (var r in rules)
        {
            var where = $"{r.File}: rule '{r.Id}'";
            if (string.IsNullOrWhiteSpace(r.Id)) errors.Add($"{r.File}: a rule has no id.");
            else if (!seen.Add(r.Id)) errors.Add($"{where}: duplicate id.");
            var m = r.Match;
            if (m.Ids.Count == 0 && m.Vendor is null && m.ClassCode is null && m.Category is null && m.Bus is null && m.Cpu is null)
                errors.Add($"{where}: 'match' is empty, so it would match every device.");
            if (m.Category is not null && !categories.Contains(m.Category)) errors.Add($"{where}: unknown category '{m.Category}'.");
            if (m.Bus is not null && !buses.Contains(m.Bus)) errors.Add($"{where}: unknown bus '{m.Bus}'.");
            foreach (var k in m.Ids.Concat(m.ExcludeIds))
                if (k != k.ToLowerInvariant() || k.Count(ch => ch == ':') is not (2 or 4))
                    errors.Add($"{where}: id '{k}' must look like 'pci:8086:2723' or 'pci:8086:2723:8086:0084' (lower case).");
            if (!ValidConfidence.Contains(r.Confidence)) errors.Add($"{where}: confidence must be high, medium or low.");
            if (r.Sources.Count == 0) errors.Add($"{where}: every rule needs at least one source.");
            if (r.Ratings.Count == 0) errors.Add($"{where}: no ratings.");
            if (r.Ratings.Count > 0 && r.Ratings[^1].When is not null)
                errors.Add($"{where}: the last rating must have no 'when', so every distro gets a rating.");
            foreach (var o in r.Ratings)
            {
                if (!ValidRatings.Contains(o.Rating)) errors.Add($"{where}: unknown rating '{o.Rating}'.");
                if (string.IsNullOrWhiteSpace(o.Explanation)) errors.Add($"{where}: every rating needs an explanation.");
                if (o.Confidence is not null && !ValidConfidence.Contains(o.Confidence)) errors.Add($"{where}: unknown confidence '{o.Confidence}'.");
                foreach (var d in o.When?.Distros ?? [])
                    if (!distroIds.Contains(d)) errors.Add($"{where}: unknown distro '{d}' in 'when'.");
                var fix = o.Fix ?? r.Fix;
                if (o.Rating is "worksWithExtraDriver" && fix is null)
                    errors.Add($"{where}: a 'worksWithExtraDriver' rating needs a fix.");
            }
            foreach (var fix in new[] { r.Fix }.Concat(r.Ratings.Select(o => o.Fix)).OfType<RuleFix>())
            {
                if (!ValidSecureBoot.Contains(fix.SecureBoot)) errors.Add($"{where}: fix secureBoot must be none, mokMayBeRequired or mokRequired.");
                foreach (var key in fix.Commands.Keys.Concat(fix.Steps.Keys))
                    if (key != "default" && !distroIds.Contains(key)) errors.Add($"{where}: fix uses unknown distro '{key}'.");
            }
        }
        if (errors.Count > 0) throw new InvalidDataException("Invalid rules:\n  " + string.Join("\n  ", errors));
    }
}
