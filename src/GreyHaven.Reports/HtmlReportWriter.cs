using System.Net;
using System.Reflection;
using System.Text;
using GreyHaven.Core.Hardware;

namespace GreyHaven.Reports;

/// <summary>
/// Renders a <see cref="ReportDocument"/> as one self-contained HTML file in the "Market Day"
/// look: no scripts, no external files (the brand fonts are embedded), light and dark themes,
/// Windows contrast themes, print-friendly, with real headings, captioned tables, collapsible
/// details and checkboxes for the steps.
/// </summary>
public static class HtmlReportWriter
{
    /// <summary>
    /// The app's colours (GreyHaven.Presentation's Palette, "Market Day", which tests hold to
    /// WCAG AA), so exports look like the app. A test keeps these in step with the palette.
    /// "page" is the card colour (the reading surface); "paper" is the app's page background.
    /// </summary>
    public static readonly IReadOnlyDictionary<string, (string Light, string Dark)> Colours = new Dictionary<string, (string, string)>
    {
        ["page"] = ("#FFFFFF", "#18212B"),
        ["paper"] = ("#F3EDE0", "#0D1218"),
        ["text"] = ("#1F1B16", "#F3EFE7"),
        ["text-secondary"] = ("#52473B", "#B8C3CE"),
        ["border"] = ("#1F1B16", "#8C9BAB"),
        ["accent"] = ("#B2361A", "#FF9065"),
        ["on-accent"] = ("#FFFFFF", "#0D1218"),
        ["works"] = ("#2B6630", "#8FD69B"),
        ["extra"] = ("#0D5986", "#8DCBF5"),
        ["partial"] = ("#845000", "#FFCF5C"),
        ["unsupported"] = ("#9E1C20", "#FF9F95"),
        ["unknown"] = ("#52473B", "#B8C3CE"),
        ["notice-info"] = ("#E1EDD8", "#1B2B2A"),
        ["notice-warning"] = ("#FFE08A", "#2D2616"),
        ["command"] = ("#EEE6D5", "#111820"),
        ["shadow"] = ("#1F1B16", "#05080B"),
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
        sb.AppendLine("<style>").Append(Fonts.Value).Append(Css()).AppendLine("</style>");
        sb.AppendLine("</head>");
        sb.AppendLine("<body>");
        sb.AppendLine("<main>");
        var state = new State();
        foreach (var block in doc.Blocks) WriteBlock(sb, block, state);
        sb.AppendLine("</main>");
        sb.AppendLine("</body>");
        sb.AppendLine("</html>");
        return sb.ToString();
    }

    /// <summary>Per-document counters, for unique picture ids.</summary>
    private sealed class State { public int Pictures; }

