using System.Net;
using System.Text;
using GreyHaven.Core;
using GreyHaven.Core.Compatibility;

namespace GreyHaven.Reports;

/// <summary>
/// Writes the public phone guide as a static website (deployed to Cloudflare Pages by
/// .github/workflows/phone-guide.yml): one page per <see cref="PhoneGuidePage"/>, an index, and
/// security headers. Nothing personal is ever written here.
/// </summary>
public static class PhoneGuideSiteWriter
{
    public static IReadOnlyList<string> Write(string directory, IReadOnlyList<PhoneGuidePage> pages)
    {
        Directory.CreateDirectory(directory);
        var script = GuideFragment.Script();
        var written = new List<string>();
        foreach (var page in pages)
        {
            File.WriteAllText(Path.Combine(directory, page.FileName), HtmlReportWriter.Write(PublicGuideBuilder.Build(page), script), new UTF8Encoding(false));
            written.Add(page.FileName);
        }

        var index = new StringBuilder();
        index.AppendLine("<!DOCTYPE html><html lang=\"en\"><head><meta charset=\"utf-8\"><meta name=\"viewport\" content=\"width=device-width, initial-scale=1\">");
        index.AppendLine("<meta name=\"color-scheme\" content=\"light dark\"><title>GreyHaven phone guides</title>");
        index.AppendLine("<style>body{font:17px/1.6 system-ui,sans-serif;max-width:40rem;margin:2rem auto;padding:0 1rem}a{color:#B2361A}@media(prefers-color-scheme:dark){body{background:#0D1218;color:#F3EFE7}a{color:#FF9065}}</style></head><body>");
        index.AppendLine("<h1>GreyHaven phone guides</h1><p>Step-by-step help for trying and installing Linux. GreyHaven shows you a QR code that opens the right one for your PC.</p><ul>");
        foreach (var p in pages)
            index.AppendLine($"<li><a href=\"{WebUtility.HtmlEncode(p.FileName)}\">{WebUtility.HtmlEncode($"{p.DistroName}, {p.EditionName}, {(p.Install == InstallChoice.KeepWindows ? "keeping Windows" : "replacing Windows")}")}</a></li>");
        index.AppendLine("</ul></body></html>");
        File.WriteAllText(Path.Combine(directory, "index.html"), index.ToString(), new UTF8Encoding(false));
        written.Add("index.html");

        // Cloudflare Pages reads _headers. The CSP is also in each page's <meta>.
        File.WriteAllText(Path.Combine(directory, "_headers"),
            "/*\n" +
            $"  Content-Security-Policy: {HtmlReportWriter.ContentSecurityPolicy(script)}\n" +
            "  Referrer-Policy: no-referrer\n" +
            "  X-Content-Type-Options: nosniff\n" +
            "  Permissions-Policy: camera=(), microphone=(), geolocation=(), interest-cohort=()\n", new UTF8Encoding(false));
        written.Add("_headers");
        return written;
    }
}
