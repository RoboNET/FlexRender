using FlexRender.Parsing.Ast;
using FlexRender.Rendering;
using SkiaSharp;
using Xunit;

namespace FlexRender.Tests.Rendering;

/// <summary>
/// Tests for canvas rotation functionality in SkiaRenderer.
/// </summary>
public class SkiaRendererRotationTests : IDisposable
{
    private readonly SkiaRenderer _renderer = new();

    public void Dispose()
    {
        _renderer.Dispose();
    }

    [Fact]
    public void Render_WithRotateRight_SwapsDimensions()
    {
        // Arrange: 300x100 canvas should become 100x300 after 90 degree rotation
        var template = new Template
        {
            Canvas = new CanvasSettings
            {
                Fixed = FixedDimension.Width,
                Width = 300,
                Background = "#ffffff",
                Rotate = "right"
            },
            Elements = new List<TemplateElement>
            {
                new FlexElement
                {
                    Width = "300",
                    Height = "100",
                    Background = "#ff0000",
                    Children = new List<TemplateElement>()
                }
            }
        };
        var data = new ObjectValue();

        // Act
        var size = _renderer.Measure(template, data);
        using var bitmap = new SKBitmap((int)size.Width, (int)size.Height);
        _renderer.Render(bitmap, template, data);

        // Assert: dimensions should be swapped
        Assert.Equal(100, size.Width);
        Assert.Equal(300, size.Height);
        Assert.Equal(100, bitmap.Width);
        Assert.Equal(300, bitmap.Height);
    }

    [Fact]
    public void Render_WithRotateLeft_SwapsDimensions()
    {
        // Arrange: 300x100 canvas should become 100x300 after 270 degree rotation
        var template = new Template
        {
            Canvas = new CanvasSettings
            {
                Fixed = FixedDimension.Width,
                Width = 300,
                Background = "#ffffff",
                Rotate = "left"
            },
            Elements = new List<TemplateElement>
            {
                new FlexElement
                {
                    Width = "300",
                    Height = "100",
                    Background = "#0000ff",
                    Children = new List<TemplateElement>()
                }
            }
        };
        var data = new ObjectValue();

        // Act
        var size = _renderer.Measure(template, data);
        using var bitmap = new SKBitmap((int)size.Width, (int)size.Height);
        _renderer.Render(bitmap, template, data);

        // Assert: dimensions should be swapped
        Assert.Equal(100, size.Width);
        Assert.Equal(300, size.Height);
        Assert.Equal(100, bitmap.Width);
        Assert.Equal(300, bitmap.Height);
    }

    [Fact]
    public void Render_WithRotateFlip_KeepsDimensions()
    {
        // Arrange: 300x100 canvas should remain 300x100 after 180 degree rotation
        var template = new Template
        {
            Canvas = new CanvasSettings
            {
                Fixed = FixedDimension.Width,
                Width = 300,
                Background = "#ffffff",
                Rotate = "flip"
            },
            Elements = new List<TemplateElement>
            {
                new FlexElement
                {
                    Width = "300",
                    Height = "100",
                    Background = "#00ff00",
                    Children = new List<TemplateElement>()
                }
            }
        };
        var data = new ObjectValue();

        // Act
        var size = _renderer.Measure(template, data);
        using var bitmap = new SKBitmap((int)size.Width, (int)size.Height);
        _renderer.Render(bitmap, template, data);

        // Assert: dimensions should NOT be swapped
        Assert.Equal(300, size.Width);
        Assert.Equal(100, size.Height);
        Assert.Equal(300, bitmap.Width);
        Assert.Equal(100, bitmap.Height);
    }

    [Fact]
    public void Render_WithRotateNone_DoesNothing()
    {
        // Arrange: 300x100 canvas should remain 300x100 with no rotation
        var template = new Template
        {
            Canvas = new CanvasSettings
            {
                Fixed = FixedDimension.Width,
                Width = 300,
                Background = "#ffffff",
                Rotate = "none"
            },
            Elements = new List<TemplateElement>
            {
                new FlexElement
                {
                    Width = "300",
                    Height = "100",
                    Background = "#ff00ff",
                    Children = new List<TemplateElement>()
                }
            }
        };
        var data = new ObjectValue();

        // Act
        var size = _renderer.Measure(template, data);
        using var bitmap = new SKBitmap((int)size.Width, (int)size.Height);
        _renderer.Render(bitmap, template, data);

        // Assert: dimensions should NOT be swapped
        Assert.Equal(300, size.Width);
        Assert.Equal(100, size.Height);
        Assert.Equal(300, bitmap.Width);
        Assert.Equal(100, bitmap.Height);
    }

