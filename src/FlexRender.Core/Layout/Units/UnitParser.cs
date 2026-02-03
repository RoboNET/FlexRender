using System.Globalization;

namespace FlexRender.Layout.Units;

/// <summary>
/// Parses string values into Unit instances.
/// </summary>
public static class UnitParser
{
    /// <summary>
    /// Parses a string value into a Unit.
    /// </summary>
    /// <param name="value">The string to parse (e.g., "100", "50%", "1.5em", "auto").</param>
    /// <returns>The parsed Unit.</returns>
    public static Unit Parse(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return Unit.Auto;

        value = value.Trim();

        if (value.Equals("auto", StringComparison.OrdinalIgnoreCase))
            return Unit.Auto;

        if (value.EndsWith('%'))
        {
            if (float.TryParse(value.AsSpan(0, value.Length - 1), NumberStyles.Float, CultureInfo.InvariantCulture, out var percent))
                return Unit.Percent(percent);
        }

        if (value.EndsWith("em", StringComparison.OrdinalIgnoreCase))
        {
            if (float.TryParse(value.AsSpan(0, value.Length - 2), NumberStyles.Float, CultureInfo.InvariantCulture, out var em))
                return Unit.Em(em);
        }

        if (value.EndsWith("px", StringComparison.OrdinalIgnoreCase))
        {
            if (float.TryParse(value.AsSpan(0, value.Length - 2), NumberStyles.Float, CultureInfo.InvariantCulture, out var px))
                return Unit.Pixels(px);
        }

        // Default: treat as pixels
        if (float.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var pixels))
            return Unit.Pixels(pixels);

        return Unit.Auto;
    }

    /// <summary>
    /// Tries to parse a string value into a Unit.
    /// </summary>
    /// <param name="value">The string to parse.</param>
    /// <param name="unit">The parsed unit if successful.</param>
    /// <returns>True if parsing succeeded; otherwise false.</returns>
    public static bool TryParse(string? value, out Unit unit)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            unit = Unit.Auto;
            return false;
        }

        value = value.Trim();

        if (value.Equals("auto", StringComparison.OrdinalIgnoreCase))
        {
            unit = Unit.Auto;
            return true;
        }

        if (value.EndsWith('%'))
        {
            if (float.TryParse(value.AsSpan(0, value.Length - 1), NumberStyles.Float, CultureInfo.InvariantCulture, out var percent))
            {
                unit = Unit.Percent(percent);
                return true;
            }
        }

        if (value.EndsWith("em", StringComparison.OrdinalIgnoreCase))
        {
            if (float.TryParse(value.AsSpan(0, value.Length - 2), NumberStyles.Float, CultureInfo.InvariantCulture, out var em))
            {
                unit = Unit.Em(em);
                return true;
            }
        }

        if (value.EndsWith("px", StringComparison.OrdinalIgnoreCase))
        {
            if (float.TryParse(value.AsSpan(0, value.Length - 2), NumberStyles.Float, CultureInfo.InvariantCulture, out var px))
            {
                unit = Unit.Pixels(px);
                return true;
            }
        }

        if (float.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var pixels))
        {
            unit = Unit.Pixels(pixels);
            return true;
        }

        unit = Unit.Auto;
        return false;
    }
}
