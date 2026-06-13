using System;

namespace FlexRender.Charts;

/// <summary>
/// An axis-aligned plot rectangle in chart-local coordinates (origin at the chart box top-left).
/// </summary>
/// <param name="Left">The left edge.</param>
/// <param name="Top">The top edge.</param>
/// <param name="Right">The right edge.</param>
/// <param name="Bottom">The bottom edge.</param>
public readonly record struct PlotArea(float Left, float Top, float Right, float Bottom)
{
    /// <summary>Gets the plot width.</summary>
    public float Width => Right - Left;

    /// <summary>Gets the plot height.</summary>
    public float Height => Bottom - Top;
}

/// <summary>
/// Pure, renderer-agnostic computation of the chart plot area by subtracting reserved bands
/// for the title, legend, and axis label gutters.
/// </summary>
public static class ChartLayout
{
    /// <summary>
    /// Computes the inner plot rectangle.
    /// </summary>
    /// <param name="width">The chart box width.</param>
    /// <param name="height">The chart box height.</param>
    /// <param name="hasTitle">Whether a title band is reserved at the top.</param>
    /// <param name="legend">The legend position (reserves a band on the corresponding side).</param>
    /// <param name="axisGutterLeft">The left gutter reserved for y-axis labels.</param>
    /// <param name="axisGutterBottom">The bottom gutter reserved for x-axis labels.</param>
    /// <param name="titleHeight">The title band height when <paramref name="hasTitle"/> is true.</param>
    /// <param name="legendExtent">The legend band size (height for top/bottom, width for left/right).</param>
    /// <returns>The computed <see cref="PlotArea"/>, never inverted.</returns>
    public static PlotArea ComputePlotArea(
        float width,
        float height,
        bool hasTitle,
        LegendPosition legend,
        float axisGutterLeft,
        float axisGutterBottom,
        float titleHeight,
        float legendExtent)
    {
        var left = axisGutterLeft;
        var top = hasTitle ? titleHeight : 0f;
        // Mirror the left gutter on the right so series geometry and edge labels are not clipped.
        var right = width - axisGutterLeft;
        var bottom = height - axisGutterBottom;

        switch (legend)
        {
            case LegendPosition.Top:
                top += legendExtent;
                break;
            case LegendPosition.Bottom:
                bottom -= legendExtent;
                break;
            case LegendPosition.Left:
                left += legendExtent;
                break;
            case LegendPosition.Right:
                right -= legendExtent;
                break;
            case LegendPosition.None:
            default:
                break;
        }

        // Guard against inversion on tiny boxes.
        right = Math.Max(right, left);
        bottom = Math.Max(bottom, top);

        return new PlotArea(left, top, right, bottom);
    }
}
