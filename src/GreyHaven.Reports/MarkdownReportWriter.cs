using System.Text;
using GreyHaven.Core.Hardware;

namespace GreyHaven.Reports;

/// <summary>Renders a <see cref="ReportDocument"/> as GitHub-flavoured Markdown.</summary>
public static class MarkdownReportWriter
{
    public static string Write(ReportDocument doc)
    {
        var sb = new StringBuilder();
        foreach (var block in doc.Blocks) WriteBlock(sb, block);
        return sb.ToString().TrimEnd() + "\n";
    }

    private static void WriteBlock(StringBuilder sb, Block block)
    {
        switch (block)
        {
            case Heading h:
                sb.Append('#', h.Level).Append(' ').AppendLine(Escape(h.Text)).AppendLine();
                break;
            case Paragraph p:
                sb.AppendLine(Inlines(p.Content)).AppendLine();
                break;
            case Callout c:
                var mark = c.Kind switch { CalloutKind.Warning or CalloutKind.Danger => "⚠\uFE0E ", _ => "" };
                sb.AppendLine($"> **{mark}{Escape(c.Title)}:** {Inlines(c.Content)}").AppendLine();
                break;
            case ScoreHeadline h:
                sb.AppendLine($"## {Escape(h.Title)}").AppendLine();
                sb.AppendLine(string.Join(" ", new[] { $"**{h.Score} out of 100.**", h.Suggestion is null ? null : Escape(h.Suggestion), Escape(h.Summary) }.Where(x => x is not null))).AppendLine();
                break;
            case KeyFact k:
                sb.AppendLine($"{Escape(k.Label)}: **{Escape(k.Value)}**").AppendLine();
                break;
            case PictureGuide g:
                for (var i = 0; i < g.Steps.Count; i++)
                    sb.AppendLine($"{i + 1}. {Escape(g.Steps[i].Text)} *(Picture: {Escape(g.Steps[i].PictureCaption)})*");
                sb.AppendLine();
                break;
            case ListBlock l:
                for (var i = 0; i < l.Items.Count; i++)
                {
                    var marker = l.Kind switch
                    {
                        ListKind.Numbered => $"{i + 1}.",
                        ListKind.Checkboxes => "- [ ]",
                        _ => "-",
                    };
                    sb.Append(marker).Append(' ').AppendLine(Inlines(l.Items[i]));
                }
                sb.AppendLine();
                break;
            case CommandBlock cmd:
                sb.AppendLine("```sh");
                foreach (var line in cmd.Lines) sb.AppendLine(line);
                sb.AppendLine("```").AppendLine();
                break;
            case Table t:
                sb.Append("| ").AppendJoin(" | ", t.Headers.Select(Escape)).AppendLine(" |");
                sb.Append('|').Append(string.Concat(t.Headers.Select(_ => "---|"))).AppendLine();
                foreach (var row in t.Rows)
                    sb.Append("| ").AppendJoin(" | ", row.Select(Inlines)).AppendLine(" |");
                sb.AppendLine();
                break;
            case Details d:
                sb.AppendLine($"**{Escape(d.Summary)}**").AppendLine();
                foreach (var inner in d.Content) WriteBlock(sb, inner);
                break;
            case OnlyIf o:
                foreach (var inner in o.Content) WriteBlock(sb, inner);
                break;
        }
    }

    private static string Inlines(IReadOnlyList<Inline> content) => string.Concat(content.Select(Inline));

    private static string Inline(Inline i) => i switch
    {
        Text t => Escape(t.Value),
        Strong s => $"**{Escape(s.Value.TrimEnd())}**{(s.Value.EndsWith(' ') ? " " : "")}",
        Link l when HtmlReportWriter.IsWebUrl(l.Url) => $"[{Escape(l.Label)}](<{l.Url}>)",
        Link l => Escape(l.Label),
        RatingTag r => $"{RatingSymbols.Of(r.Rating)} {PlainWords.Rating(r.Rating)}",
        Fill f => Escape(f.General),
        Code c => $"`{c.Value.Replace("`", "")}`",
        _ => "",
    };

    /// <summary>Escapes characters that Markdown would otherwise treat as formatting (device names come from Windows).</summary>
    internal static string Escape(string s)
    {
        var sb = new StringBuilder(s.Length);
        foreach (var ch in s)
        {
            if (ch is '\\' or '`' or '*' or '_' or '[' or ']' or '<' or '>' or '|' or '#') sb.Append('\\');
            sb.Append(ch is '\r' or '\n' ? ' ' : ch);
        }
        return sb.ToString();
    }
}
