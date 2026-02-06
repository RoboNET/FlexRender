using FlexRender.Abstractions;
using FlexRender.Configuration;
using FlexRender.Yaml;
using Xunit;

namespace FlexRender.Tests.Extensions;

public sealed class FlexRenderExtensionsFormatTests : IDisposable
{
    private readonly IFlexRender _render;
    private readonly string _tempDir;
    private readonly string _templatePath;

    private const string SimpleYaml = """
        canvas:
          fixed: both
          width: 100
          height: 50
          background: "#ffffff"
        layout:
          - type: text
            content: "Hello"
            size: 16
            color: "#000000"
        """;

    public FlexRenderExtensionsFormatTests()
    {
        _render = new FlexRenderBuilder()
            .WithSkia()
            .Build();

        _tempDir = Path.Combine(Path.GetTempPath(), $"FlexRenderExtTests_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);
        _templatePath = Path.Combine(_tempDir, "test.yaml");
        File.WriteAllText(_templatePath, SimpleYaml);
    }

    public void Dispose()
    {
        _render.Dispose();
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, recursive: true);
    }

    // --- RenderYamlToPng ---

    [Fact]
    public async Task RenderYamlToPng_ValidYaml_ProducesPng()
    {
        var result = await _render.RenderYamlToPng(SimpleYaml);

        Assert.NotNull(result);
        Assert.True(result.Length > 0);
        Assert.Equal(0x89, result[0]); // PNG magic
    }

    [Fact]
    public async Task RenderYamlToPng_NullRender_ThrowsArgumentNullException()
    {
        IFlexRender nullRender = null!;

        await Assert.ThrowsAsync<ArgumentNullException>(
            () => nullRender.RenderYamlToPng("yaml"));
    }

    [Fact]
    public async Task RenderYamlToPng_NullYaml_ThrowsArgumentNullException()
    {
        await Assert.ThrowsAsync<ArgumentNullException>(
            () => _render.RenderYamlToPng(null!));
    }

    // --- RenderYamlToJpeg ---

    [Fact]
    public async Task RenderYamlToJpeg_ValidYaml_ProducesJpeg()
    {
        var result = await _render.RenderYamlToJpeg(SimpleYaml);

        Assert.NotNull(result);
        Assert.True(result.Length > 0);
        Assert.Equal(0xFF, result[0]); // JPEG magic
    }

    [Fact]
    public async Task RenderYamlToJpeg_WithQuality_UsesQuality()
    {
        var lowQ = await _render.RenderYamlToJpeg(SimpleYaml, options: new JpegOptions { Quality = 1 });
        var highQ = await _render.RenderYamlToJpeg(SimpleYaml, options: new JpegOptions { Quality = 100 });

        Assert.True(highQ.Length > lowQ.Length);
    }

    // --- RenderYamlToBmp ---

    [Fact]
    public async Task RenderYamlToBmp_ValidYaml_ProducesBmp()
    {
        var result = await _render.RenderYamlToBmp(SimpleYaml);

        Assert.NotNull(result);
        Assert.True(result.Length > 0);
        Assert.Equal((byte)'B', result[0]); // BMP magic
    }

    // --- RenderYamlToRaw ---

    [Fact]
    public async Task RenderYamlToRaw_ValidYaml_ProducesRaw()
    {
        var result = await _render.RenderYamlToRaw(SimpleYaml);

        Assert.NotNull(result);
        Assert.Equal(100 * 50 * 4, result.Length);
    }

    // --- RenderFileToPng ---

    [Fact]
    public async Task RenderFileToPng_ValidPath_ProducesPng()
    {
        var result = await _render.RenderFileToPng(_templatePath);

        Assert.NotNull(result);
        Assert.True(result.Length > 0);
        Assert.Equal(0x89, result[0]);
    }

    [Fact]
    public async Task RenderFileToPng_NullRender_ThrowsArgumentNullException()
    {
        IFlexRender nullRender = null!;

        await Assert.ThrowsAsync<ArgumentNullException>(
            () => nullRender.RenderFileToPng(_templatePath));
    }

    [Fact]
    public async Task RenderFileToPng_NullPath_ThrowsArgumentNullException()
    {
        await Assert.ThrowsAsync<ArgumentNullException>(
            () => _render.RenderFileToPng(null!));
    }

    // --- RenderFileToJpeg ---

    [Fact]
    public async Task RenderFileToJpeg_ValidPath_ProducesJpeg()
    {
        var result = await _render.RenderFileToJpeg(_templatePath);

        Assert.NotNull(result);
        Assert.True(result.Length > 0);
        Assert.Equal(0xFF, result[0]);
    }

    [Fact]
    public async Task RenderFileToJpeg_WithQuality_UsesQuality()
    {
        var lowQ = await _render.RenderFileToJpeg(_templatePath, options: new JpegOptions { Quality = 1 });
        var highQ = await _render.RenderFileToJpeg(_templatePath, options: new JpegOptions { Quality = 100 });

        Assert.True(highQ.Length > lowQ.Length);
    }

    // --- RenderFileToBmp ---

    [Fact]
    public async Task RenderFileToBmp_ValidPath_ProducesBmp()
    {
        var result = await _render.RenderFileToBmp(_templatePath);

        Assert.NotNull(result);
        Assert.True(result.Length > 0);
        Assert.Equal((byte)'B', result[0]);
    }

    [Fact]
    public async Task RenderFileToBmp_WithColorMode_UsesColorMode()
    {
        var full = await _render.RenderFileToBmp(_templatePath, options: new BmpOptions { ColorMode = BmpColorMode.Bgra32 });
        var mono = await _render.RenderFileToBmp(_templatePath, options: new BmpOptions { ColorMode = BmpColorMode.Monochrome1 });

        Assert.True(mono.Length < full.Length);
    }

    // --- RenderFileToRaw ---

    [Fact]
    public async Task RenderFileToRaw_ValidPath_ProducesRaw()
    {
        var result = await _render.RenderFileToRaw(_templatePath);

        Assert.NotNull(result);
        Assert.Equal(100 * 50 * 4, result.Length);
    }
}
