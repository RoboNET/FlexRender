using FlexRender.Parsing.Ast;
using FlexRender.Rendering;
using SkiaSharp;
using Xunit;

namespace FlexRender.Tests.Rendering;

/// <summary>
/// Tests that text baseline positioning keeps descenders within the layout box.
/// Verifies that glyphs with descenders (y, g, p) do not extend below bounds.Bottom.
/// </summary>
public sealed class TextBaselineTests : IDisposable
{
    private readonly FontManager _fontManager;
    private readonly TextRenderer _textRenderer;

    /// <summary>
    /// Initializes font manager with Inter-Regular for deterministic rendering.
    /// </summary>
    public TextBaselineTests()
    {
        _fontManager = new FontManager();

        // Register Inter font for deterministic cross-platform rendering
        var fontPath = FindFontPath();
        _fontManager.RegisterFont("main", fontPath);
        _fontManager.RegisterFont("default", fontPath);

        _textRenderer = new TextRenderer(_fontManager);
    }

    /// <inheritdoc />
    public void Dispose()
    {
        _fontManager.Dispose();
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// Verifies that text with descenders (y, g, p) does not render pixels
    /// below the measured text box boundary. The baseline of the first line
    /// must be at bounds.Top + ascent, not bounds.Top + lineHeight.
    /// </summary>
    [Fact]
    public void DrawText_Descenders_DoNotExtendBelowBounds()
    {
        // Arrange: text with descenders at a large size to make the effect visible
        var element = new TextElement
        {
            Content = "Typography",
            Size = "48",
            Color = "#000000",
            Wrap = false
        };

        const float baseFontSize = 12f;
        const float maxWidth = 800f;

        // Measure the text to determine its expected bounding box
        var measuredSize = _textRenderer.MeasureText(element, maxWidth, baseFontSize);

        // Create a bitmap taller than the measured text so we can detect overflow
        var bitmapWidth = (int)Math.Ceiling(measuredSize.Width) + 20;
        var bitmapHeight = (int)Math.Ceiling(measuredSize.Height) + 40; // extra space below
        using var bitmap = new SKBitmap(bitmapWidth, bitmapHeight, SKColorType.Rgba8888, SKAlphaType.Premul);
        using var canvas = new SKCanvas(bitmap);
        canvas.Clear(SKColors.White);

        // Define the text bounds as exactly the measured size, starting at a known offset
        const float boundsTop = 10f;
        var bounds = new SKRect(5f, boundsTop, 5f + measuredSize.Width, boundsTop + measuredSize.Height);

        // Act: draw the text
        _textRenderer.DrawText(canvas, element, bounds, baseFontSize);
        canvas.Flush();

        // Assert: no non-white pixels should exist below bounds.Bottom
        var boundsBottomPixel = (int)Math.Ceiling(bounds.Bottom);
        var hasPixelsBelowBounds = false;

        for (var y = boundsBottomPixel; y < bitmapHeight; y++)
        {
            for (var x = 0; x < bitmapWidth; x++)
            {
                var pixel = bitmap.GetPixel(x, y);
                // Check for any non-white pixel (text ink, antialiasing)
                if (pixel.Red < 250 || pixel.Green < 250 || pixel.Blue < 250)
                {
                    hasPixelsBelowBounds = true;
                    break;
                }
            }

            if (hasPixelsBelowBounds)
                break;
        }

        Assert.False(
            hasPixelsBelowBounds,
            $"Text descenders extend below bounds.Bottom ({bounds.Bottom:F1}px). " +
            $"Measured height: {measuredSize.Height:F1}px. " +
            "The baseline should be at bounds.Top + |Top|, keeping all glyph extents within the box.");
    }

    /// <summary>
    /// Verifies that two text elements stacked vertically with gap:0 do not
    /// produce visual overlap. The first text's descenders must not bleed
    /// into the second text's box.
    /// </summary>
    [Fact]
    public void DrawText_StackedTexts_NoOverlapAtBoundary()
    {
        // Arrange: two text blocks, top one with descenders
        var topElement = new TextElement
        {
            Content = "Jumping gyroscope",
            Size = "36",
            Color = "#ff0000", // Red text
            Wrap = false
        };

        var bottomElement = new TextElement
        {
            Content = "ABCDEFGHIJK",
            Size = "36",
            Color = "#0000ff", // Blue text
            Wrap = false
        };

        const float baseFontSize = 12f;
        const float maxWidth = 800f;

        var topSize = _textRenderer.MeasureText(topElement, maxWidth, baseFontSize);
        var bottomSize = _textRenderer.MeasureText(bottomElement, maxWidth, baseFontSize);

        var bitmapWidth = (int)Math.Ceiling(Math.Max(topSize.Width, bottomSize.Width)) + 20;
        var bitmapHeight = (int)Math.Ceiling(topSize.Height + bottomSize.Height) + 40;
        using var bitmap = new SKBitmap(bitmapWidth, bitmapHeight, SKColorType.Rgba8888, SKAlphaType.Premul);
        using var canvas = new SKCanvas(bitmap);
        canvas.Clear(SKColors.White);

        // Stack them vertically with no gap
        var topBounds = new SKRect(5f, 5f, 5f + topSize.Width, 5f + topSize.Height);
        var bottomBounds = new SKRect(5f, topBounds.Bottom, 5f + bottomSize.Width, topBounds.Bottom + bottomSize.Height);

        // Act
        _textRenderer.DrawText(canvas, topElement, topBounds, baseFontSize);
        _textRenderer.DrawText(canvas, bottomElement, bottomBounds, baseFontSize);
        canvas.Flush();

        // Assert: check the row of pixels at the boundary for red text bleeding down
        var boundaryY = (int)Math.Floor(topBounds.Bottom);
        var hasRedBelowBoundary = false;

        // Check a few rows below the boundary for red pixels from the top text
        for (var y = boundaryY; y < Math.Min(boundaryY + 5, bitmapHeight); y++)
        {
            for (var x = 0; x < bitmapWidth; x++)
            {
                var pixel = bitmap.GetPixel(x, y);
                // Red text bleeding: pixel has significant red but little blue
                if (pixel.Red > 100 && pixel.Blue < 50 && pixel.Alpha > 50)
                {
                    hasRedBelowBoundary = true;
                    break;
                }
            }

            if (hasRedBelowBoundary)
                break;
        }

        Assert.False(
            hasRedBelowBoundary,
            $"Red text from top element bleeds below boundary at y={boundaryY}. " +
            "Descenders from 'Jumping gyroscope' overflow into the bottom text box. " +
            "The baseline should be at bounds.Top + |Top|, keeping all glyph extents within the box.");
    }

    /// <summary>
    /// Verifies that the text is actually drawn within the top portion of the box.
    /// With correct baseline positioning using full glyph extents (|Top|),
    /// pixels should appear in the top third of the bounds.
    /// </summary>
    [Fact]
    public void DrawText_TextAppearsNearTopOfBounds()
    {
        // Arrange
        var element = new TextElement
        {
            Content = "Hello",
            Size = "48",
            Color = "#000000",
            Wrap = false
        };

        const float baseFontSize = 12f;
        const float maxWidth = 800f;

        var measuredSize = _textRenderer.MeasureText(element, maxWidth, baseFontSize);

        var bitmapWidth = (int)Math.Ceiling(measuredSize.Width) + 20;
        var bitmapHeight = (int)Math.Ceiling(measuredSize.Height) + 40;
        using var bitmap = new SKBitmap(bitmapWidth, bitmapHeight, SKColorType.Rgba8888, SKAlphaType.Premul);
        using var canvas = new SKCanvas(bitmap);
        canvas.Clear(SKColors.White);

        const float boundsTop = 10f;
        var bounds = new SKRect(5f, boundsTop, 5f + measuredSize.Width, boundsTop + measuredSize.Height);

        // Act
        _textRenderer.DrawText(canvas, element, bounds, baseFontSize);
        canvas.Flush();

        // Assert: there should be non-white pixels in the top third of the bounds.
        // The baseline is at bounds.Top + |Top| (full glyph extent above baseline),
        // which includes headroom for diacritical marks above the ascent line.
        // Ascenders start at the ascent line, so ink appears slightly below the box top.
        var topThirdBottom = (int)(boundsTop + measuredSize.Height * 0.34f);
        var hasPixelsInTopThird = false;

        for (var y = (int)boundsTop; y < topThirdBottom; y++)
        {
            for (var x = 0; x < bitmapWidth; x++)
            {
                var pixel = bitmap.GetPixel(x, y);
                if (pixel.Red < 200 || pixel.Green < 200 || pixel.Blue < 200)
                {
                    hasPixelsInTopThird = true;
                    break;
                }
            }

            if (hasPixelsInTopThird)
                break;
        }

        Assert.True(
            hasPixelsInTopThird,
            $"No text pixels found in the top third of bounds (y={boundsTop:F0} to y={topThirdBottom}). " +
            "With correct baseline positioning (bounds.Top + |Top|), " +
            "the ascenders of 'H', 'l' should appear within the top third of the box.");
    }

    /// <summary>
    /// Locates the Inter-Regular.ttf font file relative to the test assembly.
    /// </summary>
    private static string FindFontPath()
    {
        // Navigate up from the test execution directory to find the Snapshots/Fonts dir
        var currentDir = AppContext.BaseDirectory;

        while (!string.IsNullOrEmpty(currentDir))
        {
            var candidate = Path.Combine(currentDir, "Snapshots", "Fonts", "Inter-Regular.ttf");
            if (File.Exists(candidate))
                return candidate;

            var csprojFiles = Directory.GetFiles(currentDir, "*.csproj");
            if (csprojFiles.Length > 0)
            {
                // Found the project root - check Snapshots/Fonts from here
                candidate = Path.Combine(currentDir, "Snapshots", "Fonts", "Inter-Regular.ttf");
                if (File.Exists(candidate))
                    return candidate;
            }

            currentDir = Directory.GetParent(currentDir)?.FullName;
        }

        throw new FileNotFoundException(
            "Could not find Inter-Regular.ttf. Ensure it exists at tests/FlexRender.Tests/Snapshots/Fonts/Inter-Regular.ttf");
    }
}
