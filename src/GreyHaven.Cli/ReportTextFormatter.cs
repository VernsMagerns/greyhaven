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
        sb.AppendLine($"Hardware data built {r.Data.BuiltOn:yyyy-MM-dd} ({(r.Data.AgeDays == 1 ? "1 day" : $"{r.Data.AgeDays} days")} old){(r.Data.ReleasesChecked ? ", checked for newer distro releases" : "")}");
        if (r.Data.Notice is not null) Wrap(sb, "UPDATE NOTICE: " + r.Data.Notice, "");
        if (p.Description is not null) Wrap(sb, p.Description, "");
        sb.AppendLine();

        if (r.ContextNotes.Count > 0)
        {
            sb.AppendLine("ABOUT YOUR WINDOWS");
            foreach (var n in r.ContextNotes)
            {
                Wrap(sb, n.Text, "  ");
                Wrap(sb, $"Source: {n.Source.Title}{(n.Source.Url is null ? "" : $" <{n.Source.Url}>")}", "  ");
            }
            sb.AppendLine();
        }

        sb.AppendLine("RECOMMENDATION");
        Wrap(sb, r.Recommendation.Paragraph, "  ");
        if (r.Recommendation.Paths.Count > 1)
        {
            foreach (var path in r.Recommendation.Paths)
            {
                sb.AppendLine();
                var chosen = path.Kind == r.SelectedPath;
                sb.AppendLine($"  {(chosen ? "(*)" : "( )")} {path.Title}{(path.Kind == r.Recommendation.DefaultPath ? " - default" : "")}{(chosen ? " - selected" : "")}   [download: {path.EditionName}]");
                Wrap(sb, path.Paragraph, "      ");
                if (path.SecureBootNotice is not null) Wrap(sb, "Secure Boot: " + path.SecureBootNotice, "      ");
                if (path.SecureBootGuide is { } g) Wrap(sb, "How-to: " + GuideText(g), "      ");
            }
            sb.AppendLine("  Choose with --path easy|advanced.");
        }
        else if (r.SelectedRecommendation is { } only)
        {
            if (only.SecureBootNotice is not null) Wrap(sb, "Secure Boot: " + only.SecureBootNotice, "  ");
            if (only.SecureBootGuide is { } g) Wrap(sb, "How-to: " + GuideText(g), "  ");
            sb.AppendLine($"  Download: {only.EditionName}");
        }
        sb.AppendLine();

        sb.AppendLine("SCORES");
        foreach (var d in r.Distros)
        {
            var s = d.Score;
            var flag = d.DistroId == r.SelectedDistroId ? "*" : " ";
            sb.AppendLine($" {flag}{d.DisplayName,-30} {s.Score,3}/100  {s.Band,-5}  coverage {s.Coverage:P0}{(s.Incomplete ? "  INCOMPLETE" : "")}   [{d.Edition}, kernel {d.KernelVersion}]");
            if (s.CapReason is not null) Wrap(sb, s.CapReason, "      ");
            foreach (var n in d.Notes) Wrap(sb, "Note: " + n, "      ");
        }
        sb.AppendLine();

        var distros = r.Distros.Select(d => d.DistroId).ToList();
        var detailDistro = onlyDistro ?? r.SelectedDistroId ?? distros[0];
        if (!distros.Contains(detailDistro))
            throw new CliException($"Unknown distro '{detailDistro}'. Choose one of: {string.Join(", ", distros)}.");

        // Ratings first, then the full name, so long names never need truncating.
        sb.AppendLine("COMPONENTS");
        sb.AppendLine($"  {string.Join("", distros.Select(d => $"{Short(d),-10}"))}Part");
        foreach (var group in r.Components.OrderBy(c => c.Component.Category).GroupBy(c => c.Importance).OrderBy(g => g.Key))
        {
            sb.AppendLine($"  {Labels.Of(group.Key)} parts");
            foreach (var c in group)
                sb.AppendLine($"  {string.Join("", distros.Select(d => $"{Symbol(c.For(d).Rating),-10}"))}{Labels.Of(c.Component.Category)}: {c.DisplayName}");
        }
        Wrap(sb, $"Key: {string.Join("  ", Enum.GetValues<Rating>().Select(x => $"{Symbol(x)} = {Describe(x)}"))}", "  ");
        foreach (var i in Enum.GetValues<Importance>()) Wrap(sb, Labels.Explain(i), "       ");
        sb.AppendLine();

        sb.AppendLine($"DETAILS FOR {r.Distros.First(d => d.DistroId == detailDistro).DisplayName.ToUpperInvariant()}{(onlyDistro is null ? " (anything not working out of the box; use --distro for others)" : "")}");
        foreach (var c in r.Components.OrderBy(c => c.Importance).ThenBy(c => c.Component.Category))
        {
            var rating = c.For(detailDistro);
            if (onlyDistro is null && rating.Rating == Rating.WorksOutOfTheBox) continue;
            sb.AppendLine();
            sb.AppendLine($"  {Labels.Of(c.Component.Category)}: {c.DisplayName}  ({c.Component.Id?.FullKey ?? "cpu"}, {Labels.Of(c.Importance).ToLowerInvariant()} part)");
            sb.AppendLine($"    {Symbol(rating.Rating)} {Describe(rating.Rating)}  - confidence {rating.Confidence.ToString().ToLowerInvariant()}{(rating.IsWeakData ? " (limited data)" : "")}");
            Wrap(sb, rating.Explanation, "    ");
            if (rating.ApproximationNote is not null) Wrap(sb, "Approximate data: " + rating.ApproximationNote, "    ");
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
            foreach (var n in p.Warnings.Concat(r.DataNotes)) Wrap(sb, "- " + n, "  ");
        }
        return sb.ToString().TrimEnd();
    }

    private static string GuideText(GuideLink g)
    {
        var where = (g.Url, g.LocalPath) switch
        {
            ({ } url, { } local) => $"<{url}> (also in {local} next to GreyHaven)",
            ({ } url, null) => $"<{url}>",
            (null, { } local) => $"{local} (next to GreyHaven)",
            _ => "",
        };
        var basis = g.MatchedBy == "motherboard" ? " - chosen from your motherboard's maker" : "";
        return $"{g.Title}{basis} {where}".TrimEnd();
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
