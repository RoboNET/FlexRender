using System.Globalization;

namespace FlexRender.Layout;

/// <summary>
/// Resolves line height values from string specifications.
/// Supports plain multiplier ("1.8"), pixel units ("24px"), and em units ("2em").
/// </summary>
public static class LineHeightResolver
{
    /// <summary>
    /// Resolves a line height specification to an absolute pixel value.
    /// </summary>
    /// <param name="lineHeight">The line height string (e.g., "1.8", "24px", "2em"). Null or empty means use default.</param>
    /// <param name="fontSize">The element's computed font size in pixels, used for multiplier and em resolution.</param>
    /// <param name="defaultLineHeight">The fallback value when lineHeight is empty or unparseable.</param>
    /// <returns>The resolved line height in pixels.</returns>
    public static float Resolve(string? lineHeight, float fontSize, float defaultLineHeight)
    {
        if (string.IsNullOrEmpty(lineHeight))
            return defaultLineHeight;

        var value = lineHeight.Trim();

        // px units -- absolute value
        if (value.EndsWith("px", StringComparison.OrdinalIgnoreCase))
        {
            var numStr = value[..^2];
            if (float.TryParse(numStr, NumberStyles.Float, CultureInfo.InvariantCulture, out var px))
                return Math.Max(0f, px);
            return defaultLineHeight;
        }

        // em units -- relative to element's own fontSize
        if (value.EndsWith("em", StringComparison.OrdinalIgnoreCase))
        {
            var numStr = value[..^2];
            if (float.TryParse(numStr, NumberStyles.Float, CultureInfo.InvariantCulture, out var em))
                return Math.Max(0f, em * fontSize);
            return defaultLineHeight;
        }

        // Plain number -- multiplier of fontSize
        if (float.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var multiplier))
            return Math.Max(0f, multiplier * fontSize);

        return defaultLineHeight;
    }
}
