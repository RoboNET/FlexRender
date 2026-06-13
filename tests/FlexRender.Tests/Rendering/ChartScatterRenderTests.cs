using System.Collections.Generic;
using FlexRender.Charts;
using FlexRender.Parsing.Ast;
using FlexRender.Rendering;
using SkiaSharp;
using Xunit;

namespace FlexRender.Tests.Rendering;

/// <summary>
/// Verifies scatter and bubble draw palette-colored markers.
/// </summary>
public sealed class ChartScatterRenderTests
{
    [Fact]
    public void Scatter_DrawsColoredDots()
    {
        var chart = new ChartElement(ChartType.Scatter, new List<ChartSeries>
        {
            ChartSeries.FromPoints("cloud", new List<ChartPoint>
            {
                ChartPoint.Xy(1d, 10d), ChartPoint.Xy(5d, 25d), ChartPoint.Xy(9d, 18d)
            })
        })
        {
            Legend = LegendPosition.None,
            Palette = new ChartPalette(new[] { "#ff0000" }),
            Theme = ChartThemes.Default
        };

        using var bitmap = new SKBitmap(300, 200, SKColorType.Rgba8888, SKAlphaType.Premul);
        using var canvas = new SKCanvas(bitmap);
        canvas.Clear(SKColors.White);

        ChartRenderer.Draw(canvas, chart, 0f, 0f, 300f, 200f, typeface: null, antialias: true);

        Assert.True(HasRedPixel(bitmap), "Expected red scatter dots.");
    }

    [Fact]
    public void Bubble_DrawsLargerMarkersForLargerRadius()
    {
        var chart = new ChartElement(ChartType.Bubble, new List<ChartSeries>
        {
            ChartSeries.FromPoints("b", new List<ChartPoint>
            {
                new ChartPoint(2d, 10d, 2d), new ChartPoint(8d, 20d, 30d)
            })
        })
        {
            Legend = LegendPosition.None,
            Palette = new ChartPalette(new[] { "#0000ff" }),
            Theme = ChartThemes.Default
        };

        using var bitmap = new SKBitmap(300, 200, SKColorType.Rgba8888, SKAlphaType.Premul);
        using var canvas = new SKCanvas(bitmap);
        canvas.Clear(SKColors.White);

        ChartRenderer.Draw(canvas, chart, 0f, 0f, 300f, 200f, typeface: null, antialias: true);

        Assert.True(CountBluePixels(bitmap) > 50, "Expected substantial blue bubble area.");
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

    private static int CountBluePixels(SKBitmap bitmap)
    {
        var count = 0;
        for (var y = 0; y < bitmap.Height; y++)
        for (var x = 0; x < bitmap.Width; x++)
        {
            var p = bitmap.GetPixel(x, y);
            if (p.Blue > 150 && p.Red < 120 && p.Green < 120)
                count++;
        }
        return count;
    }
}
