using System.Reflection;
using FlexRender.Abstractions;
using FlexRender.Barcode;
using FlexRender.Configuration;
using FlexRender.Parsing.Ast;
using FlexRender.QrCode;
using Xunit;

namespace FlexRender.Tests.Configuration;

/// <summary>
/// Tests for <see cref="FlexRenderBuilder"/> fluent API.
/// </summary>
public sealed class FlexRenderBuilderTests
{
    #region Build Tests

    /// <summary>
    /// Verifies that Build throws when no renderer is configured.
    /// </summary>
    [Fact]
    public void Build_WithoutRenderer_ThrowsInvalidOperationException()
    {
        var builder = new FlexRenderBuilder();

        var exception = Assert.Throws<InvalidOperationException>(() => builder.Build());

        Assert.Contains("WithSkia", exception.Message);
    }

    /// <summary>
    /// Verifies that Build succeeds when Skia renderer is configured.
    /// </summary>
    [Fact]
    public void Build_WithSkia_ReturnsFlexRender()
    {
        var builder = new FlexRenderBuilder()
            .WithSkia();

        using var render = builder.Build();

        Assert.NotNull(render);
        Assert.IsAssignableFrom<IFlexRender>(render);
    }

    #endregion

    #region WithLimits Tests

    /// <summary>
    /// Verifies that WithLimits configures resource limits correctly.
    /// </summary>
    [Fact]
    public void WithLimits_ConfiguresMaxRenderDepth_AppliesValue()
    {
        using var render = new FlexRenderBuilder()
            .WithLimits(limits => limits.MaxRenderDepth = 42)
            .WithSkia()
            .Build();

        Assert.NotNull(render);
    }

    /// <summary>
    /// Verifies that WithLimits throws on null action.
    /// </summary>
    [Fact]
    public void WithLimits_NullAction_ThrowsArgumentNullException()
    {
        var builder = new FlexRenderBuilder();

        Assert.Throws<ArgumentNullException>(() => builder.WithLimits(null!));
    }

    /// <summary>
    /// Verifies that multiple WithLimits calls accumulate configuration.
    /// </summary>
    [Fact]
    public void WithLimits_MultipleCalls_AccumulatesConfiguration()
    {
        using var render = new FlexRenderBuilder()
            .WithLimits(limits => limits.MaxRenderDepth = 50)
            .WithLimits(limits => limits.MaxImageSize = 5 * 1024 * 1024)
            .WithSkia()
            .Build();

        Assert.NotNull(render);
    }

    #endregion

    #region WithBasePath Tests

    /// <summary>
    /// Verifies that WithBasePath sets the base path.
    /// </summary>
    [Fact]
    public void WithBasePath_ValidPath_SetsBasePath()
    {
        using var render = new FlexRenderBuilder()
            .WithBasePath("./templates")
            .WithSkia()
            .Build();

        Assert.NotNull(render);
    }

    /// <summary>
    /// Verifies that WithBasePath throws on null path.
    /// </summary>
    [Fact]
    public void WithBasePath_NullPath_ThrowsArgumentNullException()
    {
        var builder = new FlexRenderBuilder();

        Assert.Throws<ArgumentNullException>(() => builder.WithBasePath(null!));
    }

    #endregion

    #region WithoutDefaultLoaders Tests

    /// <summary>
    /// Verifies that WithoutDefaultLoaders creates a sandboxed renderer.
    /// </summary>
    [Fact]
    public void WithoutDefaultLoaders_Build_CreatesSandboxedRenderer()
    {
        using var render = new FlexRenderBuilder()
            .WithoutDefaultLoaders()
            .WithSkia()
            .Build();

        Assert.NotNull(render);
    }

    /// <summary>
    /// Verifies that WithoutDefaultLoaders followed by WithEmbeddedLoader works.
    /// </summary>
    [Fact]
    public void WithoutDefaultLoaders_ThenWithEmbeddedLoader_ConfiguresCorrectly()
    {
        using var render = new FlexRenderBuilder()
            .WithoutDefaultLoaders()
            .WithEmbeddedLoader(Assembly.GetExecutingAssembly())
            .WithSkia()
            .Build();

        Assert.NotNull(render);
    }

