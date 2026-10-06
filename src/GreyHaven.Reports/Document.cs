using GreyHaven.Core.Compatibility;

namespace GreyHaven.Reports;

// A small, format-neutral document. ReportBuilder fills it once; the Markdown and HTML writers
// render the same content, so the formats can't drift apart.

public abstract record Inline;
public sealed record Text(string Value) : Inline;
public sealed record Strong(string Value) : Inline;
/// <summary>Rendered as a link only for http(s) URLs; anything else is shown as text.</summary>
public sealed record Link(string Label, string Url) : Inline;
/// <summary>A rating as symbol plus words, never colour alone.</summary>
public sealed record RatingTag(Rating Rating) : Inline;
/// <summary>A terminal command inside running text.</summary>
public sealed record Code(string Value) : Inline;

public enum CalloutKind { Info, Warning }

public abstract record Block;
public sealed record Heading(int Level, string Text) : Block;
public sealed record Paragraph(IReadOnlyList<Inline> Content) : Block;
/// <summary>A boxed note, e.g. the Windows 10 notice or "check for an update".</summary>
public sealed record Callout(CalloutKind Kind, string Title, IReadOnlyList<Inline> Content) : Block;
public enum ListKind { Bullets, Numbered, Checkboxes }
public sealed record ListBlock(ListKind Kind, IReadOnlyList<IReadOnlyList<Inline>> Items) : Block;
/// <summary>Exact terminal commands, one per line.</summary>
public sealed record CommandBlock(IReadOnlyList<string> Lines) : Block;
/// <summary>The first column of each row is its row header.</summary>
public sealed record Table(string Caption, IReadOnlyList<string> Headers, IReadOnlyList<IReadOnlyList<IReadOnlyList<Inline>>> Rows) : Block;
/// <summary>Collapsed in HTML; a labelled section in Markdown.</summary>
public sealed record Details(string Summary, IReadOnlyList<Block> Content) : Block;

public sealed record ReportDocument(string Title, IReadOnlyList<Block> Blocks);

internal static class Doc
{
    public static Paragraph P(params Inline[] content) => new(content);
    public static Paragraph P(string text) => new([new Text(text)]);
    public static IReadOnlyList<Inline> Line(params Inline[] content) => content;
    public static IReadOnlyList<Inline> Line(string text) => [new Text(text)];
    public static IReadOnlyList<IReadOnlyList<Inline>> Cells(params IReadOnlyList<Inline>[] cells) => cells;
    public static Inline SourceLink(Source s) => s.Url is null ? new Text(s.Title) : new Link(s.Title, s.Url);
}

/// <summary>How ratings look in exports: a symbol and words (the app uses icons and the same words).</summary>
public static class RatingSymbols
{
    public static string Of(Rating r) => r switch
    {
        Rating.WorksOutOfTheBox => "✓",
        Rating.WorksWithExtraDriver => "⚙︎", // text-style gear, not an emoji
        Rating.Partial => "◐",
        Rating.Unsupported => "✗",
        _ => "?",
    };

    /// <summary>CSS class for the rating colour (see HtmlReportWriter).</summary>
    public static string CssClass(Rating r) => r switch
    {
        Rating.WorksOutOfTheBox => "works",
        Rating.WorksWithExtraDriver => "extra",
        Rating.Partial => "partial",
        Rating.Unsupported => "unsupported",
        _ => "unknown",
    };
}
