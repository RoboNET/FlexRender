using FlexRender.Parsing.Ast;
using FlexRender.Providers;
using SkiaSharp;
using Xunit;

namespace FlexRender.Tests.Providers;

/// <summary>
/// Tests for ImageProvider.
/// </summary>
public sealed class ImageProviderTests : IDisposable
{
    private readonly ImageProvider _provider = new();
    private readonly List<string> _tempFiles = new();

    /// <summary>
    /// Creates a temporary test image file.
    /// </summary>
    /// <param name="width">Image width.</param>
    /// <param name="height">Image height.</param>
    /// <param name="color">Fill color.</param>
    /// <returns>Path to the temporary file.</returns>
    private string CreateTestImage(int width, int height, SKColor color)
    {
        var tempPath = Path.GetTempFileName();
        var pngPath = Path.ChangeExtension(tempPath, ".png");

        using var bitmap = new SKBitmap(width, height);
        using var canvas = new SKCanvas(bitmap);
        canvas.Clear(color);

        using var stream = File.OpenWrite(pngPath);
        bitmap.Encode(stream, SKEncodedImageFormat.Png, 100);

        _tempFiles.Add(tempPath);
        _tempFiles.Add(pngPath);

        return pngPath;
    }

    /// <summary>
    /// Verifies image loading from file with natural dimensions.
    /// </summary>
    [Fact]
    public void Generate_FromFile_LoadsImage()
    {
        var imagePath = CreateTestImage(100, 50, SKColors.Red);
        var element = new ImageElement { Src = imagePath };

        using var bitmap = _provider.Generate(element);

        Assert.NotNull(bitmap);
        Assert.Equal(100, bitmap.Width);
        Assert.Equal(50, bitmap.Height);
    }

    /// <summary>
    /// Verifies image loading from base64 data URL.
    /// </summary>
    [Fact]
    public void Generate_FromBase64_LoadsImage()
    {
        // A minimal 1x1 red PNG
        var dataUrl = "data:image/png;base64,iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mP8z8DwHwAFBQIAX8jx0gAAAABJRU5ErkJggg==";

        var element = new ImageElement { Src = dataUrl };

        using var bitmap = _provider.Generate(element);

        Assert.NotNull(bitmap);
        Assert.Equal(1, bitmap.Width);
        Assert.Equal(1, bitmap.Height);
    }

    /// <summary>
    /// Verifies ImageFit.Fill stretches image to target dimensions.
    /// </summary>
    [Fact]
    public void Generate_FitFill_StretchesToTargetDimensions()
    {
        var imagePath = CreateTestImage(100, 50, SKColors.Blue);
        var element = new ImageElement
        {
            Src = imagePath,
            ImageWidth = 200,
            ImageHeight = 200,
            Fit = ImageFit.Fill
        };

        using var bitmap = _provider.Generate(element);

        Assert.Equal(200, bitmap.Width);
        Assert.Equal(200, bitmap.Height);
    }

    /// <summary>
    /// Verifies ImageFit.Contain maintains aspect ratio within bounds.
    /// </summary>
    [Fact]
    public void Generate_FitContain_MaintainsAspectRatio()
    {
        var imagePath = CreateTestImage(200, 100, SKColors.Green);
        var element = new ImageElement
        {
            Src = imagePath,
            ImageWidth = 100,
            ImageHeight = 100,
            Fit = ImageFit.Contain
        };

        using var bitmap = _provider.Generate(element);

        Assert.Equal(100, bitmap.Width);
        Assert.Equal(100, bitmap.Height);
        // The image should be centered with transparent padding
    }

    /// <summary>
    /// Verifies ImageFit.Cover fills target dimensions by cropping.
    /// </summary>
    [Fact]
    public void Generate_FitCover_FillsTargetDimensions()
    {
        var imagePath = CreateTestImage(200, 100, SKColors.Yellow);
        var element = new ImageElement
        {
            Src = imagePath,
            ImageWidth = 100,
            ImageHeight = 100,
            Fit = ImageFit.Cover
        };

        using var bitmap = _provider.Generate(element);

        Assert.Equal(100, bitmap.Width);
        Assert.Equal(100, bitmap.Height);
    }

    /// <summary>
    /// Verifies ImageFit.None uses natural dimensions centered.
    /// </summary>
    [Fact]
    public void Generate_FitNone_CentersInTargetDimensions()
    {
        var imagePath = CreateTestImage(50, 50, SKColors.Purple);
        var element = new ImageElement
        {
            Src = imagePath,
            ImageWidth = 100,
            ImageHeight = 100,
            Fit = ImageFit.None
        };

        using var bitmap = _provider.Generate(element);

        Assert.Equal(100, bitmap.Width);
        Assert.Equal(100, bitmap.Height);
    }

