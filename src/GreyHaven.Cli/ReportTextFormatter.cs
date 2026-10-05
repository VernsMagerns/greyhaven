using System.Text;
using GreyHaven.Core.Compatibility;
using GreyHaven.Core.Hardware;

namespace GreyHaven.Cli;

/// <summary>Readable terminal version of a <see cref="CompatibilityReport"/>.</summary>
internal static class ReportTextFormatter
{
    private const int Width = 100;

    public static string Format(CompatibilityReport r, string? onlyDistro)
    {
        var sb = new StringBuilder();
        var p = r.Profile;
        sb.AppendLine($"GreyHaven report  ({p.Source})");
        if (p.Description is not null) Wrap(sb, p.Description, "");
        sb.AppendLine();

        sb.AppendLine("RECOMMENDATION");
        Wrap(sb, r.Recommendation.Paragraph, "  ");
        sb.AppendLine();

        sb.AppendLine("SCORES");
        foreach (var d in r.Distros)
        {
            var s = d.Score;
            var flag = d.DistroId == r.Recommendation.DistroId ? "*" : " ";
            sb.AppendLine($" {flag}{d.DisplayName,-30} {s.Score,3}/100  {s.Band,-5}  coverage {s.Coverage:P0}{(s.Incomplete ? "  INCOMPLETE" : "")}   [{d.Edition}, kernel {d.KernelVersion}]");
            if (s.CapReason is not null) Wrap(sb, s.CapReason, "      ");
            foreach (var n in d.Notes) Wrap(sb, "Note: " + n, "      ");
        }
        sb.AppendLine();

        var distros = r.Distros.Select(d => d.DistroId).ToList();
        var detailDistro = onlyDistro ?? r.Recommendation.DistroId ?? distros[0];
        if (!distros.Contains(detailDistro))
            throw new CliException($"Unknown distro '{detailDistro}'. Choose one of: {string.Join(", ", distros)}.");

        sb.AppendLine($"COMPONENTS{"",-38}{string.Join("", distros.Select(d => $"{Short(d),-10}"))}");
        foreach (var c in r.Components.OrderBy(c => c.Importance).ThenBy(c => c.Component.Category))
        {
            var name = $"{Labels.Of(c.Component.Category)}: {c.Component.Name}";
            if (name.Length > 46) name = name[..45] + "…";
            sb.AppendLine($"  {Tag(c.Importance)} {name,-46}{string.Join("", distros.Select(d => $"{Symbol(c.For(d).Rating),-10}"))}");
        }
        sb.AppendLine($"  Key: {string.Join("  ", Enum.GetValues<Rating>().Select(x => $"{Symbol(x)} = {Describe(x)}"))}");
        sb.AppendLine();

        sb.AppendLine($"DETAILS FOR {r.Distros.First(d => d.DistroId == detailDistro).DisplayName.ToUpperInvariant()}{(onlyDistro is null ? " (anything not working out of the box; use --distro for others)" : "")}");
        foreach (var c in r.Components.OrderBy(c => c.Importance).ThenBy(c => c.Component.Category))
        {
            var rating = c.For(detailDistro);
            if (onlyDistro is null && rating.Rating == Rating.WorksOutOfTheBox) continue;
            sb.AppendLine();
            sb.AppendLine($"  {Labels.Of(c.Component.Category)}: {c.Component.Name}  ({c.Component.Id?.FullKey ?? "cpu"})");
            sb.AppendLine($"    {Symbol(rating.Rating)} {Describe(rating.Rating)}  - confidence {rating.Confidence.ToString().ToLowerInvariant()}{(rating.IsWeakData ? " (limited data)" : "")}");
            Wrap(sb, rating.Explanation, "    ");
            if (rating.Fix is { } fix)
            {
                sb.AppendLine($"    Fix: {fix.Driver}");
                foreach (var step in fix.Steps) Wrap(sb, "- " + step, "      ");
                foreach (var cmd in fix.Commands) sb.AppendLine($"      $ {cmd}");
                if (fix.Note is not null) Wrap(sb, fix.Note, "      ");
                if (fix.SecureBootAdvice is not null) Wrap(sb, fix.SecureBootAdvice, "      ");
            }
            foreach (var src in rating.Sources) Wrap(sb, $"Source: {src.Title}{(src.Url is null ? "" : $" <{src.Url}>")}", "    ");
            if (c.CaveatIds.Count > 0) sb.AppendLine($"    Test with a live USB: {string.Join(", ", r.Caveats.Where(x => c.CaveatIds.Contains(x.Id)).Select(x => x.Title))}");
        }

        if (r.Caveats.Count > 0)
        {
            sb.AppendLine();
            sb.AppendLine("CAN'T BE JUDGED FROM WINDOWS - TEST WITH A LIVE USB");
            foreach (var cv in r.Caveats)
            {
                sb.AppendLine($"  {cv.Title}");
                Wrap(sb, cv.Why, "    ");
            }
        }
        if (r.DataNotes.Count > 0 || p.Warnings.Count > 0)
        {
            sb.AppendLine();
            sb.AppendLine("NOTES");
            foreach (var n in p.Warnings.Concat(r.DataNotes)) Wrap(sb, "! " + n, "  ");
        }
        return sb.ToString().TrimEnd();
    }

    internal static string Symbol(Rating r) => r switch
    {
        Rating.WorksOutOfTheBox => "[OK]",
        Rating.WorksWithExtraDriver => "[DRIVER]",
        Rating.Partial => "[PARTIAL]",
        Rating.Unsupported => "[NO]",
        _ => "[?]",
    };

    internal static string Describe(Rating r) => r switch
    {
        Rating.WorksOutOfTheBox => "Works out of the box",
        Rating.WorksWithExtraDriver => "Works with extra driver/config",
        Rating.Partial => "Partial/known issues",
        Rating.Unsupported => "Unsupported",
        _ => "Unknown",
    };

    private static string Tag(Importance i) => i switch
    {
        Importance.Critical => "!!",
        Importance.Important => "! ",
        _ => "  ",
    };

    private static string Short(string distroId) => distroId switch
    {
        "pop-os" => "Pop!_OS",
        "mint" => "Mint",
        "ubuntu" => "Ubuntu",
        _ => distroId,
    };

    private static void Wrap(StringBuilder sb, string text, string indent)
    {
        var line = new StringBuilder(indent);
        foreach (var word in text.Split(' ', StringSplitOptions.RemoveEmptyEntries))
        {
            if (line.Length + word.Length + 1 > Width && line.Length > indent.Length)
            {
                sb.AppendLine(line.ToString().TrimEnd());
                line.Clear().Append(indent).Append(indent.Length > 0 && text.StartsWith("- ") ? "  " : "");
            }
            line.Append(word).Append(' ');
        }
        if (line.Length > indent.Length) sb.AppendLine(line.ToString().TrimEnd());
    }
}
