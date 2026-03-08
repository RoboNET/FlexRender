using FlexRender.Configuration;
using FlexRender.Parsing.Ast;
using FlexRender.TemplateEngine;
using Xunit;

namespace FlexRender.Tests.TemplateEngine;

/// <summary>
/// Tests for BytesValue resolution in image source expressions.
/// Verifies that binary image data provided as template variables is stored
/// in ExprValue.Bytes during template expansion.
/// </summary>
public sealed class TemplateExpanderBytesImageTests
{
    private readonly TemplateExpander _expander = new();

    private static Template CreateTemplate(params TemplateElement[] elements)
    {
        var template = new Template();
        foreach (var element in elements)
        {
            template.AddElement(element);
        }
        return template;
    }

    [Fact]
    public async Task Expand_ImageSrcBytesValue_StoresBytesInExprValue()
    {
        var pngBytes = new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A };
        var template = CreateTemplate(
            new ImageElement { Src = "{{logo}}" }
        );
        var data = new ObjectValue
        {
            ["logo"] = new BytesValue(pngBytes, "image/png")
        };

        var result = await _expander.ExpandAsync(template, data);

        var img = Assert.IsType<ImageElement>(result.Elements[0]);
        Assert.NotNull(img.Src.Bytes);
        Assert.Equal(pngBytes, img.Src.Bytes!.Value);
        Assert.Equal("image/png", img.Src.Bytes.MimeType);
    }

    [Fact]
    public async Task Expand_ImageSrcBytesValue_RoundTripsBytes()
    {
        var pngBytes = new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0x00, 0x01, 0x02, 0xFF };
        var template = CreateTemplate(
            new ImageElement { Src = "{{logo}}" }
        );
        var data = new ObjectValue
        {
            ["logo"] = new BytesValue(pngBytes, "image/png")
        };

        var result = await _expander.ExpandAsync(template, data);

        var img = Assert.IsType<ImageElement>(result.Elements[0]);
        Assert.NotNull(img.Src.Bytes);
        Assert.Equal(pngBytes, img.Src.Bytes!.Value);
        Assert.Equal("image/png", img.Src.Bytes.MimeType);
    }

    [Fact]
    public async Task Expand_ImageSrcBytesValueWithoutMimeType_PreservesNullMimeType()
    {
        var rawBytes = new byte[] { 0x01, 0x02, 0x03 };
        var template = CreateTemplate(
            new ImageElement { Src = "{{data}}" }
        );
        var data = new ObjectValue
        {
            ["data"] = new BytesValue(rawBytes)
        };

        var result = await _expander.ExpandAsync(template, data);

        var img = Assert.IsType<ImageElement>(result.Elements[0]);
        Assert.NotNull(img.Src.Bytes);
        Assert.Equal(rawBytes, img.Src.Bytes!.Value);
        Assert.Null(img.Src.Bytes.MimeType);
    }

    [Fact]
    public async Task Expand_ImageSrcStringValue_ResolvesAsStringPath()
    {
        var template = CreateTemplate(
            new ImageElement { Src = "{{imagePath}}" }
        );
        var data = new ObjectValue
        {
            ["imagePath"] = new StringValue("/images/logo.png")
        };

        var result = await _expander.ExpandAsync(template, data);

        var img = Assert.IsType<ImageElement>(result.Elements[0]);
        Assert.Equal("/images/logo.png", img.Src.Value);
    }

    [Fact]
    public async Task Expand_ImageSrcLiteral_PreservesLiteral()
    {
        var template = CreateTemplate(
            new ImageElement { Src = "/static/logo.png" }
        );
        var data = new ObjectValue();

        var result = await _expander.ExpandAsync(template, data);

        var img = Assert.IsType<ImageElement>(result.Elements[0]);
        Assert.Equal("/static/logo.png", img.Src.Value);
    }

    [Fact]
    public async Task Expand_ImageSrcMixedExpression_FallsBackToStringSubstitution()
    {
        // Mixed expression like "prefix_{{name}}.png" should not trigger bytes resolution
        var template = CreateTemplate(
            new ImageElement { Src = "prefix_{{name}}.png" }
        );
        var data = new ObjectValue
        {
            ["name"] = new StringValue("logo")
        };

        var result = await _expander.ExpandAsync(template, data);

        var img = Assert.IsType<ImageElement>(result.Elements[0]);
        Assert.Equal("prefix_logo.png", img.Src.Value);
    }

    [Fact]
    public async Task Expand_ImageSrcBytesValueInEach_StoresBytesInExprValue()
    {
        var bytes1 = new byte[] { 0x01, 0x02 };
        var bytes2 = new byte[] { 0x03, 0x04 };
        var each = new EachElement(new List<TemplateElement>
        {
            new ImageElement { Src = "{{icon}}" }
        })
        {
            ArrayPath = "items"
        };

        var template = CreateTemplate(each);
        var data = new ObjectValue
        {
            ["items"] = new ArrayValue(new List<TemplateValue>
            {
                new ObjectValue { ["icon"] = new BytesValue(bytes1, "image/jpeg") },
                new ObjectValue { ["icon"] = new BytesValue(bytes2, "image/jpeg") }
            })
        };

        var result = await _expander.ExpandAsync(template, data);

        Assert.Equal(2, result.Elements.Count);
        var img1 = Assert.IsType<ImageElement>(result.Elements[0]);
        var img2 = Assert.IsType<ImageElement>(result.Elements[1]);
        Assert.NotNull(img1.Src.Bytes);
        Assert.NotNull(img2.Src.Bytes);
        Assert.Equal(bytes1, img1.Src.Bytes!.Value);
        Assert.Equal(bytes2, img2.Src.Bytes!.Value);
    }

    [Fact]
    public async Task Expand_ImageSrcBytesValueNestedPath_StoresBytesInExprValue()
    {
        var pngBytes = new byte[] { 0x89, 0x50, 0x4E, 0x47 };
        var template = CreateTemplate(
            new ImageElement { Src = "{{brand.logo}}" }
        );
        var data = new ObjectValue
        {
            ["brand"] = new ObjectValue
            {
                ["logo"] = new BytesValue(pngBytes, "image/png")
            }
        };

        var result = await _expander.ExpandAsync(template, data);

        var img = Assert.IsType<ImageElement>(result.Elements[0]);
        Assert.NotNull(img.Src.Bytes);
        Assert.Equal(pngBytes, img.Src.Bytes!.Value);
        Assert.Equal("image/png", img.Src.Bytes.MimeType);
    }
}
