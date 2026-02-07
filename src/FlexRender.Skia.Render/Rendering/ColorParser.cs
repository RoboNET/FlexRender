using System.Globalization;
using SkiaSharp;

namespace FlexRender.Rendering;

/// <summary>
/// Parses color strings into SKColor values.
/// Supports hex formats (#RGB, #RGBA, #RRGGBB, #AARRGGBB) and
/// CSS function formats (rgb(r, g, b), rgba(r, g, b, a)).
/// </summary>
public static class ColorParser
{
    /// <summary>
    /// Parses a color string to SKColor.
    /// Supports formats: #RGB, #RGBA, #RRGGBB, #AARRGGBB, rgb(r,g,b), rgba(r,g,b,a).
    /// </summary>
    /// <param name="hex">The color string (e.g., "#ff0000" or "rgba(255, 0, 0, 0.5)").</param>
    /// <returns>The parsed color, or Black if parsing fails.</returns>
    public static SKColor Parse(string? hex)
    {
        if (TryParse(hex, out var color))
            return color;
        return SKColors.Black;
    }

    /// <summary>
    /// Tries to parse a color string to SKColor.
    /// Supports hex formats (#RGB, #RGBA, #RRGGBB, #AARRGGBB) and
    /// CSS function formats (rgb(r, g, b), rgba(r, g, b, a)).
    /// </summary>
    /// <param name="hex">The color string.</param>
    /// <param name="color">The parsed color if successful.</param>
    /// <returns>True if parsing succeeded; otherwise, false.</returns>
    public static bool TryParse(string? hex, out SKColor color)
    {
        color = default;

        if (string.IsNullOrEmpty(hex))
            return false;

        if (hex.StartsWith("rgba(", StringComparison.OrdinalIgnoreCase) ||
            hex.StartsWith("rgb(", StringComparison.OrdinalIgnoreCase))
        {
            return TryParseRgbFunction(hex, out color);
        }

        if (!hex.StartsWith('#'))
            return false;

        var hexValue = hex[1..];

        try
        {
            switch (hexValue.Length)
            {
                case 3: // #RGB
                    color = new SKColor(
                        (byte)(ParseHexDigit(hexValue[0]) * 17),
                        (byte)(ParseHexDigit(hexValue[1]) * 17),
                        (byte)(ParseHexDigit(hexValue[2]) * 17));
                    return true;

                case 4: // #ARGB
                    color = new SKColor(
                        (byte)(ParseHexDigit(hexValue[1]) * 17),
                        (byte)(ParseHexDigit(hexValue[2]) * 17),
                        (byte)(ParseHexDigit(hexValue[3]) * 17),
                        (byte)(ParseHexDigit(hexValue[0]) * 17));
                    return true;

                case 6: // #RRGGBB
                    color = new SKColor(
                        Convert.ToByte(hexValue[..2], 16),
                        Convert.ToByte(hexValue[2..4], 16),
                        Convert.ToByte(hexValue[4..6], 16));
                    return true;

                case 8: // #AARRGGBB
                    color = new SKColor(
                        Convert.ToByte(hexValue[2..4], 16),
                        Convert.ToByte(hexValue[4..6], 16),
                        Convert.ToByte(hexValue[6..8], 16),
                        Convert.ToByte(hexValue[..2], 16));
                    return true;

                default:
                    return false;
            }
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Tries to parse an rgb() or rgba() CSS color function string.
    /// </summary>
    private static bool TryParseRgbFunction(string input, out SKColor color)
    {
        color = default;

        // Strip the function name and parentheses: "rgba(..." -> "..."
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

        if (parts.Length < 3 || parts.Length > 4)
            return false;

        if (!int.TryParse(parts[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out var r) ||
            !int.TryParse(parts[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out var g) ||
            !int.TryParse(parts[2], NumberStyles.Integer, CultureInfo.InvariantCulture, out var b))
            return false;

        if (r < 0 || r > 255 || g < 0 || g > 255 || b < 0 || b > 255)
            return false;

        var alpha = 1.0f;
        if (parts.Length == 4)
        {
            if (!float.TryParse(parts[3], NumberStyles.Float, CultureInfo.InvariantCulture, out alpha))
                return false;
            if (alpha < 0.0f || alpha > 1.0f)
                return false;
        }

        var a = (byte)Math.Round(alpha * 255);
        color = new SKColor((byte)r, (byte)g, (byte)b, a);
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
