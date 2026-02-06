using FlexRender.Abstractions;
using FlexRender.Configuration;
using FlexRender.Parsing.Ast;
using Xunit;

namespace FlexRender.Tests.Rendering;

public sealed class AntialiasingTests : IDisposable
{
    private readonly IFlexRender _render;

    public AntialiasingTests()
    {
        _render = new FlexRenderBuilder()
            .WithSkia()
            .Build();
    }

    public void Dispose() => _render.Dispose();

    [Fact]
    public async Task RenderToRaw_WithAndWithoutAntialiasing_BothProduceValidOutput()
    {
        var template = new Template
        {
            Canvas = new CanvasSettings { Fixed = FixedDimension.Both, Width = 100, Height = 50, Background = "#ffffff" },
            Elements = [new TextElement { Content = "AaBbCc", Size = "14", Color = "#000000" }]
        };

        var withAA = await _render.RenderToRaw(template, renderOptions: new RenderOptions { Antialiasing = true });
        var withoutAA = await _render.RenderToRaw(template, renderOptions: new RenderOptions { Antialiasing = false });

        // Both should produce valid, non-empty pixel data of the same size
        Assert.NotEmpty(withAA);
        Assert.NotEmpty(withoutAA);
        Assert.Equal(withAA.Length, withoutAA.Length);
    }

    [Fact]
    public async Task RenderToRaw_DefaultAntialiasing_IsEnabled()
    {
        var template = new Template
        {
            Canvas = new CanvasSettings { Fixed = FixedDimension.Both, Width = 100, Height = 50, Background = "#ffffff" },
            Elements = [new TextElement { Content = "Test", Size = "16", Color = "#000000" }]
        };

        // Default options (null) should produce same result as explicit Antialiasing = true
        var defaultResult = await _render.RenderToRaw(template);
        var explicitTrue = await _render.RenderToRaw(template, renderOptions: new RenderOptions { Antialiasing = true });

        Assert.Equal(defaultResult, explicitTrue);
    }
}
