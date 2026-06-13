using System;
using System.Collections.Generic;
using FlexRender.Charts;
using FlexRender.TemplateEngine;

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

    /// <summary>Gets or sets the indicator value for gauge/progress charts. Null renders a "no data" placeholder.</summary>
    public double? Value { get; set; }

    /// <summary>Gets or sets the indicator maximum for gauge/progress charts. Null defaults to 100.</summary>
    public double? Max { get; set; }

    /// <summary>Gets or sets the centered caption for gauge/progress charts (distinct from <see cref="Title"/>).</summary>
    public string? ValueLabel { get; set; }

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
            PieLabels = PieLabels,
            Value = Value,
            Max = Max,
            ValueLabel = ValueLabel
        };
        CopyBasePropertiesTo(clone, substitutor);
        return clone;
    }

    /// <inheritdoc/>
    public override void ResolveExpressions(Func<string, ObjectValue, string> resolver, ObjectValue data)
    {
        ArgumentNullException.ThrowIfNull(resolver);
        ArgumentNullException.ThrowIfNull(data);

        base.ResolveExpressions(resolver, data);

        var anyBound = false;
        foreach (var series in Series)
        {
            if (series.DataExpression is not null)
            {
                anyBound = true;
                break;
            }
        }

        if (!anyBound)
        {
            return;
        }

        var context = new TemplateContext(data);
        var resolved = new List<ChartSeries>(Series.Count);

        foreach (var series in Series)
        {
            if (series.DataExpression is null)
            {
                resolved.Add(series);
                continue;
            }

            var path = StripBraces(series.DataExpression);
            var value = ExpressionEvaluator.Resolve(path, context);
            resolved.Add(series.WithData(ConvertToDoubles(value, series.Label)));
        }

        SetSeries(resolved);
    }

    /// <summary>
    /// Removes surrounding <c>{{ }}</c> braces and whitespace from a data expression, yielding the
    /// inner path for <see cref="ExpressionEvaluator.Resolve"/>. Non-wrapped input is returned trimmed.
    /// </summary>
    /// <param name="expression">The raw expression (e.g. "{{ sales }}").</param>
    /// <returns>The inner path (e.g. "sales").</returns>
    private static string StripBraces(string expression)
    {
        var span = expression.AsSpan().Trim();
        if (span.StartsWith("{{") && span.EndsWith("}}"))
        {
            span = span[2..^2].Trim();
        }

        return span.ToString();
    }

    /// <summary>
    /// Converts a resolved <see cref="ArrayValue"/> of numbers to a double array. A non-array
    /// (e.g. a missing path resolving to <see cref="NullValue"/>) yields an empty array; a
    /// non-numeric element raises a clear template error naming the series.
    /// </summary>
    /// <param name="value">The resolved template value.</param>
    /// <param name="label">The series label, used in error messages.</param>
    /// <returns>The numeric values (possibly empty).</returns>
    /// <exception cref="TemplateEngineException">Thrown when an array element is not numeric.</exception>
    private static double[] ConvertToDoubles(TemplateValue value, string? label)
    {
        if (value is not ArrayValue array)
        {
            return Array.Empty<double>();
        }

        var result = new double[array.Count];
        for (var i = 0; i < array.Count; i++)
        {
            if (array[i] is NumberValue number)
            {
                result[i] = (double)number.Value;
            }
            else
            {
                throw new TemplateEngineException(
                    $"Chart series '{label ?? "(unlabeled)"}' data element at index {i} is not numeric " +
                    $"(got {array[i].GetType().Name}). Series data must resolve to an array of numbers.");
            }
        }

        return result;
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
