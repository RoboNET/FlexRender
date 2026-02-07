using System.Globalization;
using SkiaSharp;

namespace FlexRender.Rendering;

/// <summary>
/// Parsed box shadow values.
/// </summary>
/// <param name="OffsetX">Horizontal offset in pixels.</param>
/// <param name="OffsetY">Vertical offset in pixels.</param>
/// <param name="BlurRadius">Blur radius in pixels (must be >= 0).</param>
/// <param name="Color">Shadow color.</param>
public sealed record BoxShadowValues(float OffsetX, float OffsetY, float BlurRadius, SKColor Color);

/// <summary>
/// Parses box-shadow strings in the format "offsetX offsetY blurRadius color".
/// </summary>
/// <remarks>
/// <para>Examples:</para>
/// <list type="bullet">
///   <item><c>"4 4 8 rgba(0,0,0,0.3)"</c> -- offset 4x4, blur 8, semi-transparent black</item>
///   <item><c>"2 2 4 #333333"</c> -- offset 2x2, blur 4, dark gray</item>
///   <item><c>"0 2 6 #00000080"</c> -- no horizontal offset, 2px down, blur 6, 50% black</item>
/// </list>
/// </remarks>
public static class BoxShadowParser
{
    /// <summary>
    /// Tries to parse a box-shadow string into <see cref="BoxShadowValues"/>.
    /// </summary>
    /// <param name="value">The box-shadow string to parse.</param>
    /// <param name="shadow">The parsed shadow values if successful.</param>
    /// <returns>True if parsing succeeded; otherwise, false.</returns>
    public static bool TryParse(string? value, out BoxShadowValues? shadow)
    {
        shadow = null;

        if (string.IsNullOrWhiteSpace(value))
            return false;

        var trimmed = value.AsSpan().Trim();

        // We need at least 3 numeric tokens and a color.
        // Strategy: parse the first 3 tokens as numbers, then the rest is color.
        // Tokens are space-separated, but color may contain spaces (e.g., "rgba(0, 0, 0, 0.3)").

        var index = 0;

        if (!TryParseNextFloat(trimmed, ref index, out var offsetX))
            return false;

        if (!TryParseNextFloat(trimmed, ref index, out var offsetY))
            return false;

        if (!TryParseNextFloat(trimmed, ref index, out var blurRadius))
            return false;

        // Blur radius must be non-negative
        if (blurRadius < 0f)
            return false;

        // Skip whitespace to get to color
        while (index < trimmed.Length && char.IsWhiteSpace(trimmed[index]))
            index++;

        if (index >= trimmed.Length)
            return false;

        // Rest of the string is the color
        var colorStr = trimmed[index..].ToString();

        if (!ColorParser.TryParse(colorStr, out var color))
            return false;

        shadow = new BoxShadowValues(offsetX, offsetY, blurRadius, color);
        return true;
    }

    /// <summary>
    /// Parses the next float token from the span, advancing the index past it.
    /// </summary>
    private static bool TryParseNextFloat(ReadOnlySpan<char> span, ref int index, out float result)
    {
        result = 0f;

        // Skip leading whitespace
        while (index < span.Length && char.IsWhiteSpace(span[index]))
            index++;

        if (index >= span.Length)
            return false;

        // Find end of number token (may include '-', '.', digits)
        var start = index;
        if (index < span.Length && (span[index] == '-' || span[index] == '+'))
            index++;

        while (index < span.Length && (char.IsDigit(span[index]) || span[index] == '.'))
            index++;

        if (index == start)
            return false;

        var numberSpan = span[start..index];
        return float.TryParse(numberSpan, NumberStyles.Float, CultureInfo.InvariantCulture, out result);
    }
}
