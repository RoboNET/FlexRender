using System.Collections.Generic;
using System.Globalization;
using FlexRender.Charts;
using FlexRender.Parsing.Ast;
using YamlDotNet.RepresentationModel;
using static FlexRender.Parsing.YamlPropertyHelpers;

namespace FlexRender.Parsing;

/// <summary>
/// Provides static helpers for parsing the <c>chart</c> element from YAML.
/// </summary>
public static class ChartParsers
{
    /// <summary>
    /// Parses a <c>chart</c> element.
    /// </summary>
    /// <param name="node">The YAML node containing the chart definition.</param>
    /// <param name="maxSeries">The maximum number of series allowed.</param>
    /// <param name="maxDataPoints">The maximum number of data points per series.</param>
    /// <returns>The parsed <see cref="ChartElement"/>.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="node"/> is null.</exception>
    /// <exception cref="TemplateParseException">Thrown on an unknown chart-type, malformed series, or exceeded limits.</exception>
    internal static TemplateElement ParseChartElement(YamlMappingNode node, int maxSeries, int maxDataPoints)
    {
        ArgumentNullException.ThrowIfNull(node);

        var chartType = ParseChartType(node);
        var series = ParseSeries(node, chartType, maxSeries, maxDataPoints);

        var chart = new ChartElement(chartType, series)
        {
            Categories = ParseCategories(node),
            Palette = ParsePalette(node),
            Theme = ParseTheme(node),
            Legend = ParseLegend(node),
            Title = GetStringValue(node, "title"),
            Horizontal = GetBoolValue(node, "horizontal", false),
            Stacked = GetBoolValue(node, "stacked", false),
            Smooth = GetBoolValue(node, "smooth", false),
            ShowPoints = GetBoolValue(node, "points", false),
            PieLabels = ParsePieLabels(node),
            Background = GetStringValue(node, "background")!,
            Rotate = GetExprStringValue(node, "rotate", "none"),
            Padding = GetExprStringValue(node, "padding", "0"),
            Margin = GetExprStringValue(node, "margin", "0")
        };

        ElementParsers.ApplyFlexItemProperties(node, chart);
        return chart;
    }

    /// <summary>
    /// Parses and validates the <c>chart-type</c> property.
    /// </summary>
    /// <param name="node">The chart mapping node.</param>
    /// <returns>The resolved <see cref="ChartType"/>.</returns>
    /// <exception cref="TemplateParseException">Thrown when the value is not a known chart type.</exception>
    private static ChartType ParseChartType(YamlMappingNode node)
    {
        var raw = GetStringValue(node, "chart-type", "bar");
        if (!Enum.TryParse<ChartType>(raw, ignoreCase: true, out var chartType))
        {
            throw new TemplateParseException(
                $"Unknown chart-type '{raw}'. Valid values: bar, line, area, pie, donut, scatter, bubble, gauge, progress, sparkline.");
        }
        return chartType;
    }

    /// <summary>
    /// Parses and validates the <c>legend</c> position.
    /// </summary>
    /// <param name="node">The chart mapping node.</param>
    /// <returns>The resolved <see cref="LegendPosition"/>.</returns>
    /// <exception cref="TemplateParseException">Thrown when the value is not a known legend position.</exception>
    private static LegendPosition ParseLegend(YamlMappingNode node)
    {
        var raw = GetStringValue(node, "legend", "bottom");
        if (!Enum.TryParse<LegendPosition>(raw, ignoreCase: true, out var legend))
        {
            throw new TemplateParseException(
                $"Unknown legend position '{raw}'. Valid values: top, bottom, left, right, none.");
        }
        return legend;
    }

    /// <summary>
    /// Parses and validates the pie/donut <c>labels</c> mode.
    /// </summary>
    /// <param name="node">The chart mapping node.</param>
    /// <returns>The resolved <see cref="PieLabelMode"/>.</returns>
    /// <exception cref="TemplateParseException">Thrown when the value is not a known label mode.</exception>
    private static PieLabelMode ParsePieLabels(YamlMappingNode node)
    {
        var raw = GetStringValue(node, "labels", "percent");
        if (!Enum.TryParse<PieLabelMode>(raw, ignoreCase: true, out var mode))
        {
            throw new TemplateParseException(
                $"Unknown labels mode '{raw}'. Valid values: percent, value, none.");
        }
        return mode;
    }