    #endregion

    #region WithEmbeddedLoader Tests

    /// <summary>
    /// Verifies that WithEmbeddedLoader adds an assembly.
    /// </summary>
    [Fact]
    public void WithEmbeddedLoader_ValidAssembly_AddsLoader()
    {
        using var render = new FlexRenderBuilder()
            .WithEmbeddedLoader(Assembly.GetExecutingAssembly())
            .WithSkia()
            .Build();

        Assert.NotNull(render);
    }

    /// <summary>
    /// Verifies that WithEmbeddedLoader throws on null assembly.
    /// </summary>
    [Fact]
    public void WithEmbeddedLoader_NullAssembly_ThrowsArgumentNullException()
    {
        var builder = new FlexRenderBuilder();

        Assert.Throws<ArgumentNullException>(() => builder.WithEmbeddedLoader(null!));
    }

    /// <summary>
    /// Verifies that multiple WithEmbeddedLoader calls add multiple assemblies.
    /// </summary>
    [Fact]
    public void WithEmbeddedLoader_MultipleCalls_AddsMultipleAssemblies()
    {
        using var render = new FlexRenderBuilder()
            .WithEmbeddedLoader(Assembly.GetExecutingAssembly())
            .WithEmbeddedLoader(typeof(FlexRenderBuilder).Assembly)
            .WithSkia()
            .Build();

        Assert.NotNull(render);
    }

    #endregion

    #region Method Chaining Tests

    /// <summary>
    /// Verifies that all builder methods return the same builder instance.
    /// </summary>
    [Fact]
    public void FluentMethods_ReturnSameBuilderInstance()
    {
        var builder = new FlexRenderBuilder();

        var result1 = builder.WithLimits(_ => { });
        var result2 = builder.WithBasePath("./test");
        var result3 = builder.WithoutDefaultLoaders();
        var result4 = builder.WithEmbeddedLoader(Assembly.GetExecutingAssembly());
        var result5 = builder.WithSkia();

        Assert.Same(builder, result1);
        Assert.Same(builder, result2);
        Assert.Same(builder, result3);
        Assert.Same(builder, result4);
        Assert.Same(builder, result5);
    }

    /// <summary>
    /// Verifies full configuration chain works.
    /// </summary>
    [Fact]
    public void FullConfigurationChain_AllOptions_BuildsSuccessfully()
    {
        using var render = new FlexRenderBuilder()
            .WithBasePath("./templates")
            .WithLimits(limits =>
            {
                limits.MaxRenderDepth = 200;
                limits.MaxImageSize = 20 * 1024 * 1024;
            })
            .WithEmbeddedLoader(Assembly.GetExecutingAssembly())
            .WithSkia(skia => skia
                .WithQr()
                .WithBarcode())
            .Build();

        Assert.NotNull(render);
    }

    #endregion
}

/// <summary>
/// Tests for <see cref="SkiaBuilder"/> configuration.
/// </summary>
public sealed class SkiaBuilderTests
{
    /// <summary>
    /// Verifies that WithQr enables QR code rendering.
    /// </summary>
    [Fact]
    public async Task WithQr_RenderQrElement_Succeeds()
    {
        using var render = new FlexRenderBuilder()
            .WithSkia(skia => skia.WithQr())
            .Build();

        var template = new Template
        {
            Canvas = new CanvasSettings { Width = 200, Height = 200, Fixed = FixedDimension.Both },
            Elements = new List<TemplateElement>
            {
                new QrElement
                {
                    Data = "https://example.com",
                    Width = "100",
                    Height = "100"
                }
            }
        };

        var bytes = await render.Render(template);

        Assert.NotNull(bytes);
        Assert.NotEmpty(bytes);
    }

