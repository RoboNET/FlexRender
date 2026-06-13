namespace FlexRender.Charts;

/// <summary>
/// Visual styling preset for a chart: colors, label/title sizes, line widths and bar rounding.
/// Renderer-agnostic, immutable static data.
/// </summary>
/// <param name="BackgroundColor">The chart background fill (hex). Empty means transparent.</param>
/// <param name="GridColor">The grid-line color (hex).</param>
/// <param name="AxisColor">The axis-line color (hex).</param>
/// <param name="LabelColor">The axis/legend/slice label text color (hex).</param>
/// <param name="TitleColor">The title text color (hex).</param>
/// <param name="LabelSize">The label font size in pixels.</param>
/// <param name="TitleSize">The title font size in pixels.</param>
/// <param name="LineWidth">The series line width in pixels (line/area charts).</param>
/// <param name="BarCornerRadius">The corner radius applied to bars in pixels.</param>
public sealed record ChartTheme(
    string BackgroundColor,
    string GridColor,
    string AxisColor,
    string LabelColor,
    string TitleColor,
    float LabelSize,
    float TitleSize,
    float LineWidth,
    float BarCornerRadius);
