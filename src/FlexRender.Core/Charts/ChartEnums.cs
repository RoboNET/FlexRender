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
    Donut
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