    [Fact]
    public void Measure_WithRotate90_ReturnsSwappedDimensions()
    {
        // Arrange: receipt template 630px wide, content height ~200px
        var template = new Template
        {
            Canvas = new CanvasSettings
            {
                Fixed = FixedDimension.Width,
                Width = 630,
                Background = "#ffffff",
                Rotate = "90"  // numeric value
            },
            Elements = new List<TemplateElement>
            {
                new FlexElement
                {
                    Width = "630",
                    Height = "200",
                    Children = new List<TemplateElement>()
                }
            }
        };
        var data = new ObjectValue();

        // Act
        var size = _renderer.Measure(template, data);

        // Assert: width and height should be swapped
        Assert.Equal(200, size.Width);
        Assert.Equal(630, size.Height);
    }

    [Fact]
    public void Render_WithRotateRight_RotatesContentCorrectly()
    {
        // Arrange: Place a red element at top-left, after 90 CW rotation it should be at top-right
        var template = new Template
        {
            Canvas = new CanvasSettings
            {
                Fixed = FixedDimension.Width,
                Width = 100,
                Background = "#ffffff",
                Rotate = "right"
            },
            Elements = new List<TemplateElement>
            {
                // Red square at top-left (0,0) - 20x20
                new FlexElement
                {
                    Width = "20",
                    Height = "20",
                    Background = "#ff0000",
                    Children = new List<TemplateElement>()
                },
                // Blue square below - to make height 40
                new FlexElement
                {
                    Width = "100",
                    Height = "20",
                    Background = "#0000ff",
                    Children = new List<TemplateElement>()
                }
            }
        };
        var data = new ObjectValue();

        // Act
        var size = _renderer.Measure(template, data);
        using var bitmap = new SKBitmap((int)size.Width, (int)size.Height);
        _renderer.Render(bitmap, template, data);

        // Assert: After 90 CW rotation, original top-left becomes top-right
        // The red element was at (0-20, 0-20) in original 100x40
        // After 90 CW rotation to 40x100:
        // - Top-left of original -> Top-right of rotated
        // - The red pixel at (10, 10) original should be at around (30, 10) in rotated
        Assert.Equal(40, size.Width);
        Assert.Equal(100, size.Height);
    }

    [Fact]
    public void Render_WithRotateLeft_RotatesContentCorrectly()
    {
        // Arrange: Similar setup for 270 degree (left) rotation
        var template = new Template
        {
            Canvas = new CanvasSettings
            {
                Fixed = FixedDimension.Width,
                Width = 100,
                Background = "#ffffff",
                Rotate = "left"
            },
            Elements = new List<TemplateElement>
            {
                new FlexElement
                {
                    Width = "20",
                    Height = "20",
                    Background = "#ff0000",
                    Children = new List<TemplateElement>()
                },
                new FlexElement
                {
                    Width = "100",
                    Height = "20",
                    Background = "#0000ff",
                    Children = new List<TemplateElement>()
                }
            }
        };
        var data = new ObjectValue();

        // Act
        var size = _renderer.Measure(template, data);
        using var bitmap = new SKBitmap((int)size.Width, (int)size.Height);
        _renderer.Render(bitmap, template, data);

        // Assert: After 270 CCW rotation
        Assert.Equal(40, size.Width);
        Assert.Equal(100, size.Height);
    }

    [Fact]
    public void Render_ThermalPrinterScenario_RotatesReceiptCorrectly()
    {
        // Arrange: Thermal printer receipt scenario
        // Original: 630px wide, ~300px tall receipt
        // After rotate: right, should be 300px wide, 630px tall
        var template = new Template
        {
            Canvas = new CanvasSettings
            {
                Fixed = FixedDimension.Width,
                Width = 630,
                Background = "#ffffff",
                Rotate = "right"
            },
            Elements = new List<TemplateElement>
            {
                new TextElement
                {
                    Content = "Receipt Header",
                    Size = "24",
                    Width = "630",
                    Height = "50"
                },
                new FlexElement
                {
                    Width = "630",
                    Height = "200",
                    Background = "#eeeeee",
                    Children = new List<TemplateElement>
                    {
                        new TextElement { Content = "Item 1", Size = "16" },
                        new TextElement { Content = "Item 2", Size = "16" }
                    }
                },
                new TextElement
                {
                    Content = "Total: $99.99",
                    Size = "20",
                    Width = "630",
                    Height = "50"
                }
            }
        };
        var data = new ObjectValue();

        // Act
        var size = _renderer.Measure(template, data);
        using var bitmap = new SKBitmap((int)size.Width, (int)size.Height);
        _renderer.Render(bitmap, template, data);

        // Assert: Width and height should be swapped for thermal printer
        Assert.Equal(300, size.Width);
        Assert.Equal(630, size.Height);
        Assert.Equal(300, bitmap.Width);
        Assert.Equal(630, bitmap.Height);
    }

