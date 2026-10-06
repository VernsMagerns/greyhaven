using System.Net;
using System.Text;
using GreyHaven.Core.Hardware;

namespace GreyHaven.Reports;

/// <summary>
/// Renders a <see cref="ReportDocument"/> as one self-contained HTML file: no scripts, no
/// external files, light and dark themes, print-friendly, with real headings, captioned tables
/// and checkboxes for the live-USB checks.
/// </summary>
public static class HtmlReportWriter
{
    /// <summary>
    /// The app's colours (GreyHaven.Presentation's Palette, which tests hold to WCAG AA), so
    /// exports look like the app. A test keeps these in step with the palette.
    /// </summary>
    public static readonly IReadOnlyDictionary<string, (string Light, string Dark)> Colours = new Dictionary<string, (string, string)>
    {
        ["page"] = ("#FFFFFF", "#2B2B2B"),
        ["text"] = ("#1A1A1A", "#FFFFFF"),
        ["text-secondary"] = ("#595959", "#C8C8C8"),
        ["border"] = ("#8A8A8A", "#8A8A8A"),
        ["accent"] = ("#005FB8", "#60CDFF"),
        ["works"] = ("#0E6E0E", "#6CCB5F"),
        ["extra"] = ("#005FB8", "#60CDFF"),
        ["partial"] = ("#8A5000", "#FCE100"),
        ["unsupported"] = ("#B3261E", "#FF99A4"),
        ["unknown"] = ("#595959", "#C8C8C8"),
        ["notice-info"] = ("#E6F0FA", "#1F3347"),
        ["notice-warning"] = ("#FFF4CE", "#433519"),
        ["command"] = ("#F0F0F0", "#1C1C1C"),
    };

    public static string Write(ReportDocument doc)
    {
        var sb = new StringBuilder();
        sb.AppendLine("<!DOCTYPE html>");
        sb.AppendLine("<html lang=\"en\">");
        sb.AppendLine("<head>");
        sb.AppendLine("<meta charset=\"utf-8\">");
        sb.AppendLine("<meta name=\"viewport\" content=\"width=device-width, initial-scale=1\">");
        sb.AppendLine("<meta name=\"color-scheme\" content=\"light dark\">");
        sb.AppendLine($"<title>{E(doc.Title)}</title>");
        sb.AppendLine("<style>").Append(Css()).AppendLine("</style>");
        sb.AppendLine("</head>");
        sb.AppendLine("<body>");
        sb.AppendLine("<main>");
        foreach (var block in doc.Blocks) WriteBlock(sb, block);
        sb.AppendLine("</main>");
        sb.AppendLine("</body>");
        sb.AppendLine("</html>");
        return sb.ToString();
    }

    private static void WriteBlock(StringBuilder sb, Block block)
    {
        switch (block)
        {
            case Heading h:
                sb.AppendLine($"<h{h.Level}>{E(h.Text)}</h{h.Level}>");
                break;
            case Paragraph p:
                sb.AppendLine($"<p>{Inlines(p.Content)}</p>");
                break;
            case Callout c:
                sb.AppendLine($"<aside class=\"callout {(c.Kind == CalloutKind.Warning ? "warning" : "info")}\"><p><strong>{E(c.Title)}:</strong> {Inlines(c.Content)}</p></aside>");
                break;
            case ListBlock l when l.Kind == ListKind.Checkboxes:
                sb.AppendLine("<ul class=\"checks\">");
                foreach (var item in l.Items)
                    sb.AppendLine($"<li><label><input type=\"checkbox\"> <span>{Inlines(item)}</span></label></li>");
                sb.AppendLine("</ul>");
                break;
            case ListBlock l:
                var tag = l.Kind == ListKind.Numbered ? "ol" : "ul";
                sb.AppendLine($"<{tag}>");
                foreach (var item in l.Items) sb.AppendLine($"<li>{Inlines(item)}</li>");
                sb.AppendLine($"</{tag}>");
                break;
            case CommandBlock cmd:
                sb.AppendLine($"<pre class=\"command\"><code>{E(string.Join("\n", cmd.Lines))}</code></pre>");
                break;
            case Table t:
                sb.AppendLine("<div class=\"table-wrap\"><table>");
                sb.AppendLine($"<caption>{E(t.Caption)}</caption>");
                sb.Append("<thead><tr>");
                foreach (var h in t.Headers) sb.Append($"<th scope=\"col\">{E(h)}</th>");
                sb.AppendLine("</tr></thead>");
                sb.AppendLine("<tbody>");
                foreach (var row in t.Rows)
                {
                    sb.Append("<tr>");
                    for (var i = 0; i < row.Count; i++)
                        sb.Append(i == 0 ? $"<th scope=\"row\">{Inlines(row[i])}</th>" : $"<td>{Inlines(row[i])}</td>");
                    sb.AppendLine("</tr>");
                }
                sb.AppendLine("</tbody></table></div>");
                break;
            case Details d:
                sb.AppendLine($"<details><summary>{E(d.Summary)}</summary>");
                foreach (var inner in d.Content) WriteBlock(sb, inner);
                sb.AppendLine("</details>");
                break;
        }
    }

