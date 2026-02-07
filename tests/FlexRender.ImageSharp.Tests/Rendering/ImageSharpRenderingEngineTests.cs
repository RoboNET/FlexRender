using FlexRender.Configuration;
using FlexRender.ImageSharp.Rendering;
using FlexRender.Layout;
using FlexRender.Parsing.Ast;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using Xunit;

namespace FlexRender.ImageSharp.Tests.Rendering;

public sealed class ImageSharpRenderingEngineTests : IDisposable
{
    private readonly ImageSharpFontManager _fontManager;
    private readonly ImageSharpRenderingEngine _engine;

    public ImageSharpRenderingEngineTests()
    {
        _fontManager = new ImageSharpFontManager();
        var assemblyDir = Path.GetDirectoryName(typeof(ImageSharpRenderingEngineTests).Assembly.Location)!;
        var fontPath = Path.Combine(assemblyDir, "Fonts", "Inter-Regular.ttf");
        _fontManager.RegisterFont("default", fontPath);
        _fontManager.RegisterFont("main", fontPath);
        var textRenderer = new ImageSharpTextRenderer(_fontManager);
        _engine = new ImageSharpRenderingEngine(
            textRenderer,
            _fontManager,
            new ResourceLimits(),
            baseFontSize: 12f);
    }

    [Fact]
    public void RenderToImage_SimpleBackground_ProducesColoredImage()
    {
        var template = new Template
        {
            Canvas = new CanvasSettings { Width = 100, Height = 50, Fixed = FixedDimension.Both, Background = "#ff0000" }
        };

        using var image = _engine.RenderToImage(template, new ObjectValue());

        Assert.Equal(100, image.Width);
        Assert.Equal(50, image.Height);

        // Check top-left pixel is red
        var pixel = image[0, 0];
        Assert.Equal(255, pixel.R);
        Assert.Equal(0, pixel.G);
        Assert.Equal(0, pixel.B);
    }

    [Fact]
    public void RenderToImage_TextElement_DrawsText()
    {
        var template = new Template
        {
            Canvas = new CanvasSettings { Width = 200, Height = 50, Fixed = FixedDimension.Both, Background = "#ffffff" }
        };
        template.AddElement(new TextElement { Content = "Hello", Size = "16", Color = "#000000" });

        using var image = _engine.RenderToImage(template, new ObjectValue());

        // Verify some pixels are non-white (text was drawn)
        var hasNonWhitePixel = false;
        image.ProcessPixelRows(accessor =>
        {
            for (var y = 0; y < accessor.Height; y++)
            {
                var row = accessor.GetRowSpan(y);
                for (var x = 0; x < row.Length; x++)
                {
                    if (row[x].R < 250 || row[x].G < 250 || row[x].B < 250)
                    {
                        hasNonWhitePixel = true;
                        return;
                    }
                }
                if (hasNonWhitePixel) return;
            }
        });

        Assert.True(hasNonWhitePixel, "Expected text to be drawn");
    }

    [Fact]
    public void RenderToImage_SeparatorElement_DrawsLine()
    {
        var template = new Template
        {
            Canvas = new CanvasSettings { Width = 200, Height = 20, Fixed = FixedDimension.Both, Background = "#ffffff" }
        };
        template.AddElement(new SeparatorElement { Color = "#000000", Thickness = 2 });

        using var image = _engine.RenderToImage(template, new ObjectValue());

        // Verify some pixels are non-white (separator was drawn)
        var hasNonWhitePixel = false;
        image.ProcessPixelRows(accessor =>
        {
            for (var y = 0; y < accessor.Height; y++)
            {
                var row = accessor.GetRowSpan(y);
                for (var x = 0; x < row.Length; x++)
                {
                    if (row[x].R < 250)
                    {
                        hasNonWhitePixel = true;
                        return;
                    }
                }
                if (hasNonWhitePixel) return;
            }
        });

        Assert.True(hasNonWhitePixel, "Expected separator to be drawn");
    }

    [Fact]
    public void RenderToImage_NestedFlex_ProducesImage()
    {
        var template = new Template
        {
            Canvas = new CanvasSettings { Width = 300, Height = 100, Fixed = FixedDimension.Both, Background = "#ffffff" }
        };
        var flex = new FlexElement { Background = "#0000ff" };
        flex.AddChild(new TextElement { Content = "Inside flex", Color = "#ffffff", Size = "14" });
        template.AddElement(flex);

        using var image = _engine.RenderToImage(template, new ObjectValue());

        Assert.Equal(300, image.Width);
        Assert.Equal(100, image.Height);
    }

    [Fact]
    public void RenderToImage_DisplayNone_SkipsElement()
    {
        var template = new Template
        {
            Canvas = new CanvasSettings { Width = 100, Height = 50, Fixed = FixedDimension.Both, Background = "#ffffff" }
        };
        template.AddElement(new TextElement
        {
            Content = "Hidden",
            Color = "#ff0000",
            Display = Display.None
        });

        using var image = _engine.RenderToImage(template, new ObjectValue());

        // All pixels should be white since the element is hidden
        var allWhite = true;
        image.ProcessPixelRows(accessor =>
        {
            for (var y = 0; y < accessor.Height; y++)
            {
                var row = accessor.GetRowSpan(y);
                for (var x = 0; x < row.Length; x++)
                {
                    if (row[x].R < 254 || row[x].G < 254 || row[x].B < 254)
                    {
                        allWhite = false;
                        return;
                    }
                }
                if (!allWhite) return;
            }
        });

        Assert.True(allWhite, "Hidden element should not produce visible output");
    }

