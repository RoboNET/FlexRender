using System.Globalization;
using FlexRender.Barcode.Code128;
using FlexRender.Parsing.Ast;
using FlexRender.Providers;
using SixLabors.Fonts;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Drawing.Processing;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

namespace FlexRender.Barcode.ImageSharp.Providers;

/// <summary>
/// Provides barcode generation for ImageSharp rendering.
/// </summary>
public sealed class BarcodeImageSharpProvider : IImageSharpContentProvider<BarcodeElement>
{
    private const int TextHeight = 16;
    private const int TextPadding = 4;


    /// <summary>
    /// Generates a barcode image with the specified dimensions.
    /// </summary>
    /// <param name="element">The barcode element configuration.</param>
    /// <param name="width">The target width in pixels.</param>
    /// <param name="height">The target height in pixels.</param>
    /// <returns>An ImageSharp image containing the barcode.</returns>
    /// <exception cref="ArgumentNullException">Thrown when element is null.</exception>
    /// <exception cref="ArgumentException">Thrown when element data is empty or dimensions are invalid.</exception>
    /// <exception cref="NotSupportedException">Thrown when the barcode format is not supported.</exception>
    public Image<Rgba32> GenerateImage(BarcodeElement element, int width, int height)
    {
        ArgumentNullException.ThrowIfNull(element);

        if (string.IsNullOrEmpty(element.Data))
        {
            throw new ArgumentException("Barcode data cannot be empty.", nameof(element));
        }

        if (width <= 0 || height <= 0)
        {
            throw new ArgumentException("Barcode dimensions must be positive.");
        }

        return element.Format switch
        {
            BarcodeFormat.Code128 => GenerateCode128(element, width, height),
            _ => throw new NotSupportedException($"Barcode format '{element.Format}' is not yet supported.")
        };
    }

    private static Image<Rgba32> GenerateCode128(BarcodeElement element, int targetWidth, int targetHeight)
    {
        var pattern = Code128Encoding.BuildPattern(element.Data);

        var totalUnits = pattern.Length;
        var barWidth = targetWidth / (float)totalUnits;
        var barcodeHeight = element.ShowText
            ? targetHeight - TextHeight - TextPadding
            : targetHeight;

        var foreground = ParseColor(element.Foreground, Color.Black);
        var background = element.Background is not null
            ? ParseColor(element.Background, Color.Transparent)
            : Color.Transparent;

        var image = new Image<Rgba32>(targetWidth, targetHeight);
        image.Mutate(ctx =>
        {
            ctx.Fill(background);

            var x = 0f;
            foreach (var bit in pattern)
            {
                if (bit == '1')
                {
                    ctx.Fill(foreground, new RectangleF(x, 0, barWidth, barcodeHeight));
                }
                x += barWidth;
            }

            if (element.ShowText)
            {
                var font = ResolveFont(TextHeight - 2);
                var textY = barcodeHeight + TextPadding;
                var options = new RichTextOptions(font)
                {
                    Origin = new PointF(targetWidth / 2f, textY),
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Top
                };
                ctx.DrawText(options, element.Data, foreground);
            }
        });

        return image;
    }

    private static Font ResolveFont(float size)
    {
        if (SystemFonts.TryGet("Arial", out var arialFamily))
            return arialFamily.CreateFont(size, FontStyle.Regular);

        if (SystemFonts.TryGet("Liberation Sans", out var liberationFamily))
            return liberationFamily.CreateFont(size, FontStyle.Regular);

        foreach (var family in SystemFonts.Families)
        {
            return family.CreateFont(size, FontStyle.Regular);
        }

        throw new InvalidOperationException("No system fonts are available for barcode text rendering.");
    }

    private static Color ParseColor(string value, Color fallback)
    {
        return TryParseColor(value, out var color) ? color : fallback;
    }

    private static bool TryParseColor(string? value, out Color color)
    {
        color = default;

        if (string.IsNullOrEmpty(value))
            return false;

        if (value.StartsWith("rgba(", StringComparison.OrdinalIgnoreCase) ||
            value.StartsWith("rgb(", StringComparison.OrdinalIgnoreCase))
        {
            return TryParseRgbFunction(value, out color);
        }

        if (!value.StartsWith('#'))
            return false;

        var hexValue = value[1..];

        try
        {
            switch (hexValue.Length)
            {
                case 3: // #RGB
                    color = Color.FromPixel(new Rgba32(
                        (byte)(ParseHexDigit(hexValue[0]) * 17),
                        (byte)(ParseHexDigit(hexValue[1]) * 17),
                        (byte)(ParseHexDigit(hexValue[2]) * 17),
                        255));
                    return true;

                case 4: // #ARGB
                    color = Color.FromPixel(new Rgba32(
                        (byte)(ParseHexDigit(hexValue[1]) * 17),
                        (byte)(ParseHexDigit(hexValue[2]) * 17),
                        (byte)(ParseHexDigit(hexValue[3]) * 17),
                        (byte)(ParseHexDigit(hexValue[0]) * 17)));
                    return true;

                case 6: // #RRGGBB
                    color = Color.FromPixel(new Rgba32(
                        Convert.ToByte(hexValue[..2], 16),
                        Convert.ToByte(hexValue[2..4], 16),
                        Convert.ToByte(hexValue[4..6], 16),
                        255));
                    return true;

                case 8: // #AARRGGBB
                    color = Color.FromPixel(new Rgba32(
                        Convert.ToByte(hexValue[2..4], 16),
                        Convert.ToByte(hexValue[4..6], 16),
                        Convert.ToByte(hexValue[6..8], 16),
                        Convert.ToByte(hexValue[..2], 16)));
                    return true;

                default:
                    return false;
            }
        }
        catch (FormatException)
        {
            return false;
        }
        catch (OverflowException)
        {
            return false;
        }
    }

    private static bool TryParseRgbFunction(string input, out Color color)
    {
        color = default;

        var openParen = input.IndexOf('(');
        var closeParen = input.IndexOf(')');
        if (openParen < 0 || closeParen < 0 || closeParen <= openParen + 1)
            return false;

        if (closeParen != input.Length - 1)
        {
            var trailing = input[(closeParen + 1)..].Trim();
            if (trailing.Length > 0)
                return false;
        }

        var inner = input[(openParen + 1)..closeParen];
        var parts = inner.Split(',', StringSplitOptions.TrimEntries);

        if (parts.Length is < 3 or > 4)
            return false;

        if (!int.TryParse(parts[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out var r) ||
            !int.TryParse(parts[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out var g) ||
            !int.TryParse(parts[2], NumberStyles.Integer, CultureInfo.InvariantCulture, out var b))
            return false;

        if (r is < 0 or > 255 || g is < 0 or > 255 || b is < 0 or > 255)
            return false;

        var alpha = 1.0f;
        if (parts.Length == 4)
        {
            if (!float.TryParse(parts[3], NumberStyles.Float, CultureInfo.InvariantCulture, out alpha))
                return false;
            if (alpha is < 0.0f or > 1.0f)
                return false;
        }

        var a = (byte)Math.Round(alpha * 255);
        color = Color.FromPixel(new Rgba32((byte)r, (byte)g, (byte)b, a));
        return true;
    }

    private static int ParseHexDigit(char c)
    {
        return c switch
        {
            >= '0' and <= '9' => c - '0',
            >= 'a' and <= 'f' => c - 'a' + 10,
            >= 'A' and <= 'F' => c - 'A' + 10,
            _ => throw new FormatException($"Invalid hex digit: {c}")
        };
    }
}
