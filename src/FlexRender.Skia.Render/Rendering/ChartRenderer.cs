using System;
using System.Collections.Generic;
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
            DrawTitle(canvas, chart, theme, width, typeface, antialias);
            DrawLegend(canvas, chart, theme, width, height, typeface, antialias);
        }
        finally
        {
            canvas.Restore();
        }
    }

    /// <summary>Returns whether the chart has anything to draw (flat data, tuple points, or a gauge value).</summary>
    private static bool HasAnyData(ChartElement chart)
    {
        if (chart.ChartType is ChartType.Gauge or ChartType.Progress)
            return chart.Value is not null;

        foreach (var s in chart.Series)
        {
            if (s.Data.Count > 0 || s.Points.Count > 0)
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
            case ChartType.Line:
                DrawLineOrArea(canvas, chart, theme, width, height, typeface, fillArea: false, antialias);
                break;
            case ChartType.Area:
                DrawLineOrArea(canvas, chart, theme, width, height, typeface, fillArea: true, antialias);
                break;
            case ChartType.Pie:
                DrawPie(canvas, chart, theme, width, height, typeface, isDonut: false, antialias);
                break;
            case ChartType.Donut:
                DrawPie(canvas, chart, theme, width, height, typeface, isDonut: true, antialias);
                break;
            case ChartType.Sparkline:
                DrawSparkline(canvas, chart, theme, width, height, antialias);
                break;
            case ChartType.Scatter:
                DrawScatterOrBubble(canvas, chart, theme, width, height, typeface, isBubble: false, antialias);
                break;
            case ChartType.Bubble:
                DrawScatterOrBubble(canvas, chart, theme, width, height, typeface, isBubble: true, antialias);
                break;
            case ChartType.Gauge:
                DrawGauge(canvas, chart, theme, width, height, typeface, antialias);
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

    /// <summary>
    /// Computes the X and Y data ranges across all series' tuple points (scatter/bubble).
    /// Returns unit ranges when there are no points so a chart can still draw.
    /// </summary>
    private static (double MinX, double MaxX, double MinY, double MaxY) PointBounds(ChartElement chart)
    {
        var minX = double.MaxValue;
        var maxX = double.MinValue;
        var minY = double.MaxValue;
        var maxY = double.MinValue;

        foreach (var s in chart.Series)
        {
            foreach (var p in s.Points)
            {
                if (p.X < minX) minX = p.X;
                if (p.X > maxX) maxX = p.X;
                if (p.Y < minY) minY = p.Y;
                if (p.Y > maxY) maxY = p.Y;
            }
        }

        if (minX == double.MaxValue)
            return (0d, 1d, 0d, 1d);
        return (minX, maxX, minY, maxY);
    }

    /// <summary>
    /// Computes the value-axis bounds for stacked bars: the maximum is the largest per-category
    /// stacked total (sum of all series' values at that category) and the minimum is the smallest
    /// per-category stacked total clamped at zero, so the baseline stays at zero for non-negative data.
    /// </summary>
    private static (double Min, double Max) StackedBounds(ChartElement chart)
    {
        var categoryCount = 0;
        foreach (var s in chart.Series)
            categoryCount = Math.Max(categoryCount, s.Data.Count);
        if (categoryCount == 0)
            return (0d, 1d);

        var maxTotal = double.MinValue;
        var minTotal = double.MaxValue;
        for (var ci = 0; ci < categoryCount; ci++)
        {
            var positiveSum = 0d;
            var negativeSum = 0d;
            foreach (var s in chart.Series)
            {
                if (ci >= s.Data.Count)
                    continue;
                var v = s.Data[ci];
                if (v >= 0d)
                    positiveSum += v;
                else
                    negativeSum += v;
            }
            if (positiveSum > maxTotal) maxTotal = positiveSum;
            if (negativeSum < minTotal) minTotal = negativeSum;
        }

        var min = Math.Min(0d, minTotal);
        var max = Math.Max(0d, maxTotal);
        if (min == max)
            max = min + 1d;
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
        // Stacked vertical bars scale the value axis to per-category stacked totals; grouped (and
        // horizontal) bars scale to the single-value range. Horizontal stacking is not implemented;
        // a horizontal stacked chart falls back to grouped rendering.
        var stacked = chart.Stacked && !chart.Horizontal;
        var (dataMin, dataMax) = stacked ? StackedBounds(chart) : DataBounds(chart);
        var scale = AxisScale.Compute(dataMin, dataMax, targetTicks: 5);

        var hasTitle = !string.IsNullOrEmpty(chart.Title);
        var plot = ChartLayout.ComputePlotArea(
            width, height, hasTitle, chart.Legend,
            axisGutterLeft: 44f, axisGutterBottom: 22f, titleHeight: theme.TitleSize + 8f, legendExtent: 28f);

        var palette = chart.Palette ?? ChartPalettes.Default;

        var seriesCount = chart.Series.Count;
        if (seriesCount == 0)
            return;

        // Determine category count from the longest series.
        var categoryCount = 0;
        foreach (var s in chart.Series)
            categoryCount = Math.Max(categoryCount, s.Data.Count);
        if (categoryCount == 0)
            return;

        if (chart.Horizontal)
        {
            DrawHorizontalBars(canvas, chart, theme, plot, scale, palette, seriesCount, categoryCount, antialias);
        }
        else if (stacked)
        {
            DrawGridAndYAxis(canvas, theme, plot, scale, typeface, antialias);
            DrawStackedVerticalBars(canvas, chart, theme, plot, scale, palette, seriesCount, categoryCount, antialias);
            DrawCategoryLabels(canvas, chart, theme, plot, typeface, antialias);
        }
        else
        {
            DrawGridAndYAxis(canvas, theme, plot, scale, typeface, antialias);
            DrawVerticalBars(canvas, chart, theme, plot, scale, palette, seriesCount, categoryCount, antialias);
            DrawCategoryLabels(canvas, chart, theme, plot, typeface, antialias);
        }
    }

    /// <summary>
    /// Draws stacked vertical bars: one full-width bar per category whose series segments are
    /// stacked on top of one another from the zero baseline. Positive segments stack upward and
    /// negative segments stack downward, each keeping a running pixel baseline per category.
    /// </summary>
    private static void DrawStackedVerticalBars(
        SKCanvas canvas, ChartElement chart, ChartTheme theme, in PlotArea plot, AxisScale scale,
        ChartPalette palette, int seriesCount, int categoryCount, bool antialias)
    {
        var mapper = new ValueMapper(scale.Min, scale.Max, plot.Top, plot.Bottom);
        var zeroY = mapper.MapY(0d);

        var slot = plot.Width / categoryCount;
        var padding = slot * 0.15f;
        var barWidth = slot - (2f * padding);

        // Running pixel baselines per category: one for the upward (positive) stack and one for the
        // downward (negative) stack, both starting at the zero line.
        var positiveBaseline = new float[categoryCount];
        var negativeBaseline = new float[categoryCount];
        for (var ci = 0; ci < categoryCount; ci++)
        {
            positiveBaseline[ci] = zeroY;
            negativeBaseline[ci] = zeroY;
        }

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
                if (value == 0d)
                    continue;

                // Segment height in pixels is the distance the mapped value moves from the zero line.
                var segmentHeight = zeroY - mapper.MapY(value);
                var barLeft = plot.Left + (slot * ci) + padding;

                float top, bottom;
                if (value > 0d)
                {
                    bottom = positiveBaseline[ci];
                    top = bottom - segmentHeight;
                    positiveBaseline[ci] = top;
                }
                else
                {
                    top = negativeBaseline[ci];
                    bottom = top - segmentHeight; // segmentHeight is negative here, so bottom > top
                    negativeBaseline[ci] = bottom;
                }

                var rect = new SKRect(barLeft, Math.Min(top, bottom), barLeft + barWidth, Math.Max(top, bottom));
                if (theme.BarCornerRadius > 0f)
                    canvas.DrawRoundRect(rect, theme.BarCornerRadius, theme.BarCornerRadius, paint);
                else
                    canvas.DrawRect(rect, paint);
            }
        }
    }

    /// <summary>Draws bars rising vertically from the zero baseline, grouped along the X axis by category.</summary>
    private static void DrawVerticalBars(
        SKCanvas canvas, ChartElement chart, ChartTheme theme, in PlotArea plot, AxisScale scale,
        ChartPalette palette, int seriesCount, int categoryCount, bool antialias)
    {
        var mapper = new ValueMapper(scale.Min, scale.Max, plot.Top, plot.Bottom);
        var zeroY = mapper.MapY(0d);

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
                var valueY = mapper.MapY(data[ci]);
                var barLeft = plot.Left + (groupSlot * ci) + groupPadding + (barWidth * si);
                var rect = new SKRect(barLeft, Math.Min(valueY, zeroY), barLeft + barWidth, Math.Max(valueY, zeroY));

                if (theme.BarCornerRadius > 0f)
                    canvas.DrawRoundRect(rect, theme.BarCornerRadius, theme.BarCornerRadius, paint);
                else
                    canvas.DrawRect(rect, paint);
            }
        }
    }

    /// <summary>Draws bars extending horizontally from the zero baseline, grouped along the Y axis by category.</summary>
    private static void DrawHorizontalBars(
        SKCanvas canvas, ChartElement chart, ChartTheme theme, in PlotArea plot, AxisScale scale,
        ChartPalette palette, int seriesCount, int categoryCount, bool antialias)
    {
        // For horizontal bars the value axis is X: min -> plot.Left, max -> plot.Right.
        // Copy the readonly plot fields into locals so the mapping closure can capture them.
        var plotLeft = plot.Left;
        var plotWidth = plot.Width;
        var scaleMin = scale.Min;
        var xSpan = scale.Max - scale.Min;
        float MapX(double value) => xSpan <= 0d
            ? plotLeft
            : plotLeft + (float)(((value - scaleMin) / xSpan) * plotWidth);
        var zeroX = MapX(0d);

        var groupSlot = plot.Height / categoryCount;
        var groupPadding = groupSlot * 0.15f;
        var barAreaHeight = groupSlot - (2f * groupPadding);
        var barHeight = barAreaHeight / seriesCount;

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
                var valueX = MapX(data[ci]);
                var barTop = plot.Top + (groupSlot * ci) + groupPadding + (barHeight * si);
                var rect = new SKRect(Math.Min(valueX, zeroX), barTop, Math.Max(valueX, zeroX), barTop + barHeight);

                if (theme.BarCornerRadius > 0f)
                    canvas.DrawRoundRect(rect, theme.BarCornerRadius, theme.BarCornerRadius, paint);
                else
                    canvas.DrawRect(rect, paint);
            }
        }
    }

    /// <summary>Draws a line chart, optionally filling the area under each series.</summary>
    private static void DrawLineOrArea(
        SKCanvas canvas, ChartElement chart, ChartTheme theme,
        float width, float height, SKTypeface? typeface, bool fillArea, bool antialias)
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

        for (var si = 0; si < chart.Series.Count; si++)
        {
            var data = chart.Series[si].Data;
            if (data.Count == 0)
                continue;

            var color = ColorParser.Parse(palette.ColorAt(si));
            var step = data.Count > 1 ? plot.Width / (data.Count - 1) : 0f;

            float X(int i) => data.Count > 1 ? plot.Left + (step * i) : plot.Left + (plot.Width / 2f);

            // Build the series point list once and reuse it for both the line and area paths.
            var points = new SKPoint[data.Count];
            for (var i = 0; i < data.Count; i++)
                points[i] = new SKPoint(X(i), mapper.MapY(data[i]));

            using var linePath = BuildSeriesPath(points, chart.Smooth);

            if (fillArea)
            {
                using var areaPath = BuildSeriesPath(points, chart.Smooth);
                // Close the area down to the zero baseline along the right edge and back to the start.
                areaPath.LineTo(points[^1].X, zeroY);
                areaPath.LineTo(points[0].X, zeroY);
                areaPath.Close();

                var fillColor = color.WithAlpha(70);
                using var areaPaint = new SKPaint { Color = fillColor, Style = SKPaintStyle.Fill, IsAntialias = antialias };
                canvas.DrawPath(areaPath, areaPaint);
            }

            using var linePaint = new SKPaint
            {
                Color = color,
                Style = SKPaintStyle.Stroke,
                StrokeWidth = theme.LineWidth,
                IsAntialias = antialias,
                StrokeCap = SKStrokeCap.Round,
                StrokeJoin = SKStrokeJoin.Round
            };
            canvas.DrawPath(linePath, linePaint);

            if (chart.ShowPoints)
            {
                using var pointPaint = new SKPaint { Color = color, Style = SKPaintStyle.Fill, IsAntialias = antialias };
                for (var i = 0; i < data.Count; i++)
                    canvas.DrawCircle(X(i), mapper.MapY(data[i]), theme.LineWidth + 1f, pointPaint);
            }
        }

        DrawCategoryLabels(canvas, chart, theme, plot, typeface, antialias);
    }

    /// <summary>
    /// Draws a sparkline: a single small line filling the box (minus a small inset) with no grid,
    /// axes, labels, or legend. Honors <see cref="ChartElement.Smooth"/> and
    /// <see cref="ChartElement.ShowPoints"/>; uses the first series' flat data.
    /// </summary>
    private static void DrawSparkline(
        SKCanvas canvas, ChartElement chart, ChartTheme theme, float width, float height, bool antialias)
    {
        if (chart.Series.Count == 0)
            return;

        var data = chart.Series[0].Data;
        if (data.Count == 0)
            return;

        var inset = MathF.Max(2f, MathF.Min(width, height) * 0.12f);
        var left = inset;
        var top = inset;
        var right = width - inset;
        var bottom = height - inset;
        if (right <= left || bottom <= top)
            return;

        var (dataMin, dataMax) = DataBounds(chart);
        var mapper = new ValueMapper(dataMin, dataMax, top, bottom);

        var step = data.Count > 1 ? (right - left) / (data.Count - 1) : 0f;
        float X(int i) => data.Count > 1 ? left + (step * i) : (left + right) / 2f;

        var points = new SKPoint[data.Count];
        for (var i = 0; i < data.Count; i++)
            points[i] = new SKPoint(X(i), mapper.MapY(data[i]));

        var palette = chart.Palette ?? ChartPalettes.Default;
        var color = ColorParser.Parse(palette.ColorAt(0));

        using var path = BuildSeriesPath(points, chart.Smooth);
        using var linePaint = new SKPaint
        {
            Color = color,
            Style = SKPaintStyle.Stroke,
            StrokeWidth = MathF.Max(1f, theme.LineWidth - 0.5f),
            IsAntialias = antialias,
            StrokeCap = SKStrokeCap.Round,
            StrokeJoin = SKStrokeJoin.Round
        };
        canvas.DrawPath(path, linePaint);

        if (chart.ShowPoints)
        {
            using var pointPaint = new SKPaint { Color = color, Style = SKPaintStyle.Fill, IsAntialias = antialias };
            for (var i = 0; i < data.Count; i++)
                canvas.DrawCircle(X(i), mapper.MapY(data[i]), theme.LineWidth, pointPaint);
        }
    }

    /// <summary>
    /// Draws a scatter (dots) or bubble (radius-scaled dots) chart. Both axes are scaled to the
    /// point data ranges; bubble marker radii are mapped from the largest <see cref="ChartPoint.R"/>
    /// to a fixed maximum pixel radius so the largest bubble fits comfortably in the plot.
    /// </summary>
    private static void DrawScatterOrBubble(
        SKCanvas canvas, ChartElement chart, ChartTheme theme,
        float width, float height, SKTypeface? typeface, bool isBubble, bool antialias)
    {
        var (minX, maxX, minY, maxY) = PointBounds(chart);
        var xScale = AxisScale.Compute(minX, maxX, targetTicks: 5);
        var yScale = AxisScale.Compute(minY, maxY, targetTicks: 5);

        var hasTitle = !string.IsNullOrEmpty(chart.Title);
        var plot = ChartLayout.ComputePlotArea(
            width, height, hasTitle, chart.Legend,
            axisGutterLeft: 44f, axisGutterBottom: 22f, titleHeight: theme.TitleSize + 8f, legendExtent: 28f);

        DrawGridAndYAxis(canvas, theme, plot, yScale, typeface, antialias);

        var yMapper = new ValueMapper(yScale.Min, yScale.Max, plot.Top, plot.Bottom);

        // X mapping mirrors ValueMapper.MapY but along the horizontal axis (min -> left, max -> right).
        var plotLeft = plot.Left;
        var plotWidth = plot.Width;
        var xMin = xScale.Min;
        var xSpan = xScale.Max - xScale.Min;
        float MapX(double value) => xSpan <= 0d
            ? plotLeft + (plotWidth / 2f)
            : plotLeft + (float)(((value - xMin) / xSpan) * plotWidth);

        // Determine the largest radius for bubble scaling.
        var maxR = 0d;
        if (isBubble)
        {
            foreach (var s in chart.Series)
                foreach (var p in s.Points)
                    if (p.R > maxR) maxR = p.R;
        }
        var maxPixelRadius = MathF.Max(4f, MathF.Min(plot.Width, plot.Height) * 0.12f);
        const float scatterRadius = 4f;

        var palette = chart.Palette ?? ChartPalettes.Default;

        for (var si = 0; si < chart.Series.Count; si++)
        {
            var pts = chart.Series[si].Points;
            if (pts.Count == 0)
                continue;

            var color = ColorParser.Parse(palette.ColorAt(si));
            using var paint = new SKPaint
            {
                Color = isBubble ? color.WithAlpha(150) : color,
                Style = SKPaintStyle.Fill,
                IsAntialias = antialias
            };

            foreach (var p in pts)
            {
                var cx = MapX(p.X);
                var cy = yMapper.MapY(p.Y);
                float radius;
                if (isBubble && maxR > 0d)
                    radius = MathF.Max(2f, (float)(p.R / maxR) * maxPixelRadius);
                else
                    radius = scatterRadius;
                canvas.DrawCircle(cx, cy, radius, paint);
            }
        }
    }

    /// <summary>
    /// Draws a 270-degree gauge dial: a faint background arc plus a colored value arc proportional
    /// to value/max, with the value and optional caption centered below the dial.
    /// </summary>
    private static void DrawGauge(
        SKCanvas canvas, ChartElement chart, ChartTheme theme,
        float width, float height, SKTypeface? typeface, bool antialias)
    {
        var value = chart.Value ?? 0d;
        var max = chart.Max is > 0d ? chart.Max.Value : 100d;
        var fraction = max <= 0d ? 0d : Math.Clamp(value / max, 0d, 1d);

        // Geometry: a 270-degree sweep starting at 135 degrees (bottom-left), open at the bottom.
        const float startAngle = 135f;
        const float fullSweep = 270f;

        var diameter = MathF.Min(width, height) * 0.7f;
        var radius = diameter / 2f;
        var cx = width / 2f;
        var cy = height / 2f;
        var thickness = MathF.Max(6f, radius * 0.22f);

        var bounds = new SKRect(cx - radius, cy - radius, cx + radius, cy + radius);
        var palette = chart.Palette ?? ChartPalettes.Default;
        var valueColor = ColorParser.Parse(palette.ColorAt(0));

        using var trackPaint = new SKPaint
        {
            Color = ColorParser.Parse(theme.GridColor),
            Style = SKPaintStyle.Stroke,
            StrokeWidth = thickness,
            StrokeCap = SKStrokeCap.Round,
            IsAntialias = antialias
        };
        using var track = new SKPath();
        track.AddArc(bounds, startAngle, fullSweep);
        canvas.DrawPath(track, trackPaint);

        if (fraction > 0d)
        {
            using var valuePaint = new SKPaint
            {
                Color = valueColor,
                Style = SKPaintStyle.Stroke,
                StrokeWidth = thickness,
                StrokeCap = SKStrokeCap.Round,
                IsAntialias = antialias
            };
            using var valueArc = new SKPath();
            valueArc.AddArc(bounds, startAngle, (float)(fullSweep * fraction));
            canvas.DrawPath(valueArc, valuePaint);
        }

        DrawIndicatorText(canvas, chart, theme, value, max, cx, cy, typeface, antialias);
    }

    /// <summary>
    /// Draws the centered numeric value (and optional caption) for gauge/progress charts when a
    /// typeface is available.
    /// </summary>
    private static void DrawIndicatorText(
        SKCanvas canvas, ChartElement chart, ChartTheme theme, double value, double max,
        float cx, float cy, SKTypeface? typeface, bool antialias)
    {
        if (typeface is null)
            return;

        var valueText = FormatTick(value);
        using var valueFont = new SKFont(typeface, theme.TitleSize);
        using var valuePaint = new SKPaint { Color = ColorParser.Parse(theme.TitleColor), IsAntialias = antialias };
        var vw = valueFont.MeasureText(valueText);
        canvas.DrawText(valueText, cx - (vw / 2f), cy + (theme.TitleSize / 3f), SKTextAlign.Left, valueFont, valuePaint);

        if (string.IsNullOrEmpty(chart.ValueLabel))
            return;

        using var capFont = new SKFont(typeface, theme.LabelSize);
        using var capPaint = new SKPaint { Color = ColorParser.Parse(theme.LabelColor), IsAntialias = antialias };
        var cw = capFont.MeasureText(chart.ValueLabel);
        canvas.DrawText(chart.ValueLabel, cx - (cw / 2f), cy + theme.TitleSize, SKTextAlign.Left, capFont, capPaint);
    }

    /// <summary>
    /// Builds an open path through the given series points. When <paramref name="smooth"/> is true,
    /// adjacent points are joined with cubic Bézier segments derived from Catmull-Rom tangents,
    /// producing a smooth curve; otherwise the points are joined with straight line segments. The
    /// caller owns and must dispose the returned path.
    /// </summary>
    /// <param name="points">The series points in plot pixel coordinates.</param>
    /// <param name="smooth">Whether to build a smooth curve instead of straight segments.</param>
    /// <returns>A new <see cref="SKPath"/> starting at the first point and ending at the last.</returns>
    private static SKPath BuildSeriesPath(SKPoint[] points, bool smooth)
    {
        var path = new SKPath();
        if (points.Length == 0)
            return path;

        path.MoveTo(points[0]);
        if (points.Length == 1)
            return path;

        if (!smooth)
        {
            for (var i = 1; i < points.Length; i++)
                path.LineTo(points[i]);
            return path;
        }

        // Catmull-Rom to cubic Bézier: for each segment P[i] -> P[i+1], the control points use the
        // tangents at the endpoints, estimated from the neighbouring points (clamped at the ends).
        for (var i = 0; i < points.Length - 1; i++)
        {
            var p0 = points[Math.Max(i - 1, 0)];
            var p1 = points[i];
            var p2 = points[i + 1];
            var p3 = points[Math.Min(i + 2, points.Length - 1)];

            var c1 = new SKPoint(
                p1.X + ((p2.X - p0.X) / 6f),
                p1.Y + ((p2.Y - p0.Y) / 6f));
            var c2 = new SKPoint(
                p2.X - ((p3.X - p1.X) / 6f),
                p2.Y - ((p3.Y - p1.Y) / 6f));

            path.CubicTo(c1, c2, p2);
        }

        return path;
    }

    /// <summary>
    /// Draws a pie or donut chart from the first series' values. Slices are proportional to each
    /// value's share of the total; donut leaves a hollow center.
    /// </summary>
    private static void DrawPie(
        SKCanvas canvas, ChartElement chart, ChartTheme theme,
        float width, float height, SKTypeface? typeface, bool isDonut, bool antialias)
    {
        if (chart.Series.Count == 0)
            return;

        var data = chart.Series[0].Data;
        var total = 0d;
        foreach (var v in data)
        {
            if (v > 0d)
                total += v;
        }
        if (total <= 0d)
        {
            DrawNoData(canvas, theme, width, height, typeface, antialias);
            return;
        }

        var hasTitle = !string.IsNullOrEmpty(chart.Title);
        var top = hasTitle ? theme.TitleSize + 8f : 0f;
        var legendReserve = chart.Legend == LegendPosition.Bottom ? 28f : 0f;
        var availH = height - top - legendReserve;
        var availW = width;

        var diameter = MathF.Min(availW, availH) * 0.85f;
        var radius = diameter / 2f;
        var cx = width / 2f;
        var cy = top + (availH / 2f);

        var palette = chart.Palette ?? ChartPalettes.Default;
        var bounds = new SKRect(cx - radius, cy - radius, cx + radius, cy + radius);

        var startAngle = -90f; // start at 12 o'clock
        for (var i = 0; i < data.Count; i++)
        {
            if (data[i] <= 0d)
                continue;

            var sweep = (float)(data[i] / total * 360d);
            using var slice = new SKPath();
            slice.MoveTo(cx, cy);
            slice.ArcTo(bounds, startAngle, sweep, forceMoveTo: false);
            slice.Close();

            using var paint = new SKPaint
            {
                Color = ColorParser.Parse(palette.ColorAt(i)),
                Style = SKPaintStyle.Fill,
                IsAntialias = antialias
            };
            canvas.DrawPath(slice, paint);

            startAngle += sweep;
        }

        if (isDonut)
        {
            using var hole = new SKPaint
            {
                Color = string.IsNullOrEmpty(theme.BackgroundColor)
                    ? SKColors.White
                    : ColorParser.Parse(theme.BackgroundColor),
                Style = SKPaintStyle.Fill,
                IsAntialias = antialias
            };
            canvas.DrawCircle(cx, cy, radius * 0.55f, hole);
        }

        DrawPieLabels(canvas, chart, theme, data, total, cx, cy, radius, isDonut, typeface, antialias);
    }

    /// <summary>
    /// Draws a text label at each slice's mid-angle when a typeface is available and the chart's
    /// <see cref="PieLabelMode"/> is not <see cref="PieLabelMode.None"/>. Labels are placed on a
    /// ring between the (optional) donut hole and the outer edge, centered on their anchor point.
    /// </summary>
    private static void DrawPieLabels(
        SKCanvas canvas, ChartElement chart, ChartTheme theme,
        IReadOnlyList<double> data, double total, float cx, float cy, float radius, bool isDonut,
        SKTypeface? typeface, bool antialias)
    {
        if (typeface is null || chart.PieLabels == PieLabelMode.None || total <= 0d)
            return;

        // For donuts the hole occupies the inner 55% of the radius, so place labels in the visible
        // ring; for pies use ~0.7 of the radius from the center.
        var labelRadius = isDonut ? radius * 0.78f : radius * 0.7f;

        using var font = new SKFont(typeface, theme.LabelSize);
        using var paint = new SKPaint { Color = ColorParser.Parse(theme.LabelColor), IsAntialias = antialias };

        var startAngle = -90f; // matches the slice drawing start (12 o'clock)
        for (var i = 0; i < data.Count; i++)
        {
            if (data[i] <= 0d)
                continue;

            var sweep = (float)(data[i] / total * 360d);
            var midAngle = startAngle + (sweep / 2f);
            startAngle += sweep;

            // Skip very thin slices to avoid overlapping clutter.
            if (sweep < 6f)
                continue;

            var label = chart.PieLabels == PieLabelMode.Value
                ? FormatTick(data[i])
                : $"{Math.Round(data[i] / total * 100d):0}%";

            var rad = midAngle * (MathF.PI / 180f);
            var tx = cx + (labelRadius * MathF.Cos(rad));
            var ty = cy + (labelRadius * MathF.Sin(rad));

            var tw = font.MeasureText(label);
            canvas.DrawText(label, tx - (tw / 2f), ty + (theme.LabelSize / 3f), SKTextAlign.Left, font, paint);
        }
    }

    /// <summary>Draws the centred chart title in the reserved top band, when present.</summary>
    private static void DrawTitle(
        SKCanvas canvas, ChartElement chart, ChartTheme theme, float width, SKTypeface? typeface, bool antialias)
    {
        if (typeface is null || string.IsNullOrEmpty(chart.Title))
            return;

        using var font = new SKFont(typeface, theme.TitleSize);
        using var paint = new SKPaint { Color = ColorParser.Parse(theme.TitleColor), IsAntialias = antialias };
        var tw = font.MeasureText(chart.Title);
        canvas.DrawText(chart.Title, (width - tw) / 2f, theme.TitleSize + 2f, SKTextAlign.Left, font, paint);
    }

    /// <summary>
    /// Draws a simple legend (colored swatch + series label) for each labeled series.
    /// Supports the bottom legend band; other positions reserve space but use the same row layout.
    /// </summary>
    private static void DrawLegend(
        SKCanvas canvas, ChartElement chart, ChartTheme theme,
        float width, float height, SKTypeface? typeface, bool antialias)
    {
        if (typeface is null || chart.Legend == LegendPosition.None)
            return;

        using var font = new SKFont(typeface, theme.LabelSize);
        using var textPaint = new SKPaint { Color = ColorParser.Parse(theme.LabelColor), IsAntialias = antialias };

        var palette = chart.Palette ?? ChartPalettes.Default;
        const float swatch = 10f;
        const float gap = 6f;
        const float itemGap = 16f;

        // Build entries: for pie/donut use categories, otherwise use series labels.
        var labels = new List<string>();
        if (chart.ChartType is ChartType.Pie or ChartType.Donut)
        {
            foreach (var c in chart.Categories)
                labels.Add(c);
        }
        else
        {
            for (var i = 0; i < chart.Series.Count; i++)
                labels.Add(chart.Series[i].Label ?? $"Series {i + 1}");
        }

        if (labels.Count == 0)
            return;

        // Measure total width for centring along the bottom.
        var totalWidth = 0f;
        foreach (var label in labels)
            totalWidth += swatch + gap + font.MeasureText(label) + itemGap;
        totalWidth -= itemGap;

        var startX = (width - totalWidth) / 2f;
        var rowY = chart.Legend == LegendPosition.Top
            ? theme.LabelSize + 4f
            : height - (theme.LabelSize / 2f) - 4f;

        var x = startX;
        for (var i = 0; i < labels.Count; i++)
        {
            using (var swatchPaint = new SKPaint
            {
                Color = ColorParser.Parse(palette.ColorAt(i)),
                Style = SKPaintStyle.Fill,
                IsAntialias = antialias
            })
            {
                canvas.DrawRect(x, rowY - swatch, swatch, swatch, swatchPaint);
            }

            x += swatch + gap;
            canvas.DrawText(labels[i], x, rowY, SKTextAlign.Left, font, textPaint);
            x += font.MeasureText(labels[i]) + itemGap;
        }
    }

    /// <summary>Formats a tick value compactly, trimming trailing zeros.</summary>
    private static string FormatTick(double value)
    {
        if (value == Math.Floor(value) && Math.Abs(value) < 1e15)
            return ((long)value).ToString(System.Globalization.CultureInfo.InvariantCulture);
        return value.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture);
    }
}
