using System.Text;
using FlexRender.Parsing.Ast;
using FlexRender.Providers;
using FlexRender.Rendering;
using QRCoder;
using SkiaSharp;

namespace FlexRender.QrCode.Providers;

/// <summary>
/// Provides QR code generation as both raster bitmaps and native SVG markup.
/// </summary>
/// <remarks>
/// <para>
/// For raster output (PNG, JPEG), generates an <see cref="SKBitmap"/> via <see cref="IContentProvider{TElement}"/>.
/// For SVG output, generates native SVG path elements via <see cref="ISvgContentProvider{TElement}"/>,
/// producing smaller, scalable, pixel-perfect vector QR codes.
/// The <see cref="ISkiaNativeProvider{TElement}"/> interface provides optimized direct Skia bitmap rendering.
/// </para>
/// <para>
/// The SVG output uses horizontal run-length encoding to minimize path data size.
/// Adjacent dark modules on the same row are merged into a single rectangle sub-path.
/// </para>
/// </remarks>
public sealed class QrProvider : IContentProvider<QrElement>, ISvgContentProvider<QrElement>, ISkiaNativeProvider<QrElement>
{
    /// <summary>
    /// Generates a PNG-encoded QR code at the specified dimensions.
    /// Uses <c>Math.Min(width, height)</c> since QR codes are always square.
    /// </summary>
    /// <param name="element">The QR code element configuration.</param>
    /// <param name="width">The allocated width in pixels.</param>
    /// <param name="height">The allocated height in pixels.</param>
    /// <returns>A <see cref="ContentResult"/> containing PNG bytes and dimensions.</returns>
    /// <exception cref="ArgumentNullException">Thrown when element is null.</exception>
    /// <exception cref="ArgumentException">Thrown when element data is empty or size is invalid.</exception>
    public ContentResult Generate(QrElement element, int width, int height)
    {
        using var bitmap = GenerateBitmap(element, width, height);
        using var image = SKImage.FromBitmap(bitmap);
        using var data = image.Encode(SKEncodedImageFormat.Png, 100);
        return new ContentResult(data.ToArray(), bitmap.Width, bitmap.Height);
    }

    /// <summary>
    /// Generates a QR code bitmap for direct Skia canvas drawing,
    /// avoiding PNG encode/decode overhead.
    /// </summary>
    /// <param name="element">The QR code element configuration.</param>
    /// <param name="width">The allocated width in pixels.</param>
    /// <param name="height">The allocated height in pixels.</param>
    /// <returns>An <see cref="SKBitmap"/> containing the rendered QR code. Caller is responsible for disposal.</returns>
    /// <exception cref="ArgumentNullException">Thrown when element is null.</exception>
    /// <exception cref="ArgumentException">Thrown when element data is empty or size is invalid.</exception>
    SKBitmap ISkiaNativeProvider<QrElement>.GenerateBitmap(QrElement element, int width, int height)
    {
        return GenerateBitmap(element, width, height);
    }

    /// <summary>
    /// Generates a QR code bitmap at the specified dimensions.
    /// Uses <c>Math.Min(width, height)</c> since QR codes are always square.
    /// </summary>
    /// <param name="element">The QR code element configuration.</param>
    /// <param name="width">The allocated width in pixels.</param>
    /// <param name="height">The allocated height in pixels.</param>
    /// <returns>A bitmap containing the rendered QR code.</returns>
    private static SKBitmap GenerateBitmap(QrElement element, int width, int height)
    {
        return Generate(element, Math.Min(width, height), null);
    }

    /// <summary>
    /// Generates a QR code bitmap with optional layout-computed dimensions.
    /// </summary>
    /// <param name="element">The QR code element configuration.</param>
    /// <param name="layoutWidth">Optional layout-computed width. Takes precedence over element.Size.</param>
    /// <param name="layoutHeight">Optional layout-computed height. Takes precedence over element.Size.</param>
    /// <returns>A bitmap containing the rendered QR code.</returns>
    /// <exception cref="ArgumentNullException">Thrown when element is null.</exception>
    /// <exception cref="ArgumentException">Thrown when element data is empty or size is invalid.</exception>
    public static SKBitmap Generate(QrElement element, int? layoutWidth, int? layoutHeight)
    {
        ArgumentNullException.ThrowIfNull(element);

        if (string.IsNullOrEmpty(element.Data))
        {
            throw new ArgumentException("QR code data cannot be empty.", nameof(element));
        }

        // Priority order: layout dimensions > element.Size > default 100px
        var targetSize = layoutWidth ?? element.Size ?? 100;

        if (targetSize <= 0)
        {
            throw new ArgumentException("QR code size must be positive.", nameof(element));
        }

        var eccLevel = MapEccLevel(element.ErrorCorrection);
        QrDataValidator.ValidateDataCapacity(element);

        using var qrGenerator = new QRCodeGenerator();
        using var qrCodeData = qrGenerator.CreateQrCode(element.Data, eccLevel);

        var moduleCount = qrCodeData.ModuleMatrix.Count;
        var moduleSize = targetSize / (float)moduleCount;

        var bitmap = new SKBitmap(targetSize, targetSize);
        using var canvas = new SKCanvas(bitmap);

        var foreground = ColorParser.Parse(element.Foreground);
        var background = element.Background is not null
            ? ColorParser.Parse(element.Background)
            : SKColors.Transparent;

        // Fill background
        canvas.Clear(background);

        using var paint = new SKPaint
        {
            Color = foreground,
            IsAntialias = false,
            Style = SKPaintStyle.Fill
        };

        // Draw QR code modules
        for (var y = 0; y < moduleCount; y++)
        {
            for (var x = 0; x < moduleCount; x++)
            {
                if (qrCodeData.ModuleMatrix[y][x])
                {
                    var rect = new SKRect(
                        x * moduleSize,
                        y * moduleSize,
                        (x + 1) * moduleSize,
                        (y + 1) * moduleSize);
                    canvas.DrawRect(rect, paint);
                }
            }
        }

        return bitmap;
    }

