using FlexRender.Barcode.Providers;
using FlexRender.Parsing.Ast;
using FlexRender.Providers;
using SkiaSharp;
using Xunit;

namespace FlexRender.Tests.Providers;

/// <summary>
/// Tests for BarcodeProvider.
/// </summary>
public class BarcodeProviderTests
{
    private readonly BarcodeProvider _provider = new();

    /// <summary>
    /// Verifies barcode generation with default settings.
    /// </summary>
    [Fact]
    public void Generate_Code128_DefaultSettings_CreatesResult()
    {
        var element = new BarcodeElement
        {
            Data = "ABC123",
            Format = BarcodeFormat.Code128
        };

        var result = _provider.Generate(element, 200, 80);

        Assert.True(result.PngBytes.Length > 0);
        Assert.Equal(200, result.Width);
        Assert.Equal(80, result.Height);
    }

    /// <summary>
    /// Verifies barcode generation with custom dimensions.
    /// </summary>
    [Fact]
    public void Generate_CustomDimensions_CreatesCorrectSize()
    {
        var element = new BarcodeElement
        {
            Data = "TEST",
            Format = BarcodeFormat.Code128,
            BarcodeWidth = 300,
            BarcodeHeight = 100
        };

        var result = _provider.Generate(element, 300, 100);

        Assert.Equal(300, result.Width);
        Assert.Equal(100, result.Height);
    }

    /// <summary>
    /// Verifies barcode uses specified foreground color via ISkiaNativeProvider.
    /// </summary>
    [Fact]
    public void GenerateBitmap_CustomForeground_UsesForegroundColor()
    {
        var element = new BarcodeElement
        {
            Data = "COLOR",
            Format = BarcodeFormat.Code128,
            Foreground = "#0000ff"
        };

        ISkiaNativeProvider<BarcodeElement> nativeProvider = _provider;
        using var bitmap = nativeProvider.GenerateBitmap(element, 200, 80);

        // Check that blue pixels exist (bars)
        var hasBluePixels = false;
        for (var x = 0; x < bitmap.Width && !hasBluePixels; x++)
        {
            for (var y = 0; y < bitmap.Height && !hasBluePixels; y++)
            {
                var pixel = bitmap.GetPixel(x, y);
                if (pixel.Blue == 255 && pixel.Red == 0 && pixel.Green == 0)
                {
                    hasBluePixels = true;
                }
            }
        }

        Assert.True(hasBluePixels, "Barcode should contain blue foreground pixels");
    }

    /// <summary>
    /// Verifies barcode uses specified background color via ISkiaNativeProvider.
    /// </summary>
    [Fact]
    public void GenerateBitmap_CustomBackground_UsesBackgroundColor()
    {
        var element = new BarcodeElement
        {
            Data = "BG",
            Format = BarcodeFormat.Code128,
            Background = "#ffff00"
        };

        ISkiaNativeProvider<BarcodeElement> nativeProvider = _provider;
        using var bitmap = nativeProvider.GenerateBitmap(element, 200, 80);

        // Check that yellow pixels exist (background)
        var hasYellowPixels = false;
        for (var x = 0; x < bitmap.Width && !hasYellowPixels; x++)
        {
            for (var y = 0; y < bitmap.Height && !hasYellowPixels; y++)
            {
                var pixel = bitmap.GetPixel(x, y);
                if (pixel.Red == 255 && pixel.Green == 255 && pixel.Blue == 0)
                {
                    hasYellowPixels = true;
                }
            }
        }

        Assert.True(hasYellowPixels, "Barcode should contain yellow background pixels");
    }

    /// <summary>
    /// Verifies barcode generation without text via ISkiaNativeProvider.
    /// </summary>
    [Fact]
    public void GenerateBitmap_ShowTextFalse_CreatesShorterBars()
    {
        var element = new BarcodeElement
        {
            Data = "NOTEXT",
            Format = BarcodeFormat.Code128,
            ShowText = false,
            BarcodeHeight = 80
        };

        ISkiaNativeProvider<BarcodeElement> nativeProvider = _provider;
        using var bitmap = nativeProvider.GenerateBitmap(element, 200, 80);

        // The bars should extend to the full height when text is hidden
        // Check for foreground pixels near the bottom
        var hasBarsAtBottom = false;
        for (var x = 0; x < bitmap.Width && !hasBarsAtBottom; x++)
        {
            var pixel = bitmap.GetPixel(x, bitmap.Height - 5);
            if (pixel.Red == 0 && pixel.Green == 0 && pixel.Blue == 0)
            {
                hasBarsAtBottom = true;
            }
        }

        Assert.True(hasBarsAtBottom, "Bars should extend to full height when text is hidden");
    }

    /// <summary>
    /// Verifies Code128 supports alphanumeric characters.
    /// </summary>
    [Theory]
    [InlineData("ABC123")]
    [InlineData("Hello World")]
    [InlineData("test-data")]
    [InlineData("UPPER_lower")]
    public void Generate_Code128_AlphanumericData_CreatesResult(string data)
    {
        var element = new BarcodeElement
        {
            Data = data,
            Format = BarcodeFormat.Code128
        };

        var result = _provider.Generate(element, 200, 80);

        Assert.True(result.PngBytes.Length > 0);
    }

    /// <summary>
    /// Verifies exception is thrown for null element.
    /// </summary>
    [Fact]
    public void Generate_NullElement_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => _provider.Generate(null!, 200, 80));
    }

    /// <summary>
    /// Verifies exception is thrown for empty data.
    /// </summary>
    [Fact]
    public void Generate_EmptyData_ThrowsArgumentException()
    {
        var element = new BarcodeElement { Data = "" };

        Assert.Throws<ArgumentException>(() => _provider.Generate(element, 200, 80));
    }

    /// <summary>
    /// Verifies exception is thrown for invalid dimensions.
    /// </summary>
    [Theory]
    [InlineData(0, 80)]
    [InlineData(200, 0)]
    [InlineData(-1, 80)]
    [InlineData(200, -1)]
    public void Generate_InvalidDimensions_ThrowsArgumentException(int width, int height)
    {
        var element = new BarcodeElement
        {
            Data = "TEST",
            BarcodeWidth = width,
            BarcodeHeight = height
        };

        Assert.Throws<ArgumentException>(() => _provider.Generate(element, width, height));
    }

    /// <summary>
    /// Verifies unsupported format throws NotSupportedException.
    /// </summary>
    [Theory]
    [InlineData(BarcodeFormat.Code39)]
    [InlineData(BarcodeFormat.Ean13)]
    [InlineData(BarcodeFormat.Ean8)]
    [InlineData(BarcodeFormat.Upc)]
    public void Generate_UnsupportedFormat_ThrowsNotSupportedException(BarcodeFormat format)
    {
        var element = new BarcodeElement
        {
            Data = "123456789012",
            Format = format
        };

        Assert.Throws<NotSupportedException>(() => _provider.Generate(element, 200, 80));
    }

    /// <summary>
    /// Verifies exception for unsupported characters in Code128.
    /// </summary>
    [Fact]
    public void Generate_Code128_UnsupportedCharacter_ThrowsArgumentException()
    {
        var element = new BarcodeElement
        {
            Data = "Test\x00Invalid", // Contains null character
            Format = BarcodeFormat.Code128
        };

        Assert.Throws<ArgumentException>(() => _provider.Generate(element, 200, 80));
    }
}
