using System.Globalization;
using FlexRender.Parsing.Ast;
using FlexRender.Providers;
using FlexRender.QrCode;
using QRCoder;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Drawing.Processing;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

namespace FlexRender.QrCode.ImageSharp.Providers;

/// <summary>
/// Provides QR code generation for ImageSharp rendering.
/// </summary>
public sealed class QrImageSharpProvider : IImageSharpContentProvider<QrElement>
{
    /// <summary>
    /// Generates a QR code image with the specified dimensions.
    /// </summary>
    /// <param name="element">The QR element configuration.</param>
    /// <param name="width">The target width in pixels.</param>
    /// <param name="height">The target height in pixels.</param>
    /// <returns>An ImageSharp image containing the QR code.</returns>
    /// <exception cref="ArgumentNullException">Thrown when element is null.</exception>
    /// <exception cref="ArgumentException">Thrown when element data is empty or size is invalid.</exception>
    public Image<Rgba32> GenerateImage(QrElement element, int width, int height)
    {
        ArgumentNullException.ThrowIfNull(element);

        if (string.IsNullOrEmpty(element.Data))
        {
            throw new ArgumentException("QR code data cannot be empty.", nameof(element));
        }

        if (width <= 0 || height <= 0)
        {
            throw new ArgumentException("QR code size must be positive.");
        }

        var eccLevel = MapEccLevel(element.ErrorCorrection);
        QrDataValidator.ValidateDataCapacity(element);

        using var qrGenerator = new QRCodeGenerator();
        using var qrCodeData = qrGenerator.CreateQrCode(element.Data, eccLevel);

        var moduleCount = qrCodeData.ModuleMatrix.Count;

        var foreground = ParseColor(element.Foreground, Color.Black);
        var background = element.Background is not null
            ? ParseColor(element.Background, Color.Transparent)
            : Color.Transparent;

        var image = new Image<Rgba32>(width, height);
        image.Mutate(ctx =>
        {
            ctx.Fill(background);

            for (var y = 0; y < moduleCount; y++)
            {
                for (var x = 0; x < moduleCount; x++)
                {
                    if (!qrCodeData.ModuleMatrix[y][x])
                    {
                        continue;
                    }

                    // Use integer pixel snapping to eliminate sub-pixel gaps between modules.
                    // Computing start/end from rounded grid positions ensures adjacent modules
                    // share exact pixel boundaries with no floating-point rounding seams.
                    var x1 = (int)Math.Round(x * (float)width / moduleCount);
                    var y1 = (int)Math.Round(y * (float)height / moduleCount);
                    var x2 = (int)Math.Round((x + 1) * (float)width / moduleCount);
                    var y2 = (int)Math.Round((y + 1) * (float)height / moduleCount);

                    var rect = new RectangleF(x1, y1, x2 - x1, y2 - y1);
                    ctx.Fill(foreground, rect);
                }
            }
        });

        return image;
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
