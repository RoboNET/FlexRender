namespace FlexRender.Charts;

/// <summary>
/// The kind of chart to render.
/// </summary>
public enum ChartType
{
    /// <summary>Vertical (or horizontal) bar chart.</summary>
    Bar,

    /// <summary>Line chart.</summary>
    Line,

    /// <summary>Filled area chart.</summary>
    Area,

    /// <summary>Pie chart.</summary>
    Pie,

    /// <summary>Donut (ring) chart.</summary>
    Donut,

    /// <summary>XY scatter plot ([x, y] tuples).</summary>
    Scatter,

    /// <summary>Bubble plot ([x, y, r] tuples; the third value sizes the bubble).</summary>
    Bubble,

    /// <summary>Single-value arc/dial gauge.</summary>
    Gauge,

    /// <summary>Single-value progress ring.</summary>
    Progress,

    /// <summary>Tiny inline line chart with no axes, labels, or legend.</summary>
    Sparkline,

    /// <summary>Grid of value-colored cells (2D data: rows = series, columns = categories).</summary>
    Heatmap,

    /// <summary>Radar (spider) chart: category spokes with one closed polygon per series.</summary>
    Radar
}

/// <summary>
/// Where the legend is placed relative to the plot area.
/// </summary>
public enum LegendPosition
{
    /// <summary>Above the plot area.</summary>
    Top,

    /// <summary>Below the plot area.</summary>
    Bottom,

    /// <summary>Left of the plot area.</summary>
    Left,

    /// <summary>Right of the plot area.</summary>
    Right,

    /// <summary>No legend.</summary>
    None
}

/// <summary>
/// How pie/donut slice labels are rendered.
/// </summary>
public enum PieLabelMode
{
    /// <summary>Show each slice's percentage of the total.</summary>
    Percent,

    /// <summary>Show each slice's raw value.</summary>
    Value,

    /// <summary>Show no slice labels.</summary>
    None
}
