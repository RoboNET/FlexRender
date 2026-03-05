using FlexRender.Configuration;
using FlexRender.Parsing.Ast;
using FlexRender.TemplateEngine;
using Xunit;

namespace FlexRender.Tests.TemplateEngine;

/// <summary>
/// Tests for BytesValue resolution in image source expressions.
/// Verifies that binary image data provided as template variables is converted
/// to data: URIs during template expansion.
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
    public void Expand_ImageSrcBytesValue_ConvertsToDataUri()
    {
        var pngBytes = new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A };
        var template = CreateTemplate(
            new ImageElement { Src = "{{logo}}" }
        );
        var data = new ObjectValue
        {
            ["logo"] = new BytesValue(pngBytes, "image/png")
        };

        var result = _expander.Expand(template, data);

        var img = Assert.IsType<ImageElement>(result.Elements[0]);
        var expectedBase64 = Convert.ToBase64String(pngBytes);
        Assert.Equal($"data:image/png;base64,{expectedBase64}", img.Src.Value);
    }

    [Fact]
    public void Expand_ImageSrcBytesValue_RoundTripsBytes()
    {
        var pngBytes = new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0x00, 0x01, 0x02, 0xFF };
        var template = CreateTemplate(
            new ImageElement { Src = "{{logo}}" }
        );
        var data = new ObjectValue
        {
            ["logo"] = new BytesValue(pngBytes, "image/png")
        };

        var result = _expander.Expand(template, data);

        var img = Assert.IsType<ImageElement>(result.Elements[0]);
        var srcValue = img.Src.Value;
        Assert.StartsWith("data:image/png;base64,", srcValue);

        // Extract base64 portion and verify round-trip
        var base64Part = srcValue["data:image/png;base64,".Length..];
        var decoded = Convert.FromBase64String(base64Part);
        Assert.Equal(pngBytes, decoded);
    }

    [Fact]
    public void Expand_ImageSrcBytesValueWithoutMimeType_DefaultsToOctetStream()
    {
        var rawBytes = new byte[] { 0x01, 0x02, 0x03 };
        var template = CreateTemplate(
            new ImageElement { Src = "{{data}}" }
        );
        var data = new ObjectValue
        {
            ["data"] = new BytesValue(rawBytes)
        };

        var result = _expander.Expand(template, data);

        var img = Assert.IsType<ImageElement>(result.Elements[0]);
        var expectedBase64 = Convert.ToBase64String(rawBytes);
        Assert.Equal($"data:application/octet-stream;base64,{expectedBase64}", img.Src.Value);
    }

    [Fact]
    public void Expand_ImageSrcStringValue_ResolvesAsStringPath()
    {
        var template = CreateTemplate(
            new ImageElement { Src = "{{imagePath}}" }
        );
        var data = new ObjectValue
        {
            ["imagePath"] = new StringValue("/images/logo.png")
        };

        var result = _expander.Expand(template, data);

        var img = Assert.IsType<ImageElement>(result.Elements[0]);
        Assert.Equal("/images/logo.png", img.Src.Value);
    }

    [Fact]
    public void Expand_ImageSrcLiteral_PreservesLiteral()
    {
        var template = CreateTemplate(
            new ImageElement { Src = "/static/logo.png" }
        );
        var data = new ObjectValue();

        var result = _expander.Expand(template, data);

        var img = Assert.IsType<ImageElement>(result.Elements[0]);
        Assert.Equal("/static/logo.png", img.Src.Value);
    }

    [Fact]
    public void Expand_ImageSrcMixedExpression_FallsBackToStringSubstitution()
    {
        // Mixed expression like "prefix_{{name}}.png" should not trigger bytes resolution
        var template = CreateTemplate(
            new ImageElement { Src = "prefix_{{name}}.png" }
        );
        var data = new ObjectValue
        {
            ["name"] = new StringValue("logo")
        };

        var result = _expander.Expand(template, data);

        var img = Assert.IsType<ImageElement>(result.Elements[0]);
        Assert.Equal("prefix_logo.png", img.Src.Value);
    }

    [Fact]
    public void Expand_ImageSrcBytesValueInEach_ConvertsToDataUri()
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

        var result = _expander.Expand(template, data);

        Assert.Equal(2, result.Elements.Count);
        var img1 = Assert.IsType<ImageElement>(result.Elements[0]);
        var img2 = Assert.IsType<ImageElement>(result.Elements[1]);
        Assert.StartsWith("data:image/jpeg;base64,", img1.Src.Value);
        Assert.StartsWith("data:image/jpeg;base64,", img2.Src.Value);
        Assert.NotEqual(img1.Src.Value, img2.Src.Value);
    }

    [Fact]
    public void Expand_ImageSrcBytesValueNestedPath_ConvertsToDataUri()
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

        var result = _expander.Expand(template, data);

        var img = Assert.IsType<ImageElement>(result.Elements[0]);
        var expectedBase64 = Convert.ToBase64String(pngBytes);
        Assert.Equal($"data:image/png;base64,{expectedBase64}", img.Src.Value);
    }
}
