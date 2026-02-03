using System.Globalization;

namespace FlexRender.Layout;

/// <summary>
/// Resolves font size specifications to absolute pixel values.
/// Provides unified font-size resolution for both layout and rendering.
/// Supports plain pixel values, px units, em units (relative to base font size),
/// and percentage units (relative to base font size, per CSS font-size semantics).
/// </summary>
public static class FontSizeResolver
{
    /// <summary>
    /// Resolves a font size specification to an absolute pixel value.
    /// </summary>
    /// <param name="size">The font size string (e.g., "16", "48px", "1.5em", "150%"). Null or empty means use default.</param>
    /// <param name="baseFontSize">The parent/inherited font size in pixels, used for em and percentage resolution and as fallback.</param>
    /// <returns>The resolved font size in pixels.</returns>
    public static float Resolve(string? size, float baseFontSize)
    {
        if (string.IsNullOrWhiteSpace(size))
            return baseFontSize;

        var value = size.Trim();

        // px units — absolute value
        if (value.EndsWith("px", StringComparison.OrdinalIgnoreCase))
        {
            var numStr = value[..^2];
            if (float.TryParse(numStr, NumberStyles.Float, CultureInfo.InvariantCulture, out var px))
                return px;
            return baseFontSize;
        }

        // em units — relative to base (parent) font size
        if (value.EndsWith("em", StringComparison.OrdinalIgnoreCase))
        {
            var numStr = value[..^2];
            if (float.TryParse(numStr, NumberStyles.Float, CultureInfo.InvariantCulture, out var em))
                return em * baseFontSize;
            return baseFontSize;
        }

        // Percentage — relative to base (parent) font size (CSS font-size semantics)
        if (value.EndsWith('%'))
        {
            var numStr = value[..^1];
            if (float.TryParse(numStr, NumberStyles.Float, CultureInfo.InvariantCulture, out var pct))
                return (pct / 100f) * baseFontSize;
            return baseFontSize;
        }

        // Plain number — treated as pixels
        if (float.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var pixels))
            return pixels;

        return baseFontSize;
    }
}
