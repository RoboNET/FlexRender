using FlexRender.Abstractions;
using FlexRender.Configuration;
using FlexRender.Parsing.Ast;
using Xunit;

namespace FlexRender.Tests.Rendering;

public sealed class SkiaRenderFormatTests : IDisposable
{
    private readonly IFlexRender _render;
    private readonly Template _simpleTemplate;

    public SkiaRenderFormatTests()
    {
        _render = new FlexRenderBuilder()
            .WithSkia()
            .Build();

        _simpleTemplate = new Template
        {
            Canvas = new CanvasSettings { Fixed = FixedDimension.Both, Width = 100, Height = 50, Background = "#ffffff" },
            Elements = [new TextElement { Content = "Hello", Size = "16", Color = "#000000" }]
        };
    }

    public void Dispose() => _render.Dispose();

    // --- PNG ---

    [Fact]
    public async Task RenderToPng_SimpleTemplate_ProducesPngBytes()
    {
        var result = await _render.RenderToPng(_simpleTemplate);

        Assert.NotNull(result);
        Assert.True(result.Length > 0);
        // PNG magic bytes: 137 80 78 71
        Assert.Equal(0x89, result[0]);
        Assert.Equal(0x50, result[1]);
        Assert.Equal(0x4E, result[2]);
        Assert.Equal(0x47, result[3]);
    }

    [Fact]
    public async Task RenderToPng_Stream_WritesToStream()
    {
        using var stream = new MemoryStream();

        await _render.RenderToPng(stream, _simpleTemplate);

        Assert.True(stream.Length > 0);
        stream.Position = 0;
        Assert.Equal(0x89, stream.ReadByte());
    }

    [Fact]
    public async Task RenderToPng_NullTemplate_ThrowsArgumentNullException()
    {
        await Assert.ThrowsAsync<ArgumentNullException>(
            () => _render.RenderToPng(null!));
    }

    [Fact]
    public async Task RenderToPng_WithCompressionLevel0_ProducesLargerFile()
    {
        var small = await _render.RenderToPng(_simpleTemplate, options: new PngOptions { CompressionLevel = 100 });
        var large = await _render.RenderToPng(_simpleTemplate, options: new PngOptions { CompressionLevel = 0 });

        Assert.True(large.Length >= small.Length,
            $"CompressionLevel 0 ({large.Length} bytes) should produce >= CompressionLevel 100 ({small.Length} bytes)");
    }

    [Fact]
    public async Task RenderToPng_InvalidCompressionLevel_ThrowsArgumentOutOfRange()
    {
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(
            () => _render.RenderToPng(_simpleTemplate, options: new PngOptions { CompressionLevel = -1 }));

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(
            () => _render.RenderToPng(_simpleTemplate, options: new PngOptions { CompressionLevel = 101 }));
    }

    // --- JPEG ---

    [Fact]
    public async Task RenderToJpeg_SimpleTemplate_ProducesJpegBytes()
    {
        var result = await _render.RenderToJpeg(_simpleTemplate);

        Assert.NotNull(result);
        Assert.True(result.Length > 0);
        // JPEG magic bytes: FF D8
        Assert.Equal(0xFF, result[0]);
        Assert.Equal(0xD8, result[1]);
    }

    [Fact]
    public async Task RenderToJpeg_Quality100_ProducesLargerFile()
    {
        var small = await _render.RenderToJpeg(_simpleTemplate, options: new JpegOptions { Quality = 1 });
        var large = await _render.RenderToJpeg(_simpleTemplate, options: new JpegOptions { Quality = 100 });

        Assert.True(large.Length > small.Length,
            $"Quality 100 ({large.Length} bytes) should be > Quality 1 ({small.Length} bytes)");
    }

    [Fact]
    public async Task RenderToJpeg_Quality0_ThrowsArgumentOutOfRange()
    {
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(
            () => _render.RenderToJpeg(_simpleTemplate, options: new JpegOptions { Quality = 0 }));
    }

    [Fact]
    public async Task RenderToJpeg_Quality101_ThrowsArgumentOutOfRange()
    {
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(
            () => _render.RenderToJpeg(_simpleTemplate, options: new JpegOptions { Quality = 101 }));
    }

    [Fact]
    public async Task RenderToJpeg_NullTemplate_ThrowsArgumentNullException()
    {
        await Assert.ThrowsAsync<ArgumentNullException>(
            () => _render.RenderToJpeg(null!));
    }

    [Fact]
    public async Task RenderToJpeg_Stream_WritesToStream()
    {
        using var stream = new MemoryStream();

        await _render.RenderToJpeg(stream, _simpleTemplate);

        Assert.True(stream.Length > 0);
        stream.Position = 0;
        Assert.Equal(0xFF, stream.ReadByte());
    }

    // --- BMP ---

    [Fact]
    public async Task RenderToBmp_SimpleTemplate_ProducesBmpBytes()
    {
        var result = await _render.RenderToBmp(_simpleTemplate);

        Assert.NotNull(result);
        Assert.True(result.Length > 0);
        // BMP magic bytes: BM
        Assert.Equal((byte)'B', result[0]);
        Assert.Equal((byte)'M', result[1]);
    }

    [Fact]
    public async Task RenderToBmp_Monochrome1_ProducesSmallerFile()
    {
        var full = await _render.RenderToBmp(_simpleTemplate, options: new BmpOptions { ColorMode = BmpColorMode.Bgra32 });
        var mono = await _render.RenderToBmp(_simpleTemplate, options: new BmpOptions { ColorMode = BmpColorMode.Monochrome1 });

        Assert.True(mono.Length < full.Length,
            $"Monochrome ({mono.Length} bytes) should be < Bgra32 ({full.Length} bytes)");
    }

    [Fact]
    public async Task RenderToBmp_NullTemplate_ThrowsArgumentNullException()
    {
        await Assert.ThrowsAsync<ArgumentNullException>(
            () => _render.RenderToBmp(null!));
    }

    [Fact]
    public async Task RenderToBmp_Stream_WritesToStream()
    {
        using var stream = new MemoryStream();

        await _render.RenderToBmp(stream, _simpleTemplate);

        Assert.True(stream.Length > 0);
        stream.Position = 0;
        Assert.Equal((byte)'B', (byte)stream.ReadByte());
    }

    // --- Raw ---

    [Fact]
    public async Task RenderToRaw_SimpleTemplate_ProducesRawBytes()
    {
        var result = await _render.RenderToRaw(_simpleTemplate);

        Assert.NotNull(result);
        // Raw = 4 bytes per pixel * width * height
        Assert.Equal(100 * 50 * 4, result.Length);
    }

    [Fact]
    public async Task RenderToRaw_NullTemplate_ThrowsArgumentNullException()
    {
        await Assert.ThrowsAsync<ArgumentNullException>(
            () => _render.RenderToRaw(null!));
    }

    [Fact]
    public async Task RenderToRaw_Stream_WritesToStream()
    {
        using var stream = new MemoryStream();

        await _render.RenderToRaw(stream, _simpleTemplate);

        Assert.Equal(100 * 50 * 4, stream.Length);
    }

    // --- Disposed ---

    [Fact]
    public async Task RenderToPng_Disposed_ThrowsObjectDisposedException()
    {
        _render.Dispose();

        await Assert.ThrowsAsync<ObjectDisposedException>(
            () => _render.RenderToPng(_simpleTemplate));
    }
}
