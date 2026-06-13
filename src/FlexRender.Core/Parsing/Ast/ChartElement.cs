using System;
using System.Collections.Generic;
using FlexRender.Charts;

namespace FlexRender.Parsing.Ast;

/// <summary>
/// A chart element. Participates in flex layout as a leaf box with explicit width/height and is
/// drawn by the renderer into that box: grid, axes, series geometry, legend, title. The visual
/// styling comes entirely from the resolved <see cref="ChartTheme"/> and <see cref="ChartPalette"/>;
/// the template only supplies data and optional theme/palette words.
/// </summary>
public sealed class ChartElement : TemplateElement
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ChartElement"/> class.
    /// </summary>
    /// <param name="chartType">The chart type.</param>
    /// <param name="series">The data series (may be empty; an empty chart renders a "no data" placeholder).</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="series"/> is null.</exception>
    public ChartElement(ChartType chartType, IReadOnlyList<ChartSeries> series)
    {
        ArgumentNullException.ThrowIfNull(series);
        ChartType = chartType;
        Series = series;
    }

    /// <inheritdoc/>
    public override ElementType Type => ElementType.Chart;

    /// <summary>Gets the chart type.</summary>
    public ChartType ChartType { get; private set; }

    /// <summary>Gets the data series (resolved during expression resolution).</summary>
    public IReadOnlyList<ChartSeries> Series { get; private set; }

    /// <summary>Gets or sets the category labels (x-axis categories or pie slice labels).</summary>
    public IReadOnlyList<string> Categories { get; set; } = Array.Empty<string>();

    /// <summary>Gets or sets the resolved palette name or explicit color list. Null uses the theme default palette.</summary>
    public ChartPalette? Palette { get; set; }

    /// <summary>Gets or sets the resolved theme. Null falls back to the template/canvas theme then light.</summary>
    public ChartTheme? Theme { get; set; }

    /// <summary>Gets or sets the legend position.</summary>
    public LegendPosition Legend { get; set; } = LegendPosition.Bottom;

    /// <summary>Gets or sets the optional chart title.</summary>
    public string? Title { get; set; }

    /// <summary>Gets or sets whether bars are drawn horizontally (bar charts only).</summary>
    public bool Horizontal { get; set; }

    /// <summary>Gets or sets whether bars/areas are stacked (bar charts only in this phase).</summary>
    public bool Stacked { get; set; }

    /// <summary>Gets or sets whether line/area series use smoothed curves.</summary>
    public bool Smooth { get; set; }

    /// <summary>Gets or sets whether line/area series show point markers.</summary>
    public bool ShowPoints { get; set; }

    /// <summary>Gets or sets how pie/donut slice labels are rendered.</summary>
    public PieLabelMode PieLabels { get; set; } = PieLabelMode.Percent;

    /// <inheritdoc/>
    public override TemplateElement CloneWithSubstitution(Func<string?, string?> substitutor)
    {
        ArgumentNullException.ThrowIfNull(substitutor);

        var clone = new ChartElement(ChartType, Series)
        {
            Categories = Categories,
            Palette = Palette,
            Theme = Theme,
            Legend = Legend,
            Title = Title,
            Horizontal = Horizontal,
            Stacked = Stacked,
            Smooth = Smooth,
            ShowPoints = ShowPoints,
            PieLabels = PieLabels
        };
        CopyBasePropertiesTo(clone, substitutor);
        return clone;
    }

    /// <summary>
    /// Replaces the series collection. Used by expression resolution to install resolved data.
    /// </summary>
    /// <param name="series">The resolved series.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="series"/> is null.</exception>
    internal void SetSeries(IReadOnlyList<ChartSeries> series)
    {
        ArgumentNullException.ThrowIfNull(series);
        Series = series;
    }
}
