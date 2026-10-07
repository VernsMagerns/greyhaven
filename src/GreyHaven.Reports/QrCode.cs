using QRCoder;

namespace GreyHaven.Reports;

/// <summary>QR codes as SVG (QRCoder, MIT), for the "Take the guide with you" step.</summary>
public static class QrCode
{
    /// <summary>Black on white with a quiet zone, which every phone camera reads, whatever the theme.</summary>
    public static string Svg(string text)
    {
        using var generator = new QRCodeGenerator();
        using var data = generator.CreateQrCode(text, QRCodeGenerator.ECCLevel.M);
        return new SvgQRCode(data).GetGraphic(8, "#000000", "#FFFFFF", drawQuietZones: true);
    }
}
