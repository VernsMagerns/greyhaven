namespace GreyHaven.Presentation.Theming;

public enum ThemeChoice { System, Light, Dark }

/// <summary>An sRGB colour, "#RRGGBB".</summary>
public readonly record struct Rgb(byte R, byte G, byte B)
{
    public static Rgb Parse(string hex)
    {
        hex = hex.TrimStart('#');
        if (hex.Length != 6) throw new FormatException($"Colour must be #RRGGBB, got '{hex}'.");
        return new Rgb(Convert.ToByte(hex[..2], 16), Convert.ToByte(hex[2..4], 16), Convert.ToByte(hex[4..], 16));
    }

    public override string ToString() => $"#{R:X2}{G:X2}{B:X2}";

    /// <summary>WCAG 2.x relative luminance.</summary>
    public double Luminance
    {
        get
        {
            static double Channel(byte c)
            {
                var s = c / 255.0;
                return s <= 0.04045 ? s / 12.92 : Math.Pow((s + 0.055) / 1.055, 2.4);
            }
            return 0.2126 * Channel(R) + 0.7152 * Channel(G) + 0.0722 * Channel(B);
        }
    }

    /// <summary>WCAG contrast ratio between two colours (1 to 21).</summary>
    public static double Contrast(Rgb a, Rgb b)
    {
        var (hi, lo) = a.Luminance >= b.Luminance ? (a.Luminance, b.Luminance) : (b.Luminance, a.Luminance);
        return (hi + 0.05) / (lo + 0.05);
    }
}

/// <summary>
/// GreyHaven's own colours (on top of the Fluent theme's controls). Every text colour is used on
/// <see cref="Card"/>, <see cref="Page"/> and the notice backgrounds, and must reach WCAG AA
/// (4.5:1) on all of them in both themes; tests enforce this. Ratings are never shown by colour
/// alone: each has an icon and a text label too.
/// </summary>
public sealed record Palette(
    Rgb Page,
    Rgb Card,
    Rgb CardBorder,
    Rgb Text,
    Rgb TextSecondary,
    Rgb Accent,
    Rgb OnAccent,
    Rgb Works,
    Rgb ExtraStep,
    Rgb Partial,
    Rgb Unsupported,
    Rgb Unknown,
    Rgb NoticeInfo,
    Rgb NoticeWarning,
    Rgb CommandBackground,
    /// <summary>Hard "sticker" shadow under cards and buttons. Decorative; not used for text.</summary>
    Rgb Shadow)
{
    /// <summary>"Market Day" light (chosen 2026-10-06): paper cream, tomato accent, ink outlines. WCAG AA.</summary>
    public static readonly Palette Light = new(
        Page: Rgb.Parse("#F3EDE0"),
        Card: Rgb.Parse("#FFFFFF"),
        CardBorder: Rgb.Parse("#1F1B16"),
        Text: Rgb.Parse("#1F1B16"),
        TextSecondary: Rgb.Parse("#52473B"),
        Accent: Rgb.Parse("#B2361A"),
        OnAccent: Rgb.Parse("#FFFFFF"),
        Works: Rgb.Parse("#2B6630"),
        ExtraStep: Rgb.Parse("#0D5986"),
        Partial: Rgb.Parse("#845000"),
        Unsupported: Rgb.Parse("#9E1C20"),
        Unknown: Rgb.Parse("#52473B"),
        NoticeInfo: Rgb.Parse("#E1EDD8"),
        NoticeWarning: Rgb.Parse("#FFE08A"),
        CommandBackground: Rgb.Parse("#EEE6D5"),
        Shadow: Rgb.Parse("#1F1B16"));

    /// <summary>"Market Day" dark: built up from a near-black slate (Page is the darkest surface). WCAG AA.</summary>
    public static readonly Palette Dark = new(
        Page: Rgb.Parse("#0D1218"),
        Card: Rgb.Parse("#18212B"),
        CardBorder: Rgb.Parse("#8C9BAB"),
        Text: Rgb.Parse("#F3EFE7"),
        TextSecondary: Rgb.Parse("#B8C3CE"),
        Accent: Rgb.Parse("#FF9065"),
        OnAccent: Rgb.Parse("#0D1218"),
        Works: Rgb.Parse("#8FD69B"),
        ExtraStep: Rgb.Parse("#8DCBF5"),
        Partial: Rgb.Parse("#FFCF5C"),
        Unsupported: Rgb.Parse("#FF9F95"),
        Unknown: Rgb.Parse("#B8C3CE"),
        NoticeInfo: Rgb.Parse("#1B2B2A"),
        NoticeWarning: Rgb.Parse("#2D2616"),
        CommandBackground: Rgb.Parse("#111820"),
        Shadow: Rgb.Parse("#05080B"));

    /// <summary>Text colours that appear on the surfaces, for contrast checks and theming.</summary>
    public IEnumerable<(string Name, Rgb Colour)> TextColours =>
    [
        (nameof(Text), Text),
        (nameof(TextSecondary), TextSecondary),
        (nameof(Accent), Accent),
        (nameof(Works), Works),
        (nameof(ExtraStep), ExtraStep),
        (nameof(Partial), Partial),
        (nameof(Unsupported), Unsupported),
        (nameof(Unknown), Unknown),
    ];

    public IEnumerable<(string Name, Rgb Colour)> Surfaces =>
    [
        (nameof(Page), Page),
        (nameof(Card), Card),
        (nameof(NoticeInfo), NoticeInfo),
        (nameof(NoticeWarning), NoticeWarning),
        (nameof(CommandBackground), CommandBackground),
    ];

    /// <summary>All named colours, for building the WPF resource dictionary ("GH.&lt;Name&gt;").</summary>
    public IEnumerable<(string Name, Rgb Colour)> All =>
        TextColours.Concat(Surfaces).Concat([(nameof(CardBorder), CardBorder), (nameof(OnAccent), OnAccent), (nameof(Shadow), Shadow)]);
}
