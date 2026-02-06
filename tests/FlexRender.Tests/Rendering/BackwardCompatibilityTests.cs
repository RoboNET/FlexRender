using FlexRender.Abstractions;
using FlexRender.Configuration;
using FlexRender.Parsing.Ast;
using Xunit;

namespace FlexRender.Tests.Rendering;

public sealed class BackwardCompatibilityTests : IDisposable
{
    private readonly IFlexRender _render;
    private readonly Template _simpleTemplate;

    public BackwardCompatibilityTests()
    {
        _render = new FlexRenderBuilder()
            .WithSkia()
            .Build();

        _simpleTemplate = new Template
        {
            Canvas = new CanvasSettings { Fixed = FixedDimension.Both, Width = 100, Height = 50, Background = "#ffffff" },
            Elements = [new TextElement { Content = "Hello", Size = "16", Color = "#000000" }]
        };
    }

    public void Dispose() => _render.Dispose();

    [Fact]
    public async Task Render_PngDefault_SameOutputAsRenderToPngDefault()
    {
        var legacyResult = await _render.Render(_simpleTemplate, format: ImageFormat.Png);
        var newResult = await _render.RenderToPng(_simpleTemplate);

        Assert.Equal(legacyResult, newResult);
    }

    [Fact]
    public async Task Render_JpegDefault_SameOutputAsRenderToJpegDefault()
    {
        var legacyResult = await _render.Render(_simpleTemplate, format: ImageFormat.Jpeg);
        var newResult = await _render.RenderToJpeg(_simpleTemplate);

        Assert.Equal(legacyResult, newResult);
    }

    [Fact]
    public async Task Render_BmpDefault_SameOutputAsRenderToBmpDefault()
    {
        var legacyResult = await _render.Render(_simpleTemplate, format: ImageFormat.Bmp);
        var newResult = await _render.RenderToBmp(_simpleTemplate);

        Assert.Equal(legacyResult, newResult);
    }

    [Fact]
    public async Task Render_RawDefault_SameOutputAsRenderToRawDefault()
    {
        var legacyResult = await _render.Render(_simpleTemplate, format: ImageFormat.Raw);
        var newResult = await _render.RenderToRaw(_simpleTemplate);

        Assert.Equal(legacyResult, newResult);
    }
}
