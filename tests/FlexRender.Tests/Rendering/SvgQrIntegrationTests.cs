using FlexRender.Configuration;
using FlexRender.Parsing.Ast;
using FlexRender.QrCode;
using FlexRender.QrCode.Svg.Providers;
using Xunit;

namespace FlexRender.Tests.Rendering;

/// <summary>
/// End-to-end integration tests for QR SVG rendering via the builder API.
/// </summary>
public sealed class SvgQrIntegrationTests
{
    /// <summary>
    /// Verifies the full pipeline: builder -> SvgRender -> SVG with native QR paths
    /// using the dedicated QrSvgProvider for vector output.
    /// </summary>
    [Fact]
    public async Task Builder_WithSvgAndQr_ProducesNativeSvgQrCode()
    {
        using var render = new FlexRenderBuilder()
            .WithSvg(svg =>
            {
                svg.SetQrSvgProvider(new QrSvgProvider());
                svg.WithSkia(skia => skia.WithQr());
            })
            .Build();

        var template = CreateTemplate(
            data: "https://example.com",
            foreground: "#000000",
            background: "#ffffff");

        var svg = await render.RenderToSvg(template, new ObjectValue());

        // Verify native SVG path output
        Assert.Contains("<path", svg);
        Assert.Contains("d=\"M", svg);
        Assert.Contains("fill=\"#000000\"", svg);
        Assert.Contains("fill=\"#ffffff\"", svg);

        // Verify NO base64 rasterized image
        Assert.DoesNotContain("data:image/png;base64", svg);

        // Verify valid SVG structure
        Assert.StartsWith("<?xml", svg);
        Assert.Contains("<svg xmlns=", svg);
        Assert.EndsWith("</svg>", svg);
    }

    /// <summary>
    /// Verifies SVG-only mode (no Skia backend) still works with QR
    /// when QR provider is not configured -- QR element is silently skipped.
    /// </summary>
    [Fact]
    public async Task Builder_WithSvgOnly_NoQrProvider_SkipsQrElement()
    {
        using var render = new FlexRenderBuilder()
            .WithSvg()
            .Build();

        var template = CreateTemplate(data: "https://example.com");

        var svg = await render.RenderToSvg(template, new ObjectValue());

        // QR should be silently skipped (no provider configured)
        Assert.DoesNotContain("<path d=\"M", svg);
        Assert.DoesNotContain("data:image/png;base64", svg);
    }

    /// <summary>
    /// Verifies raster output still works when SVG renderer is configured with Skia backend.
    /// QR codes in raster output should still be bitmap-based (no regression).
    /// </summary>
    [Fact]
    public async Task Builder_WithSvgAndSkia_RasterOutputStillWorks()
    {
        using var render = new FlexRenderBuilder()
            .WithSvg(svg => svg.WithSkia(skia => skia.WithQr()))
            .Build();

        var template = CreateTemplate(data: "https://example.com");

        // Should not throw -- raster path still works through Skia backend
        var pngBytes = await render.RenderToPng(template, new ObjectValue());

        Assert.NotNull(pngBytes);
        Assert.True(pngBytes.Length > 0);
    }

    private static Template CreateTemplate(
        string data,
        string foreground = "#000000",
        string? background = null)
    {
        var qr = new QrElement
        {
            Data = data,
            Foreground = foreground,
            Background = background!,
            Width = "200",
            Height = "200"
        };

        var flex = new FlexElement();
        flex.AddChild(qr);

        var template = new Template
        {
            Canvas = new CanvasSettings { Width = 300, Height = 300 }
        };
        template.AddElement(flex);

        return template;
    }
}
