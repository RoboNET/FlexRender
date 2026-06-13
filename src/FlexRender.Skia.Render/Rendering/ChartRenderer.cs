using System;
using FlexRender.Charts;
using FlexRender.Parsing.Ast;
using SkiaSharp;

namespace FlexRender.Rendering;

/// <summary>
/// Draws <see cref="ChartElement"/> instances to a SkiaSharp canvas. Computes the plot area
/// (minus title/legend/axis gutters), then draws grid, axes, series geometry, and legend using
/// the resolved theme and palette. Label text uses an <see cref="SKTypeface"/> supplied by the
/// caller; when null, labels are skipped but geometry still draws.
/// </summary>
internal static class ChartRenderer
{
    /// <summary>
    /// Draws a chart into the given box.
    /// </summary>
    /// <param name="canvas">The canvas to draw on.</param>
    /// <param name="chart">The chart element.</param>
    /// <param name="x">The chart box left edge.</param>
    /// <param name="y">The chart box top edge.</param>
    /// <param name="width">The chart box width.</param>
    /// <param name="height">The chart box height.</param>
    /// <param name="typeface">The typeface for labels, or null to skip labels.</param>
    /// <param name="antialias">Whether to anti-alias drawing.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="canvas"/> or <paramref name="chart"/> is null.</exception>
    internal static void Draw(
        SKCanvas canvas,
        ChartElement chart,
        float x,
        float y,
        float width,
        float height,
        SKTypeface? typeface,
        bool antialias)
    {
        ArgumentNullException.ThrowIfNull(canvas);
        ArgumentNullException.ThrowIfNull(chart);

        if (width <= 0f || height <= 0f)
            return;

        var theme = chart.Theme ?? ChartThemes.Default;

        canvas.Save();
        try
        {
            canvas.ClipRect(new SKRect(x, y, x + width, y + height));
            canvas.Translate(x, y);

            DrawChartBackground(canvas, theme, width, height, antialias);

            if (!HasAnyData(chart))
            {
                DrawNoData(canvas, theme, width, height, typeface, antialias);
                return;
            }

            DrawSeries(canvas, chart, theme, width, height, typeface, antialias);
        }
        finally
        {
            canvas.Restore();
        }
    }

    /// <summary>Returns whether any series has at least one data point.</summary>
    private static bool HasAnyData(ChartElement chart)
    {
        foreach (var s in chart.Series)
        {
            if (s.Data.Count > 0)
                return true;
        }
        return false;
    }

    /// <summary>Fills the chart background using the theme color (skipped when transparent).</summary>
    private static void DrawChartBackground(SKCanvas canvas, ChartTheme theme, float width, float height, bool antialias)
    {
        if (string.IsNullOrEmpty(theme.BackgroundColor))
            return;

        using var paint = new SKPaint
        {
            Color = ColorParser.Parse(theme.BackgroundColor),
            Style = SKPaintStyle.Fill,
            IsAntialias = antialias
        };
        canvas.DrawRect(0f, 0f, width, height, paint);
    }

    /// <summary>
    /// Draws a centred "No data" placeholder: a light dashed border plus centered text
    /// (or just the border when no typeface is available).
    /// </summary>
    private static void DrawNoData(SKCanvas canvas, ChartTheme theme, float width, float height, SKTypeface? typeface, bool antialias)
    {
        var inset = MathF.Min(width, height) * 0.08f;
        var rect = new SKRect(inset, inset, width - inset, height - inset);

        using var dash = SKPathEffect.CreateDash([6f, 4f], 0f);
        using var border = new SKPaint
        {
            Color = ColorParser.Parse(theme.AxisColor),
            Style = SKPaintStyle.Stroke,
            StrokeWidth = 1.5f,
            IsAntialias = antialias,
            PathEffect = dash
        };
        canvas.DrawRect(rect, border);

        if (typeface is null)
            return;

        const string message = "No data";
        using var font = new SKFont(typeface, theme.LabelSize);
        using var textPaint = new SKPaint { Color = ColorParser.Parse(theme.LabelColor), IsAntialias = antialias };
        var textWidth = font.MeasureText(message);
        var tx = (width - textWidth) / 2f;
        var ty = (height + theme.LabelSize) / 2f;
        canvas.DrawText(message, tx, ty, SKTextAlign.Left, font, textPaint);
    }

    /// <summary>
    /// Draws series geometry by chart type. Bar charts render grid, axes, and columns; other
    /// chart types fall through to a faint plot border until added in later tasks.
    /// </summary>
    private static void DrawSeries(
        SKCanvas canvas,
        ChartElement chart,
        ChartTheme theme,
        float width,
        float height,
        SKTypeface? typeface,
        bool antialias)
    {
        switch (chart.ChartType)
        {
            case ChartType.Bar:
                DrawBars(canvas, chart, theme, width, height, typeface, antialias);
                break;
            default:
                // Other chart types are added in later tasks.
                using (var border = new SKPaint
                {
                    Color = ColorParser.Parse(theme.AxisColor),
                    Style = SKPaintStyle.Stroke,
                    StrokeWidth = 1f,
                    IsAntialias = antialias
                })
                {
                    canvas.DrawRect(0.5f, 0.5f, width - 1f, height - 1f, border);
                }
                break;
        }
    }

    /// <summary>Computes the combined data min/max across all series.</summary>
    private static (double Min, double Max) DataBounds(ChartElement chart)
    {
        var min = double.MaxValue;
        var max = double.MinValue;
        foreach (var s in chart.Series)
        {
            foreach (var v in s.Data)
            {
                if (v < min) min = v;
                if (v > max) max = v;
            }
        }
        if (min == double.MaxValue)
            return (0d, 1d);
        return (min, max);
    }

