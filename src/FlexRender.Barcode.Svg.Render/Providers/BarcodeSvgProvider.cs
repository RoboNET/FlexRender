using System.Text;
using FlexRender.Barcode.Code128;
using FlexRender.Parsing.Ast;
using FlexRender.Providers;
using FlexRender.Rendering;

namespace FlexRender.Barcode.Svg.Providers;

/// <summary>
/// Provides SVG-native barcode generation for Code 128 barcodes.
/// </summary>
/// <remarks>
/// <para>
/// Generates native SVG rect elements for barcode bars instead of rasterized bitmaps.
/// This produces smaller, scalable vector output suitable for SVG-only rendering.
/// </para>
/// <para>
/// Currently supports Code 128B (ASCII 32-127). The same encoding logic as the raster
/// <c>BarcodeProvider</c> is used, but output is SVG markup instead of a bitmap.
/// </para>
/// </remarks>
public sealed class BarcodeSvgProvider : ISvgContentProvider<BarcodeElement>
{

    /// <summary>
    /// Generates native SVG markup for a barcode element.
    /// </summary>
    /// <param name="element">The barcode element configuration.</param>
    /// <param name="width">The allocated width in SVG user units.</param>
    /// <param name="height">The allocated height in SVG user units.</param>
    /// <returns>SVG markup containing the barcode as vector rectangles.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="element"/> is null.</exception>
    /// <exception cref="ArgumentException">Thrown when element data is empty.</exception>
    /// <exception cref="NotSupportedException">Thrown when the barcode format is not supported.</exception>
    public string GenerateSvgContent(BarcodeElement element, float width, float height)
    {
        ArgumentNullException.ThrowIfNull(element);

        if (string.IsNullOrEmpty(element.Data.Value))
        {
            throw new ArgumentException("Barcode data cannot be empty.", nameof(element));
        }

        return element.Format.Value switch
        {
            BarcodeFormat.Code128 => GenerateCode128Svg(element, width, height),
            _ => throw new NotSupportedException($"Barcode format '{element.Format.Value}' is not yet supported.")
        };
    }

    private static string GenerateCode128Svg(BarcodeElement element, float width, float height)
    {
        var pattern = Code128Encoding.BuildPattern(element.Data.Value);

        var totalUnits = pattern.Length;
        var barWidth = width / totalUnits;

        var foreground = element.Foreground.Value;
        var background = element.Background.Value;

        var sb = new StringBuilder(512);
        sb.Append("<g>");

        if (background is not null)
        {
            sb.Append("<rect width=\"").Append(F(width));
            sb.Append("\" height=\"").Append(F(height));
            sb.Append("\" fill=\"").Append(EscapeXml(background)).Append("\"/>");
        }

        // Build optimized path data using horizontal run-length encoding
        var pathSb = new StringBuilder(totalUnits / 2);
        var x = 0f;
        var i = 0;
        while (i < totalUnits)
        {
            if (pattern[i] != '1')
            {
                x += barWidth;
                i++;
                continue;
            }

            var runStart = i;
            while (i < totalUnits && pattern[i] == '1')
            {
                i++;
            }

            var runWidth = (i - runStart) * barWidth;
            pathSb.Append('M').Append(F(x)).Append(' ').Append('0');
            pathSb.Append('h').Append(F(runWidth));
            pathSb.Append('v').Append(F(height));
            pathSb.Append('h').Append(F(-runWidth));
            pathSb.Append('z');

            x += (i - runStart) * barWidth;
        }

        if (pathSb.Length > 0)
        {
            sb.Append("<path d=\"").Append(pathSb);
            sb.Append("\" fill=\"").Append(EscapeXml(foreground)).Append("\"/>");
        }

        sb.Append("</g>");
        return sb.ToString();
    }

    private static string F(float value) => SvgFormatting.FormatFloat(value);

    private static string EscapeXml(string value) => SvgFormatting.EscapeXml(value);
}