    [Fact]
    public void RenderToImage_TextWithRotation_DrawsRotatedText()
    {
        // Use a centered flex container so the rotated text stays within the canvas bounds
        var template = new Template
        {
            Canvas = new CanvasSettings { Width = 200, Height = 200, Fixed = FixedDimension.Both, Background = "#ffffff" }
        };
        var flex = new FlexElement
        {
            Width = "200",
            Height = "200",
            Justify = JustifyContent.Center,
            Align = AlignItems.Center
        };
        flex.AddChild(new TextElement
        {
            Content = "Rotated",
            Size = "20",
            Color = "#000000",
            Rotate = "right"
        });
        template.AddElement(flex);

        using var image = _engine.RenderToImage(template, new ObjectValue());

        Assert.True(HasNonWhitePixel(image), "Expected rotated text to be drawn");
    }

    [Fact]
    public void RenderToImage_TextWithFlipRotation_DrawsText()
    {
        var template = new Template
        {
            Canvas = new CanvasSettings { Width = 200, Height = 200, Fixed = FixedDimension.Both, Background = "#ffffff" }
        };
        var flex = new FlexElement
        {
            Width = "200",
            Height = "200",
            Justify = JustifyContent.Center,
            Align = AlignItems.Center
        };
        flex.AddChild(new TextElement
        {
            Content = "Flipped",
            Size = "16",
            Color = "#000000",
            Rotate = "flip"
        });
        template.AddElement(flex);

        using var image = _engine.RenderToImage(template, new ObjectValue());

        Assert.True(HasNonWhitePixel(image), "Expected flipped text to be drawn");
    }

    [Fact]
    public void RenderToImage_TextWithNumericRotation_DrawsText()
    {
        var template = new Template
        {
            Canvas = new CanvasSettings { Width = 300, Height = 300, Fixed = FixedDimension.Both, Background = "#ffffff" }
        };
        var flex = new FlexElement
        {
            Width = "300",
            Height = "300",
            Justify = JustifyContent.Center,
            Align = AlignItems.Center
        };
        flex.AddChild(new TextElement
        {
            Content = "Angled",
            Size = "16",
            Color = "#000000",
            Rotate = "45"
        });
        template.AddElement(flex);

        using var image = _engine.RenderToImage(template, new ObjectValue());

        Assert.True(HasNonWhitePixel(image), "Expected angled text to be drawn");
    }

    [Fact]
    public void RenderToImage_TextWithNoneRotation_DrawsNormally()
    {
        // "none" rotation should behave identically to no rotation
        var template = new Template
        {
            Canvas = new CanvasSettings { Width = 200, Height = 50, Fixed = FixedDimension.Both, Background = "#ffffff" }
        };
        template.AddElement(new TextElement
        {
            Content = "Normal",
            Size = "16",
            Color = "#000000",
            Rotate = "none"
        });

        using var image = _engine.RenderToImage(template, new ObjectValue());

        Assert.True(HasNonWhitePixel(image), "Expected text to be drawn normally with 'none' rotation");
    }

    [Fact]
    public void RenderToImage_SeparatorWithRotation_DrawsRotated()
    {
        var template = new Template
        {
            Canvas = new CanvasSettings { Width = 200, Height = 200, Fixed = FixedDimension.Both, Background = "#ffffff" }
        };
        template.AddElement(new SeparatorElement
        {
            Color = "#000000",
            Thickness = 2,
            Rotate = "45"
        });

        using var image = _engine.RenderToImage(template, new ObjectValue());

        Assert.True(HasNonWhitePixel(image), "Expected rotated separator to be drawn");
    }

    [Fact]
    public void RenderToImage_TextWithNegativeRotation_DrawsText()
    {
        var template = new Template
        {
            Canvas = new CanvasSettings { Width = 300, Height = 300, Fixed = FixedDimension.Both, Background = "#ffffff" }
        };
        var flex = new FlexElement
        {
            Width = "300",
            Height = "300",
            Justify = JustifyContent.Center,
            Align = AlignItems.Center
        };
        flex.AddChild(new TextElement
        {
            Content = "Negative",
            Size = "16",
            Color = "#000000",
            Rotate = "-30"
        });
        template.AddElement(flex);

        using var image = _engine.RenderToImage(template, new ObjectValue());

        Assert.True(HasNonWhitePixel(image), "Expected text with negative rotation to be drawn");
    }

    private static bool HasNonWhitePixel(Image<Rgba32> image)
    {
        var found = false;
        image.ProcessPixelRows(accessor =>
        {
            for (var y = 0; y < accessor.Height; y++)
            {
                var row = accessor.GetRowSpan(y);
                for (var x = 0; x < row.Length; x++)
                {
                    if (row[x].R < 250 || row[x].G < 250 || row[x].B < 250)
                    {
                        found = true;
                        return;
                    }
                }
                if (found) return;
            }
        });
        return found;
    }

    public void Dispose()
    {
        _fontManager.Dispose();
    }
}