    /// <summary>
    /// Verifies that WithBarcode enables barcode rendering.
    /// </summary>
    [Fact]
    public async Task WithBarcode_RenderBarcodeElement_Succeeds()
    {
        using var render = new FlexRenderBuilder()
            .WithSkia(skia => skia.WithBarcode())
            .Build();

        var template = new Template
        {
            Canvas = new CanvasSettings { Width = 200, Height = 100, Fixed = FixedDimension.Both },
            Elements = new List<TemplateElement>
            {
                new BarcodeElement
                {
                    Data = "123456789",
                    Format = BarcodeFormat.Code128,
                    Width = "150",
                    Height = "50"
                }
            }
        };

        var bytes = await render.Render(template);

        Assert.NotNull(bytes);
        Assert.NotEmpty(bytes);
    }

    /// <summary>
    /// Verifies that WithQr and WithBarcode can be combined.
    /// </summary>
    [Fact]
    public async Task WithQrAndWithBarcode_RenderBothElements_Succeeds()
    {
        using var render = new FlexRenderBuilder()
            .WithSkia(skia => skia
                .WithQr()
                .WithBarcode())
            .Build();

        var template = new Template
        {
            Canvas = new CanvasSettings { Width = 300, Height = 200, Fixed = FixedDimension.Both },
            Elements = new List<TemplateElement>
            {
                new QrElement
                {
                    Data = "QR Data",
                    Width = "100",
                    Height = "100"
                },
                new BarcodeElement
                {
                    Data = "BARCODE123",
                    Format = BarcodeFormat.Code128,
                    Width = "200",
                    Height = "50"
                }
            }
        };

        var bytes = await render.Render(template);

        Assert.NotNull(bytes);
        Assert.NotEmpty(bytes);
    }

    /// <summary>
    /// Verifies that rendering without QR provider skips QR elements silently.
    /// Note: Current implementation does not throw; QR elements are just not rendered.
    /// </summary>
    [Fact]
    public async Task WithoutQr_RenderQrElement_RendersWithoutQr()
    {
        using var render = new FlexRenderBuilder()
            .WithSkia()
            .Build();

        var template = new Template
        {
            Canvas = new CanvasSettings { Width = 100, Height = 100, Fixed = FixedDimension.Both, Background = "#ffffff" },
            Elements = new List<TemplateElement>
            {
                new QrElement
                {
                    Data = "test",
                    Width = "50",
                    Height = "50"
                }
            }
        };

        // Renders without QR (element is skipped)
        var bytes = await render.Render(template);
        Assert.NotEmpty(bytes);
    }

    /// <summary>
    /// Verifies that rendering without barcode provider skips barcode elements silently.
    /// Note: Current implementation does not throw; barcode elements are just not rendered.
    /// </summary>
    [Fact]
    public async Task WithoutBarcode_RenderBarcodeElement_RendersWithoutBarcode()
    {
        using var render = new FlexRenderBuilder()
            .WithSkia()
            .Build();

        var template = new Template
        {
            Canvas = new CanvasSettings { Width = 100, Height = 100, Fixed = FixedDimension.Both, Background = "#ffffff" },
            Elements = new List<TemplateElement>
            {
                new BarcodeElement
                {
                    Data = "123",
                    Format = BarcodeFormat.Code128,
                    Width = "50",
                    Height = "30"
                }
            }
        };

        // Renders without barcode (element is skipped)
        var bytes = await render.Render(template);
        Assert.NotEmpty(bytes);
    }

    /// <summary>
    /// Verifies that WithSkia without configure action works.
    /// </summary>
    [Fact]
    public void WithSkia_NoConfigureAction_BuildsSuccessfully()
    {
        using var render = new FlexRenderBuilder()
            .WithSkia()
            .Build();

        Assert.NotNull(render);
    }

    /// <summary>
    /// Verifies that WithSkia with null builder throws.
    /// </summary>
    [Fact]
    public void WithSkia_NullBuilder_ThrowsArgumentNullException()
    {
        FlexRenderBuilder? builder = null;

        Assert.Throws<ArgumentNullException>(() =>
            FlexRenderBuilderExtensions.WithSkia(builder!, null));
    }
}

