using System;
using FlexRender.Parsing;
using FlexRender.Parsing.Ast;
using FlexRender.Rendering;
using FlexRender.TemplateEngine;
using SkiaSharp;
using Xunit;

namespace FlexRender.Tests.Rendering;

/// <summary>
/// Smoke tests verifying charts are drawn (not merely laid out) by the Skia pipeline.
/// </summary>
public sealed class ChartRenderSmokeTests : IDisposable
{
    private readonly SkiaRenderer _renderer = new();
    private readonly TemplateParser _parser = new();

    public void Dispose()
    {
        _renderer.Dispose();
        GC.SuppressFinalize(this);
    }

    [Fact]
    public async Task EmptyChart_DrawsNoDataPlaceholderNotBlank()
    {
        const string yaml = """
            canvas:
              width: 200
              height: 120
              background: "#ffffff"
            layout:
              - type: chart
                chart-type: bar
                width: 200
                height: 120
                series: []
            """;

        var template = _parser.Parse(yaml);
        using var bitmap = await Render(template, new ObjectValue());

        Assert.True(HasNonBackgroundPixel(bitmap), "Expected the 'no data' placeholder to draw something.");
    }

    [Fact]
    public async Task BarChart_WithData_DrawsColoredPixels()
    {
        const string yaml = """
            canvas:
              width: 300
              height: 200
              background: "#ffffff"
            layout:
              - type: chart
                chart-type: bar
                width: 300
                height: 200
                categories: [A, B, C]
                series:
                  - data: [10, 20, 15]
                legend: none
            """;

        var template = _parser.Parse(yaml);
        using var bitmap = await Render(template, new ObjectValue());

        Assert.True(HasNonBackgroundPixel(bitmap), "Expected the bar chart to draw something.");
    }

    private static bool HasNonBackgroundPixel(SKBitmap bitmap)
    {
        for (var y = 0; y < bitmap.Height; y += 3)
        for (var x = 0; x < bitmap.Width; x += 3)
        {
            var p = bitmap.GetPixel(x, y);
            if (p.Red < 240 || p.Green < 240 || p.Blue < 240)
                return true;
        }
        return false;
    }

    private async Task<SKBitmap> Render(Template template, ObjectValue data)
    {
        var size = await _renderer.MeasureAsync(template, data);
        var width = Math.Max((int)Math.Ceiling(size.Width), 1);
        var height = Math.Max((int)Math.Ceiling(size.Height), 1);
        var bitmap = new SKBitmap(width, height, SKColorType.Rgba8888, SKAlphaType.Premul);
        await _renderer.Render(bitmap, template, data, default, default);
        return bitmap;
    }
}