    /// <summary>
    /// Parses the optional <c>categories</c> sequence of axis/slice labels.
    /// </summary>
    /// <param name="node">The chart mapping node.</param>
    /// <returns>The category labels in order; empty when none are present.</returns>
    private static List<string> ParseCategories(YamlMappingNode node)
    {
        var categories = new List<string>();
        if (TryGetSequence(node, "categories", out var seq))
        {
            foreach (var item in seq.Children)
            {
                if (item is YamlScalarNode scalar && scalar.Value is not null)
                    categories.Add(scalar.Value);
            }
        }
        return categories;
    }

    /// <summary>
    /// Parses the optional <c>palette</c> property in either named or explicit-color-list form.
    /// </summary>
    /// <param name="node">The chart mapping node.</param>
    /// <returns>The resolved <see cref="ChartPalette"/>, or null when no palette is specified.</returns>
    /// <exception cref="TemplateParseException">Thrown when an empty color list or an unknown palette name is given.</exception>
    private static ChartPalette? ParsePalette(YamlMappingNode node)
    {
        // Explicit color list form.
        if (TryGetSequence(node, "palette", out var seq))
        {
            var colors = new List<string>();
            foreach (var item in seq.Children)
            {
                if (item is YamlScalarNode scalar && !string.IsNullOrWhiteSpace(scalar.Value))
                    colors.Add(scalar.Value.Trim());
            }
            if (colors.Count == 0)
                throw new TemplateParseException("A 'palette' color list must contain at least one color.");
            return new ChartPalette(colors);
        }

        // Named palette form.
        var name = GetStringValue(node, "palette");
        if (string.IsNullOrWhiteSpace(name))
            return null;

        var palette = ChartPalettes.Resolve(name);
        if (palette is null)
            throw new TemplateParseException(
                $"Unknown palette '{name}'. Valid names: default, ocean, sunset, forest, mono, vivid (or an explicit color list).");
        return palette;
    }

    /// <summary>
    /// Parses the optional named <c>theme</c> property.
    /// </summary>
    /// <param name="node">The chart mapping node.</param>
    /// <returns>The resolved <see cref="ChartTheme"/>, or null when no theme is specified.</returns>
    /// <exception cref="TemplateParseException">Thrown when an unknown theme name is given.</exception>
    private static ChartTheme? ParseTheme(YamlMappingNode node)
    {
        var name = GetStringValue(node, "theme");
        if (string.IsNullOrWhiteSpace(name))
            return null;

        var theme = ChartThemes.Resolve(name);
        if (theme is null)
            throw new TemplateParseException(
                $"Unknown chart theme '{name}'. Valid names: light, dark, minimal.");
        return theme;
    }

    /// <summary>
    /// Parses the <c>series</c> sequence, enforcing the per-chart series limit.
    /// </summary>
    /// <param name="node">The chart mapping node.</param>
    /// <param name="chartType">The chart type, used to decide whether tuple (scatter/bubble) data is expected.</param>
    /// <param name="maxSeries">The maximum number of series allowed.</param>
    /// <param name="maxDataPoints">The maximum number of data points per series.</param>
    /// <returns>The parsed series; empty when no <c>series</c> sequence is present.</returns>
    /// <exception cref="TemplateParseException">Thrown when the series limit is exceeded or an entry is malformed.</exception>
    private static List<ChartSeries> ParseSeries(YamlMappingNode node, ChartType chartType, int maxSeries, int maxDataPoints)
    {
        var result = new List<ChartSeries>();

        if (!TryGetSequence(node, "series", out var seriesSeq))
            return result;

        if (seriesSeq.Children.Count > maxSeries)
        {
            throw new TemplateParseException(
                $"Chart has {seriesSeq.Children.Count} series, which exceeds the maximum of {maxSeries}.");
        }

        foreach (var item in seriesSeq.Children)
        {
            if (item is not YamlMappingNode seriesNode)
                throw new TemplateParseException("Each entry in 'series' must be a mapping with a 'data' field.");

            result.Add(ParseOneSeries(seriesNode, chartType, maxDataPoints));
        }

        return result;
    }

