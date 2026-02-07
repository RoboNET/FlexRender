using System.Globalization;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace FlexRender.ImageSharp.Rendering;

/// <summary>
/// Parses color strings into ImageSharp Color values.
/// Supports hex formats (#RGB, #ARGB, #RRGGBB, #AARRGGBB) and
/// CSS function formats (rgb(r, g, b), rgba(r, g, b, a)).
/// </summary>
internal static class ImageSharpColorParser
{
    /// <summary>
    /// Parses a color string to an ImageSharp Color.
    /// Returns Black if parsing fails.
    /// </summary>
    /// <param name="value">The color string (e.g., "#ff0000" or "rgba(255, 0, 0, 0.5)").</param>
    /// <returns>The parsed color, or Black if parsing fails.</returns>
    public static Color Parse(string? value)
    {
        if (TryParse(value, out var color))
            return color;
        return Color.Black;
    }

    /// <summary>
    /// Tries to parse a color string to an ImageSharp Color.
    /// </summary>
    /// <param name="value">The color string.</param>
    /// <param name="color">The parsed color if successful.</param>
    /// <returns>True if parsing succeeded; otherwise, false.</returns>
    public static bool TryParse(string? value, out Color color)
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
