using System.Collections.Generic;
using FlexRender.Charts;
using FlexRender.Parsing.Ast;
using FlexRender.Rendering;
using SkiaSharp;
using Xunit;

namespace FlexRender.Tests.Rendering;

/// <summary>
/// Verifies the heatmap draws value-colored cells.
/// </summary>
public sealed class ChartHeatmapRenderTests
{
    [Fact]
    public void Heatmap_DrawsHighValueCellInHighColor()
    {
        // Two rows of two cells; the max value (40) cell must paint the palette "high" color (red).
        var chart = new ChartElement(ChartType.Heatmap, new List<ChartSeries>
        {
            ChartSeries.FromInline("r0", new[] { 0d, 10d }),
            ChartSeries.FromInline("r1", new[] { 20d, 40d })
        })
        {
            Legend = LegendPosition.None,
            // low = white, high = red, so the highest cell is red and the lowest is white.
            Palette = new ChartPalette(new[] { "#ffffff", "#ff0000" }),
            Theme = ChartThemes.Default
        };

        using var bitmap = new SKBitmap(200, 200, SKColorType.Rgba8888, SKAlphaType.Premul);
        using var canvas = new SKCanvas(bitmap);
        canvas.Clear(SKColors.White);

        ChartRenderer.Draw(canvas, chart, 0f, 0f, 200f, 200f, typeface: null, antialias: false);

        Assert.True(HasRedPixel(bitmap), "Expected the highest-value cell to be red.");
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
}
