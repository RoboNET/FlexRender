using System;
using SkiaSharp;

namespace FlexRender.Rendering;

/// <summary>
/// Maps a numeric value within a data range to a color interpolated channel-by-channel between a
/// "low" and a "high" color. Pure and deterministic (AOT-safe, no allocations, no randomness),
/// suitable for snapshot-stable heatmap cell coloring.
/// </summary>
internal static class HeatmapColorScale
{
    /// <summary>
    /// Maps <paramref name="value"/> in <paramref name="min"/>..<paramref name="max"/> to a color
    /// linearly interpolated between <paramref name="low"/> (at <paramref name="min"/>) and
    /// <paramref name="high"/> (at <paramref name="max"/>). The interpolation parameter is clamped
    /// to <c>[0, 1]</c>; a degenerate range (<paramref name="min"/> == <paramref name="max"/>)
    /// yields <paramref name="high"/>.
    /// </summary>
    /// <param name="value">The cell value.</param>
    /// <param name="min">The data minimum (maps to <paramref name="low"/>).</param>
    /// <param name="max">The data maximum (maps to <paramref name="high"/>).</param>
    /// <param name="low">The color at the low end of the range.</param>
    /// <param name="high">The color at the high end of the range.</param>
    /// <returns>The interpolated, fully opaque color.</returns>
    public static SKColor Map(double value, double min, double max, SKColor low, SKColor high)
    {
        var span = max - min;
        var t = span <= 0d ? 1d : Math.Clamp((value - min) / span, 0d, 1d);

        var r = Lerp(low.Red, high.Red, t);
        var g = Lerp(low.Green, high.Green, t);
        var b = Lerp(low.Blue, high.Blue, t);
        return new SKColor(r, g, b);
    }

    /// <summary>Linearly interpolates a single 0..255 channel and rounds to the nearest byte.</summary>
    /// <param name="a">The channel value at <c>t = 0</c>.</param>
    /// <param name="b">The channel value at <c>t = 1</c>.</param>
    /// <param name="t">The interpolation parameter in <c>[0, 1]</c>.</param>
    /// <returns>The interpolated channel as a byte.</returns>
    private static byte Lerp(byte a, byte b, double t)
    {
        var v = a + ((b - a) * t);
        return (byte)Math.Clamp(Math.Round(v), 0d, 255d);
    }
}
