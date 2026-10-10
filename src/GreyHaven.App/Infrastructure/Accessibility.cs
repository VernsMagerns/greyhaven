using System.ComponentModel;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Automation.Peers;
using System.Windows.Controls;
using System.Windows.Threading;

namespace GreyHaven.App.Infrastructure;

/// <summary>
/// Attached behaviours for screen readers.
/// <c>Announce="True"</c> on a TextBlock raises a live-region event whenever its text changes,
/// so progress like "Checking the online database (2 of 5)" is read aloud. WPF doesn't do this by
/// itself even with LiveSetting set.
/// </summary>
public static class Accessibility
{
    public static readonly DependencyProperty AnnounceProperty = DependencyProperty.RegisterAttached(
        "Announce", typeof(bool), typeof(Accessibility), new PropertyMetadata(false, OnAnnounceChanged));

    public static bool GetAnnounce(DependencyObject d) => (bool)d.GetValue(AnnounceProperty);
    public static void SetAnnounce(DependencyObject d, bool value) => d.SetValue(AnnounceProperty, value);

    private static readonly DependencyPropertyDescriptor TextDescriptor =
        DependencyPropertyDescriptor.FromProperty(TextBlock.TextProperty, typeof(TextBlock));

    private static void OnAnnounceChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not TextBlock tb) return;
        if ((bool)e.NewValue)
        {
            AutomationProperties.SetLiveSetting(tb, AutomationLiveSetting.Polite);
            TextDescriptor.AddValueChanged(tb, OnTextChanged);
        }
        else
        {
            TextDescriptor.RemoveValueChanged(tb, OnTextChanged);
        }
    }

    private static void OnTextChanged(object? sender, EventArgs e)
    {
        if (sender is not TextBlock tb) return;
        var peer = UIElementAutomationPeer.FromElement(tb) ?? UIElementAutomationPeer.CreatePeerForElement(tb);
        peer?.RaiseAutomationEvent(AutomationEvents.LiveRegionChanged);
    }

    /// <summary>
    /// <c>FocusOnShow="True"</c>: when the element becomes visible, give it keyboard focus, so
    /// keyboard and screen-reader users land on the new screen's heading.
    /// </summary>
    public static readonly DependencyProperty FocusOnShowProperty = DependencyProperty.RegisterAttached(
        "FocusOnShow", typeof(bool), typeof(Accessibility), new PropertyMetadata(false, OnFocusOnShowChanged));

    public static bool GetFocusOnShow(DependencyObject d) => (bool)d.GetValue(FocusOnShowProperty);
    public static void SetFocusOnShow(DependencyObject d, bool value) => d.SetValue(FocusOnShowProperty, value);

    private static void OnFocusOnShowChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is FrameworkElement fe && (bool)e.NewValue)
            fe.IsVisibleChanged += (_, args) =>
            {
                if (args.NewValue is true)
                    fe.Dispatcher.BeginInvoke(DispatcherPriority.Input, () => fe.Focus());
            };
    }
}

/// <summary>
/// A purely decorative glyph (e.g. a rating icon next to its text label). Hidden from screen
/// readers, which would otherwise read the icon font's private-use character.
/// </summary>
public sealed class DecorativeText : TextBlock
{
    protected override AutomationPeer OnCreateAutomationPeer() => new DecorativePeer(this);

    private sealed class DecorativePeer(TextBlock owner) : TextBlockAutomationPeer(owner)
    {
        protected override bool IsControlElementCore() => false;
        protected override bool IsContentElementCore() => false;
    }
}

/// <summary>A picture that screen readers skip, because the words next to it say the same (e.g. the logo by "GreyHaven").</summary>
public sealed class DecorativeImage : System.Windows.Controls.Image
{
    protected override AutomationPeer OnCreateAutomationPeer() => new DecorativeImagePeer(this);

    private sealed class DecorativeImagePeer(System.Windows.Controls.Image owner) : ImageAutomationPeer(owner)
    {
        protected override bool IsControlElementCore() => false;
        protected override bool IsContentElementCore() => false;
    }
}

/// <summary>
/// An ItemsControl that screen readers skip (e.g. the walkthrough's progress segments, which
/// "Step 3 of 9" already says in words). Its items aren't exposed either.
/// </summary>
public sealed class DecorativeItems : ItemsControl
{
    protected override AutomationPeer OnCreateAutomationPeer() => new DecorativeItemsPeer(this);

    private sealed class DecorativeItemsPeer(ItemsControl owner) : FrameworkElementAutomationPeer(owner)
    {
        protected override bool IsControlElementCore() => false;
        protected override bool IsContentElementCore() => false;
        protected override List<AutomationPeer>? GetChildrenCore() => null;
    }
}
