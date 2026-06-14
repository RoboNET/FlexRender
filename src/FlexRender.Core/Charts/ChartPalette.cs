using System;
using System.Collections.Generic;

namespace FlexRender.Charts;

/// <summary>
/// An ordered ramp of hex series colors. Colors cycle when there are more series than colors.
/// </summary>
public sealed class ChartPalette
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ChartPalette"/> class.
    /// </summary>
    /// <param name="colors">The ordered hex color strings. Must be non-null and non-empty.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="colors"/> is null.</exception>
    /// <exception cref="ArgumentException">Thrown when <paramref name="colors"/> is empty.</exception>
    public ChartPalette(IReadOnlyList<string> colors)
    {
        ArgumentNullException.ThrowIfNull(colors);
        if (colors.Count == 0)
            throw new ArgumentException("A palette must contain at least one color.", nameof(colors));
        Colors = colors;
    }

    /// <summary>
    /// Gets the ordered hex color strings.
    /// </summary>
    public IReadOnlyList<string> Colors { get; }

    /// <summary>
    /// Returns the color for a series index, cycling through <see cref="Colors"/> when the
    /// index exceeds the palette size.
    /// </summary>
    /// <param name="index">The zero-based series index (must be non-negative).</param>
    /// <returns>The hex color string for the series.</returns>
    public string ColorAt(int index)
    {
        var i = index % Colors.Count;
        if (i < 0)
            i += Colors.Count;
        return Colors[i];
    }
}