    /// <summary>
    /// Generates native SVG markup for a QR code element.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The output is a <c>&lt;g&gt;</c> group containing an optional background
    /// <c>&lt;rect&gt;</c> and a <c>&lt;path&gt;</c> element with optimized path data.
    /// </para>
    /// <para>
    /// Path optimization uses horizontal run-length encoding: adjacent dark modules
    /// on the same row are merged into a single rectangle sub-path, reducing the
    /// SVG file size by 60-80% compared to individual <c>&lt;rect&gt;</c> elements.
    /// </para>
    /// </remarks>
    /// <param name="element">The QR code element configuration.</param>
    /// <param name="width">The allocated width in SVG user units.</param>
    /// <param name="height">The allocated height in SVG user units.</param>
    /// <returns>SVG markup containing the QR code as vector paths.</returns>
    /// <exception cref="ArgumentNullException">Thrown when element is null.</exception>
    /// <exception cref="ArgumentException">Thrown when element data is empty.</exception>
    public string GenerateSvgContent(QrElement element, float width, float height)
    {
        ArgumentNullException.ThrowIfNull(element);

        if (string.IsNullOrEmpty(element.Data))
        {
            throw new ArgumentException("QR code data cannot be empty.", nameof(element));
        }

        var eccLevel = MapEccLevel(element.ErrorCorrection);
        QrDataValidator.ValidateDataCapacity(element);

        using var qrGenerator = new QRCodeGenerator();
        using var qrCodeData = qrGenerator.CreateQrCode(element.Data, eccLevel);

        var moduleCount = qrCodeData.ModuleMatrix.Count;
        var moduleWidth = width / moduleCount;
        var moduleHeight = height / moduleCount;

        var sb = new StringBuilder(1024);
        sb.Append("<g>");

        // Optional background
        if (element.Background is not null)
        {
            sb.Append("<rect width=\"").Append(F(width));
            sb.Append("\" height=\"").Append(F(height));
            sb.Append("\" fill=\"").Append(EscapeXml(element.Background)).Append("\"/>");
        }

        // Build optimized path data using horizontal run-length encoding
        var pathData = BuildPathData(qrCodeData, moduleCount, moduleWidth, moduleHeight);

        if (pathData.Length > 0)
        {
            sb.Append("<path d=\"").Append(pathData);
            sb.Append("\" fill=\"").Append(EscapeXml(element.Foreground)).Append("\"/>");
        }

        sb.Append("</g>");
        return sb.ToString();
    }

    /// <summary>
    /// Builds optimized SVG path data from the QR module matrix.
    /// Uses horizontal run-length encoding: adjacent dark modules on the same row
    /// are merged into a single rectangle sub-path (M x y h w v h h -w z).
    /// </summary>
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

                // Found a dark module -- scan for consecutive dark modules
                var runStart = col;
                while (col < moduleCount && qrCodeData.ModuleMatrix[row][col])
                {
                    col++;
                }

                var runLength = col - runStart;

                // Emit rectangle sub-path: M x y h w v h h -w z
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

    /// <summary>
    /// Escapes XML special characters in attribute values to prevent SVG injection.
    /// Delegates to <see cref="SvgFormatting.EscapeXml"/>.
    /// </summary>
    private static string EscapeXml(string value) => SvgFormatting.EscapeXml(value);

    /// <summary>
    /// Formats a float using invariant culture with no trailing zeros.
    /// Delegates to <see cref="SvgFormatting.FormatFloat"/>.
    /// </summary>
    private static string F(float value) => SvgFormatting.FormatFloat(value);
}
