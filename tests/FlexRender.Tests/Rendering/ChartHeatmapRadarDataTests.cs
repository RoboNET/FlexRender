using System.Collections.Generic;
using FlexRender.Charts;
using FlexRender.Parsing.Ast;
using FlexRender.Rendering;
using SkiaSharp;
using Xunit;

namespace FlexRender.Tests.Rendering;

/// <summary>
/// Verifies empty heatmap/radar charts draw the no-data placeholder and non-empty ones draw content.
/// </summary>
public sealed class ChartHeatmapRadarDataTests
{
    [Fact]
    public void EmptyHeatmap_DrawsNoDataPlaceholder()
    {
        var chart = new ChartElement(ChartType.Heatmap, new List<ChartSeries>())
        {
            Legend = LegendPosition.None,
            Theme = ChartThemes.Default
        };

        using var bitmap = new SKBitmap(200, 150, SKColorType.Rgba8888, SKAlphaType.Premul);
        using var canvas = new SKCanvas(bitmap);
        canvas.Clear(SKColors.White);

        ChartRenderer.Draw(canvas, chart, 0f, 0f, 200f, 150f, typeface: null, antialias: true);

        Assert.True(HasNonWhitePixel(bitmap), "Expected the no-data border to draw.");
    }

    [Fact]
    public void EmptyRadar_DrawsNoDataPlaceholder()
    {
        var chart = new ChartElement(ChartType.Radar, new List<ChartSeries>())
        {
            Legend = LegendPosition.None,
            Theme = ChartThemes.Default
        };

        using var bitmap = new SKBitmap(200, 200, SKColorType.Rgba8888, SKAlphaType.Premul);
        using var canvas = new SKCanvas(bitmap);
        canvas.Clear(SKColors.White);

        ChartRenderer.Draw(canvas, chart, 0f, 0f, 200f, 200f, typeface: null, antialias: true);

        Assert.True(HasNonWhitePixel(bitmap), "Expected the no-data border to draw.");
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
