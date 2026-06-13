using System;
using System.Collections.Generic;
using FlexRender.Charts;
using FlexRender.Parsing.Ast;
using FlexRender.Rendering;
using SkiaSharp;
using Xunit;

namespace FlexRender.Tests.Rendering;

/// <summary>
/// Verifies stacked bar charts draw one bar per category with each series segment stacked on the
/// previous, so both series colors appear and the second series sits above the first.
/// </summary>
public sealed class ChartStackedBarRenderTests
{
    [Fact]
    public void StackedVerticalBars_StackSeriesSegments()
    {
        var chart = new ChartElement(ChartType.Bar, new List<ChartSeries>
        {
            ChartSeries.FromInline("first", new[] { 10d, 20d, 30d }),
            ChartSeries.FromInline("second", new[] { 15d, 25d, 35d })
        })
        {
            Categories = new[] { "A", "B", "C" },
            Legend = LegendPosition.None,
            Stacked = true,
            Palette = new ChartPalette(new[] { "#ff0000", "#0000ff" }),
            Theme = ChartThemes.Default
        };

        using var bitmap = new SKBitmap(300, 200, SKColorType.Rgba8888, SKAlphaType.Premul);
        using var canvas = new SKCanvas(bitmap);
        canvas.Clear(SKColors.White);

        ChartRenderer.Draw(canvas, chart, 0f, 0f, 300f, 200f, typeface: null, antialias: true);

        var hasRed = false;
        var hasBlue = false;
        var stackedColumnFound = false;

        // In a stacked column both series share the same x-column: blue (series 1) is stacked
        // directly above red (series 0). The discriminating signal versus grouped bars (which
        // place each series in a separate x-column) is an x-column that contains BOTH red and
        // blue with blue's lowest pixel sitting above red's highest pixel.
        for (var x = 0; x < bitmap.Width; x++)
        {
            var minRedY = int.MaxValue;
            var maxBlueY = int.MinValue;
            var columnHasRed = false;
            var columnHasBlue = false;

            for (var y = 0; y < bitmap.Height; y++)
            {
                var p = bitmap.GetPixel(x, y);
                if (p.Red > 200 && p.Green < 80 && p.Blue < 80)
                {
                    hasRed = true;
                    columnHasRed = true;
                    if (y < minRedY) minRedY = y;
                }
                else if (p.Blue > 200 && p.Red < 80 && p.Green < 80)
                {
                    hasBlue = true;
                    columnHasBlue = true;
                    if (y > maxBlueY) maxBlueY = y;
                }
            }

            if (columnHasRed && columnHasBlue && maxBlueY < minRedY)
                stackedColumnFound = true;
        }

        Assert.True(hasRed, "Expected red pixels from the first stacked series segment.");
        Assert.True(hasBlue, "Expected blue pixels from the second stacked series segment.");
        Assert.True(stackedColumnFound,
            "Expected at least one x-column with blue stacked directly above red (stacked, not grouped).");
    }
}
