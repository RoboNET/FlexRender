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
/// Verifies pie/donut slice labels render as text inside the pie area when a typeface is supplied
/// and the chart's <see cref="PieLabelMode"/> is not <see cref="PieLabelMode.None"/>.
/// </summary>
public sealed class ChartPieLabelRenderTests
{
    [Fact]
    public void Pie_DrawsSliceLabelsInsidePieArea()
    {
        using var typeface = LoadInter();
        Assert.NotNull(typeface);

        var chart = new ChartElement(ChartType.Pie, new List<ChartSeries>
        {
            ChartSeries.FromInline(null, new[] { 30d, 50d, 20d })
        })
        {
            Categories = new[] { "Direct", "Social", "Search" },
            Legend = LegendPosition.None,
            PieLabels = PieLabelMode.Percent,
            Palette = new ChartPalette(new[] { "#cccccc", "#dddddd", "#eeeeee" }),
            Theme = ChartThemes.Default
        };

        const int size = 320;
        using var bitmap = new SKBitmap(size, size, SKColorType.Rgba8888, SKAlphaType.Premul);
        using var canvas = new SKCanvas(bitmap);
        canvas.Clear(SKColors.White);

        ChartRenderer.Draw(canvas, chart, 0f, 0f, size, size, typeface, antialias: true);

        // Slice labels sit on a ring around the pie center. Scan a central band (excluding the
        // outer edges and any legend area) for dark text pixels drawn over the light slices.
        var bandTop = size / 4;
        var bandBottom = size * 3 / 4;
        Assert.True(HasDarkPixelInBand(bitmap, bandTop, bandBottom),
            "Expected dark slice-label text pixels within the pie's central band.");
    }

    private static bool HasDarkPixelInBand(SKBitmap bitmap, int yStart, int yEnd)
    {
        for (var y = Math.Max(0, yStart); y < Math.Min(bitmap.Height, yEnd); y++)
        for (var x = 0; x < bitmap.Width; x++)
        {
            var p = bitmap.GetPixel(x, y);
            if (p.Red < 130 && p.Green < 130 && p.Blue < 130)
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
