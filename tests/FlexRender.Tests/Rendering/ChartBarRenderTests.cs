using System;
using System.Collections.Generic;
using FlexRender.Charts;
using FlexRender.Parsing.Ast;
using FlexRender.Rendering;
using SkiaSharp;
using Xunit;

namespace FlexRender.Tests.Rendering;

/// <summary>
/// Verifies bar geometry is drawn with the palette color.
/// </summary>
public sealed class ChartBarRenderTests
{
    [Fact]
    public void VerticalBars_DrawPaletteColoredColumns()
    {
        var chart = new ChartElement(ChartType.Bar, new List<ChartSeries>
        {
            ChartSeries.FromInline("a", new[] { 10d, 20d, 30d })
        })
        {
            Categories = new[] { "A", "B", "C" },
            Legend = LegendPosition.None,
            Palette = new ChartPalette(new[] { "#ff0000" }),
            Theme = ChartThemes.Default
        };

        using var bitmap = new SKBitmap(300, 200, SKColorType.Rgba8888, SKAlphaType.Premul);
        using var canvas = new SKCanvas(bitmap);
        canvas.Clear(SKColors.White);

        ChartRenderer.Draw(canvas, chart, 0f, 0f, 300f, 200f, typeface: null, antialias: true);

        Assert.True(HasRedPixel(bitmap), "Expected red bar pixels.");
    }

    private static bool HasRedPixel(SKBitmap bitmap)
    {
        for (var y = 0; y < bitmap.Height; y += 2)
        for (var x = 0; x < bitmap.Width; x += 2)
        {
            var p = bitmap.GetPixel(x, y);
            if (p.Red > 200 && p.Green < 80 && p.Blue < 80)
                return true;
        }
        return false;
    }
}
