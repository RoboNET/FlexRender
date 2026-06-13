using System.Collections.Generic;
using FlexRender.Charts;
using FlexRender.Parsing.Ast;
using FlexRender.Rendering;
using SkiaSharp;
using Xunit;

namespace FlexRender.Tests.Rendering;

/// <summary>
/// Verifies the gauge draws a colored value arc.
/// </summary>
public sealed class ChartGaugeRenderTests
{
    [Fact]
    public void Gauge_DrawsColoredValueArc()
    {
        var chart = new ChartElement(ChartType.Gauge, new List<ChartSeries>())
        {
            Value = 70d,
            Max = 100d,
            Legend = LegendPosition.None,
            Palette = new ChartPalette(new[] { "#ff0000" }),
            Theme = ChartThemes.Default
        };

        using var bitmap = new SKBitmap(240, 200, SKColorType.Rgba8888, SKAlphaType.Premul);
        using var canvas = new SKCanvas(bitmap);
        canvas.Clear(SKColors.White);

        ChartRenderer.Draw(canvas, chart, 0f, 0f, 240f, 200f, typeface: null, antialias: true);

        Assert.True(HasRedPixel(bitmap), "Expected a red gauge arc.");
    }

    [Fact]
    public void Gauge_MissingValue_DrawsNoDataPlaceholder()
    {
        var chart = new ChartElement(ChartType.Gauge, new List<ChartSeries>())
        {
            Legend = LegendPosition.None,
            Theme = ChartThemes.Default
        };

        using var bitmap = new SKBitmap(240, 200, SKColorType.Rgba8888, SKAlphaType.Premul);
        using var canvas = new SKCanvas(bitmap);
        canvas.Clear(SKColors.White);

        ChartRenderer.Draw(canvas, chart, 0f, 0f, 240f, 200f, typeface: null, antialias: true);

        Assert.True(HasNonWhitePixel(bitmap), "Expected the no-data border to draw.");
    }

    private static bool HasRedPixel(SKBitmap bitmap)
    {
        for (var y = 0; y < bitmap.Height; y++)
        for (var x = 0; x < bitmap.Width; x++)
        {
            var p = bitmap.GetPixel(x, y);
            if (p.Red > 180 && p.Green < 100 && p.Blue < 100)
                return true;
        }
        return false;
    }

    private static bool HasNonWhitePixel(SKBitmap bitmap)
    {
        for (var y = 0; y < bitmap.Height; y++)
        for (var x = 0; x < bitmap.Width; x++)
        {
            var p = bitmap.GetPixel(x, y);
            if (p.Red < 240 || p.Green < 240 || p.Blue < 240)
                return true;
        }
        return false;
    }
}
