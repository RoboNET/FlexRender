namespace FlexRender.Charts;

/// <summary>
/// A single XY data point with an optional radius, used by scatter ([x, y]) and bubble
/// ([x, y, r]) series. Renderer-agnostic; <see cref="R"/> is zero for scatter points.
/// </summary>
/// <param name="X">The X coordinate in data space.</param>
/// <param name="Y">The Y coordinate in data space.</param>
/// <param name="R">The bubble radius in data space (zero for scatter points).</param>
public readonly record struct ChartPoint(double X, double Y, double R)
{
    /// <summary>
    /// Creates a point with no radius (scatter).
    /// </summary>
    /// <param name="x">The X coordinate.</param>
    /// <param name="y">The Y coordinate.</param>
    /// <returns>A <see cref="ChartPoint"/> with <see cref="R"/> set to zero.</returns>
    public static ChartPoint Xy(double x, double y) => new(x, y, 0d);
}
