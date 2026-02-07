using FlexRender.Barcode.Code128;
using FlexRender.Parsing.Ast;
using FlexRender.Providers;
using FlexRender.Rendering;
using SkiaSharp;

namespace FlexRender.Barcode.Providers;

/// <summary>
/// Provides barcode generation using pure SkiaSharp drawing.
/// Implements <see cref="IContentProvider{TElement}"/> for cross-backend PNG output
/// and <see cref="ISkiaNativeProvider{TElement}"/> for optimized direct Skia bitmap rendering.
/// </summary>
public sealed class BarcodeProvider : IContentProvider<BarcodeElement>, ISkiaNativeProvider<BarcodeElement>
{
    private const int TextHeight = 16;
    private const int TextPadding = 4;

    /// <summary>
    /// Generates a PNG-encoded barcode at the specified dimensions.
    /// </summary>
    /// <param name="element">The barcode element configuration.</param>
    /// <param name="width">The allocated width in pixels.</param>
    /// <param name="height">The allocated height in pixels.</param>
    /// <returns>A <see cref="ContentResult"/> containing PNG bytes and dimensions.</returns>
    /// <exception cref="ArgumentNullException">Thrown when element is null.</exception>
    /// <exception cref="ArgumentException">Thrown when element data is empty or dimensions are invalid.</exception>
    /// <exception cref="NotSupportedException">Thrown when the barcode format is not supported.</exception>
    public ContentResult Generate(BarcodeElement element, int width, int height)
    {
        using var bitmap = GenerateBitmap(element, width, height);
        using var image = SKImage.FromBitmap(bitmap);
        using var data = image.Encode(SKEncodedImageFormat.Png, 100);
        return new ContentResult(data.ToArray(), bitmap.Width, bitmap.Height);
    }

    /// <summary>
    /// Generates a barcode bitmap for direct Skia canvas drawing,
    /// avoiding PNG encode/decode overhead.
    /// </summary>
    /// <param name="element">The barcode element configuration.</param>
    /// <param name="width">The allocated width in pixels.</param>
    /// <param name="height">The allocated height in pixels.</param>
    /// <returns>An <see cref="SKBitmap"/> containing the rendered barcode. Caller is responsible for disposal.</returns>
    /// <exception cref="ArgumentNullException">Thrown when element is null.</exception>
    /// <exception cref="ArgumentException">Thrown when element data is empty or dimensions are invalid.</exception>
    /// <exception cref="NotSupportedException">Thrown when the barcode format is not supported.</exception>
    SKBitmap ISkiaNativeProvider<BarcodeElement>.GenerateBitmap(BarcodeElement element, int width, int height)
    {
        return GenerateBitmap(element, width, height);
    }

    /// <summary>
    /// Generates a barcode bitmap with optional layout-computed dimensions.
    /// </summary>
    /// <param name="element">The barcode element configuration.</param>
    /// <param name="layoutWidth">Optional layout-computed width. Takes precedence over element.BarcodeWidth.</param>
    /// <param name="layoutHeight">Optional layout-computed height. Takes precedence over element.BarcodeHeight.</param>
    /// <returns>A bitmap containing the rendered barcode.</returns>
    /// <exception cref="ArgumentNullException">Thrown when element is null.</exception>
    /// <exception cref="ArgumentException">Thrown when element data is empty or dimensions are invalid.</exception>
    /// <exception cref="NotSupportedException">Thrown when the barcode format is not supported.</exception>
    public static SKBitmap Generate(BarcodeElement element, int? layoutWidth, int? layoutHeight)
    {
        ArgumentNullException.ThrowIfNull(element);

        if (string.IsNullOrEmpty(element.Data))
        {
            throw new ArgumentException("Barcode data cannot be empty.", nameof(element));
        }

        // Priority order: layout dimensions > element dimensions > defaults (200x80)
        var targetWidth = layoutWidth ?? element.BarcodeWidth ?? 200;
        var targetHeight = layoutHeight ?? element.BarcodeHeight ?? 80;

        if (targetWidth <= 0 || targetHeight <= 0)
        {
            throw new ArgumentException("Barcode dimensions must be positive.", nameof(element));
        }

        return element.Format switch
        {
            BarcodeFormat.Code128 => GenerateCode128(element, targetWidth, targetHeight),
            _ => throw new NotSupportedException($"Barcode format '{element.Format}' is not yet supported.")
        };
    }

    /// <summary>
    /// Generates a barcode bitmap at the specified dimensions.
    /// Delegates to the static <see cref="Generate(BarcodeElement, int?, int?)"/> method.
    /// </summary>
    /// <param name="element">The barcode element configuration.</param>
    /// <param name="width">The target width in pixels.</param>
    /// <param name="height">The target height in pixels.</param>
    /// <returns>A bitmap containing the rendered barcode.</returns>
    private static SKBitmap GenerateBitmap(BarcodeElement element, int width, int height)
    {
        return Generate(element, width, height);
    }

    /// <summary>
    /// Generates a Code 128 barcode.
    /// </summary>
    /// <param name="element">The barcode element configuration.</param>
    /// <param name="targetWidth">The target width for the barcode.</param>
    /// <param name="targetHeight">The target height for the barcode.</param>
    /// <returns>A bitmap containing the rendered Code 128 barcode.</returns>
    private static SKBitmap GenerateCode128(BarcodeElement element, int targetWidth, int targetHeight)
    {
        var pattern = Code128Encoding.BuildPattern(element.Data);

        // Calculate bar dimensions
        var totalUnits = pattern.Length;
        var barWidth = targetWidth / (float)totalUnits;
        var barcodeHeight = element.ShowText
            ? targetHeight - TextHeight - TextPadding
            : targetHeight;

        var bitmap = new SKBitmap(targetWidth, targetHeight);
        using var canvas = new SKCanvas(bitmap);

        var foreground = ColorParser.Parse(element.Foreground);
        var background = element.Background is not null
            ? ColorParser.Parse(element.Background)
            : SKColors.Transparent;

        // Fill background
        canvas.Clear(background);

        using var barPaint = new SKPaint
        {
            Color = foreground,
            IsAntialias = false,
            Style = SKPaintStyle.Fill
        };

        // Draw bars
        var x = 0f;
        foreach (var bit in pattern)
        {
            if (bit == '1')
            {
                canvas.DrawRect(x, 0, barWidth, barcodeHeight, barPaint);
            }
            x += barWidth;
        }

        // Draw text if enabled
        if (element.ShowText)
        {
            using var typeface = SKTypeface.FromFamilyName("Arial", SKFontStyle.Normal) ?? SKTypeface.Default;
            using var textFont = new SKFont(typeface, TextHeight - 2)
            {
                Subpixel = true
            };
            using var textPaint = new SKPaint
            {
                Color = foreground,
                IsAntialias = true
            };

            var textY = barcodeHeight + TextPadding + TextHeight - 2;
            canvas.DrawText(element.Data, targetWidth / 2f, textY, SKTextAlign.Center, textFont, textPaint);
        }

        return bitmap;
    }
}