    private static string Inlines(IReadOnlyList<Inline> content) => string.Concat(content.Select(Inline));

    private static string Inline(Inline i) => i switch
    {
        Text t => E(t.Value),
        Strong s => $"<strong>{E(s.Value)}</strong>",
        Link l when IsWebUrl(l.Url) => $"<a href=\"{E(l.Url)}\">{E(l.Label)}</a>",
        Link l => E(l.Label),
        // The symbol is decorative: the words carry the meaning for screen readers.
        RatingTag r => $"<span class=\"rating {RatingSymbols.CssClass(r.Rating)}\"><span aria-hidden=\"true\">{RatingSymbols.Of(r.Rating)}</span> {E(Labels.Of(r.Rating))}</span>",
        Code c => $"<code>{E(c.Value)}</code>",
        _ => "",
    };

    /// <summary>Only http(s) links are ever made clickable.</summary>
    public static bool IsWebUrl(string url) =>
        Uri.TryCreate(url, UriKind.Absolute, out var u) && (u.Scheme == Uri.UriSchemeHttps || u.Scheme == Uri.UriSchemeHttp);

    private static string E(string s) => WebUtility.HtmlEncode(s);

    private static string Css()
    {
        string Vars(Func<(string Light, string Dark), string> pick) =>
            string.Concat(Colours.Select(c => $"--{c.Key}:{pick(c.Value)};"));
        var light = ":root{" + Vars(c => c.Light) + "}";
        var dark = ":root{" + Vars(c => c.Dark) + "}";
        return
            light + "\n" +
            "@media (prefers-color-scheme: dark){" + dark + "}\n" +
            "@media print{" + light + " details{display:block} details>summary{list-style:none} a::after{content:\" (\" attr(href) \")\";font-size:.85em;word-break:break-all}}\n" +
            """
            *{box-sizing:border-box}
            body{margin:0;background:var(--page);color:var(--text);font:16px/1.55 "Segoe UI",system-ui,-apple-system,sans-serif}
            main{max-width:52rem;margin:0 auto;padding:1.5rem 1rem 3rem}
            h1{font-size:2rem;margin:.5rem 0}
            h2{font-size:1.5rem;margin:2.2rem 0 .6rem;padding-top:.6rem;border-top:1px solid var(--border)}
            h3{font-size:1.2rem;margin:1.6rem 0 .4rem}
            h4{font-size:1.05rem;margin:1.4rem 0 .3rem}
            a{color:var(--accent)}
            a:focus-visible,summary:focus-visible,input:focus-visible{outline:2px solid var(--accent);outline-offset:2px}
            .callout{border:1px solid var(--border);border-radius:6px;padding:.1rem 1rem;margin:1rem 0}
            .callout.info{background:var(--notice-info)}
            .callout.warning{background:var(--notice-warning)}
            .rating{font-weight:600;white-space:nowrap}
            .rating.works{color:var(--works)}.rating.extra{color:var(--extra)}.rating.partial{color:var(--partial)}
            .rating.unsupported{color:var(--unsupported)}.rating.unknown{color:var(--unknown)}
            .table-wrap{overflow-x:auto}
            table{border-collapse:collapse;width:100%;margin:.5rem 0 1rem}
            caption{text-align:left;color:var(--text-secondary);padding:.3rem 0}
            th,td{border:1px solid var(--border);padding:.4rem .6rem;text-align:left;vertical-align:top}
            thead th{font-weight:600}
            tbody th{font-weight:400}
            pre.command{background:var(--command);border:1px solid var(--border);border-radius:6px;padding:.7rem 1rem;overflow-x:auto}
            code{font-family:Consolas,"Cascadia Mono",ui-monospace,monospace}
            ul.checks{list-style:none;padding-left:0}
            ul.checks li{margin:.5rem 0}
            ul.checks label{display:flex;gap:.6rem;align-items:flex-start}
            ul.checks input{width:1.1rem;height:1.1rem;margin-top:.25rem;flex:none}
            details{margin:.5rem 0 1rem}
            summary{cursor:pointer;color:var(--accent)}
            @media (forced-colors: active){.rating,.callout{forced-color-adjust:auto}}

            """;
    }
}
