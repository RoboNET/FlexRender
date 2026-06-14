using System;
using System.Collections.Generic;
using FlexRender.Charts;
using FlexRender.Parsing.Ast;
using FlexRender.Rendering;
using SkiaSharp;
using Xunit;

namespace FlexRender.Tests.Rendering;

/// <summary>
/// Verifies smooth line charts draw a curve (green pixels, no crash) and that the curved render
/// differs measurably from the straight-segment render of the same data.
/// </summary>
public sealed class ChartSmoothLineRenderTests
{
    [Fact]
    public void SmoothLine_DrawsGreenPixels()
    {
        using var bitmap = RenderLine(smooth: true);

        Assert.True(CountGreenPixels(bitmap) > 0, "Expected green pixels from the smooth line.");
    }

    [Fact]
    public void SmoothLine_DiffersFromStraightLine()
    {
        using var smoothBitmap = RenderLine(smooth: true);
        using var straightBitmap = RenderLine(smooth: false);

        var smoothCount = CountGreenPixels(smoothBitmap);
        var straightCount = CountGreenPixels(straightBitmap);

        Assert.True(smoothCount > 0, "Expected green pixels from the smooth line.");
        Assert.True(straightCount > 0, "Expected green pixels from the straight line.");

        // A Catmull-Rom curve rounds/overshoots the zig-zag, so the set of covered pixels differs
        // from the straight polyline. Comparing the green pixel counts is a tolerant proxy.
        Assert.NotEqual(straightCount, smoothCount);
    }

    private static SKBitmap RenderLine(bool smooth)
    {
        var chart = new ChartElement(ChartType.Line, new List<ChartSeries>
        {
            ChartSeries.FromInline("a", new[] { 10d, 40d, 10d, 40d })
        })
        {
            Categories = new[] { "A", "B", "C", "D" },
            Legend = LegendPosition.None,
            Smooth = smooth,
            Palette = new ChartPalette(new[] { "#00ff00" }),
            Theme = ChartThemes.Default
        };

        var bitmap = new SKBitmap(300, 200, SKColorType.Rgba8888, SKAlphaType.Premul);
        using var canvas = new SKCanvas(bitmap);
        canvas.Clear(SKColors.White);
        ChartRenderer.Draw(canvas, chart, 0f, 0f, 300f, 200f, typeface: null, antialias: true);
        return bitmap;
    }

    private static int CountGreenPixels(SKBitmap bitmap)
    {
        var count = 0;
        for (var y = 0; y < bitmap.Height; y++)
        for (var x = 0; x < bitmap.Width; x++)
        {
            var p = bitmap.GetPixel(x, y);
            if (p.Green > 150 && p.Red < 120 && p.Blue < 120)
                count++;
        }
        return count;
    }
}
