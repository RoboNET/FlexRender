using System.Collections.Generic;
using FlexRender.Charts;
using FlexRender.Parsing.Ast;
using FlexRender.Rendering;
using SkiaSharp;
using Xunit;

namespace FlexRender.Tests.Rendering;

/// <summary>
/// Verifies line and area charts draw with the palette color.
/// </summary>
public sealed class ChartLineAreaRenderTests
{
    private static ChartElement Make(ChartType type) => new(type, new List<ChartSeries>
    {
        ChartSeries.FromInline("a", new[] { 10d, 30d, 20d, 40d })
    })
    {
        Categories = new[] { "A", "B", "C", "D" },
        Legend = LegendPosition.None,
        Palette = new ChartPalette(new[] { "#00aa00" }),
        Theme = ChartThemes.Default
    };

    [Fact]
    public void Line_DrawsGreenPixels()
    {
        var chart = Make(ChartType.Line);
        Assert.True(Render(chart), "Expected green line pixels.");
    }

    [Fact]
    public void Area_DrawsGreenPixels()
    {
        var chart = Make(ChartType.Area);
        Assert.True(Render(chart), "Expected green area pixels.");
    }

    private static bool Render(ChartElement chart)
    {
        using var bitmap = new SKBitmap(300, 200, SKColorType.Rgba8888, SKAlphaType.Premul);
        using var canvas = new SKCanvas(bitmap);
        canvas.Clear(SKColors.White);

        ChartRenderer.Draw(canvas, chart, 0f, 0f, 300f, 200f, typeface: null, antialias: true);

        for (var y = 0; y < bitmap.Height; y += 2)
        for (var x = 0; x < bitmap.Width; x += 2)
        {
            var p = bitmap.GetPixel(x, y);
            if (p.Green > 120 && p.Red < 120 && p.Blue < 120)
                return true;
        }
        return false;
    }
}
