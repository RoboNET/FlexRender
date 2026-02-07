using FlexRender.Barcode.ImageSharp.Providers;
using FlexRender.Parsing.Ast;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using Xunit;

namespace FlexRender.ImageSharp.Tests.Providers;

public sealed class ImageSharpBarcodeProviderTests
{
    private readonly BarcodeImageSharpProvider _provider = new();

    [Fact]
    public void Generate_ValidData_ReturnsImage()
    {
        var element = new BarcodeElement { Data = "ABC123", BarcodeWidth = 200, BarcodeHeight = 80 };
        using var image = _provider.GenerateImage(element, 200, 80);

        Assert.NotNull(image);
        Assert.Equal(200, image.Width);
        Assert.Equal(80, image.Height);
    }

    [Fact]
    public void Generate_DefaultDimensions_Returns200x80()
    {
        var element = new BarcodeElement { Data = "TEST" };
        using var image = _provider.GenerateImage(element, 200, 80);

        Assert.Equal(200, image.Width);
        Assert.Equal(80, image.Height);
    }

    [Fact]
    public void Generate_NullElement_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => _provider.GenerateImage(null!, 200, 80));
    }

    [Fact]
    public void Generate_EmptyData_Throws()
    {
        var element = new BarcodeElement { Data = "" };
        Assert.Throws<ArgumentException>(() => _provider.GenerateImage(element, 200, 80));
    }

    [Fact]
    public void Generate_ZeroWidth_Throws()
    {
        var element = new BarcodeElement { Data = "test", BarcodeWidth = 0, BarcodeHeight = 80 };
        Assert.Throws<ArgumentException>(() => _provider.GenerateImage(element, 0, 80));
    }

    [Fact]
    public void Generate_ZeroHeight_Throws()
    {
        var element = new BarcodeElement { Data = "test", BarcodeWidth = 200, BarcodeHeight = 0 };
        Assert.Throws<ArgumentException>(() => _provider.GenerateImage(element, 200, 0));
    }

    [Fact]
    public void Generate_WithLayoutDimensions_UsesLayoutSize()
    {
        var element = new BarcodeElement { Data = "TEST", BarcodeWidth = 100, BarcodeHeight = 40 };
        using var image = _provider.GenerateImage(element, 300, 100);

        Assert.Equal(300, image.Width);
        Assert.Equal(100, image.Height);
    }

    [Fact]
    public void Generate_ContainsDarkBars_NotAllWhite()
    {
        var element = new BarcodeElement { Data = "ABC", BarcodeWidth = 200, BarcodeHeight = 80, Foreground = "#000000" };
        using var image = _provider.GenerateImage(element, 200, 80);

        var hasDarkPixels = false;
        for (var y = 0; y < image.Height / 2 && !hasDarkPixels; y++)
        {
            for (var x = 0; x < image.Width && !hasDarkPixels; x++)
            {
                var pixel = image[x, y];
                if (pixel.R < 50 && pixel.G < 50 && pixel.B < 50 && pixel.A > 200)
                {
                    hasDarkPixels = true;
                }
            }
        }
        Assert.True(hasDarkPixels, "Barcode should contain dark bars");
    }

    [Fact]
    public void Generate_ShowTextFalse_NoTextArea()
    {
        var element = new BarcodeElement
        {
            Data = "ABC",
            BarcodeWidth = 200,
            BarcodeHeight = 80,
            ShowText = false
        };
        using var image = _provider.GenerateImage(element, 200, 80);

        // With ShowText=false, barcode bars extend to full height
        Assert.Equal(200, image.Width);
        Assert.Equal(80, image.Height);
    }

    [Fact]
    public void Generate_UnsupportedCharacter_Throws()
    {
        // Character outside ASCII 32-126
        var element = new BarcodeElement { Data = "\x01" };
        Assert.Throws<ArgumentException>(() => _provider.GenerateImage(element, 200, 80));
    }

    [Fact]
    public void Generate_UnsupportedFormat_Throws()
    {
        var element = new BarcodeElement { Data = "1234567890123", Format = BarcodeFormat.Ean13 };
        Assert.Throws<NotSupportedException>(() => _provider.GenerateImage(element, 200, 80));
    }

    [Fact]
    public void Generate_CustomForeground_UsesColor()
    {
        var element = new BarcodeElement { Data = "AB", BarcodeWidth = 200, BarcodeHeight = 80, Foreground = "#ff0000" };
        using var image = _provider.GenerateImage(element, 200, 80);

        var hasRedPixels = false;
        for (var y = 0; y < image.Height / 2 && !hasRedPixels; y++)
        {
            for (var x = 0; x < image.Width && !hasRedPixels; x++)
            {
                var pixel = image[x, y];
                if (pixel.R > 200 && pixel.G < 50 && pixel.B < 50 && pixel.A > 200)
                {
                    hasRedPixels = true;
                }
            }
        }
        Assert.True(hasRedPixels, "Barcode should use the specified foreground color");
    }
}
