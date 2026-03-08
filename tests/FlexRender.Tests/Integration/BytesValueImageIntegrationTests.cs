using FlexRender.Abstractions;
using FlexRender.Configuration;
using FlexRender.Loaders;
using FlexRender.Parsing.Ast;
using FlexRender.TemplateEngine;
using Xunit;

namespace FlexRender.Tests.Integration;

/// <summary>
/// Integration tests verifying BytesValue flows through the full pipeline
/// for both content elements and image elements.
/// </summary>
public sealed class BytesValueImageIntegrationTests
{
    [Fact]
    public async Task FullPipeline_ImageWithBytesVariable_BytesPreservedInExprValue()
    {
        var expander = new TemplateExpander(new ResourceLimits());
        var processor = new TemplateProcessor(new ResourceLimits());
        var pipeline = new TemplatePipeline(expander, processor);

        var pngBytes = new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A };

        var template = new Template
        {
            Name = "test",
            Version = 1,
            Canvas = new CanvasSettings { Width = 200 },
            Elements =
            [
                new ImageElement { Src = ExprValue<string>.Expression("{{logo}}") }
            ]
        };

        var data = new ObjectValue
        {
            ["logo"] = new BytesValue(pngBytes, "image/png")
        };

        var result = await pipeline.ProcessAsync(template, data);
        var image = Assert.IsType<ImageElement>(result.Elements[0]);

        Assert.NotNull(image.Src.Bytes);
        Assert.Equal(pngBytes, image.Src.Bytes!.Value);
        Assert.Equal("image/png", image.Src.Bytes.MimeType);
    }

    [Fact]
    public async Task FullPipeline_ImageWithDataUri_BytesLoadedByLoaderChain()
    {
        var options = new FlexRenderOptions();
        IReadOnlyList<IResourceLoader> loaders = [new Base64ResourceLoader(options)];
        var expander = new TemplateExpander(new ResourceLimits(), resourceLoaders: loaders);
        var processor = new TemplateProcessor(new ResourceLimits());
        var pipeline = new TemplatePipeline(expander, processor);

        var rawBytes = new byte[] { 0xDE, 0xAD };
        var base64 = Convert.ToBase64String(rawBytes);

        var template = new Template
        {
            Name = "test",
            Version = 1,
            Canvas = new CanvasSettings { Width = 200 },
            Elements =
            [
                new ImageElement { Src = $"data:application/octet-stream;base64,{base64}" }
            ]
        };

        var result = await pipeline.ProcessAsync(template, new ObjectValue());
        var image = Assert.IsType<ImageElement>(result.Elements[0]);

        Assert.NotNull(image.Src.Bytes);
        Assert.Equal(rawBytes, image.Src.Bytes!.Value);
    }

    [Fact]
    public async Task FullPipeline_SvgWithBytesVariable_BytesPreservedInExprValue()
    {
        var expander = new TemplateExpander(new ResourceLimits());
        var processor = new TemplateProcessor(new ResourceLimits());
        var pipeline = new TemplatePipeline(expander, processor);

        var svgBytes = System.Text.Encoding.UTF8.GetBytes("<svg viewBox=\"0 0 24 24\"></svg>");

        var template = new Template
        {
            Name = "test",
            Version = 1,
            Canvas = new CanvasSettings { Width = 200 },
            Elements =
            [
                new global::FlexRender.Parsing.Ast.SvgElement { Src = ExprValue<string>.Expression("{{icon}}") }
            ]
        };

        var data = new ObjectValue
        {
            ["icon"] = new BytesValue(svgBytes, "image/svg+xml")
        };

        var result = await pipeline.ProcessAsync(template, data);
        var svg = Assert.IsType<global::FlexRender.Parsing.Ast.SvgElement>(result.Elements[0]);

        Assert.NotNull(svg.Src.Bytes);
        Assert.Equal(svgBytes, svg.Src.Bytes!.Value);
    }
}
