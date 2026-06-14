using FlexRender.Parsing;
using FlexRender.Parsing.Ast;
using FlexRender.Rendering;
using FlexRender.TemplateEngine;
using SkiaSharp;
using Xunit;

namespace FlexRender.Tests.Rendering;

/// <summary>
/// Smoke tests verifying that absolute-coordinate shapes inside a <c>draw</c> element
/// (line/polyline/rect/circle/path) are actually painted to the canvas by the Skia pipeline.
/// </summary>
public sealed class DrawRenderSmokeTests : IDisposable
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
    public async Task Draw_WithBlueFilledCircle_DrawsBlueAtCircleCentre()
    {
        const string yaml = """
            canvas:
              width: 200
              height: 150
              background: "#ffffff"
            layout:
              - type: draw
                width: 200
                height: 150
                shapes:
                  - circle: {cx: 100, cy: 75, r: 40, fill: "#0000ff"}
            """;

        var template = _parser.Parse(yaml);
        var data = new ObjectValue();

        using var bitmap = await Render(template, data);

        var center = bitmap.GetPixel(100, 75);
        Assert.True(center.Blue > 200, $"Expected blue centre, got {center}");
        Assert.True(center.Red < 80, $"Expected blue centre, got {center}");
        Assert.True(center.Green < 80, $"Expected blue centre, got {center}");
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
