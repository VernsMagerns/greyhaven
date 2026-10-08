using System.Windows;
using System.Windows.Automation.Peers;
using System.Windows.Media;

namespace GreyHaven.App.Infrastructure;

/// <summary>
/// Draws a QR code from its squares (GreyHaven.Reports.QrCode.Modules): black on white in every
/// theme, including High Contrast, because phone cameras need that. Exposed as an image.
/// </summary>
public sealed class QrCodeView : FrameworkElement
{
    public static readonly DependencyProperty ModulesProperty = DependencyProperty.Register(
        nameof(Modules), typeof(bool[][]), typeof(QrCodeView), new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender));

    public bool[][]? Modules
    {
        get => (bool[][]?)GetValue(ModulesProperty);
        set => SetValue(ModulesProperty, value);
    }

    protected override void OnRender(DrawingContext dc)
    {
        dc.DrawRectangle(Brushes.White, null, new Rect(RenderSize));
        if (Modules is not { Length: > 0 } m) return;
        var size = Math.Min(ActualWidth, ActualHeight) / m.Length;
        var geometry = new StreamGeometry();
        using (var g = geometry.Open())
            for (var y = 0; y < m.Length; y++)
                for (var x = 0; x < m[y].Length; x++)
                    if (m[y][x])
                    {
                        var r = new Rect(x * size, y * size, size, size);
                        g.BeginFigure(r.TopLeft, true, true);
                        g.PolyLineTo([r.TopRight, r.BottomRight, r.BottomLeft], false, false);
                    }
        geometry.Freeze();
        dc.DrawGeometry(Brushes.Black, null, geometry);
    }

    protected override AutomationPeer OnCreateAutomationPeer() => new Peer(this);

    private sealed class Peer(QrCodeView owner) : FrameworkElementAutomationPeer(owner)
    {
        protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.Image;
        protected override string GetClassNameCore() => nameof(QrCodeView);
    }
}
