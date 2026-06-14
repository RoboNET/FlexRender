using System.Collections.Generic;
using FlexRender.Charts;
using FlexRender.Parsing.Ast;
using FlexRender.Rendering;
using SkiaSharp;
using Xunit;

namespace FlexRender.Tests.Rendering;

/// <summary>
/// Verifies radar draws a palette-colored polygon for each series.
/// </summary>
public sealed class ChartRadarRenderTests
{
    [Fact]
    public void Radar_DrawsColoredPolygon()
    {
        var chart = new ChartElement(ChartType.Radar, new List<ChartSeries>
        {
            ChartSeries.FromInline("A", new[] { 4d, 3d, 5d, 2d, 4d })
        })
        {
            Categories = new[] { "Speed", "Power", "Range", "Agility", "Armor" },
            Legend = LegendPosition.None,
            Palette = new ChartPalette(new[] { "#ff0000" }),
            Theme = ChartThemes.Default
        };

        using var bitmap = new SKBitmap(260, 260, SKColorType.Rgba8888, SKAlphaType.Premul);
        using var canvas = new SKCanvas(bitmap);
        canvas.Clear(SKColors.White);

        ChartRenderer.Draw(canvas, chart, 0f, 0f, 260f, 260f, typeface: null, antialias: true);

        Assert.True(HasReddishPixel(bitmap), "Expected a red radar polygon (fill or stroke).");
    }

    [Fact]
    public void Radar_FewerThanThreeCategories_DoesNotThrow()
    {
        var chart = new ChartElement(ChartType.Radar, new List<ChartSeries>
        {
            ChartSeries.FromInline("A", new[] { 4d, 3d })
        })
        {
            Categories = new[] { "X", "Y" },
            Legend = LegendPosition.None,
            Theme = ChartThemes.Default
        };

        using var bitmap = new SKBitmap(200, 200, SKColorType.Rgba8888, SKAlphaType.Premul);
        using var canvas = new SKCanvas(bitmap);
        canvas.Clear(SKColors.White);

        var ex = Record.Exception(() =>
            ChartRenderer.Draw(canvas, chart, 0f, 0f, 200f, 200f, typeface: null, antialias: true));
        Assert.Null(ex);
    }

    private static bool HasReddishPixel(SKBitmap bitmap)
    {
        for (var y = 0; y < bitmap.Height; y++)
        for (var x = 0; x < bitmap.Width; x++)
        {
            var p = bitmap.GetPixel(x, y);
            // The fill is semi-transparent red over white, so allow elevated red with lower G/B.
            if (p.Red > 200 && p.Green < 200 && p.Blue < 200 && (p.Red - p.Green) > 30)
                return true;
        }
        return false;
    }
}
