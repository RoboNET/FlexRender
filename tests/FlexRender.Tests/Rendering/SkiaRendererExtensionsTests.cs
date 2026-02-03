using FlexRender.Parsing.Ast;
using FlexRender.Rendering;
using SkiaSharp;
using Xunit;

namespace FlexRender.Tests.Rendering;

public class SkiaRendererExtensionsTests : IDisposable
{
    private readonly SkiaRenderer _renderer = new();
    private readonly string _tempDir;
    private readonly Template _testTemplate;
    private readonly ObjectValue _testData;

    public SkiaRendererExtensionsTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"FlexRenderExtTests_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);

        _testTemplate = new Template
        {
            Canvas = new CanvasSettings { Width = 100, Background = "#ffffff" },
            Elements = new List<TemplateElement>
            {
                new TextElement { Content = "Test", Size = "16" }
            }
        };
        _testData = new ObjectValue();
    }

    public void Dispose()
    {
        _renderer.Dispose();
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, recursive: true);
    }

    [Fact]
    public void RenderToBitmap_CreatesCorrectSizedBitmap()
    {
        using var bitmap = _renderer.RenderToBitmap(_testTemplate, _testData);

        Assert.NotNull(bitmap);
        Assert.Equal(100, bitmap.Width);
        Assert.True(bitmap.Height > 0);
    }

    [Fact]
    public void RenderToBitmap_HasCorrectBackgroundColor()
    {
        var template = new Template
        {
            Canvas = new CanvasSettings { Width = 50, Background = "#00ff00" },
            Elements = new List<TemplateElement>
            {
                new TextElement { Content = "X", Size = "30" }
            }
        };

        using var bitmap = _renderer.RenderToBitmap(template, _testData);

        // Check a pixel that should have the green background
        var pixel = bitmap.GetPixel(5, 5);
        Assert.Equal(0, pixel.Red);
        Assert.Equal(255, pixel.Green);
        Assert.Equal(0, pixel.Blue);
    }

    [Fact]
    public void RenderToPng_ReturnsValidPngBytes()
    {
        var pngBytes = _renderer.RenderToPng(_testTemplate, _testData);

        Assert.NotNull(pngBytes);
        Assert.True(pngBytes.Length > 0);

        // Check PNG magic number
        Assert.Equal(0x89, pngBytes[0]);
        Assert.Equal((byte)'P', pngBytes[1]);
        Assert.Equal((byte)'N', pngBytes[2]);
        Assert.Equal((byte)'G', pngBytes[3]);
    }

    [Fact]
    public void RenderToJpeg_ReturnsValidJpegBytes()
    {
        var jpegBytes = _renderer.RenderToJpeg(_testTemplate, _testData);

        Assert.NotNull(jpegBytes);
        Assert.True(jpegBytes.Length > 0);

        // Check JPEG magic number (SOI marker)
        Assert.Equal(0xFF, jpegBytes[0]);
        Assert.Equal(0xD8, jpegBytes[1]);
    }

    [Fact]
    public void RenderToJpeg_WithQuality_AffectsFileSize()
    {
        var lowQuality = _renderer.RenderToJpeg(_testTemplate, _testData, quality: 10);
        var highQuality = _renderer.RenderToJpeg(_testTemplate, _testData, quality: 100);

        // Higher quality typically means larger file size
        Assert.True(highQuality.Length >= lowQuality.Length);
    }

    [Fact]
    public void RenderToFile_Png_CreatesFile()
    {
        var filePath = Path.Combine(_tempDir, "output.png");

        _renderer.RenderToFile(_testTemplate, _testData, filePath);

        Assert.True(File.Exists(filePath));
        var bytes = File.ReadAllBytes(filePath);
        Assert.Equal(0x89, bytes[0]); // PNG magic
    }

    [Fact]
    public void RenderToFile_Jpg_CreatesFile()
    {
        var filePath = Path.Combine(_tempDir, "output.jpg");

        _renderer.RenderToFile(_testTemplate, _testData, filePath);

        Assert.True(File.Exists(filePath));
        var bytes = File.ReadAllBytes(filePath);
        Assert.Equal(0xFF, bytes[0]); // JPEG magic
        Assert.Equal(0xD8, bytes[1]);
    }

    [Fact]
    public void RenderToFile_Jpeg_CreatesFile()
    {
        var filePath = Path.Combine(_tempDir, "output.jpeg");

        _renderer.RenderToFile(_testTemplate, _testData, filePath);

        Assert.True(File.Exists(filePath));
    }

    [Fact]
    public void RenderToFile_Bmp_CreatesFile()
    {
        var filePath = Path.Combine(_tempDir, "output.bmp");

        _renderer.RenderToFile(_testTemplate, _testData, filePath);

        // BMP may not be supported by SkiaSharp encoder, in which case it falls back to PNG
        Assert.True(File.Exists(filePath));
        var bytes = File.ReadAllBytes(filePath);
        // Either BMP or PNG format
        var isBmp = bytes[0] == (byte)'B' && bytes[1] == (byte)'M';
        var isPng = bytes[0] == 0x89 && bytes[1] == (byte)'P';
        Assert.True(isBmp || isPng);
    }

    [Fact]
    public void RenderToFile_UnknownExtension_DefaultsToPng()
    {
        var filePath = Path.Combine(_tempDir, "output.xyz");

        _renderer.RenderToFile(_testTemplate, _testData, filePath);

        Assert.True(File.Exists(filePath));
        var bytes = File.ReadAllBytes(filePath);
        Assert.Equal(0x89, bytes[0]); // PNG magic
    }

    [Fact]
    public void RenderToFile_WithTypedData_Works()
    {
        var filePath = Path.Combine(_tempDir, "typed.png");
        var template = new Template
        {
            Canvas = new CanvasSettings { Width = 100 },
            Elements = new List<TemplateElement>
            {
                new TextElement { Content = "Price: {{price}}", Size = "16" }
            }
        };
        var data = new TestData { Price = 42.50m };

        _renderer.RenderToFile(template, data, filePath);

        Assert.True(File.Exists(filePath));
    }

    [Fact]
    public void RenderToBitmap_WithTypedData_Works()
    {
        var template = new Template
        {
            Canvas = new CanvasSettings { Width = 100 },
            Elements = new List<TemplateElement>
            {
                new TextElement { Content = "Price: {{price}}", Size = "16" }
            }
        };
        var data = new TestData { Price = 99.99m };

        using var bitmap = _renderer.RenderToBitmap(template, data);

        Assert.NotNull(bitmap);
    }

    private class TestData : ITemplateData
    {
        public decimal Price { get; set; }

        public ObjectValue ToTemplateValue()
        {
            return new ObjectValue { ["price"] = Price };
        }
    }
}
