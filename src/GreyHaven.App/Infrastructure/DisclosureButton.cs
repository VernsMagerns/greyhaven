using System.Windows;
using System.Windows.Automation;
using System.Windows.Automation.Peers;
using System.Windows.Automation.Provider;
using System.Windows.Controls.Primitives;

namespace GreyHaven.App.Infrastructure;

/// <summary>
/// The clickable header of a "Show more" expander (Styles.xaml, ShowMore). A plain ToggleButton
/// tells screen readers "on/off"; this one says "collapsed/expanded", like a web &lt;details&gt;.
/// </summary>
public sealed class DisclosureButton : ToggleButton
{
    protected override AutomationPeer OnCreateAutomationPeer() => new Peer(this);

    protected override void OnChecked(RoutedEventArgs e)
    {
        base.OnChecked(e);
        RaiseStateChanged(false, true);
    }

    protected override void OnUnchecked(RoutedEventArgs e)
    {
        base.OnUnchecked(e);
        RaiseStateChanged(true, false);
    }

    private void RaiseStateChanged(bool wasExpanded, bool isExpanded)
    {
        if (UIElementAutomationPeer.FromElement(this) is Peer peer)
            peer.RaisePropertyChangedEvent(ExpandCollapsePatternIdentifiers.ExpandCollapseStateProperty,
                wasExpanded ? ExpandCollapseState.Expanded : ExpandCollapseState.Collapsed,
                isExpanded ? ExpandCollapseState.Expanded : ExpandCollapseState.Collapsed);
    }

    private sealed class Peer(DisclosureButton owner) : FrameworkElementAutomationPeer(owner), IExpandCollapseProvider, IInvokeProvider
    {
        private DisclosureButton Button => (DisclosureButton)Owner;

        protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.Button;
        protected override string GetClassNameCore() => nameof(DisclosureButton);

        public override object? GetPattern(PatternInterface pattern) =>
            pattern is PatternInterface.ExpandCollapse or PatternInterface.Invoke ? this : base.GetPattern(pattern);

        public ExpandCollapseState ExpandCollapseState =>
            Button.IsChecked == true ? ExpandCollapseState.Expanded : ExpandCollapseState.Collapsed;

        public void Expand() => Button.IsChecked = true;
        public void Collapse() => Button.IsChecked = false;
        public void Invoke() => Button.IsChecked = Button.IsChecked != true;
    }
}
