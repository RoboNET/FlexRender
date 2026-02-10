using FlexRender.Parsing.Ast;
using FlexRender.QrCode.ImageSharp.Providers;
using Xunit;

namespace FlexRender.Tests.Providers;

/// <summary>
/// Tests verifying that QrImageSharpProvider in FlexRender.QrCode.ImageSharp
/// is a standalone provider that works independently of FlexRender.ImageSharp.
/// </summary>
public sealed class ImageSharpQrProviderStandaloneTests
{
    private readonly QrImageSharpProvider _provider = new();

    /// <summary>
    /// Verifies basic QR code generation produces an image of the expected size.
    /// </summary>
    [Fact]
    public void GenerateImage_ValidData_ReturnsCorrectSize()
    {
        var element = new QrElement { Data = "standalone test", Size = 150 };
        using var image = _provider.GenerateImage(element, 150, 150);

        Assert.Equal(150, image.Width);
        Assert.Equal(150, image.Height);
    }

    /// <summary>
    /// Verifies default size of 100 when using element defaults.
    /// </summary>
    [Fact]
    public void GenerateImage_DefaultSize_ReturnsExpectedDimensions()
    {
        var element = new QrElement { Data = "test" };
        using var image = _provider.GenerateImage(element, 100, 100);

        Assert.Equal(100, image.Width);
        Assert.Equal(100, image.Height);
    }

    /// <summary>
    /// Verifies that null element throws ArgumentNullException.
    /// </summary>
    [Fact]
    public void GenerateImage_NullElement_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => _provider.GenerateImage(null!, 100, 100));
    }

    /// <summary>
    /// Verifies that empty data throws ArgumentException.
    /// </summary>
    [Fact]
    public void GenerateImage_EmptyData_Throws()
    {
        var element = new QrElement { Data = "" };
        Assert.Throws<ArgumentException>(() => _provider.GenerateImage(element, 100, 100));
    }

    /// <summary>
    /// Verifies that zero size throws ArgumentException.
    /// </summary>
    [Fact]
    public void GenerateImage_ZeroSize_Throws()
    {
        var element = new QrElement { Data = "test", Size = 0 };
        Assert.Throws<ArgumentException>(() => _provider.GenerateImage(element, 0, 0));
    }

    /// <summary>
    /// Verifies layout dimensions are used for image size.
    /// </summary>
    [Fact]
    public void GenerateImage_WithLayoutDimensions_UsesSpecifiedSize()
    {
        var element = new QrElement { Data = "test", Size = 100 };
        using var image = _provider.GenerateImage(element, 250, 250);

        Assert.Equal(250, image.Width);
        Assert.Equal(250, image.Height);
    }

    /// <summary>
    /// Verifies the generated image contains dark pixels (QR modules).
    /// </summary>
    [Fact]
    public void GenerateImage_ProducesDarkModules()
    {
        var element = new QrElement { Data = "module test", Size = 200, Foreground = "#000000" };
        using var image = _provider.GenerateImage(element, 200, 200);

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

    /// <summary>
    /// Verifies all error correction levels produce valid images.
    /// </summary>
    [Theory]
    [InlineData(ErrorCorrectionLevel.L)]
    [InlineData(ErrorCorrectionLevel.M)]
    [InlineData(ErrorCorrectionLevel.Q)]
    [InlineData(ErrorCorrectionLevel.H)]
    public void GenerateImage_AllEccLevels_Succeeds(ErrorCorrectionLevel level)
    {
        var element = new QrElement { Data = "ecc test", ErrorCorrection = level };
        using var image = _provider.GenerateImage(element, 100, 100);

        Assert.True(image.Width > 0);
        Assert.True(image.Height > 0);
    }

    /// <summary>
    /// Verifies custom foreground color is applied to QR modules.
    /// </summary>
    [Fact]
    public void GenerateImage_CustomForeground_UsesColor()
    {
        var element = new QrElement { Data = "test", Size = 200, Foreground = "#0000ff" };
        using var image = _provider.GenerateImage(element, 200, 200);

        var hasBluePixels = false;
        for (var y = 0; y < image.Height && !hasBluePixels; y++)
        {
            for (var x = 0; x < image.Width && !hasBluePixels; x++)
            {
                var pixel = image[x, y];
                if (pixel.B > 200 && pixel.R < 50 && pixel.G < 50 && pixel.A > 200)
                {
                    hasBluePixels = true;
                }
            }
        }
        Assert.True(hasBluePixels, "QR code should use the specified blue foreground color");
    }

    /// <summary>
    /// Verifies data exceeding capacity throws.
    /// </summary>
    [Fact]
    public void GenerateImage_DataExceedsCapacity_Throws()
    {
        var element = new QrElement
        {
            Data = new string('x', 1300),
            ErrorCorrection = ErrorCorrectionLevel.H
        };

        Assert.Throws<ArgumentException>(() => _provider.GenerateImage(element, 100, 100));
    }
}