    /// <summary>
    /// Parses a single series entry, supporting an inline numeric array, an array-of-arrays of
    /// XY/XY(R) tuples (scatter/bubble), or a template expression.
    /// </summary>
    /// <param name="seriesNode">The series mapping node.</param>
    /// <param name="chartType">The chart type, used to decide tuple arity rules for scatter/bubble.</param>
    /// <param name="maxDataPoints">The maximum number of data points allowed in the series.</param>
    /// <returns>The parsed <see cref="ChartSeries"/>.</returns>
    /// <exception cref="TemplateParseException">Thrown when the data-point limit is exceeded or a value is non-numeric/non-finite.</exception>
    private static ChartSeries ParseOneSeries(YamlMappingNode seriesNode, ChartType chartType, int maxDataPoints)
    {
        var label = GetStringValue(seriesNode, "label");

        // Bubble tuples may carry a third (radius) element; scatter tuples are strictly [x, y].
        var allowRadius = chartType is ChartType.Bubble;

        // Inline array form.
        if (TryGetSequence(seriesNode, "data", out var dataSeq))
        {
            if (dataSeq.Children.Count > maxDataPoints)
            {
                throw new TemplateParseException(
                    $"Series '{label ?? "(unlabeled)"}' has {dataSeq.Children.Count} data points, which exceeds the maximum of {maxDataPoints}.");
            }

            // Tuple data: the items are themselves sequences ([x, y] or [x, y, r]).
            if (dataSeq.Children.Count > 0 && dataSeq.Children[0] is YamlSequenceNode)
                return ParseTupleSeries(label, dataSeq, allowRadius);

            // Flat numeric data (bar/line/area/pie/donut/sparkline/gauge-progress series).
            var values = new List<double>(dataSeq.Children.Count);
            foreach (var v in dataSeq.Children)
            {
                if (v is not YamlScalarNode scalar
                    || !double.TryParse(scalar.Value, NumberStyles.Float, CultureInfo.InvariantCulture, out var d)
                    || !double.IsFinite(d))
                {
                    throw new TemplateParseException(
                        $"Series '{label ?? "(unlabeled)"}' contains a non-numeric data value '{(v as YamlScalarNode)?.Value}'.");
                }
                values.Add(d);
            }
            return ChartSeries.FromInline(label, values);
        }

        // Expression form (scalar string containing {{ }}).
        var expr = GetStringValue(seriesNode, "data");
        if (!string.IsNullOrWhiteSpace(expr))
            return ChartSeries.FromExpression(label, expr);

        // No data at all -> empty inline series (renders as "no data" if all series empty).
        return ChartSeries.FromInline(label, Array.Empty<double>());
    }

    /// <summary>
    /// Parses an array-of-arrays data sequence into XY (scatter) or XY(R) (bubble) tuple points.
    /// </summary>
    /// <param name="label">The series label, used in error messages.</param>
    /// <param name="dataSeq">The data sequence whose items are 2- or 3-element scalar sequences.</param>
    /// <param name="allowRadius">Whether a third (radius) element is permitted (bubble).</param>
    /// <returns>A point-bearing <see cref="ChartSeries"/>.</returns>
    /// <exception cref="TemplateParseException">Thrown on wrong arity, non-numeric, or non-finite tuple values.</exception>
    private static ChartSeries ParseTupleSeries(string? label, YamlSequenceNode dataSeq, bool allowRadius)
    {
        var points = new List<ChartPoint>(dataSeq.Children.Count);
        foreach (var item in dataSeq.Children)
        {
            if (item is not YamlSequenceNode tuple)
            {
                throw new TemplateParseException(
                    $"Series '{label ?? "(unlabeled)"}' mixes tuple and scalar data; every item must be an [x, y] (or [x, y, r]) array.");
            }

            var arity = tuple.Children.Count;
            var maxArity = allowRadius ? 3 : 2;
            if (arity < 2 || arity > maxArity)
            {
                throw new TemplateParseException(
                    $"Series '{label ?? "(unlabeled)"}' has a tuple with {arity} elements; expected 2{(allowRadius ? " or 3" : string.Empty)}.");
            }

            var x = ParseTupleScalar(tuple.Children[0], label);
            var y = ParseTupleScalar(tuple.Children[1], label);
            var r = arity == 3 ? ParseTupleScalar(tuple.Children[2], label) : 0d;
            points.Add(new ChartPoint(x, y, r));
        }

        return ChartSeries.FromPoints(label, points);
    }

    /// <summary>Parses a single tuple element as a finite double, raising a named error otherwise.</summary>
    /// <param name="node">The tuple element node.</param>
    /// <param name="label">The series label, used in error messages.</param>
    /// <returns>The parsed finite double.</returns>
    /// <exception cref="TemplateParseException">Thrown when the value is non-numeric or non-finite.</exception>
    private static double ParseTupleScalar(YamlNode node, string? label)
    {
        if (node is YamlScalarNode scalar
            && double.TryParse(scalar.Value, NumberStyles.Float, CultureInfo.InvariantCulture, out var d)
            && double.IsFinite(d))
        {
            return d;
        }

        throw new TemplateParseException(
            $"Series '{label ?? "(unlabeled)"}' contains a non-numeric tuple value '{(node as YamlScalarNode)?.Value}'.");
    }
}
