using FlexRender.Abstractions;
using FlexRender.Configuration;
using FlexRender.Parsing.Ast;
using Xunit;

namespace FlexRender.ImageSharp.Tests;

public sealed class ImageSharpRenderTests : IDisposable
{
    private readonly ImageSharpRender _render;

    public ImageSharpRenderTests()
    {
        _render = new ImageSharpRender(
            new ResourceLimits(),
            new FlexRenderOptions(),
            Array.Empty<IResourceLoader>(),
            new ImageSharpBuilder());
    }

    [Fact]
    public async Task Render_SimpleTemplate_ReturnsPngBytes()
    {
        var template = new Template
        {
            Canvas = new CanvasSettings { Width = 100, Height = 50, Fixed = FixedDimension.Both, Background = "#ff0000" }
        };

        var result = await _render.Render(template);

        Assert.NotNull(result);
        Assert.True(result.Length > 0);
        // PNG magic bytes: 137 80 78 71 13 10 26 10
        Assert.Equal(137, result[0]);
        Assert.Equal(80, result[1]);
        Assert.Equal(78, result[2]);
        Assert.Equal(71, result[3]);
    }

    [Fact]
    public async Task RenderToPng_ReturnsValidPng()
    {
        var template = new Template
        {
            Canvas = new CanvasSettings { Width = 100, Height = 50, Fixed = FixedDimension.Both, Background = "#00ff00" }
        };

        var result = await _render.RenderToPng(template);

        Assert.True(result.Length > 0);
        Assert.Equal(137, result[0]); // PNG magic byte
    }

    [Fact]
    public async Task RenderToJpeg_ReturnsValidJpeg()
    {
        var template = new Template
        {
            Canvas = new CanvasSettings { Width = 100, Height = 50, Fixed = FixedDimension.Both, Background = "#0000ff" }
        };

        var result = await _render.RenderToJpeg(template);

        Assert.True(result.Length > 0);
        // JPEG magic bytes: FF D8 FF
        Assert.Equal(0xFF, result[0]);
        Assert.Equal(0xD8, result[1]);
    }

    [Fact]
    public async Task RenderToBmp_ReturnsValidBmp()
    {
        var template = new Template
        {
            Canvas = new CanvasSettings { Width = 100, Height = 50, Fixed = FixedDimension.Both, Background = "#ff0000" }
        };

        var result = await _render.RenderToBmp(template);

        Assert.True(result.Length > 0);
        // BMP magic bytes: 42 4D (BM)
        Assert.Equal(0x42, result[0]);
        Assert.Equal(0x4D, result[1]);
    }

    [Fact]
    public async Task RenderToRaw_ReturnsPixelData()
    {
        var template = new Template
        {
            Canvas = new CanvasSettings { Width = 10, Height = 5, Fixed = FixedDimension.Both, Background = "#ff0000" }
        };

        var result = await _render.RenderToRaw(template);

        // 10x5 pixels * 4 bytes per pixel (RGBA) = 200 bytes
        Assert.Equal(200, result.Length);
    }

    [Fact]
    public async Task Render_WithTextElement_ProducesOutput()
    {
        var template = new Template
        {
            Canvas = new CanvasSettings { Width = 200, Height = 50, Fixed = FixedDimension.Both, Background = "#ffffff" }
        };
        template.AddElement(new TextElement { Content = "Hello", Size = "16", Color = "#000000" });

        var result = await _render.Render(template);

        Assert.True(result.Length > 100); // Should produce meaningful output
    }

    [Fact]
    public async Task RenderToPng_ToStream_WritesData()
    {
        var template = new Template
        {
            Canvas = new CanvasSettings { Width = 100, Height = 50, Fixed = FixedDimension.Both, Background = "#ff0000" }
        };

        using var stream = new MemoryStream();
        await _render.RenderToPng(stream, template);

        Assert.True(stream.Length > 0);
    }

