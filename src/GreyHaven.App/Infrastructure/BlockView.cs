using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using System.Windows.Shapes;
using GreyHaven.Core.Hardware;
using GreyHaven.Reports;
using Block = GreyHaven.Reports.Block;
using Inline = GreyHaven.Reports.Inline;
using Paragraph = GreyHaven.Reports.Paragraph;
using Table = GreyHaven.Reports.Table;

namespace GreyHaven.App.Infrastructure;

/// <summary>
/// Shows report blocks (GreyHaven.Reports' format-neutral document) as WPF controls, so the app's
/// walkthrough shows exactly what the saved report prints. Usage on a StackPanel:
/// <c>infra:BlockView.Blocks="{Binding Body}"</c>. Styles come from Styles.xaml.
/// </summary>
public static class BlockView
{
    /// <summary>Opens links (https only, checked by the app). Set at startup.</summary>
    public static Action<string>? Open { get; set; }
    /// <summary>Copies a command to the clipboard. Set at startup.</summary>
    public static Action<string>? Copy { get; set; }

    public static readonly DependencyProperty BlocksProperty = DependencyProperty.RegisterAttached(
        "Blocks", typeof(IReadOnlyList<Block>), typeof(BlockView), new PropertyMetadata(null, OnBlocksChanged));

    public static IReadOnlyList<Block>? GetBlocks(DependencyObject d) => (IReadOnlyList<Block>?)d.GetValue(BlocksProperty);
    public static void SetBlocks(DependencyObject d, IReadOnlyList<Block>? value) => d.SetValue(BlocksProperty, value);

