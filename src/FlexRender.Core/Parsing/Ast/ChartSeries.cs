using System;
using System.Collections.Generic;
using FlexRender.Charts;

namespace FlexRender.Parsing.Ast;

/// <summary>
/// A single chart data series: a label plus its numeric values. The values may come from an
/// inline YAML array (resolved at parse time) or from a template expression (resolved against
/// the data context during <see cref="ChartElement"/> expression resolution).
/// </summary>
public sealed class ChartSeries
{
    private static readonly IReadOnlyList<double> Empty = Array.Empty<double>();
    private static readonly IReadOnlyList<ChartPoint> EmptyPoints = Array.Empty<ChartPoint>();

    private ChartSeries(string? label, string? dataExpression, IReadOnlyList<double> data, IReadOnlyList<ChartPoint> points)
    {
        Label = label;
        DataExpression = dataExpression;
        Data = data;
        Points = points;
    }

    /// <summary>Gets the optional series label shown in the legend.</summary>
    public string? Label { get; }

    /// <summary>
    /// Gets the raw template expression (e.g. "{{ sales }}") when the data is data-bound;
    /// null when the data was supplied inline.
    /// </summary>
    public string? DataExpression { get; }

    /// <summary>Gets the resolved numeric values. Empty until a bound expression is resolved.</summary>
    public IReadOnlyList<double> Data { get; }

    /// <summary>
    /// Gets the resolved XY(R) tuple points for scatter/bubble series. Empty for flat numeric
    /// series (which use <see cref="Data"/> instead).
    /// </summary>
    public IReadOnlyList<ChartPoint> Points { get; }

    /// <summary>
    /// Creates a series with inline numeric data.
    /// </summary>
    /// <param name="label">The optional legend label.</param>
    /// <param name="data">The numeric values.</param>
    /// <returns>A new <see cref="ChartSeries"/>.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="data"/> is null.</exception>
    public static ChartSeries FromInline(string? label, IReadOnlyList<double> data)
    {
        ArgumentNullException.ThrowIfNull(data);
        return new ChartSeries(label, dataExpression: null, data, EmptyPoints);
    }

    /// <summary>
    /// Creates a data-bound series whose values come from a template expression.
    /// </summary>
    /// <param name="label">The optional legend label.</param>
    /// <param name="dataExpression">The raw expression string (e.g. "{{ sales }}").</param>
    /// <returns>A new <see cref="ChartSeries"/> with empty data until resolved.</returns>
    public static ChartSeries FromExpression(string? label, string dataExpression)
    {
        ArgumentNullException.ThrowIfNull(dataExpression);
        return new ChartSeries(label, dataExpression, Empty, EmptyPoints);
    }

    /// <summary>
    /// Creates a series with inline XY(R) tuple points (scatter/bubble).
    /// </summary>
    /// <param name="label">The optional legend label.</param>
    /// <param name="points">The tuple points.</param>
    /// <returns>A new <see cref="ChartSeries"/> whose <see cref="Data"/> is empty.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="points"/> is null.</exception>
    public static ChartSeries FromPoints(string? label, IReadOnlyList<ChartPoint> points)
    {
        ArgumentNullException.ThrowIfNull(points);
        return new ChartSeries(label, dataExpression: null, Empty, points);
    }

    /// <summary>
    /// Returns a copy of this series with its <see cref="Data"/> replaced, preserving the label
    /// and expression. Used when a bound expression has been resolved to concrete values.
    /// </summary>
    /// <param name="data">The resolved numeric values.</param>
    /// <returns>A new <see cref="ChartSeries"/> with the new data.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="data"/> is null.</exception>
    public ChartSeries WithData(IReadOnlyList<double> data)
    {
        ArgumentNullException.ThrowIfNull(data);
        return new ChartSeries(Label, DataExpression, data, EmptyPoints);
    }
}
