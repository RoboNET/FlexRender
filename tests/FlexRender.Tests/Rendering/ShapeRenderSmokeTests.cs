using FlexRender.Parsing;
using FlexRender.Parsing.Ast;
using FlexRender.Rendering;
using FlexRender.TemplateEngine;
using SkiaSharp;
using Xunit;

namespace FlexRender.Tests.Rendering;

/// <summary>
/// Smoke tests verifying that box shapes (rect/circle/ellipse) are actually drawn to the canvas
/// by the Skia rendering pipeline, not merely laid out.
/// </summary>
public sealed class ShapeRenderSmokeTests : IDisposable
{
    private readonly SkiaRenderer _renderer = new();
    private readonly TemplateParser _parser = new();

    /// <summary>Releases the renderer used by the tests.</summary>
    public void Dispose()
    {
        _renderer.Dispose();
        GC.SuppressFinalize(this);
    }

    [Fact]
    public async Task Rect_WithRedFill_DrawsRedAtCanvasCenter()
    {
        const string yaml = """
            canvas:
              width: 120
              height: 80
              background: "#ffffff"
            layout:
              - type: rect
                width: 100
                height: 50
                fill: "#ff0000"
                margin: "10"
            """;

        var template = _parser.Parse(yaml);
        var data = new ObjectValue();

        using var bitmap = await Render(template, data);

        var center = bitmap.GetPixel(bitmap.Width / 2, bitmap.Height / 2);
        Assert.True(center.Red > 200, $"Expected red center, got {center}");
        Assert.True(center.Green < 80, $"Expected red center, got {center}");
        Assert.True(center.Blue < 80, $"Expected red center, got {center}");
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
