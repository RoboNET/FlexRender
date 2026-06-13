using System.Collections.Generic;
using FlexRender.Charts;
using FlexRender.Parsing.Ast;
using FlexRender.Rendering;
using SkiaSharp;
using Xunit;

namespace FlexRender.Tests.Rendering;

/// <summary>
/// Verifies horizontal bars extend along the X axis from the left baseline.
/// </summary>
public sealed class ChartHorizontalBarRenderTests
{
    [Fact]
    public void HorizontalBars_DrawWiderForLargerValues()
    {
        var chart = new ChartElement(ChartType.Bar, new List<ChartSeries>
        {
            ChartSeries.FromInline("a", new[] { 5d, 40d })
        })
        {
            Categories = new[] { "A", "B" },
            Legend = LegendPosition.None,
            Horizontal = true,
            Palette = new ChartPalette(new[] { "#0000ff" }),
            Theme = ChartThemes.Default
        };

        using var bitmap = new SKBitmap(300, 200, SKColorType.Rgba8888, SKAlphaType.Premul);
        using var canvas = new SKCanvas(bitmap);
        canvas.Clear(SKColors.White);

        ChartRenderer.Draw(canvas, chart, 0f, 0f, 300f, 200f, typeface: null, antialias: true);

        Assert.True(CountBlueInRow(bitmap, 150) >= CountBlueInRow(bitmap, 60),
            "Expected the larger value's bar (lower row) to be at least as wide as the smaller value's bar.");
    }

    private static int CountBlueInRow(SKBitmap bitmap, int y)
    {
        var count = 0;
        for (var x = 0; x < bitmap.Width; x++)
        {
            var p = bitmap.GetPixel(x, y);
            if (p.Blue > 200 && p.Red < 80 && p.Green < 80)
                count++;
        }
        return count;
    }
}
