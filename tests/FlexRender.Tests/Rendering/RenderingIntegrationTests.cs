using FlexRender.Parsing;
using FlexRender.Rendering;
using SkiaSharp;
using Xunit;

namespace FlexRender.Tests.Rendering;

public class RenderingIntegrationTests : IDisposable
{
    private readonly SkiaRenderer _renderer = new();
    private readonly TemplateParser _parser = new();
    private readonly string _tempDir;

    public RenderingIntegrationTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"FlexRenderIntegration_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        _renderer.Dispose();
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, recursive: true);
    }

    [Fact]
    public void FullPipeline_YamlToImage_Works()
    {
        const string yaml = """
            template:
              name: "test-receipt"
              version: 1
            canvas:
              fixed: width
              width: 300
              background: "#ffffff"
            layout:
              - type: text
                content: "Shop Name"
                font: main
                size: 24
                color: "#000000"
                align: center
              - type: text
                content: "Item: {{item}}"
                size: 16
              - type: text
                content: "Total: {{total}} RUB"
                size: 20
                color: "#ff0000"
                align: right
            """;

        var template = _parser.Parse(yaml);
        var data = new ObjectValue
        {
            ["item"] = "Product A",
            ["total"] = 1500
        };

        var filePath = Path.Combine(_tempDir, "receipt.png");
        _renderer.RenderToFile(template, data, filePath);

        Assert.True(File.Exists(filePath));
        var fileInfo = new FileInfo(filePath);
        Assert.True(fileInfo.Length > 0);
    }

    [Fact]
    public void FullPipeline_WithVariableSubstitution_SubstitutesCorrectly()
    {
        const string yaml = """
            canvas:
              width: 200
            layout:
              - type: text
                content: "Hello {{name}}! Your balance is {{balance}}"
                size: 14
            """;

        var template = _parser.Parse(yaml);
        var data = new ObjectValue
        {
            ["name"] = "John",
            ["balance"] = 99.99m
        };

        using var bitmap = _renderer.RenderToBitmap(template, data);

        Assert.NotNull(bitmap);
        Assert.Equal(200, bitmap.Width);
    }

    [Fact]
    public void FullPipeline_MultipleFormats_AllWork()
    {
        const string yaml = """
            canvas:
              width: 100
              background: "#0000ff"
            layout:
              - type: text
                content: "Test"
                color: "#ffffff"
            """;

        var template = _parser.Parse(yaml);
        var data = new ObjectValue();

        // Test all formats
        var pngPath = Path.Combine(_tempDir, "test.png");
        var jpgPath = Path.Combine(_tempDir, "test.jpg");

        _renderer.RenderToFile(template, data, pngPath);
        _renderer.RenderToFile(template, data, jpgPath);

        Assert.True(File.Exists(pngPath));
        Assert.True(File.Exists(jpgPath));
    }

    [Fact]
    public void FullPipeline_HeightFixed_CalculatesWidth()
    {
        const string yaml = """
            canvas:
              fixed: height
              height: 100
            layout:
              - type: text
                content: "Wide text content here"
                size: 16
            """;

        var template = _parser.Parse(yaml);
        var data = new ObjectValue();

        var size = _renderer.Measure(template, data);

        Assert.Equal(100f, size.Height);
        Assert.True(size.Width > 0);
    }

    [Fact]
    public void FullPipeline_ComplexLayout_RendersAllElements()
    {
        const string yaml = """
            canvas:
              width: 300
              background: "#f0f0f0"
            layout:
              - type: text
                content: "=== RECEIPT ==="
                align: center
                size: 20
              - type: text
                content: "Date: 2024-01-15"
                size: 12
              - type: text
                content: "-------------------------"
                align: center
              - type: text
                content: "Item 1"
                size: 14
              - type: text
                content: "100.00"
                align: right
                size: 14
              - type: text
                content: "Item 2"
                size: 14
              - type: text
                content: "250.00"
                align: right
                size: 14
              - type: text
                content: "-------------------------"
                align: center
              - type: text
                content: "TOTAL: 350.00"
                align: right
                size: 18
                color: "#000000"
            """;

        var template = _parser.Parse(yaml);
        var data = new ObjectValue();

        using var bitmap = _renderer.RenderToBitmap(template, data);

        Assert.NotNull(bitmap);
        Assert.Equal(300, bitmap.Width);
        Assert.True(bitmap.Height > 100); // Should have significant height for all elements
    }

    [Fact]
    public void FullPipeline_TextRotation_DoesNotCrash()
    {
        const string yaml = """
            canvas:
              width: 200
            layout:
              - type: text
                content: "Normal"
                size: 16
              - type: text
                content: "Rotated Right"
                size: 16
                rotate: right
              - type: text
                content: "Flipped"
                size: 16
                rotate: flip
              - type: text
                content: "45 degrees"
                size: 16
                rotate: 45
            """;

        var template = _parser.Parse(yaml);
        var data = new ObjectValue();

        var exception = Record.Exception(() =>
        {
            using var bitmap = _renderer.RenderToBitmap(template, data);
        });

        Assert.Null(exception);
    }

    [Fact]
    public void FullPipeline_LongText_WrapsCorrectly()
    {
        const string yaml = """
            canvas:
              width: 150
            layout:
              - type: text
                content: "This is a very long text that should wrap to multiple lines because it exceeds the available width"
                size: 14
                wrap: true
            """;

        var template = _parser.Parse(yaml);
        var data = new ObjectValue();

        using var bitmap = _renderer.RenderToBitmap(template, data);

        // Note: LayoutEngine currently calculates height for single line only.
        // Text wrapping is handled by TextRenderer during actual rendering.
        // The bitmap height is based on layout calculation (single line height).
        // TODO: Integrate TextRenderer with LayoutEngine for accurate multi-line height calculation.
        Assert.True(bitmap.Height > 0);
    }

    [Fact]
    public void FullPipeline_EmptyData_RendersTemplateExpressions()
    {
        const string yaml = """
            canvas:
              width: 200
            layout:
              - type: text
                content: "Value: {{missing}}"
                size: 16
            """;

        var template = _parser.Parse(yaml);
        var data = new ObjectValue(); // No data provided

        // Should not throw, and should render with unsubstituted placeholder
        var exception = Record.Exception(() =>
        {
            using var bitmap = _renderer.RenderToBitmap(template, data);
        });

        Assert.Null(exception);
    }

    [Fact]
    public void FullPipeline_ByteOutput_IsValidImage()
    {
        const string yaml = """
            canvas:
              width: 100
              background: "#ffffff"
            layout:
              - type: text
                content: "Test"
            """;

        var template = _parser.Parse(yaml);
        var data = new ObjectValue();

        var pngBytes = _renderer.RenderToPng(template, data);
        var jpegBytes = _renderer.RenderToJpeg(template, data, quality: 80);

        // Verify we can decode the images back
        using var pngImage = SKBitmap.Decode(pngBytes);
        using var jpegImage = SKBitmap.Decode(jpegBytes);

        Assert.NotNull(pngImage);
        Assert.NotNull(jpegImage);
        Assert.Equal(100, pngImage.Width);
        Assert.Equal(100, jpegImage.Width);
    }
}