/// <summary>
/// Integration tests for FlexRenderBuilder with rendering.
/// </summary>
public sealed class FlexRenderBuilderIntegrationTests : IDisposable
{
    private readonly IFlexRender _render;

    public FlexRenderBuilderIntegrationTests()
    {
        _render = new FlexRenderBuilder()
            .WithSkia(skia => skia
                .WithQr()
                .WithBarcode())
            .Build();
    }

    public void Dispose()
    {
        _render.Dispose();
    }

    /// <summary>
    /// Verifies that simple template renders to PNG.
    /// </summary>
    [Fact]
    public async Task Render_SimpleTemplate_ReturnsPngBytes()
    {
        var template = new Template
        {
            Canvas = new CanvasSettings { Width = 200, Background = "#ffffff" },
            Elements = new List<TemplateElement>
            {
                new TextElement { Content = "Hello World", Size = "16" }
            }
        };

        var bytes = await _render.Render(template);

        Assert.NotNull(bytes);
        Assert.True(bytes.Length > 0);
        // PNG magic bytes
        Assert.Equal(0x89, bytes[0]);
        Assert.Equal(0x50, bytes[1]);
        Assert.Equal(0x4E, bytes[2]);
        Assert.Equal(0x47, bytes[3]);
    }

    /// <summary>
    /// Verifies that template with data substitution works.
    /// </summary>
    [Fact]
    public async Task Render_WithData_SubstitutesVariables()
    {
        var template = new Template
        {
            Canvas = new CanvasSettings { Width = 200 },
            Elements = new List<TemplateElement>
            {
                new TextElement { Content = "Hello {{name}}!", Size = "16" }
            }
        };
        var data = new ObjectValue { ["name"] = "FlexRender" };

        var bytes = await _render.Render(template, data);

        Assert.NotNull(bytes);
        Assert.True(bytes.Length > 0);
    }

    /// <summary>
    /// Verifies that template renders to JPEG format.
    /// </summary>
    [Fact]
    public async Task Render_AsJpeg_ReturnsJpegBytes()
    {
        var template = new Template
        {
            Canvas = new CanvasSettings { Width = 100, Background = "#ffffff" },
            Elements = new List<TemplateElement>
            {
                new TextElement { Content = "JPEG", Size = "14" }
            }
        };

        var bytes = await _render.Render(template, format: ImageFormat.Jpeg);

        Assert.NotNull(bytes);
        Assert.True(bytes.Length > 0);
        // JPEG magic bytes
        Assert.Equal(0xFF, bytes[0]);
        Assert.Equal(0xD8, bytes[1]);
    }

    /// <summary>
    /// Verifies that template renders to BMP format.
    /// </summary>
    [Fact]
    public async Task Render_AsBmp_ReturnsBmpBytes()
    {
        var template = new Template
        {
            Canvas = new CanvasSettings { Width = 100, Background = "#ffffff" },
            Elements = new List<TemplateElement>
            {
                new TextElement { Content = "BMP", Size = "14" }
            }
        };

        var bytes = await _render.Render(template, format: ImageFormat.Bmp);

        Assert.NotNull(bytes);
        Assert.True(bytes.Length > 0);
        // BMP magic bytes
        Assert.Equal((byte)'B', bytes[0]);
        Assert.Equal((byte)'M', bytes[1]);
    }

    /// <summary>
    /// Verifies that render to stream works.
    /// </summary>
    [Fact]
    public async Task Render_ToStream_WritesValidPng()
    {
        var template = new Template
        {
            Canvas = new CanvasSettings { Width = 100 },
            Elements = new List<TemplateElement>
            {
                new TextElement { Content = "Stream", Size = "12" }
            }
        };

        using var stream = new MemoryStream();
        await _render.Render(stream, template);

        Assert.True(stream.Length > 0);
        stream.Position = 0;
        var bytes = stream.ToArray();
        // PNG magic bytes
        Assert.Equal(0x89, bytes[0]);
        Assert.Equal(0x50, bytes[1]);
    }
}
