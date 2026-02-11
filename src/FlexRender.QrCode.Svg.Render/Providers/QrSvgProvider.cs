using System.Text;
using FlexRender.Parsing.Ast;
using FlexRender.Providers;
using FlexRender.Rendering;
using QRCoder;

namespace FlexRender.QrCode.Svg.Providers;

/// <summary>
/// Provides SVG-native QR code generation.
/// </summary>
public sealed class QrSvgProvider : ISvgContentProvider<QrElement>
{
    /// <summary>
    /// Generates native SVG markup for a QR code element.
    /// </summary>
    /// <param name="element">The QR code element configuration.</param>
    /// <param name="width">The allocated width in SVG user units.</param>
    /// <param name="height">The allocated height in SVG user units.</param>
    /// <returns>SVG markup containing the QR code as vector paths.</returns>
    /// <exception cref="ArgumentNullException">Thrown when element is null.</exception>
    /// <exception cref="ArgumentException">Thrown when element data is empty.</exception>
    public string GenerateSvgContent(QrElement element, float width, float height)
    {
        ArgumentNullException.ThrowIfNull(element);

        if (string.IsNullOrEmpty(element.Data.Value))
        {
            throw new ArgumentException("QR code data cannot be empty.", nameof(element));
        }

        var eccLevel = MapEccLevel(element.ErrorCorrection.Value);
        QrDataValidator.ValidateDataCapacity(element);

        using var qrGenerator = new QRCodeGenerator();
        using var qrCodeData = qrGenerator.CreateQrCode(element.Data.Value, eccLevel);

        var moduleCount = qrCodeData.ModuleMatrix.Count;
        var moduleWidth = width / moduleCount;
        var moduleHeight = height / moduleCount;

        var sb = new StringBuilder(1024);
        sb.Append("<g>");

        if (element.Background.Value is not null)
        {
            sb.Append("<rect width=\"").Append(F(width));
            sb.Append("\" height=\"").Append(F(height));
            sb.Append("\" fill=\"").Append(EscapeXml(element.Background.Value)).Append("\"/>");
        }

        var pathData = BuildPathData(qrCodeData, moduleCount, moduleWidth, moduleHeight);
        if (pathData.Length > 0)
        {
            sb.Append("<path d=\"").Append(pathData);
            sb.Append("\" fill=\"").Append(EscapeXml(element.Foreground.Value)).Append("\"/>");
        }

        sb.Append("</g>");
        return sb.ToString();
    }

    private static string BuildPathData(
        QRCodeData qrCodeData,
        int moduleCount,
        float moduleWidth,
        float moduleHeight)
    {
        var sb = new StringBuilder(moduleCount * moduleCount / 2);

        for (var row = 0; row < moduleCount; row++)
        {
            var col = 0;
            while (col < moduleCount)
            {
                if (!qrCodeData.ModuleMatrix[row][col])
                {
                    col++;
                    continue;
                }

                var runStart = col;
                while (col < moduleCount && qrCodeData.ModuleMatrix[row][col])
                {
                    col++;
                }

                var runLength = col - runStart;
                var x = runStart * moduleWidth;
                var y = row * moduleHeight;
                var w = runLength * moduleWidth;

                sb.Append('M').Append(F(x)).Append(' ').Append(F(y));
                sb.Append('h').Append(F(w));
                sb.Append('v').Append(F(moduleHeight));
                sb.Append('h').Append(F(-w));
                sb.Append('z');
            }
        }

        return sb.ToString();
    }

    private static QRCodeGenerator.ECCLevel MapEccLevel(ErrorCorrectionLevel level)
    {
        return level switch
        {
            ErrorCorrectionLevel.L => QRCodeGenerator.ECCLevel.L,
            ErrorCorrectionLevel.M => QRCodeGenerator.ECCLevel.M,
            ErrorCorrectionLevel.Q => QRCodeGenerator.ECCLevel.Q,
            ErrorCorrectionLevel.H => QRCodeGenerator.ECCLevel.H,
            _ => QRCodeGenerator.ECCLevel.M
        };
    }

    private static string EscapeXml(string value) => SvgFormatting.EscapeXml(value);

    private static string F(float value) => SvgFormatting.FormatFloat(value);
}
