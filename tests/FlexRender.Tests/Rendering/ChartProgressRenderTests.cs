using System.Collections.Generic;
using FlexRender.Charts;
using FlexRender.Parsing.Ast;
using FlexRender.Rendering;
using SkiaSharp;
using Xunit;

namespace FlexRender.Tests.Rendering;

/// <summary>
/// Verifies the progress ring draws a colored value arc.
/// </summary>
public sealed class ChartProgressRenderTests
{
    [Fact]
    public void Progress_DrawsColoredRingArc()
    {
        var chart = new ChartElement(ChartType.Progress, new List<ChartSeries>())
        {
            Value = 40d,
            Max = 100d,
            Legend = LegendPosition.None,
            Palette = new ChartPalette(new[] { "#ff0000" }),
            Theme = ChartThemes.Default
        };

        using var bitmap = new SKBitmap(200, 200, SKColorType.Rgba8888, SKAlphaType.Premul);
        using var canvas = new SKCanvas(bitmap);
        canvas.Clear(SKColors.White);

        ChartRenderer.Draw(canvas, chart, 0f, 0f, 200f, 200f, typeface: null, antialias: true);

        Assert.True(HasRedPixel(bitmap), "Expected a red progress arc.");
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
