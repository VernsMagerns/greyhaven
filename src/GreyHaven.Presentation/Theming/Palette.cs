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
    Rgb CommandBackground)
{
    /// <summary>Matches Windows 11 light-mode Fluent colours, darkened where needed for AA contrast.</summary>
    public static readonly Palette Light = new(
        Page: Rgb.Parse("#F3F3F3"),
        Card: Rgb.Parse("#FFFFFF"),
        CardBorder: Rgb.Parse("#8A8A8A"),
        Text: Rgb.Parse("#1A1A1A"),
        TextSecondary: Rgb.Parse("#595959"),
        Accent: Rgb.Parse("#005FB8"),
        OnAccent: Rgb.Parse("#FFFFFF"),
        Works: Rgb.Parse("#0E6E0E"),
        ExtraStep: Rgb.Parse("#005FB8"),
        Partial: Rgb.Parse("#8A5000"),
        Unsupported: Rgb.Parse("#B3261E"),
        Unknown: Rgb.Parse("#595959"),
        NoticeInfo: Rgb.Parse("#E6F0FA"),
        NoticeWarning: Rgb.Parse("#FFF4CE"),
        CommandBackground: Rgb.Parse("#F0F0F0"));

    /// <summary>Matches Windows 11 dark-mode Fluent colours.</summary>
    public static readonly Palette Dark = new(
        Page: Rgb.Parse("#202020"),
        Card: Rgb.Parse("#2B2B2B"),
        CardBorder: Rgb.Parse("#8A8A8A"),
        Text: Rgb.Parse("#FFFFFF"),
        TextSecondary: Rgb.Parse("#C8C8C8"),
        Accent: Rgb.Parse("#60CDFF"),
        OnAccent: Rgb.Parse("#000000"),
        Works: Rgb.Parse("#6CCB5F"),
        ExtraStep: Rgb.Parse("#60CDFF"),
        Partial: Rgb.Parse("#FCE100"),
        Unsupported: Rgb.Parse("#FF99A4"),
        Unknown: Rgb.Parse("#C8C8C8"),
        NoticeInfo: Rgb.Parse("#1F3347"),
        NoticeWarning: Rgb.Parse("#433519"),
        CommandBackground: Rgb.Parse("#1C1C1C"));

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
        TextColours.Concat(Surfaces).Concat([(nameof(CardBorder), CardBorder), (nameof(OnAccent), OnAccent)]);
}
