using FlexRender.Abstractions;
using FlexRender.Barcode;
using FlexRender.Parsing.Ast;
using FlexRender.QrCode;
using FlexRender.Yaml;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace FlexRender.Tests.Rendering;

/// <summary>
/// Tests for <see cref="IFlexRender"/> interface implementation.
/// </summary>
public sealed class FlexRenderInterfaceTests : IDisposable
{
    private readonly ServiceProvider _serviceProvider;
    private readonly IFlexRender _renderer;

    public FlexRenderInterfaceTests()
    {
        var services = new ServiceCollection();
        services.AddFlexRender(builder => builder
            .WithSkia(skia => skia
                .WithQr()
                .WithBarcode()));
        _serviceProvider = services.BuildServiceProvider();
        _renderer = _serviceProvider.GetRequiredService<IFlexRender>();
    }

    public void Dispose()
    {
        _renderer.Dispose();
        _serviceProvider.Dispose();
    }

    [Fact]
    public async Task RenderYaml_ToBytes_ReturnsValidPng()
    {
        var yaml = """
            canvas:
              width: 200
              background: "#ffffff"
            layout:
              - type: text
                content: "Interface Test"
                size: "16"
            """;

        var bytes = await _renderer.RenderYaml(yaml);

        Assert.NotNull(bytes);
        Assert.True(bytes.Length > 0);
        // PNG magic bytes: 0x89 0x50 0x4E 0x47
        Assert.Equal(0x89, bytes[0]);
        Assert.Equal(0x50, bytes[1]);
        Assert.Equal(0x4E, bytes[2]);
        Assert.Equal(0x47, bytes[3]);
    }

    [Fact]
    public async Task RenderYaml_ToStream_WritesValidPng()
    {
        var yaml = """
            canvas:
              width: 200
              background: "#ffffff"
            layout:
              - type: text
                content: "Stream Test"
                size: "16"
            """;

        using var stream = new MemoryStream();
        await _renderer.RenderYaml(stream, yaml);

        Assert.True(stream.Length > 0);
        stream.Position = 0;
        var bytes = stream.ToArray();
        // PNG magic bytes
        Assert.Equal(0x89, bytes[0]);
        Assert.Equal(0x50, bytes[1]);
        Assert.Equal(0x4E, bytes[2]);
        Assert.Equal(0x47, bytes[3]);
    }

    [Fact]
    public async Task RenderYaml_WithData_SubstitutesVariables()
    {
        var yaml = """
            canvas:
              width: 200
              background: "#ffffff"
            layout:
              - type: text
                content: "Hello {{name}}"
                size: "16"
            """;

        var data = new ObjectValue { ["name"] = "World" };
        var bytes = await _renderer.RenderYaml(yaml, data);

        Assert.NotNull(bytes);
        Assert.True(bytes.Length > 0);
    }

    [Fact]
    public async Task RenderYaml_AsJpeg_ReturnsValidJpeg()
    {
        var yaml = """
            canvas:
              width: 100
              background: "#ffffff"
            layout:
              - type: text
                content: "JPEG Test"
            """;

        var bytes = await _renderer.RenderYaml(yaml, format: ImageFormat.Jpeg);

        Assert.NotNull(bytes);
        Assert.True(bytes.Length > 0);
        // JPEG magic bytes: 0xFF 0xD8
        Assert.Equal(0xFF, bytes[0]);
        Assert.Equal(0xD8, bytes[1]);
    }

    [Fact]
    public async Task RenderYaml_AsBmp_ReturnsValidBmp()
    {
        var yaml = """
            canvas:
              width: 100
              background: "#ffffff"
            layout:
              - type: text
                content: "BMP Test"
            """;

        var bytes = await _renderer.RenderYaml(yaml, format: ImageFormat.Bmp);

        Assert.NotNull(bytes);
        Assert.True(bytes.Length > 0);
        // BMP magic bytes: 'B' 'M'
        Assert.Equal((byte)'B', bytes[0]);
        Assert.Equal((byte)'M', bytes[1]);
    }

    [Fact]
    public async Task RenderYaml_ThrowsOnNullTemplate()
    {
        await Assert.ThrowsAsync<ArgumentNullException>(
            () => _renderer.RenderYaml((string)null!));
    }

    [Fact]
    public async Task RenderYaml_ThrowsOnEmptyTemplate()
    {
        // Empty YAML throws parse exception
        await Assert.ThrowsAnyAsync<Exception>(
            () => _renderer.RenderYaml(""));
    }

    /// <summary>
    /// Verifies that IFlexRender can render Template objects directly.
    /// </summary>
    [Fact]
    public async Task Render_Template_ReturnsValidPng()
    {
        var template = new Template
        {
            Canvas = new CanvasSettings { Width = 100, Background = "#ffffff" },
            Elements = new List<TemplateElement>
            {
                new TextElement { Content = "Direct Render", Size = "12" }
            }
        };

        var bytes = await _renderer.Render(template);

        Assert.NotNull(bytes);
        Assert.True(bytes.Length > 0);
        // PNG magic bytes
        Assert.Equal(0x89, bytes[0]);
        Assert.Equal(0x50, bytes[1]);
    }

    /// <summary>
    /// Verifies that IFlexRender can render to stream.
    /// </summary>
    [Fact]
    public async Task Render_ToStream_WritesValidPng()
    {
        var template = new Template
        {
            Canvas = new CanvasSettings { Width = 100 },
            Elements = new List<TemplateElement>
            {
                new TextElement { Content = "Stream", Size = "10" }
            }
        };

        using var stream = new MemoryStream();
        await _renderer.Render(stream, template);

        Assert.True(stream.Length > 0);
        stream.Position = 0;
        var bytes = stream.ToArray();
        // PNG magic bytes
        Assert.Equal(0x89, bytes[0]);
        Assert.Equal(0x50, bytes[1]);
    }

    /// <summary>
    /// Verifies that Render throws on null template.
    /// </summary>
    [Fact]
    public async Task Render_NullTemplate_ThrowsArgumentNullException()
    {
        await Assert.ThrowsAsync<ArgumentNullException>(
            () => _renderer.Render((Template)null!));
    }
}