    /// <summary>Draws horizontal grid lines and y-axis tick labels for the given scale.</summary>
    private static void DrawGridAndYAxis(
        SKCanvas canvas, ChartTheme theme, in PlotArea plot, AxisScale scale,
        SKTypeface? typeface, bool antialias)
    {
        var mapper = new ValueMapper(scale.Min, scale.Max, plot.Top, plot.Bottom);

        using var grid = new SKPaint
        {
            Color = ColorParser.Parse(theme.GridColor),
            Style = SKPaintStyle.Stroke,
            StrokeWidth = 1f,
            IsAntialias = antialias
        };

        SKFont? font = null;
        SKPaint? labelPaint = null;
        if (typeface is not null)
        {
            font = new SKFont(typeface, theme.LabelSize);
            labelPaint = new SKPaint { Color = ColorParser.Parse(theme.LabelColor), IsAntialias = antialias };
        }

        try
        {
            foreach (var tick in scale.Ticks)
            {
                var ty = mapper.MapY(tick);
                canvas.DrawLine(plot.Left, ty, plot.Right, ty, grid);

                if (font is not null && labelPaint is not null)
                {
                    var label = FormatTick(tick);
                    var tw = font.MeasureText(label);
                    canvas.DrawText(label, plot.Left - tw - 4f, ty + (theme.LabelSize / 3f), SKTextAlign.Left, font, labelPaint);
                }
            }
        }
        finally
        {
            font?.Dispose();
            labelPaint?.Dispose();
        }
    }

    /// <summary>Draws x-axis category labels centred under each category slot.</summary>
    private static void DrawCategoryLabels(
        SKCanvas canvas, ChartElement chart, ChartTheme theme, in PlotArea plot,
        SKTypeface? typeface, bool antialias)
    {
        if (typeface is null || chart.Categories.Count == 0)
            return;

        using var font = new SKFont(typeface, theme.LabelSize);
        using var paint = new SKPaint { Color = ColorParser.Parse(theme.LabelColor), IsAntialias = antialias };

        var slot = plot.Width / chart.Categories.Count;
        for (var i = 0; i < chart.Categories.Count; i++)
        {
            var label = chart.Categories[i];
            var tw = font.MeasureText(label);
            var cx = plot.Left + (slot * (i + 0.5f));
            canvas.DrawText(label, cx - (tw / 2f), plot.Bottom + theme.LabelSize + 2f, SKTextAlign.Left, font, paint);
        }
    }

    /// <summary>Draws a vertical or horizontal bar chart with grid and axis labels.</summary>
    private static void DrawBars(
        SKCanvas canvas, ChartElement chart, ChartTheme theme,
        float width, float height, SKTypeface? typeface, bool antialias)
    {
        var (dataMin, dataMax) = DataBounds(chart);
        var scale = AxisScale.Compute(dataMin, dataMax, targetTicks: 5);

        var hasTitle = !string.IsNullOrEmpty(chart.Title);
        var plot = ChartLayout.ComputePlotArea(
            width, height, hasTitle, chart.Legend,
            axisGutterLeft: 44f, axisGutterBottom: 22f, titleHeight: theme.TitleSize + 8f, legendExtent: 28f);

        DrawGridAndYAxis(canvas, theme, plot, scale, typeface, antialias);

        var palette = chart.Palette ?? ChartPalettes.Default;
        var mapper = new ValueMapper(scale.Min, scale.Max, plot.Top, plot.Bottom);
        var zeroY = mapper.MapY(0d);

        var seriesCount = chart.Series.Count;
        if (seriesCount == 0)
            return;

        // Determine category count from the longest series.
        var categoryCount = 0;
        foreach (var s in chart.Series)
            categoryCount = Math.Max(categoryCount, s.Data.Count);
        if (categoryCount == 0)
            return;

        var groupSlot = plot.Width / categoryCount;
        var groupPadding = groupSlot * 0.15f;
        var barAreaWidth = groupSlot - (2f * groupPadding);
        var barWidth = barAreaWidth / seriesCount;

        for (var si = 0; si < seriesCount; si++)
        {
            var data = chart.Series[si].Data;
            using var paint = new SKPaint
            {
                Color = ColorParser.Parse(palette.ColorAt(si)),
                Style = SKPaintStyle.Fill,
                IsAntialias = antialias
            };

            for (var ci = 0; ci < data.Count; ci++)
            {
                var value = data[ci];
                var valueY = mapper.MapY(value);
                var barLeft = plot.Left + (groupSlot * ci) + groupPadding + (barWidth * si);
                var top = Math.Min(valueY, zeroY);
                var bottom = Math.Max(valueY, zeroY);
                var rect = new SKRect(barLeft, top, barLeft + barWidth, bottom);

                if (theme.BarCornerRadius > 0f)
                    canvas.DrawRoundRect(rect, theme.BarCornerRadius, theme.BarCornerRadius, paint);
                else
                    canvas.DrawRect(rect, paint);
            }
        }

        DrawCategoryLabels(canvas, chart, theme, plot, typeface, antialias);
    }

    /// <summary>Formats a tick value compactly, trimming trailing zeros.</summary>
    private static string FormatTick(double value)
    {
        if (value == Math.Floor(value) && Math.Abs(value) < 1e15)
            return ((long)value).ToString(System.Globalization.CultureInfo.InvariantCulture);
        return value.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture);
    }
}