    private static void WriteBlock(StringBuilder sb, Block block, State state)
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
                var kind = c.Kind.ToString().ToLowerInvariant();
                sb.AppendLine($"<aside class=\"callout {kind}\"><p><strong>{E(c.Title)}</strong></p><p>{Inlines(c.Content)}</p></aside>");
                break;
            case ScoreHeadline h:
                sb.AppendLine("<section class=\"headline\"><div>");
                sb.AppendLine($"<h2>{E(h.Title)}</h2>");
                if (h.Suggestion is not null) sb.AppendLine($"<p class=\"suggest\">{E(h.Suggestion)}</p>");
                sb.AppendLine($"<p>{E(h.Summary)}</p></div>");
                sb.AppendLine($"<div class=\"score\" role=\"img\" aria-label=\"Score: {h.Score} out of 100\"><span class=\"num\">{h.Score}</span><span>out of 100</span></div></section>");
                break;
            case KeyFact k:
                sb.AppendLine($"<div class=\"keyfact\"><p>{E(k.Label)}</p><p class=\"key\">{E(k.Value)}</p></div>");
                break;
            case PictureGuide g:
                sb.AppendLine("<ol class=\"pictures\">");
                foreach (var s in g.Steps)
                {
                    // The SVG carries its own alt text (role="img" + aria-label). Under forced colours
                    // it's hidden and the caption remains. Pictures enlarge without scripts: the link
                    // targets the list item (CSS :target), and Close (or Back, or a tap outside)
                    // goes back to the list.
                    string picture;
                    var id = $"pic-{++state.Pictures}";
                    if (Illustrations.Svg(s.PictureId) is { } svg)
                    {
                        var alt = E(Illustrations.Alt(s.PictureId!) ?? s.PictureCaption);
                        picture = $"<a class=\"zoom\" href=\"#{id}\" aria-label=\"Enlarge picture: {alt}\"><div class=\"pic\">{svg}</div></a>" +
                                  $"<a class=\"unzoom backdrop\" href=\"#{id}-back\" tabindex=\"-1\" aria-hidden=\"true\"></a>" +
                                  $"<a class=\"unzoom close\" href=\"#{id}-back\">Close picture</a>";
                    }
                    else
                        picture = $"<div class=\"frame\" role=\"img\" aria-label=\"Picture coming soon: {E(s.PictureCaption)}\"><span>Picture coming soon</span></div>";
                    sb.AppendLine($"<li id=\"{id}\"><span id=\"{id}-back\" class=\"anchor\"></span><figure>{picture}<figcaption>{E(s.PictureCaption)}</figcaption></figure><p>{E(s.Text)}</p></li>");
                }
                sb.AppendLine("</ol>");
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
                sb.AppendLine($"<details><summary>{E(d.Summary)}</summary><div class=\"more\">");
                foreach (var inner in d.Content) WriteBlock(sb, inner, state);
                sb.AppendLine("</div></details>");
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
        RatingTag r => $"<span class=\"rating {RatingSymbols.CssClass(r.Rating)}\"><span aria-hidden=\"true\">{RatingSymbols.Of(r.Rating)}</span> {E(PlainWords.Rating(r.Rating))}</span>",
        Code c => $"<code>{E(c.Value)}</code>",
        _ => "",
    };

    /// <summary>Only http(s) links are ever made clickable.</summary>
    public static bool IsWebUrl(string url) =>
        Uri.TryCreate(url, UriKind.Absolute, out var u) && (u.Scheme == Uri.UriSchemeHttps || u.Scheme == Uri.UriSchemeHttp);

    private static string E(string s) => WebUtility.HtmlEncode(s);

    /// <summary>The brand fonts (SIL OFL 1.1), embedded so the file stays self-contained and works offline.</summary>
    private static readonly Lazy<string> Fonts = new(() =>
    {
        var asm = typeof(HtmlReportWriter).Assembly;
        string Face(string family, string file, int weight)
        {
            using var stream = asm.GetManifestResourceStream($"Fonts.{file}");
            if (stream is null) return "";
            using var ms = new MemoryStream();
            stream.CopyTo(ms);
            return $"@font-face{{font-family:\"{family}\";font-weight:{weight};font-display:swap;src:url(data:font/ttf;base64,{Convert.ToBase64String(ms.ToArray())}) format(\"truetype\")}}\n";
        }
        return Face("Rubik", "Rubik-Bold.ttf", 700) + Face("Lexend", "Lexend-Regular.ttf", 400) + Face("Lexend", "Lexend-SemiBold.ttf", 600);
    });

    private static string Css()
    {
        string Vars(Func<(string Light, string Dark), string> pick) => string.Concat(Colours.Select(c => $"--{c.Key}:{pick(c.Value)};"));
        var light = ":root{" + Vars(c => c.Light) + "}";
        var dark = ":root{" + Vars(c => c.Dark) + "}";
        return
            light + "\n" +
            "@media (prefers-color-scheme: dark){" + dark + "}\n" +
            "@media print{" + light + " details{display:block} details>summary{list-style:none} .more{border:0;padding:0} a::after{content:\" (\" attr(href) \")\";font-size:.85em;word-break:break-all} .card,section.headline,details,.callout{box-shadow:none} h3{break-after:avoid}}\n" +
            """
            *{box-sizing:border-box}
            body{margin:0;background:var(--paper);color:var(--text);font:16px/1.6 Lexend,"Segoe UI",system-ui,sans-serif}
            main{max-width:52rem;margin:1.5rem auto 3rem;padding:1.5rem 1.25rem;background:var(--page);border:2.5px solid var(--border);border-radius:12px;box-shadow:4px 4px 0 var(--shadow)}
            h1,h2,h3,h4{font-family:Rubik,"Segoe UI",system-ui,sans-serif;font-weight:700;line-height:1.25}
            h1{font-size:2rem;margin:.25rem 0 .5rem}
            h2{font-size:1.5rem;margin:2.2rem 0 .6rem}
            h3{font-size:1.2rem;margin:1.6rem 0 .4rem}
            h4{font-size:1.05rem;margin:1.2rem 0 .3rem}
            p{margin:.4rem 0}
            a{color:var(--accent)}
            a:focus-visible,summary:focus-visible,input:focus-visible{outline:3px solid var(--text);outline-offset:2px}
            section.headline{display:grid;grid-template-columns:1fr auto;gap:1rem;align-items:center;border:2.5px solid var(--border);border-radius:12px;padding:1rem 1.25rem;margin:1rem 0;box-shadow:4px 4px 0 var(--shadow)}
            section.headline h2{margin:0}
            .suggest{font-size:1.2rem}
            .score{display:grid;place-items:center;text-align:center;width:7rem;height:7rem;border-radius:50%;background:var(--accent);color:var(--on-accent);border:2.5px solid var(--border);font-size:.85rem;line-height:1.1}
            .score .num{font-family:Rubik,sans-serif;font-size:2.6rem;font-weight:700;align-self:end}
            .callout{border:2.5px solid var(--border);border-radius:12px;padding:.4rem 1rem;margin:1rem 0;box-shadow:4px 4px 0 var(--shadow)}
            .callout.info,.callout.tip{background:var(--notice-info)}
            .callout.warning{background:var(--notice-warning)}
            .callout.danger{background:var(--notice-warning);border-width:4px}
            .rating{font-weight:600;white-space:nowrap}
            .rating.works{color:var(--works)}.rating.extra{color:var(--extra)}.rating.partial{color:var(--partial)}
            .rating.unsupported{color:var(--unsupported)}.rating.unknown{color:var(--unknown)}
            .keyfact{text-align:center;border:2.5px solid var(--border);border-radius:12px;padding:.5rem;margin:1rem 0}
            .keyfact .key{font-family:Rubik,sans-serif;font-size:2.4rem;font-weight:700;margin:0}
            ol.pictures{display:grid;grid-template-columns:repeat(auto-fill,minmax(13rem,1fr));gap:1rem;padding:0;list-style:none;counter-reset:pic}
            ol.pictures li{counter-increment:pic}
            ol.pictures li>p{font-weight:600}
            ol.pictures li>p::before{content:counter(pic) ". ";font-family:Rubik,sans-serif}
            ol.pictures figure{margin:0}
            .pic svg{display:block;width:100%;height:auto;border-radius:12px;border:2px solid var(--border)}
            a.zoom{display:block;border-radius:12px;cursor:zoom-in}
            a.zoom:focus-visible{outline:3px solid var(--text);outline-offset:3px}
            .unzoom{display:none}
            li:target{position:static}
            li:target a.zoom{position:fixed;z-index:3;inset:3.5rem 1rem 1rem;display:grid;place-items:center;cursor:default;pointer-events:none}
            li:target a.zoom .pic{width:min(100%,calc((100vh - 5rem) * 1.6))}
            li:target .backdrop{display:block;position:fixed;inset:0;z-index:2;background:rgba(0,0,0,.8);cursor:zoom-out}
            li:target .close{display:block;position:fixed;z-index:4;top:.75rem;right:1rem;padding:.5rem 1rem;min-height:2.75rem;background:var(--page);color:var(--text);border:2.5px solid var(--border);border-radius:10px;font-weight:600;text-decoration:none}
            li:target .close:focus-visible{outline:3px solid var(--accent);outline-offset:2px}
            .anchor{display:block;position:relative;top:-1rem}
            figcaption{font-size:.8rem;color:var(--text-secondary);margin-top:.25rem}
            .frame{display:grid;place-items:center;text-align:center;min-height:7.5rem;padding:.5rem;background:var(--command);border:2px dashed var(--border);border-radius:12px;color:var(--text-secondary);font-size:.85rem}
            .table-wrap{overflow-x:auto}
            table{border-collapse:collapse;width:100%;margin:.5rem 0 1rem}
            caption{text-align:left;color:var(--text-secondary);padding:.3rem 0}
            th,td{border:1px solid var(--border);padding:.4rem .6rem;text-align:left;vertical-align:top}
            thead th{font-weight:600}
            tbody th{font-weight:400}
            pre.command{background:var(--command);border:1px solid var(--border);border-radius:8px;padding:.7rem 1rem;overflow-x:auto}
            code{font-family:Consolas,"Cascadia Mono",ui-monospace,monospace}
            ul.checks{list-style:none;padding-left:0}
            ul.checks li{margin:.5rem 0}
            ul.checks label{display:flex;gap:.6rem;align-items:flex-start}
            ul.checks input{width:1.2rem;height:1.2rem;margin-top:.3rem;flex:none;accent-color:var(--accent)}
            details{margin:.6rem 0 1rem}
            summary{cursor:pointer;color:var(--accent);font-weight:600;text-decoration:underline;text-underline-offset:.2em;padding:.35rem .25rem;min-height:2rem;border-radius:6px;width:fit-content}
            summary:hover{background:var(--command)}
            .more{margin-top:.5rem;padding-left:1rem;border-left:3px solid var(--border)}
            @media (max-width:40rem){section.headline{grid-template-columns:1fr}main{margin:0;border-radius:0}}
            @media (forced-colors: active){.pic,a.zoom,.unzoom{display:none}summary{color:LinkText}.rating,.callout,.score{forced-color-adjust:auto}.score{border:2px solid CanvasText}main,section.headline,.callout{box-shadow:none}}

            """;
    }
}
