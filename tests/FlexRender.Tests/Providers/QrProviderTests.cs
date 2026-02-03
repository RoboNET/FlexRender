using FlexRender.Parsing.Ast;
using FlexRender.Providers;
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
    public void Generate_DefaultSettings_CreatesBitmap()
    {
        var element = new QrElement
        {
            Data = "Hello, World!",
            Size = 100
        };

        using var bitmap = _provider.Generate(element);

        Assert.NotNull(bitmap);
        Assert.Equal(100, bitmap.Width);
        Assert.Equal(100, bitmap.Height);
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

        using var bitmap = _provider.Generate(element);

        Assert.Equal(200, bitmap.Width);
        Assert.Equal(200, bitmap.Height);
    }

    /// <summary>
    /// Verifies QR code uses specified foreground color.
    /// </summary>
    [Fact]
    public void Generate_CustomForeground_UsesForegroundColor()
    {
        var element = new QrElement
        {
            Data = "Color Test",
            Size = 100,
            Foreground = "#ff0000"
        };

        using var bitmap = _provider.Generate(element);

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
    /// Verifies QR code uses specified background color.
    /// </summary>
    [Fact]
    public void Generate_CustomBackground_UsesBackgroundColor()
    {
        var element = new QrElement
        {
            Data = "Background Test",
            Size = 100,
            Background = "#00ff00"
        };

        using var bitmap = _provider.Generate(element);

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
    public void Generate_AllErrorCorrectionLevels_CreatesBitmap(ErrorCorrectionLevel level)
    {
        var element = new QrElement
        {
            Data = "Error correction test",
            Size = 100,
            ErrorCorrection = level
        };

        using var bitmap = _provider.Generate(element);

        Assert.NotNull(bitmap);
        Assert.Equal(100, bitmap.Width);
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
    /// Verifies exception is thrown for empty data.
    /// </summary>
    [Fact]
    public void Generate_EmptyData_ThrowsArgumentException()
    {
        var element = new QrElement { Data = "" };

        Assert.Throws<ArgumentException>(() => _provider.Generate(element));
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

        Assert.Throws<ArgumentException>(() => _provider.Generate(element));
    }

    /// <summary>
    /// Verifies QR code can encode URLs.
    /// </summary>
    [Fact]
    public void Generate_Url_CreatesBitmap()
    {
        var element = new QrElement
        {
            Data = "https://example.com/path?query=value",
            Size = 150
        };

        using var bitmap = _provider.Generate(element);

        Assert.NotNull(bitmap);
        Assert.Equal(150, bitmap.Width);
    }

    /// <summary>
    /// Verifies QR code can encode special characters.
    /// </summary>
    [Fact]
    public void Generate_SpecialCharacters_CreatesBitmap()
    {
        var element = new QrElement
        {
            Data = "Special: !@#$%^&*()_+-=[]{}|;':\",./<>?",
            Size = 200
        };

        using var bitmap = _provider.Generate(element);

        Assert.NotNull(bitmap);
    }
}
