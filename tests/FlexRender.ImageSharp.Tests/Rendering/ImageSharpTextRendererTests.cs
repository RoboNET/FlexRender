using FlexRender.ImageSharp.Rendering;
using FlexRender.Parsing.Ast;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using Xunit;

namespace FlexRender.ImageSharp.Tests.Rendering;

public sealed class ImageSharpTextRendererTests : IDisposable
{
    private readonly ImageSharpFontManager _fontManager;
    private readonly ImageSharpTextRenderer _textRenderer;

    public ImageSharpTextRendererTests()
    {
        _fontManager = new ImageSharpFontManager();
        var assemblyDir = Path.GetDirectoryName(typeof(ImageSharpTextRendererTests).Assembly.Location)!;
        var fontPath = Path.Combine(assemblyDir, "Fonts", "Inter-Regular.ttf");
        _fontManager.RegisterFont("default", fontPath);
        _fontManager.RegisterFont("main", fontPath);
        _textRenderer = new ImageSharpTextRenderer(_fontManager);
    }

    [Fact]
    public void MeasureText_SimpleText_ReturnsPositiveSize()
    {
        var element = new TextElement { Content = "Hello, World!", Size = "16" };
        var size = _textRenderer.MeasureText(element, 500f, 12f);
        Assert.True(size.Width > 0);
        Assert.True(size.Height > 0);
    }

    [Fact]
    public void MeasureText_EmptyContent_ReturnsZeroSize()
    {
        var element = new TextElement { Content = "" };
        var size = _textRenderer.MeasureText(element, 500f, 12f);
        Assert.Equal(0f, size.Width);
        Assert.Equal(0f, size.Height);
    }

    [Fact]
    public void MeasureText_NullContent_ReturnsZeroSize()
    {
        var element = new TextElement { Content = null! };
        var size = _textRenderer.MeasureText(element, 500f, 12f);
        Assert.Equal(0f, size.Width);
        Assert.Equal(0f, size.Height);
    }

    [Fact]
    public void MeasureText_LargerFont_ReturnsLargerSize()
    {
        var small = new TextElement { Content = "Test", Size = "12" };
        var large = new TextElement { Content = "Test", Size = "24" };

        var smallSize = _textRenderer.MeasureText(small, 500f, 12f);
        var largeSize = _textRenderer.MeasureText(large, 500f, 12f);

        Assert.True(largeSize.Width > smallSize.Width);
        Assert.True(largeSize.Height > smallSize.Height);
    }

    [Fact]
    public void MeasureText_WrappedText_HeightIncreasesWithWrapping()
    {
        var element = new TextElement
        {
            Content = "This is a long sentence that should wrap to multiple lines when constrained",
            Wrap = true,
            Size = "14"
        };

        var wideSize = _textRenderer.MeasureText(element, 1000f, 12f);
        var narrowSize = _textRenderer.MeasureText(element, 100f, 12f);

        Assert.True(narrowSize.Height > wideSize.Height);
    }

    [Fact]
    public void DrawText_DoesNotThrow()
    {
        var element = new TextElement { Content = "Hello", Size = "16", Color = "#ff0000" };
        using var image = new Image<Rgba32>(200, 50, SixLabors.ImageSharp.Color.White);

        image.Mutate(ctx =>
        {
            _textRenderer.DrawText(ctx, element, 0, 0, 200, 50, 12f);
        });

        // If we got here without throwing, the test passes
    }

    [Fact]
    public void DrawText_TextActuallyDrawn_PixelsChange()
    {
        var element = new TextElement { Content = "Hello", Size = "16", Color = "#ff0000" };
        using var image = new Image<Rgba32>(200, 50, SixLabors.ImageSharp.Color.White);

        image.Mutate(ctx =>
        {
            _textRenderer.DrawText(ctx, element, 0, 0, 200, 50, 12f);
        });

        // Check that at least some pixels are not white (text was drawn)
        var hasNonWhitePixel = false;
        image.ProcessPixelRows(accessor =>
        {
            for (var y = 0; y < accessor.Height; y++)
            {
                var row = accessor.GetRowSpan(y);
                for (var x = 0; x < row.Length; x++)
                {
                    var pixel = row[x];
                    if (pixel.R < 255 || pixel.G < 255 || pixel.B < 255)
                    {
                        hasNonWhitePixel = true;
                        return;
                    }
                }
                if (hasNonWhitePixel) return;
            }
        });

        Assert.True(hasNonWhitePixel, "Expected text to be drawn on the image");
    }

    [Fact]
    public void DrawText_WithRotationProperty_StillDrawsText()
    {
        // TextRenderer draws text without applying rotation -- rotation is handled by the engine.
        // Verifies that the presence of a Rotate value does not prevent text from being drawn.
        var element = new TextElement
        {
            Content = "Rotated Right",
            Size = "16",
            Color = "#ff0000",
            Rotate = "right"
        };
        using var image = new Image<Rgba32>(200, 200, SixLabors.ImageSharp.Color.White);

        image.Mutate(ctx =>
        {
            _textRenderer.DrawText(ctx, element, 50, 50, 100, 100, 12f);
        });

        var hasNonWhitePixel = false;
        image.ProcessPixelRows(accessor =>
        {
            for (var y = 0; y < accessor.Height; y++)
            {
                var row = accessor.GetRowSpan(y);
                for (var x = 0; x < row.Length; x++)
                {
                    var pixel = row[x];
                    if (pixel.R < 255 || pixel.G < 255 || pixel.B < 255)
                    {
                        hasNonWhitePixel = true;
                        return;
                    }
                }
                if (hasNonWhitePixel) return;
            }
        });

        Assert.True(hasNonWhitePixel, "Expected text to be drawn even when Rotate property is set");
    }

    [Fact]
    public void DrawText_WithEmptyContent_AndRotation_DoesNotThrow()
    {
        var element = new TextElement
        {
            Content = "",
            Size = "16",
            Color = "#000000",
            Rotate = "right"
        };
        using var image = new Image<Rgba32>(200, 200, SixLabors.ImageSharp.Color.White);

        image.Mutate(ctx =>
        {
            _textRenderer.DrawText(ctx, element, 50, 50, 100, 100, 12f);
        });
    }

    public void Dispose()
    {
        _fontManager.Dispose();
    }
}