    [Fact]
    public async Task RenderToJpeg_ToStream_WritesData()
    {
        var template = new Template
        {
            Canvas = new CanvasSettings { Width = 100, Height = 50, Fixed = FixedDimension.Both, Background = "#0000ff" }
        };

        using var stream = new MemoryStream();
        await _render.RenderToJpeg(stream, template);

        Assert.True(stream.Length > 0);
    }

    [Fact]
    public async Task RenderToBmp_ToStream_WritesData()
    {
        var template = new Template
        {
            Canvas = new CanvasSettings { Width = 100, Height = 50, Fixed = FixedDimension.Both, Background = "#00ff00" }
        };

        using var stream = new MemoryStream();
        await _render.RenderToBmp(stream, template);

        Assert.True(stream.Length > 0);
    }

    [Fact]
    public async Task RenderToRaw_ToStream_WritesData()
    {
        var template = new Template
        {
            Canvas = new CanvasSettings { Width = 10, Height = 5, Fixed = FixedDimension.Both, Background = "#ff0000" }
        };

        using var stream = new MemoryStream();
        await _render.RenderToRaw(stream, template);

        Assert.Equal(200, stream.Length);
    }

    [Fact]
    public async Task Render_DisposedRenderer_Throws()
    {
        var render = new ImageSharpRender(
            new ResourceLimits(),
            new FlexRenderOptions(),
            Array.Empty<IResourceLoader>(),
            new ImageSharpBuilder());

        render.Dispose();

        var template = new Template
        {
            Canvas = new CanvasSettings { Width = 100, Height = 50, Fixed = FixedDimension.Both }
        };

        await Assert.ThrowsAsync<ObjectDisposedException>(() => render.Render(template));
    }

    [Fact]
    public async Task Render_NullTemplate_ThrowsArgumentNull()
    {
        await Assert.ThrowsAsync<ArgumentNullException>(() => _render.Render(null!));
    }

    [Fact]
    public async Task RenderToSvg_ThrowsNotSupported()
    {
        var template = new Template
        {
            Canvas = new CanvasSettings { Width = 100, Height = 50, Fixed = FixedDimension.Both }
        };

        IFlexRender render = _render;
        await Assert.ThrowsAsync<NotSupportedException>(() => render.RenderToSvg(template));
    }

    [Fact]
    public async Task Render_JpegFormat_ReturnsJpeg()
    {
        var template = new Template
        {
            Canvas = new CanvasSettings { Width = 100, Height = 50, Fixed = FixedDimension.Both, Background = "#ff0000" }
        };

        var result = await _render.Render(template, format: ImageFormat.Jpeg);

        Assert.Equal(0xFF, result[0]);
        Assert.Equal(0xD8, result[1]);
    }

    [Fact]
    public async Task Render_BmpFormat_ReturnsBmp()
    {
        var template = new Template
        {
            Canvas = new CanvasSettings { Width = 100, Height = 50, Fixed = FixedDimension.Both, Background = "#ff0000" }
        };

        var result = await _render.Render(template, format: ImageFormat.Bmp);

        Assert.Equal(0x42, result[0]);
        Assert.Equal(0x4D, result[1]);
    }

    [Fact]
    public async Task Render_RawFormat_ReturnsPixelData()
    {
        var template = new Template
        {
            Canvas = new CanvasSettings { Width = 10, Height = 5, Fixed = FixedDimension.Both, Background = "#ff0000" }
        };

        var result = await _render.Render(template, format: ImageFormat.Raw);

        Assert.Equal(200, result.Length);
    }

    [Fact]
    public static void Dispose_MultipleTimes_DoesNotThrow()
    {
        var render = new ImageSharpRender(
            new ResourceLimits(),
            new FlexRenderOptions(),
            Array.Empty<IResourceLoader>(),
            new ImageSharpBuilder());

        render.Dispose();
        render.Dispose(); // Should not throw
    }

    public void Dispose()
    {
        _render.Dispose();
    }
}
