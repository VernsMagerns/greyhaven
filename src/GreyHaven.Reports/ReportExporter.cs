using GreyHaven.Core;
using GreyHaven.Core.Compatibility;
using GreyHaven.Core.Serialization;

namespace GreyHaven.Reports;

public enum ReportFormat { Markdown, Html, Json }

/// <summary>Saves a report as Markdown, a self-contained HTML page or JSON. Used by the CLI and the app.</summary>
public static class ReportExporter
{
    /// <summary>Version of the JSON export's layout. Bump when a field is renamed or removed.</summary>
    public const int JsonFormatVersion = 1;

    /// <param name="distroId">Details for this distro instead of the chosen path's (Markdown and HTML).</param>
    public static string Render(CompatibilityReport report, ReportFormat format, string? distroId = null) => format switch
    {
        ReportFormat.Markdown => MarkdownReportWriter.Write(ReportBuilder.Build(report, distroId)),
        ReportFormat.Html => HtmlReportWriter.Write(ReportBuilder.Build(report, distroId)),
        ReportFormat.Json => GreyHavenJson.Serialize(new JsonReport(
            "greyhaven-report", JsonFormatVersion, $"{GreyHavenInfo.ProductName} {GreyHavenInfo.Version}", report)),
        _ => throw new ArgumentOutOfRangeException(nameof(format)),
    };

    /// <param name="includeApps">Saved reports leave out the person's app list unless they tick "Include my apps".</param>
    public static async Task SaveAsync(CompatibilityReport report, string path, ReportFormat? format = null, string? distroId = null, bool includeApps = false, CancellationToken cancellationToken = default)
    {
        var f = format ?? FormatFromPath(path) ?? throw new ArgumentException($"Can't tell the format of '{path}'. Use a name ending in .md, .html or .json.", nameof(path));
        if (!includeApps) report = WithoutApps(report);
        await File.WriteAllTextAsync(path, Render(report, f, distroId), new System.Text.UTF8Encoding(false), cancellationToken).ConfigureAwait(false);
    }

    /// <summary>The report with the app list removed (from the ratings and the scan).</summary>
    public static CompatibilityReport WithoutApps(CompatibilityReport report) =>
        report with { Apps = [], MustHaveApps = [], Profile = report.Profile with { Apps = [] } };

    public static ReportFormat? FormatFromPath(string path) => Path.GetExtension(path).ToLowerInvariant() switch
    {
        ".md" or ".markdown" => ReportFormat.Markdown,
        ".html" or ".htm" => ReportFormat.Html,
        ".json" => ReportFormat.Json,
        _ => null,
    };

    public static string Extension(ReportFormat f) => f switch
    {
        ReportFormat.Markdown => ".md",
        ReportFormat.Html => ".html",
        _ => ".json",
    };

    /// <summary>e.g. "greyhaven-report-2026-10-05.html" (the report's own, local date).</summary>
    public static string SuggestedFileName(CompatibilityReport report, ReportFormat format) =>
        $"greyhaven-report-{report.GeneratedAt:yyyy-MM-dd}{Extension(format)}";

    /// <summary>The JSON export: a small envelope around the full report.</summary>
    public sealed record JsonReport(string Format, int FormatVersion, string Generator, CompatibilityReport Report);
}
