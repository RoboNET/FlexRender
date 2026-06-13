using System.Collections.Generic;
using FlexRender.Charts;
using FlexRender.Parsing.Ast;
using FlexRender.Rendering;
using SkiaSharp;
using Xunit;

namespace FlexRender.Tests.Rendering;

/// <summary>
/// Verifies pie and donut charts draw slices, and the donut leaves a hollow center.
/// </summary>
public sealed class ChartPieRenderTests
{
    private static ChartElement Make(ChartType type) => new(type, new List<ChartSeries>
    {
        ChartSeries.FromInline(null, new[] { 30d, 50d, 20d })
    })
    {
        Categories = new[] { "A", "B", "C" },
        Legend = LegendPosition.None,
        Palette = new ChartPalette(new[] { "#ff0000", "#00ff00", "#0000ff" }),
        Theme = ChartThemes.Default,
        PieLabels = PieLabelMode.None
    };

    [Fact]
    public void Pie_DrawsColoredSlices()
    {
        using var bitmap = Render(Make(ChartType.Pie));
        Assert.True(HasColor(bitmap, redDominant: true), "Expected red slice pixels.");
    }

    [Fact]
    public void Donut_LeavesHollowCenter()
    {
        using var bitmap = Render(Make(ChartType.Donut));
        var center = bitmap.GetPixel(bitmap.Width / 2, bitmap.Height / 2);
        Assert.True(center.Red > 230 && center.Green > 230 && center.Blue > 230,
            $"Expected white donut center, got {center}.");
    }

    private static SKBitmap Render(ChartElement chart)
    {
        var bitmap = new SKBitmap(200, 200, SKColorType.Rgba8888, SKAlphaType.Premul);
        using var canvas = new SKCanvas(bitmap);
        canvas.Clear(SKColors.White);
        ChartRenderer.Draw(canvas, chart, 0f, 0f, 200f, 200f, typeface: null, antialias: true);
        return bitmap;
    }

    private static bool HasColor(SKBitmap bitmap, bool redDominant)
    {
        for (var y = 0; y < bitmap.Height; y += 2)
        for (var x = 0; x < bitmap.Width; x += 2)
        {
            var p = bitmap.GetPixel(x, y);
            if (redDominant && p.Red > 200 && p.Green < 120 && p.Blue < 120)
                return true;
        }
        return false;
    }
}