    /// <summary>
    /// Verifies exception is thrown for null element.
    /// </summary>
    [Fact]
    public void Generate_NullElement_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => _provider.Generate(null!));
    }

    /// <summary>
    /// Verifies exception is thrown for empty source.
    /// </summary>
    [Fact]
    public void Generate_EmptySource_ThrowsArgumentException()
    {
        var element = new ImageElement { Src = "" };

        Assert.Throws<ArgumentException>(() => _provider.Generate(element));
    }

    /// <summary>
    /// Verifies exception is thrown for non-existent file.
    /// </summary>
    [Fact]
    public void Generate_NonExistentFile_ThrowsFileNotFoundException()
    {
        var element = new ImageElement { Src = "/nonexistent/path/image.png" };

        Assert.Throws<FileNotFoundException>(() => _provider.Generate(element));
    }

    /// <summary>
    /// Verifies exception is thrown for invalid base64 data.
    /// </summary>
    [Fact]
    public void Generate_InvalidBase64_ThrowsArgumentException()
    {
        var element = new ImageElement { Src = "data:image/png;base64,not-valid-base64!!!" };

        Assert.Throws<ArgumentException>(() => _provider.Generate(element));
    }

    /// <summary>
    /// Verifies exception is thrown for malformed data URL.
    /// </summary>
    [Fact]
    public void Generate_MalformedDataUrl_ThrowsArgumentException()
    {
        var element = new ImageElement { Src = "data:image/png;base64" }; // Missing comma

        Assert.Throws<ArgumentException>(() => _provider.Generate(element));
    }

    /// <summary>
    /// Verifies width-only constraint uses natural height.
    /// </summary>
    [Fact]
    public void Generate_WidthOnlyConstraint_UsesNaturalHeight()
    {
        var imagePath = CreateTestImage(100, 50, SKColors.Orange);
        var element = new ImageElement
        {
            Src = imagePath,
            ImageWidth = 200
        };

        using var bitmap = _provider.Generate(element);

        Assert.Equal(200, bitmap.Width);
        Assert.Equal(50, bitmap.Height);
    }

    /// <summary>
    /// Verifies height-only constraint uses natural width.
    /// </summary>
    [Fact]
    public void Generate_HeightOnlyConstraint_UsesNaturalWidth()
    {
        var imagePath = CreateTestImage(100, 50, SKColors.Cyan);
        var element = new ImageElement
        {
            Src = imagePath,
            ImageHeight = 100
        };

        using var bitmap = _provider.Generate(element);

        Assert.Equal(100, bitmap.Width);
        Assert.Equal(100, bitmap.Height);
    }

    /// <summary>
    /// Verifies that Generate uses cached bitmap when available.
    /// </summary>
    [Fact]
    public void Generate_WithCacheHit_UsesCachedBitmap()
    {
        using var cachedBitmap = new SKBitmap(80, 40);
        using var canvas = new SKCanvas(cachedBitmap);
        canvas.Clear(SKColors.Magenta);

        var cache = new Dictionary<string, SKBitmap> { ["http://example.com/test.png"] = cachedBitmap };

        var element = new ImageElement { Src = "http://example.com/test.png" };

        using var result = _provider.Generate(element, cache);

        Assert.NotNull(result);
        Assert.Equal(80, result.Width);
        Assert.Equal(40, result.Height);
    }

    /// <summary>
    /// Verifies that Generate falls back to file loading on cache miss.
    /// </summary>
    [Fact]
    public void Generate_WithCacheMiss_FallsBackToFileLoading()
    {
        var imagePath = CreateTestImage(60, 30, SKColors.Teal);

        var cache = new Dictionary<string, SKBitmap>();

        var element = new ImageElement { Src = imagePath };

        using var result = _provider.Generate(element, cache);

        Assert.NotNull(result);
        Assert.Equal(60, result.Width);
        Assert.Equal(30, result.Height);
    }

    /// <summary>
    /// Verifies that cached bitmap is processed with fit mode.
    /// </summary>
    [Fact]
    public void Generate_WithCacheHit_AppliesFitMode()
    {
        using var cachedBitmap = new SKBitmap(200, 100);
        using var canvas = new SKCanvas(cachedBitmap);
        canvas.Clear(SKColors.Navy);

        var cache = new Dictionary<string, SKBitmap> { ["cached://image"] = cachedBitmap };

        var element = new ImageElement
        {
            Src = "cached://image",
            ImageWidth = 50,
            ImageHeight = 50,
            Fit = ImageFit.Fill
        };

        using var result = _provider.Generate(element, cache);

        Assert.Equal(50, result.Width);
        Assert.Equal(50, result.Height);
    }

    /// <summary>
    /// Verifies that existing behavior is preserved when no cache is set.
    /// </summary>
    [Fact]
    public void Generate_WithNoCache_UsesInlineLoading()
    {
        var imagePath = CreateTestImage(70, 35, SKColors.Lime);
        var element = new ImageElement { Src = imagePath };

        // No SetImageCache call — default behavior
        using var result = _provider.Generate(element);

        Assert.NotNull(result);
        Assert.Equal(70, result.Width);
        Assert.Equal(35, result.Height);
    }

    /// <summary>
    /// Cleanup temporary files.
    /// </summary>
    public void Dispose()
    {
        foreach (var file in _tempFiles)
        {
            try
            {
                if (File.Exists(file))
                {
                    File.Delete(file);
                }
            }
            catch
            {
                // Ignore cleanup errors
            }
        }
    }
}
