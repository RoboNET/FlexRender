using FlexRender.Parsing.Ast;
using FlexRender.TemplateEngine;
using Xunit;

namespace FlexRender.Tests.TemplateEngine;

/// <summary>
/// Tests that BytesValue variables are resolved into ExprValue.Bytes
/// instead of being converted to data: URI strings.
/// </summary>
public sealed class ExpandImageBytesTests
{
    [Fact]
    public async Task ExpandAsync_ImageWithBytesVariable_StoresBytesInExprValue()
    {
        var expander = new TemplateExpander();
        var rawBytes = new byte[] { 0x89, 0x50, 0x4E, 0x47 };
        var template = new Template
        {
            Name = "test",
            Version = 1,
            Canvas = new CanvasSettings { Width = 200 },
            Elements =
            [
                new ImageElement { Src = "{{logo}}" }
            ]
        };

        var data = new ObjectValue { ["logo"] = new BytesValue(rawBytes, "image/png") };
        var result = await expander.ExpandAsync(template, data);

        var image = Assert.IsType<ImageElement>(result.Elements[0]);
        Assert.NotNull(image.Src.Bytes);
        Assert.Equal(rawBytes, image.Src.Bytes!.Value);
        Assert.Equal("image/png", image.Src.Bytes.MimeType);
    }

    [Fact]
    public async Task ExpandAsync_ImageWithStringVariable_NoBytesInExprValue()
    {
        var expander = new TemplateExpander();
        var template = new Template
        {
            Name = "test",
            Version = 1,
            Canvas = new CanvasSettings { Width = 200 },
            Elements =
            [
                new ImageElement { Src = "{{logoUrl}}" }
            ]
        };

        var data = new ObjectValue { ["logoUrl"] = new StringValue("logo.png") };
        var result = await expander.ExpandAsync(template, data);

        var image = Assert.IsType<ImageElement>(result.Elements[0]);
        Assert.Null(image.Src.Bytes);
        Assert.Equal("logo.png", image.Src.Value);
    }

    [Fact]
    public async Task ExpandAsync_SvgWithBytesVariable_StoresBytesInExprValue()
    {
        var expander = new TemplateExpander();
        var svgBytes = System.Text.Encoding.UTF8.GetBytes("<svg></svg>");
        var template = new Template
        {
            Name = "test",
            Version = 1,
            Canvas = new CanvasSettings { Width = 200 },
            Elements =
            [
                new global::FlexRender.Parsing.Ast.SvgElement { Src = "{{icon}}" }
            ]
        };

        var data = new ObjectValue { ["icon"] = new BytesValue(svgBytes, "image/svg+xml") };
        var result = await expander.ExpandAsync(template, data);

        var svg = Assert.IsType<global::FlexRender.Parsing.Ast.SvgElement>(result.Elements[0]);
        Assert.NotNull(svg.Src.Bytes);
        Assert.Equal(svgBytes, svg.Src.Bytes!.Value);
    }
}
