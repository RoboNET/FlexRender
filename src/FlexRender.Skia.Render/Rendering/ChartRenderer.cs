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

            // Series geometry is added in subsequent tasks (bar/line/area/pie/donut).
            DrawSeries(canvas, theme, width, height, antialias);
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
    /// Draws series geometry by chart type. Phase-2 bar geometry is added in Task 17; this
    /// placeholder keeps the dispatch surface in place so smoke tests can run.
    /// </summary>
    private static void DrawSeries(SKCanvas canvas, ChartTheme theme, float width, float height, bool antialias)
    {
        // Filled in by subsequent tasks. Until then, draw a faint plot border so a chart with
        // data is visibly non-blank in smoke tests.
        using var border = new SKPaint
        {
            Color = ColorParser.Parse(theme.AxisColor),
            Style = SKPaintStyle.Stroke,
            StrokeWidth = 1f,
            IsAntialias = antialias
        };
        canvas.DrawRect(0.5f, 0.5f, width - 1f, height - 1f, border);
    }
}
