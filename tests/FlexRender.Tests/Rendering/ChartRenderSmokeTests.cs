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

    [Fact]
    public async Task ScatterChart_FromYaml_DrawsPixels()
    {
        const string yaml = """
            canvas:
              width: 320
              height: 240
              background: "#ffffff"
            layout:
              - type: chart
                chart-type: scatter
                width: 320
                height: 240
                series:
                  - data: [[1, 10], [4, 25], [7, 18], [9, 30]]
                legend: none
            """;

        var template = _parser.Parse(yaml);
        using var bitmap = await Render(template, new ObjectValue());

        Assert.True(HasNonBackgroundPixel(bitmap), "Expected scatter to draw.");
    }

    [Fact]
    public async Task BubbleChart_FromYaml_DrawsPixels()
    {
        const string yaml = """
            canvas:
              width: 320
              height: 240
              background: "#ffffff"
            layout:
              - type: chart
                chart-type: bubble
                width: 320
                height: 240
                series:
                  - data: [[1, 10, 5], [5, 25, 18], [8, 15, 9]]
                legend: none
            """;

        var template = _parser.Parse(yaml);
        using var bitmap = await Render(template, new ObjectValue());

        Assert.True(HasNonBackgroundPixel(bitmap), "Expected bubble to draw.");
    }

    [Fact]
    public async Task GaugeChart_FromYaml_DrawsPixels()
    {
        const string yaml = """
            canvas:
              width: 240
              height: 200
              background: "#ffffff"
            layout:
              - type: chart
                chart-type: gauge
                width: 240
                height: 200
                value: 65
                max: 100
                label: CPU
            """;

        var template = _parser.Parse(yaml);
        using var bitmap = await Render(template, new ObjectValue());

        Assert.True(HasNonBackgroundPixel(bitmap), "Expected gauge to draw.");
    }

    [Fact]
    public async Task ProgressChart_FromYaml_DrawsPixels()
    {
        const string yaml = """
            canvas:
              width: 200
              height: 200
              background: "#ffffff"
            layout:
              - type: chart
                chart-type: progress
                width: 200
                height: 200
                value: 45
            """;

        var template = _parser.Parse(yaml);
        using var bitmap = await Render(template, new ObjectValue());

        Assert.True(HasNonBackgroundPixel(bitmap), "Expected progress to draw.");
    }

    [Fact]
    public async Task SparklineChart_FromYaml_DrawsPixels()
    {
        const string yaml = """
            canvas:
              width: 160
              height: 50
              background: "#ffffff"
            layout:
              - type: chart
                chart-type: sparkline
                width: 160
                height: 50
                series:
                  - data: [3, 8, 4, 10, 6, 9]
            """;

        var template = _parser.Parse(yaml);
        using var bitmap = await Render(template, new ObjectValue());

        Assert.True(HasNonBackgroundPixel(bitmap), "Expected sparkline to draw.");
    }

    [Fact]
    public async Task HeatmapChart_FromYaml_DrawsPixels()
    {
        const string yaml = """
            canvas:
              width: 320
              height: 240
              background: "#ffffff"
            layout:
              - type: chart
                chart-type: heatmap
                width: 320
                height: 240
                x-labels: [Mon, Tue, Wed]
                y-labels: [AM, PM]
                cell-values: true
                series:
                  - data: [1, 5, 9]
                  - data: [7, 3, 2]
                legend: none
                palette: ocean
            """;

        var template = _parser.Parse(yaml);
        using var bitmap = await Render(template, new ObjectValue());

        Assert.True(HasNonBackgroundPixel(bitmap), "Expected heatmap to draw.");
    }

    [Fact]
    public async Task RadarChart_FromYaml_DrawsPixels()
    {
        const string yaml = """
            canvas:
              width: 300
              height: 300
              background: "#ffffff"
            layout:
              - type: chart
                chart-type: radar
                width: 300
                height: 300
                categories: [Speed, Power, Range, Agility, Armor]
                series:
                  - label: A
                    data: [4, 3, 5, 2, 4]
                  - label: B
                    data: [3, 5, 2, 4, 3]
                legend: none
                palette: vivid
            """;

        var template = _parser.Parse(yaml);
        using var bitmap = await Render(template, new ObjectValue());

        Assert.True(HasNonBackgroundPixel(bitmap), "Expected radar to draw.");
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
