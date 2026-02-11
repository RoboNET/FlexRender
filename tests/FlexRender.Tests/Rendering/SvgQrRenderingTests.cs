using FlexRender.Configuration;
using FlexRender.Layout;
using FlexRender.Parsing.Ast;
using FlexRender.Providers;
using FlexRender.QrCode.Providers;
using FlexRender.QrCode.Svg.Providers;
using FlexRender.Svg.Rendering;
using FlexRender.TemplateEngine;
using Xunit;


namespace FlexRender.Tests.Rendering;

/// <summary>
/// Tests for SVG rendering of QR code elements using native SVG paths.
/// </summary>
public sealed class SvgQrRenderingTests
{
    /// <summary>
    /// Verifies that QR elements render as SVG paths (not base64 images) when
    /// using the dedicated QrSvgProvider.
    /// </summary>
    [Fact]
    public void RenderToSvg_QrElement_ProducesSvgPathNotBase64()
    {
        var svgProvider = new QrSvgProvider();
        var engine = CreateEngineWithSvgProvider(svgProvider);

        var template = CreateTemplateWithQr("https://example.com");
        var svg = engine.RenderToSvg(template, new ObjectValue());

        // Should contain native SVG path, not base64 image
        Assert.Contains("<path", svg);
        Assert.Contains("d=\"M", svg);
        Assert.DoesNotContain("data:image/png;base64", svg);
    }

    /// <summary>
    /// Verifies that QR elements fall back to bitmap when provider
    /// does not implement ISvgContentProvider.
    /// </summary>
    [Fact]
    public void RenderToSvg_QrElementWithBitmapOnlyProvider_ProducesBase64Image()
    {
        var bitmapOnlyProvider = new BitmapOnlyQrProvider();
        var engine = CreateEngineWithRasterProvider(bitmapOnlyProvider);

        var template = CreateTemplateWithQr("https://example.com");
        var svg = engine.RenderToSvg(template, new ObjectValue());

        // Should fall back to base64 image
        Assert.Contains("data:image/png;base64", svg);
        Assert.DoesNotContain("<path d=\"M", svg);
    }

    /// <summary>
    /// Verifies the SVG output contains the correct foreground color.
    /// </summary>
    [Fact]
    public void RenderToSvg_QrElementWithCustomColor_UsesForegroundColor()
    {
        var svgProvider = new QrSvgProvider();
        var engine = CreateEngineWithSvgProvider(svgProvider);

        var template = CreateTemplateWithQr("Hello", foreground: "#ff0000");
        var svg = engine.RenderToSvg(template, new ObjectValue());

        Assert.Contains("fill=\"#ff0000\"", svg);
    }

    /// <summary>
    /// Verifies the SVG output contains background rect when background is set.
    /// </summary>
    [Fact]
    public void RenderToSvg_QrElementWithBackground_ContainsBackgroundRect()
    {
        var svgProvider = new QrSvgProvider();
        var engine = CreateEngineWithSvgProvider(svgProvider);

        var template = CreateTemplateWithQr("Hello", background: "#ffffff");
        var svg = engine.RenderToSvg(template, new ObjectValue());

        Assert.Contains("fill=\"#ffffff\"", svg);
    }

    private static SvgRenderingEngine CreateEngineWithSvgProvider(ISvgContentProvider<QrElement> svgProvider)
    {
        var limits = new ResourceLimits();
        var expander = new TemplateExpander(limits);
        var pipeline = new TemplatePipeline(expander, new TemplateProcessor(limits));
        var layoutEngine = new LayoutEngine(limits);

        return new SvgRenderingEngine(
            limits,
            pipeline,
            layoutEngine,
            baseFontSize: 16f,
            qrSvgProvider: svgProvider);
    }

    private static SvgRenderingEngine CreateEngineWithRasterProvider(IContentProvider<QrElement> rasterProvider)
    {
        var limits = new ResourceLimits();
        var expander = new TemplateExpander(limits);
        var pipeline = new TemplatePipeline(expander, new TemplateProcessor(limits));
        var layoutEngine = new LayoutEngine(limits);

        return new SvgRenderingEngine(
            limits,
            pipeline,
            layoutEngine,
            baseFontSize: 16f,
            qrProvider: rasterProvider);
    }

    private static Template CreateTemplateWithQr(
        string data,
        string foreground = "#000000",
        string? background = null)
    {
        var qr = new QrElement
        {
            Data = data,
            Foreground = foreground,
            Background = background!,
            Width = "100",
            Height = "100"
        };

        var flex = new FlexElement();
        flex.AddChild(qr);

        var template = new Template
        {
            Canvas = new CanvasSettings { Width = 200, Height = 200 }
        };
        template.AddElement(flex);

        return template;
    }

    /// <summary>
    /// A QR provider that only implements IContentProvider (no ISvgContentProvider).
    /// Used to test bitmap fallback behavior.
    /// </summary>
    private sealed class BitmapOnlyQrProvider : IContentProvider<QrElement>
    {
        private readonly QrProvider _inner = new();

        public ContentResult Generate(QrElement element, int width, int height)
        {
            return _inner.Generate(element, width, height);
        }
    }
}
