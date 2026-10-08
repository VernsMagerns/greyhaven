using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using GreyHaven.Reports;

namespace GreyHaven.App.Infrastructure;

/// <summary>A short pop-up of report blocks (e.g. a version of Linux's pros and cons). Close or Escape; focus returns.</summary>
public sealed class InfoWindow : Window
{
    private InfoWindow(string title, IReadOnlyList<Block> blocks)
    {
        Title = title;
        WindowStyle = WindowStyle.ToolWindow;
        ShowInTaskbar = false;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        SizeToContent = SizeToContent.Height;
        Width = 560;
        MaxHeight = SystemParameters.WorkArea.Height * 0.9;
        SetResourceReference(BackgroundProperty, "GH.Page");
        SetResourceReference(FontFamilyProperty, "BodyFont");
        var heading = new TextBlock { Text = title, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 0, 0, 8) };
        heading.SetResourceReference(StyleProperty, "Heading2");
        var body = new StackPanel();
        BlockView.SetBlocks(body, blocks);
        var close = new Button { Content = "Close", Padding = new Thickness(24, 6, 24, 6), HorizontalAlignment = HorizontalAlignment.Right, IsCancel = true, IsDefault = true, Margin = new Thickness(0, 12, 0, 0) };
        close.Click += (_, _) => Close();
        var panel = new StackPanel { Margin = new Thickness(24) };
        panel.Children.Add(heading);
        panel.Children.Add(body);
        panel.Children.Add(close);
        Content = new ScrollViewer { Content = panel, VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
        Loaded += (_, _) => close.Focus();
    }

    public static void Show(Window? owner, string title, IReadOnlyList<Block> blocks, IInputElement returnFocusTo)
    {
        new InfoWindow(title, blocks) { Owner = owner }.ShowDialog();
        Keyboard.Focus(returnFocusTo);
    }
}
