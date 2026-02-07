using FlexRender.Parsing.Ast;
using FlexRender.Providers;
using FlexRender.QrCode.Providers;
using SkiaSharp;
using Xunit;

namespace FlexRender.Tests.Providers;

/// <summary>
/// Tests for QrProvider.
/// </summary>
public class QrProviderTests
{
    private readonly QrProvider _provider = new();

    /// <summary>
    /// Verifies QR code generation with default settings.
    /// </summary>
    [Fact]
    public void Generate_DefaultSettings_CreatesResult()
    {
        var element = new QrElement
        {
            Data = "Hello, World!",
            Size = 100
        };

        var result = _provider.Generate(element, 100, 100);

        Assert.True(result.PngBytes.Length > 0);
        Assert.Equal(100, result.Width);
        Assert.Equal(100, result.Height);
    }

    /// <summary>
    /// Verifies QR code generation with custom size.
    /// </summary>
    [Fact]
    public void Generate_CustomSize_CreatesCorrectDimensions()
    {
        var element = new QrElement
        {
            Data = "Test",
            Size = 200
        };

        var result = _provider.Generate(element, 200, 200);

        Assert.Equal(200, result.Width);
        Assert.Equal(200, result.Height);
    }

    /// <summary>
    /// Verifies QR code uses specified foreground color via ISkiaNativeProvider.
    /// </summary>
    [Fact]
    public void GenerateBitmap_CustomForeground_UsesForegroundColor()
    {
        var element = new QrElement
        {
            Data = "Color Test",
            Size = 100,
            Foreground = "#ff0000"
        };

        ISkiaNativeProvider<QrElement> nativeProvider = _provider;
        using var bitmap = nativeProvider.GenerateBitmap(element, 100, 100);

        // Check that red pixels exist (QR modules)
        var hasRedPixels = false;
        for (var x = 0; x < bitmap.Width && !hasRedPixels; x++)
        {
            for (var y = 0; y < bitmap.Height && !hasRedPixels; y++)
            {
                var pixel = bitmap.GetPixel(x, y);
                if (pixel.Red == 255 && pixel.Green == 0 && pixel.Blue == 0)
                {
                    hasRedPixels = true;
                }
            }
        }

        Assert.True(hasRedPixels, "QR code should contain red foreground pixels");
    }

    /// <summary>
    /// Verifies QR code uses specified background color via ISkiaNativeProvider.
    /// </summary>
    [Fact]
    public void GenerateBitmap_CustomBackground_UsesBackgroundColor()
    {
        var element = new QrElement
        {
            Data = "Background Test",
            Size = 100,
            Background = "#00ff00"
        };

        ISkiaNativeProvider<QrElement> nativeProvider = _provider;
        using var bitmap = nativeProvider.GenerateBitmap(element, 100, 100);

        // Check that green pixels exist (background)
        var hasGreenPixels = false;
        for (var x = 0; x < bitmap.Width && !hasGreenPixels; x++)
        {
            for (var y = 0; y < bitmap.Height && !hasGreenPixels; y++)
            {
                var pixel = bitmap.GetPixel(x, y);
                if (pixel.Green == 255 && pixel.Red == 0 && pixel.Blue == 0)
                {
                    hasGreenPixels = true;
                }
            }
        }

        Assert.True(hasGreenPixels, "QR code should contain green background pixels");
    }

    /// <summary>
    /// Verifies all error correction levels work.
    /// </summary>
    [Theory]
    [InlineData(ErrorCorrectionLevel.L)]
    [InlineData(ErrorCorrectionLevel.M)]
    [InlineData(ErrorCorrectionLevel.Q)]
    [InlineData(ErrorCorrectionLevel.H)]
    public void Generate_AllErrorCorrectionLevels_CreatesResult(ErrorCorrectionLevel level)
    {
        var element = new QrElement
        {
            Data = "Error correction test",
            Size = 100,
            ErrorCorrection = level
        };

        var result = _provider.Generate(element, 100, 100);

        Assert.True(result.PngBytes.Length > 0);
        Assert.Equal(100, result.Width);
    }

    /// <summary>
    /// Verifies exception is thrown for null element.
    /// </summary>
    [Fact]
    public void Generate_NullElement_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => _provider.Generate(null!, 100, 100));
    }

    /// <summary>
    /// Verifies exception is thrown for empty data.
    /// </summary>
    [Fact]
    public void Generate_EmptyData_ThrowsArgumentException()
    {
        var element = new QrElement { Data = "" };

        Assert.Throws<ArgumentException>(() => _provider.Generate(element, 100, 100));
    }

    /// <summary>
    /// Verifies exception is thrown for invalid size.
    /// </summary>
    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Generate_InvalidSize_ThrowsArgumentException(int size)
    {
        var element = new QrElement
        {
            Data = "Test",
            Size = size
        };

        Assert.Throws<ArgumentException>(() => _provider.Generate(element, size, size));
    }

    /// <summary>
    /// Verifies QR code can encode URLs.
    /// </summary>
    [Fact]
    public void Generate_Url_CreatesResult()
    {
        var element = new QrElement
        {
            Data = "https://example.com/path?query=value",
            Size = 150
        };

        var result = _provider.Generate(element, 150, 150);

        Assert.True(result.PngBytes.Length > 0);
        Assert.Equal(150, result.Width);
    }

    /// <summary>
    /// Verifies QR code can encode special characters.
    /// </summary>
    [Fact]
    public void Generate_SpecialCharacters_CreatesResult()
    {
        var element = new QrElement
        {
            Data = "Special: !@#$%^&*()_+-=[]{}|;':\",./<>?",
            Size = 200
        };

        var result = _provider.Generate(element, 200, 200);

        Assert.True(result.PngBytes.Length > 0);
    }
}
