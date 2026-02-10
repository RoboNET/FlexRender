using FlexRender.Abstractions;
using FlexRender.Configuration;
using FlexRender.Parsing;
using Xunit;

namespace FlexRender.ImageSharp.Tests;

public sealed class IntegrationTests : IDisposable
{
    private readonly IFlexRender _render;

    public IntegrationTests()
    {
        _render = new FlexRenderBuilder()
            .WithImageSharp()
            .Build();
    }

    [Fact]
    public async Task RenderYaml_SimpleText_ProducesPng()
    {
        var parser = new TemplateParser();
        var template = parser.Parse("""
            canvas:
              width: 300
              height: 50
              background: "#ffffff"
            layout:
              - type: text
                content: "Hello from ImageSharp!"
                color: "#000000"
                size: "16"
            """);

        var result = await _render.RenderToPng(template);

        Assert.True(result.Length > 100);
        // PNG magic bytes
        Assert.Equal(137, result[0]);
    }

    [Fact]
    public async Task RenderYaml_FlexLayout_ProducesPng()
    {
        var parser = new TemplateParser();
        var template = parser.Parse("""
            canvas:
              width: 400
              height: 200
              background: "#f0f0f0"
            layout:
              - type: flex
                direction: column
                padding: "20"
                background: "#ffffff"
                children:
                  - type: text
                    content: "Title"
                    size: "24"
                    color: "#333333"
                  - type: separator
                    color: "#cccccc"
                    thickness: 1
                  - type: text
                    content: "Body text goes here"
                    size: "14"
                    color: "#666666"
            """);

        var result = await _render.RenderToPng(template);

        Assert.True(result.Length > 100);
    }

    [Fact]
    public async Task RenderYaml_WithData_ProducesPng()
    {
        var parser = new TemplateParser();
        var template = parser.Parse("""
            canvas:
              width: 300
              height: 50
              background: "#ffffff"
            layout:
              - type: text
                content: "Hello, {{name}}!"
                color: "#000000"
                size: "16"
            """);

        var data = new ObjectValue();
        data["name"] = new StringValue("World");

        var result = await _render.RenderToPng(template, data);

        Assert.True(result.Length > 100);
    }

    [Fact]
    public async Task RenderYaml_MultipleFormats_AllProduce()
    {
        var parser = new TemplateParser();
        var template = parser.Parse("""
            canvas:
              width: 100
              height: 50
              fixed: both
              background: "#ff0000"
            layout:
              - type: text
                content: "Test"
                color: "#ffffff"
            """);

        var png = await _render.RenderToPng(template);
        var jpeg = await _render.RenderToJpeg(template);
        var bmp = await _render.RenderToBmp(template);
        var raw = await _render.RenderToRaw(template);

        Assert.True(png.Length > 0, "PNG should not be empty");
        Assert.True(jpeg.Length > 0, "JPEG should not be empty");
        Assert.True(bmp.Length > 0, "BMP should not be empty");
        Assert.True(raw.Length > 0, "Raw should not be empty");

        // Raw should be exactly width * height * 4 bytes (RGBA)
        Assert.Equal(100 * 50 * 4, raw.Length);
    }

    public void Dispose()
    {
        _render.Dispose();
    }
}