    [Fact]
    public void Render_WithDefaultRotation_DoesNotRotate()
    {
        // Arrange: Default CanvasSettings has Rotate = "none"
        var template = new Template
        {
            Canvas = new CanvasSettings
            {
                Fixed = FixedDimension.Width,
                Width = 200
                // Rotate defaults to "none"
            },
            Elements = new List<TemplateElement>
            {
                new FlexElement
                {
                    Width = "200",
                    Height = "100",
                    Children = new List<TemplateElement>()
                }
            }
        };
        var data = new ObjectValue();

        // Act
        var size = _renderer.Measure(template, data);

        // Assert: No dimension swap
        Assert.Equal(200, size.Width);
        Assert.Equal(100, size.Height);
    }

    [Theory]
    [InlineData("right")]
    [InlineData("left")]
    [InlineData("flip")]
    [InlineData("none")]
    [InlineData("90")]
    [InlineData("180")]
    [InlineData("270")]
    public void Render_VariousRotationValues_DoesNotThrow(string rotateValue)
    {
        // Arrange
        var template = new Template
        {
            Canvas = new CanvasSettings
            {
                Fixed = FixedDimension.Width,
                Width = 100,
                Background = "#ffffff",
                Rotate = rotateValue
            },
            Elements = new List<TemplateElement>
            {
                new TextElement { Content = "Test", Size = "16" }
            }
        };
        var data = new ObjectValue();

        // Act & Assert: Should not throw
        var exception = Record.Exception(() =>
        {
            var size = _renderer.Measure(template, data);
            using var bitmap = new SKBitmap((int)Math.Max(size.Width, 1), (int)Math.Max(size.Height, 1));
            _renderer.Render(bitmap, template, data);
        });

        Assert.Null(exception);
    }

    [Fact]
    public async Task RenderAsync_WithRotation_AppliesRotation()
    {
        // Arrange
        var template = new Template
        {
            Canvas = new CanvasSettings
            {
                Fixed = FixedDimension.Width,
                Width = 200,
                Background = "#ffffff",
                Rotate = "right"
            },
            Elements = new List<TemplateElement>
            {
                new FlexElement
                {
                    Width = "200",
                    Height = "100",
                    Background = "#ff0000",
                    Children = new List<TemplateElement>()
                }
            }
        };
        var data = new ObjectValue();

        // Act
        using var bitmap = await _renderer.Render(template, data);

        // Assert: dimensions should be swapped
        Assert.Equal(100, bitmap.Width);
        Assert.Equal(200, bitmap.Height);
    }

    [Fact]
    public async Task RenderToPng_WithRotation_AppliesRotation()
    {
        // Arrange
        var template = new Template
        {
            Canvas = new CanvasSettings
            {
                Fixed = FixedDimension.Width,
                Width = 200,
                Background = "#ffffff",
                Rotate = "right"
            },
            Elements = new List<TemplateElement>
            {
                new FlexElement
                {
                    Width = "200",
                    Height = "100",
                    Background = "#ff0000",
                    Children = new List<TemplateElement>()
                }
            }
        };
        var data = new ObjectValue();

        // Act
        using var stream = new MemoryStream();
        await _renderer.RenderToPng(stream, template, data);

        // Assert: Decode PNG and verify dimensions
        stream.Position = 0;
        using var decodedBitmap = SKBitmap.Decode(stream);
        Assert.Equal(100, decodedBitmap.Width);
        Assert.Equal(200, decodedBitmap.Height);
    }

    [Fact]
    public async Task RenderToJpeg_WithRotation_AppliesRotation()
    {
        // Arrange
        var template = new Template
        {
            Canvas = new CanvasSettings
            {
                Fixed = FixedDimension.Width,
                Width = 200,
                Background = "#ffffff",
                Rotate = "right"
            },
            Elements = new List<TemplateElement>
            {
                new FlexElement
                {
                    Width = "200",
                    Height = "100",
                    Background = "#ff0000",
                    Children = new List<TemplateElement>()
                }
            }
        };
        var data = new ObjectValue();

        // Act
        using var stream = new MemoryStream();
        await _renderer.RenderToJpeg(stream, template, data);

        // Assert: Decode JPEG and verify dimensions
        stream.Position = 0;
        using var decodedBitmap = SKBitmap.Decode(stream);
        Assert.Equal(100, decodedBitmap.Width);
        Assert.Equal(200, decodedBitmap.Height);
    }
}
