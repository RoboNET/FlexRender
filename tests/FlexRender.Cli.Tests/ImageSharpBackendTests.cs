using FlexRender.Parsing.Ast;
using Xunit;

namespace FlexRender.Cli.Tests;

public sealed class ImageSharpBackendTests
{
    [Fact]
    public async Task CreateRenderBuilder_ImageSharpBackend_CanRenderQrElement()
    {
        var builder = Program.CreateRenderBuilder(backend: "imagesharp");
        using var render = builder.Build();

        var template = new Template
        {
            Canvas = new CanvasSettings { Width = 200, Height = 200, Fixed = FixedDimension.Both, Background = "#ffffff" }
        };
        template.AddElement(new QrElement { Data = "https://example.com", Size = 150 });

        var result = await render.Render(template);
        Assert.True(result.Length > 0);
    }

    [Fact]
    public async Task CreateRenderBuilder_ImageSharpBackend_CanRenderBarcodeElement()
    {
        var builder = Program.CreateRenderBuilder(backend: "imagesharp");
        using var render = builder.Build();

        var template = new Template
        {
            Canvas = new CanvasSettings { Width = 300, Height = 100, Fixed = FixedDimension.Both, Background = "#ffffff" }
        };
        template.AddElement(new BarcodeElement { Data = "ABC123", BarcodeWidth = 280, BarcodeHeight = 80 });

        var result = await render.Render(template);
        Assert.True(result.Length > 0);
    }

    [Fact]
    public async Task CreateRenderBuilder_ImageSharpBackend_CanRenderTextElement()
    {
        var builder = Program.CreateRenderBuilder(backend: "imagesharp");
        using var render = builder.Build();

        var template = new Template
        {
            Canvas = new CanvasSettings { Width = 200, Height = 100, Fixed = FixedDimension.Both, Background = "#ffffff" }
        };
        template.AddElement(new TextElement { Content = "Hello", Size = "16", Color = "#000000" });

        var result = await render.Render(template);
        Assert.True(result.Length > 0);
    }
}
