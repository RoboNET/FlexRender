using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using FlexRender.Charts;
using FlexRender.Parsing.Ast;
using FlexRender.Rendering;
using SkiaSharp;
using Xunit;

namespace FlexRender.Tests.Rendering;

/// <summary>
/// Verifies the title band and bottom legend render text when a typeface is available.
/// </summary>
public sealed class ChartLegendTitleRenderTests
{
    [Fact]
    public void TitleAndLegend_DrawTextInReservedBands()
    {
        using var typeface = LoadInter();
        Assert.NotNull(typeface);

        var chart = new ChartElement(ChartType.Bar, new List<ChartSeries>
        {
            ChartSeries.FromInline("Series One", new[] { 10d, 20d, 30d })
        })
        {
            Categories = new[] { "A", "B", "C" },
            Legend = LegendPosition.Bottom,
            Title = "Revenue",
            Palette = new ChartPalette(new[] { "#3366cc" }),
            Theme = ChartThemes.Default
        };

        using var bitmap = new SKBitmap(320, 240, SKColorType.Rgba8888, SKAlphaType.Premul);
        using var canvas = new SKCanvas(bitmap);
        canvas.Clear(SKColors.White);

        ChartRenderer.Draw(canvas, chart, 0f, 0f, 320f, 240f, typeface, antialias: true);

        Assert.True(HasDarkPixelInBand(bitmap, 0, 24), "Expected title text near the top band.");
        Assert.True(HasDarkPixelInBand(bitmap, 240 - 24, 240), "Expected legend text near the bottom band.");
    }

    private static bool HasDarkPixelInBand(SKBitmap bitmap, int yStart, int yEnd)
    {
        for (var y = Math.Max(0, yStart); y < Math.Min(bitmap.Height, yEnd); y++)
        for (var x = 0; x < bitmap.Width; x++)
        {
            var p = bitmap.GetPixel(x, y);
            if (p.Red < 150 && p.Green < 150 && p.Blue < 150)
                return true;
        }
        return false;
    }

    private static SKTypeface? LoadInter()
    {
        var asmDir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location)!;
        var current = asmDir;
        while (!string.IsNullOrEmpty(current))
        {
            if (Directory.GetFiles(current, "*.csproj").Length > 0)
            {
                var fontPath = Path.Combine(current, "Snapshots", "Fonts", "Inter-Regular.ttf");
                return File.Exists(fontPath) ? SKTypeface.FromFile(fontPath) : null;
            }
            current = Directory.GetParent(current)?.FullName;
        }
        return null;
    }
}