    private static void OnBlocksChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not Panel panel) return;
        panel.Children.Clear();
        foreach (var block in (IReadOnlyList<Block>?)e.NewValue ?? [])
            panel.Children.Add(Render(panel, block));
    }

    private static Style? Style(FrameworkElement scope, string key) => scope.TryFindResource(key) as Style;

    private static UIElement Render(FrameworkElement scope, Block block) => block switch
    {
        Heading h => Heading(scope, h),
        Paragraph p => Text(scope, p.Content, "Body", new Thickness(0, 0, 0, 10)),
        Callout c => Callout(scope, c),
        ListBlock l when l.Kind == ListKind.Checkboxes => Checklist(scope, l),
        ListBlock l => List(scope, l),
        CommandBlock c => Commands(scope, c),
        Details d => Details(scope, d),
        PictureGuide g => Pictures(scope, g),
        KeyFact k => KeyFact(scope, k),
        Table t => Text(scope, [new Text(string.Join("\n", t.Rows.Select(r => string.Join(" · ", r.Select(c => string.Concat(c.Select(Plain)))))))], "Body", new Thickness(0, 0, 0, 10)),
        _ => new TextBlock(),
    };

    private static TextBlock Heading(FrameworkElement scope, Heading h)
    {
        // Step titles are the screen's H1, so blocks start at level 2.
        var level = Math.Clamp(h.Level, 2, 3);
        return new TextBlock { Text = h.Text, Style = Style(scope, $"Heading{level}") };
    }

    private static TextBlock Text(FrameworkElement scope, IReadOnlyList<Inline> content, string style, Thickness margin)
    {
        var tb = new TextBlock { Style = Style(scope, style), Margin = margin, TextWrapping = TextWrapping.Wrap };
        foreach (var i in content) tb.Inlines.Add(ToInline(scope, i));
        return tb;
    }

    private static System.Windows.Documents.Inline ToInline(FrameworkElement scope, Inline i)
    {
        switch (i)
        {
            case Strong s:
                return new Bold(new Run(s.Value));
            case Link l when HtmlReportWriter.IsWebUrl(l.Url):
                var link = new Hyperlink(new Run(l.Label)) { NavigateUri = new Uri(l.Url) };
                link.SetResourceReference(TextElement.ForegroundProperty, "GH.Accent");
                AutomationProperties.SetHelpText(link, "Opens in your web browser");
                link.RequestNavigate += (_, e) => { Open?.Invoke(e.Uri.AbsoluteUri); e.Handled = true; };
                return link;
            case RatingTag r:
                return new Run(PlainWords.Rating(r.Rating)) { FontWeight = FontWeights.SemiBold };
            default:
                return new Run(Plain(i));
        }
    }

    private static string Plain(Inline i) => i switch
    {
        Text t => t.Value,
        Strong s => s.Value,
        Link l => l.Label,
        Code c => c.Value,
        RatingTag r => PlainWords.Rating(r.Rating),
        _ => "",
    };

    private static UIElement Callout(FrameworkElement scope, Callout c)
    {
        var panel = new StackPanel();
        panel.Children.Add(new TextBlock { Text = c.Title, Style = Style(scope, "Strong") });
        panel.Children.Add(Text(scope, c.Content, "Body", new Thickness(0, 4, 0, 0)));
        var border = new Border
        {
            Style = Style(scope, c.Kind is CalloutKind.Danger ? "DangerBox" : c.Kind is CalloutKind.Warning ? "WarningBox" : "InfoBox"),
            Child = panel,
        };
        AutomationProperties.SetName(border, $"{c.Title}. {string.Concat(c.Content.Select(Plain))}");
        return border;
    }

    private static UIElement Checklist(FrameworkElement scope, ListBlock l)
    {
        var panel = new StackPanel { Margin = new Thickness(0, 0, 0, 10) };
        foreach (var item in l.Items)
        {
            var text = Text(scope, item, "Body", new Thickness(0));
            var box = new CheckBox { Content = text, Margin = new Thickness(0, 4, 0, 4), VerticalContentAlignment = VerticalAlignment.Top };
            AutomationProperties.SetName(box, string.Concat(item.Select(Plain)));
            panel.Children.Add(box);
        }
        return panel;
    }

    private static UIElement List(FrameworkElement scope, ListBlock l)
    {
        var panel = new StackPanel { Margin = new Thickness(0, 0, 0, 10) };
        for (var n = 0; n < l.Items.Count; n++)
        {
            var row = new Grid { Margin = new Thickness(0, 2, 0, 2) };
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(26) });
            row.ColumnDefinitions.Add(new ColumnDefinition());
            row.Children.Add(new TextBlock { Text = l.Kind == ListKind.Numbered ? $"{n + 1}." : "•", Style = Style(scope, "Body") });
            var text = Text(scope, l.Items[n], "Body", new Thickness(0));
            Grid.SetColumn(text, 1);
            row.Children.Add(text);
            panel.Children.Add(row);
        }
        return panel;
    }

    private static UIElement Commands(FrameworkElement scope, CommandBlock c)
    {
        var panel = new StackPanel { Margin = new Thickness(0, 0, 0, 10) };
        foreach (var line in c.Lines)
        {
            var grid = new Grid();
            grid.ColumnDefinitions.Add(new ColumnDefinition());
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            var text = new TextBox { Text = line, IsReadOnly = true, BorderThickness = new Thickness(0), Background = Brushes.Transparent, TextWrapping = TextWrapping.Wrap, VerticalAlignment = VerticalAlignment.Center };
            text.SetResourceReference(Control.FontFamilyProperty, "CodeFont");
            text.SetResourceReference(Control.ForegroundProperty, "GH.Text");
            AutomationProperties.SetName(text, $"Command: {line}");
            var copy = new Button { Content = "Copy", Padding = new Thickness(12, 4, 12, 4), Margin = new Thickness(8, 0, 0, 0) };
            AutomationProperties.SetName(copy, $"Copy command: {line}");
            copy.Click += (_, _) => Copy?.Invoke(line);
            Grid.SetColumn(copy, 1);
            grid.Children.Add(text);
            grid.Children.Add(copy);
            panel.Children.Add(new Border { Style = Style(scope, "CommandBox"), Child = grid });
        }
        return panel;
    }

    private static UIElement Details(FrameworkElement scope, Details d)
    {
        var content = new StackPanel { Margin = new Thickness(0, 8, 0, 0) };
        foreach (var inner in d.Content) content.Children.Add(Render(scope, inner));
        return new Expander { Header = d.Summary, Content = content, Margin = new Thickness(0, 0, 0, 10), Style = Style(scope, "ShowMore") };
    }

    private static UIElement Pictures(FrameworkElement scope, PictureGuide g)
    {
        var wrap = new WrapPanel { Margin = new Thickness(0, 4, 0, 10) };
        for (var n = 0; n < g.Steps.Count; n++)
        {
            var step = g.Steps[n];
            var frame = new Grid { Height = 120, Margin = new Thickness(0, 0, 0, 6) };
            var dash = new Rectangle { StrokeThickness = 2, StrokeDashArray = [4, 3], RadiusX = 12, RadiusY = 12 };
            dash.SetResourceReference(Shape.StrokeProperty, "GH.CardBorder");
            dash.SetResourceReference(Shape.FillProperty, "GH.CommandBackground");
            frame.Children.Add(dash);
            frame.Children.Add(new TextBlock
            {
                Text = $"Picture coming soon:\n{step.PictureCaption}", Style = Style(scope, "Secondary"), FontSize = 12,
                TextAlignment = TextAlignment.Center, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(10),
            });
            AutomationProperties.SetName(frame, $"Picture coming soon: {step.PictureCaption}");
            var card = new StackPanel { Width = 220, Margin = new Thickness(0, 0, 16, 12) };
            card.Children.Add(frame);
            card.Children.Add(new TextBlock { Text = $"{n + 1}. {step.Text}", Style = Style(scope, "Strong") });
            wrap.Children.Add(card);
        }
        return wrap;
    }

    private static UIElement KeyFact(FrameworkElement scope, KeyFact k)
    {
        var panel = new StackPanel { HorizontalAlignment = HorizontalAlignment.Center };
        panel.Children.Add(new TextBlock { Text = k.Label, Style = Style(scope, "Secondary"), TextAlignment = TextAlignment.Center });
        panel.Children.Add(new TextBlock { Text = k.Value, Style = Style(scope, "BigKey") });
        var card = new ContentControl { Content = panel, Style = Style(scope, "Sticker"), Margin = new Thickness(0, 4, 0, 14) };
        AutomationProperties.SetName(card, $"{k.Label} {k.Value}");
        return card;
    }
}
