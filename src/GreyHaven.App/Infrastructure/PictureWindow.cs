using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Input;

namespace GreyHaven.App.Infrastructure;

/// <summary>
/// A walkthrough picture, big. Closes with Close, Escape or a click on the picture, and puts
/// focus back on the picture it came from. Screen readers hear its description.
/// </summary>
public sealed class PictureWindow : Window
{
    private PictureWindow(ControlTemplate picture, string alt)
    {
        Title = "Picture";
        WindowStyle = WindowStyle.ToolWindow;
        ShowInTaskbar = false;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        SetResourceReference(BackgroundProperty, "GH.Page");
        var close = new Button { Content = "Close picture", Padding = new Thickness(20, 6, 20, 6), HorizontalAlignment = HorizontalAlignment.Right, IsCancel = true, Margin = new Thickness(0, 0, 0, 10) };
        close.Click += (_, _) => Close();
        var image = new ContentControl { Template = picture, Focusable = false, Cursor = Cursors.Hand };
        image.MouseLeftButtonUp += (_, _) => Close();
        AutomationProperties.SetName(image, alt);
        var caption = new TextBlock { Text = alt, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 10, 0, 0), FontSize = 15 };
        caption.SetResourceReference(TextBlock.ForegroundProperty, "GH.TextSecondary");
        var panel = new DockPanel { Margin = new Thickness(20) };
        DockPanel.SetDock(close, Dock.Top);
        DockPanel.SetDock(caption, Dock.Bottom);
        panel.Children.Add(close);
        panel.Children.Add(caption);
        panel.Children.Add(new Viewbox { Child = image, Stretch = System.Windows.Media.Stretch.Uniform });
        Content = panel;
        Loaded += (_, _) => close.Focus();
    }

    public static void Show(Window? owner, ControlTemplate picture, string alt, IInputElement returnFocusTo)
    {
        var area = SystemParameters.WorkArea;
        var w = new PictureWindow(picture, alt)
        {
            Owner = owner,
            Width = Math.Min(1200, area.Width * 0.9),
            Height = Math.Min(860, area.Height * 0.9),
        };
        w.ShowDialog();
        Keyboard.Focus(returnFocusTo);
    }
}
