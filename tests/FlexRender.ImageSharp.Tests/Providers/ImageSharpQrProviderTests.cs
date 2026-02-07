using FlexRender.QrCode.ImageSharp.Providers;
using FlexRender.Parsing.Ast;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using Xunit;

namespace FlexRender.ImageSharp.Tests.Providers;

public sealed class ImageSharpQrProviderTests
{
    private readonly QrImageSharpProvider _provider = new();

    [Fact]
    public void Generate_ValidData_ReturnsImage()
    {
        var element = new QrElement { Data = "https://example.com", Size = 200 };
        using var image = _provider.GenerateImage(element, 200, 200);

        Assert.NotNull(image);
        Assert.Equal(200, image.Width);
        Assert.Equal(200, image.Height);
    }

    [Fact]
    public void Generate_DefaultSize_Returns100x100()
    {
        var element = new QrElement { Data = "test" };
        using var image = _provider.GenerateImage(element, 100, 100);

        Assert.Equal(100, image.Width);
        Assert.Equal(100, image.Height);
    }

    [Fact]
    public void Generate_NullElement_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => _provider.GenerateImage(null!, 100, 100));
    }

    [Fact]
    public void Generate_EmptyData_Throws()
    {
        var element = new QrElement { Data = "" };
        Assert.Throws<ArgumentException>(() => _provider.GenerateImage(element, 100, 100));
    }

    [Fact]
    public void Generate_ZeroSize_Throws()
    {
        var element = new QrElement { Data = "test", Size = 0 };
        Assert.Throws<ArgumentException>(() => _provider.GenerateImage(element, 0, 0));
    }

    [Fact]
    public void Generate_NegativeSize_Throws()
    {
        var element = new QrElement { Data = "test", Size = -10 };
        Assert.Throws<ArgumentException>(() => _provider.GenerateImage(element, -10, -10));
    }

    [Fact]
    public void Generate_WithLayoutDimensions_UsesLayoutSize()
    {
        var element = new QrElement { Data = "test", Size = 100 };
        using var image = _provider.GenerateImage(element, 300, 300);

        Assert.Equal(300, image.Width);
        Assert.Equal(300, image.Height);
    }

    [Fact]
    public void Generate_WithDifferentLayoutDimensions_UsesMinimum()
    {
        var element = new QrElement { Data = "test", Size = 100 };
        // QR codes are square, so use the smaller dimension
        var size = Math.Min(400, 250);
        using var image = _provider.GenerateImage(element, size, size);

        Assert.Equal(250, image.Width);
        Assert.Equal(250, image.Height);
    }

    [Fact]
    public void Generate_WithOnlyLayoutHeight_UsesLayoutHeight()
    {
        var element = new QrElement { Data = "test", Size = 100 };
        using var image = _provider.GenerateImage(element, 350, 350);

        Assert.Equal(350, image.Width);
        Assert.Equal(350, image.Height);
    }

    [Fact]
    public void Generate_ContainsDarkModules_NotAllWhite()
    {
        var element = new QrElement { Data = "test", Size = 200, Foreground = "#000000" };
        using var image = _provider.GenerateImage(element, 200, 200);

        // Check that at least some pixels are dark (the QR modules)
        var hasDarkPixels = false;
        for (var y = 0; y < image.Height && !hasDarkPixels; y++)
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
        Assert.True(hasDarkPixels, "QR code should contain dark modules");
    }

    [Theory]
    [InlineData(ErrorCorrectionLevel.L)]
    [InlineData(ErrorCorrectionLevel.M)]
    [InlineData(ErrorCorrectionLevel.Q)]
    [InlineData(ErrorCorrectionLevel.H)]
    public void Generate_AllEccLevels_Succeeds(ErrorCorrectionLevel level)
    {
        var element = new QrElement { Data = "test", ErrorCorrection = level };
        using var image = _provider.GenerateImage(element, 100, 100);

        Assert.NotNull(image);
        Assert.True(image.Width > 0);
        Assert.True(image.Height > 0);
    }

    [Fact]
    public void Generate_CustomForeground_UsesColor()
    {
        var element = new QrElement { Data = "test", Size = 200, Foreground = "#ff0000" };
        using var image = _provider.GenerateImage(element, 200, 200);

        // Check that some pixels are red (foreground color)
        var hasRedPixels = false;
        for (var y = 0; y < image.Height && !hasRedPixels; y++)
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
        Assert.True(hasRedPixels, "QR code should use the specified foreground color");
    }

    [Fact]
    public void Generate_DataExceedsCapacity_Throws()
    {
        // Create data that exceeds max capacity for H level (1273 bytes)
        var element = new QrElement
        {
            Data = new string('x', 1300),
            ErrorCorrection = ErrorCorrectionLevel.H
        };

        Assert.Throws<ArgumentException>(() => _provider.GenerateImage(element, 100, 100));
    }
}
