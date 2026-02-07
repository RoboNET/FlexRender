using FlexRender.Abstractions;
using FlexRender.Barcode.ImageSharp;
using FlexRender.Configuration;
using FlexRender.ImageSharp;
using FlexRender.Parsing.Ast;
using FlexRender.QrCode.ImageSharp;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using Xunit;

namespace FlexRender.ImageSharp.Tests.Rendering;

public sealed class ImageSharpRenderingEngineProviderTests : IDisposable
{
    private readonly ImageSharpRender _rendererWithProviders;
    private readonly ImageSharpRender _rendererWithoutProviders;
    private readonly string _tempDir;
    private readonly string _testImagePath;

    public ImageSharpRenderingEngineProviderTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"imgsharp_eng_test_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);

        // Create a small test image
        _testImagePath = Path.Combine(_tempDir, "test_img.png");
        using (var img = new Image<Rgba32>(50, 50, new Rgba32(0, 0, 255, 255)))
        {
            img.Save(_testImagePath, new SixLabors.ImageSharp.Formats.Png.PngEncoder());
        }

        var limits = new ResourceLimits();
        var options = new FlexRenderOptions();

        var builderWithProviders = new ImageSharpBuilder();
        builderWithProviders.WithQr();
        builderWithProviders.WithBarcode();

        _rendererWithProviders = new ImageSharpRender(
            limits, options, Array.Empty<IResourceLoader>(), builderWithProviders);

        _rendererWithoutProviders = new ImageSharpRender(
            limits, options, Array.Empty<IResourceLoader>(), new ImageSharpBuilder());
    }

    [Fact]
    public async Task Render_QrElement_WithProvider_ProducesOutput()
    {
        var template = new Template
        {
            Canvas = new CanvasSettings { Width = 200, Height = 200, Fixed = FixedDimension.Both, Background = "#ffffff" }
        };
        template.AddElement(new QrElement { Data = "https://example.com", Size = 150 });

        var result = await _rendererWithProviders.Render(template);
        Assert.True(result.Length > 0);

        // Verify image dimensions
        using var image = Image.Load<Rgba32>(result);
        Assert.Equal(200, image.Width);
        Assert.Equal(200, image.Height);
    }

    [Fact]
    public async Task Render_BarcodeElement_WithProvider_ProducesOutput()
    {
        var template = new Template
        {
            Canvas = new CanvasSettings { Width = 300, Height = 100, Fixed = FixedDimension.Both, Background = "#ffffff" }
        };
        template.AddElement(new BarcodeElement { Data = "TEST123", BarcodeWidth = 280, BarcodeHeight = 80 });

        var result = await _rendererWithProviders.Render(template);
        Assert.True(result.Length > 0);

        using var image = Image.Load<Rgba32>(result);
        Assert.Equal(300, image.Width);
        Assert.Equal(100, image.Height);
    }

    [Fact]
    public async Task Render_ImageElement_ProducesOutput()
    {
        var template = new Template
        {
            Canvas = new CanvasSettings { Width = 200, Height = 200, Fixed = FixedDimension.Both, Background = "#ffffff" }
        };
        template.AddElement(new ImageElement { Src = _testImagePath, ImageWidth = 100, ImageHeight = 100 });

        var result = await _rendererWithoutProviders.Render(template);
        Assert.True(result.Length > 0);

        using var image = Image.Load<Rgba32>(result);
        Assert.Equal(200, image.Width);
        Assert.Equal(200, image.Height);
    }

    [Fact]
    public async Task Render_QrElement_WithoutProvider_DoesNotCrash()
    {
        var template = new Template
        {
            Canvas = new CanvasSettings { Width = 200, Height = 200, Fixed = FixedDimension.Both, Background = "#ffffff" }
        };
        template.AddElement(new QrElement { Data = "test" });

        // Should not crash, just skip rendering the QR element
        var result = await _rendererWithoutProviders.Render(template);
        Assert.True(result.Length > 0);
    }

    [Fact]
    public async Task Render_BarcodeElement_WithoutProvider_DoesNotCrash()
    {
        var template = new Template
        {
            Canvas = new CanvasSettings { Width = 200, Height = 200, Fixed = FixedDimension.Both, Background = "#ffffff" }
        };
        template.AddElement(new BarcodeElement { Data = "test" });

        var result = await _rendererWithoutProviders.Render(template);
        Assert.True(result.Length > 0);
    }

    public void Dispose()
    {
        _rendererWithProviders.Dispose();
        _rendererWithoutProviders.Dispose();
        try { Directory.Delete(_tempDir, recursive: true); }
        catch { /* best effort cleanup */ }
    }
}
