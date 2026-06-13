using System.Collections.Generic;
using FlexRender.Charts;
using FlexRender.Parsing.Ast;
using FlexRender.Rendering;
using SkiaSharp;
using Xunit;

namespace FlexRender.Tests.Rendering;

/// <summary>
/// Verifies the sparkline draws a colored line with no axis chrome.
/// </summary>
public sealed class ChartSparklineRenderTests
{
    [Fact]
    public void Sparkline_DrawsColoredLine()
    {
        var chart = new ChartElement(ChartType.Sparkline, new List<ChartSeries>
        {
            ChartSeries.FromInline(null, new[] { 3d, 8d, 4d, 10d, 6d })
        })
        {
            Legend = LegendPosition.None,
            Palette = new ChartPalette(new[] { "#ff0000" }),
            Theme = ChartThemes.Default
        };

        using var bitmap = new SKBitmap(120, 40, SKColorType.Rgba8888, SKAlphaType.Premul);
        using var canvas = new SKCanvas(bitmap);
        canvas.Clear(SKColors.White);

        ChartRenderer.Draw(canvas, chart, 0f, 0f, 120f, 40f, typeface: null, antialias: true);

        Assert.True(HasRedPixel(bitmap), "Expected a red sparkline.");
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
