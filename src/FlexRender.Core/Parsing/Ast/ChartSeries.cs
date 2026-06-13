using System;
using System.Collections.Generic;

namespace FlexRender.Parsing.Ast;

/// <summary>
/// A single chart data series: a label plus its numeric values. The values may come from an
/// inline YAML array (resolved at parse time) or from a template expression (resolved against
/// the data context during ChartElement.ResolveExpressions).
/// </summary>
public sealed class ChartSeries
{
    private static readonly IReadOnlyList<double> Empty = Array.Empty<double>();

    private ChartSeries(string? label, string? dataExpression, IReadOnlyList<double> data)
    {
        Label = label;
        DataExpression = dataExpression;
        Data = data;
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
    /// Creates a series with inline numeric data.
    /// </summary>
    /// <param name="label">The optional legend label.</param>
    /// <param name="data">The numeric values.</param>
    /// <returns>A new <see cref="ChartSeries"/>.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="data"/> is null.</exception>
    public static ChartSeries FromInline(string? label, IReadOnlyList<double> data)
    {
        ArgumentNullException.ThrowIfNull(data);
        return new ChartSeries(label, dataExpression: null, data);
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
        return new ChartSeries(label, dataExpression, Empty);
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
        return new ChartSeries(Label, DataExpression, data);
    }
}
