using FlexRender.ImageSharp.Rendering;
using SixLabors.ImageSharp.PixelFormats;
using Xunit;

namespace FlexRender.ImageSharp.Tests.Rendering;

public sealed class ImageSharpColorParserTests
{
    [Theory]
    [InlineData("#ff0000", 255, 0, 0, 255)]
    [InlineData("#00ff00", 0, 255, 0, 255)]
    [InlineData("#0000ff", 0, 0, 255, 255)]
    [InlineData("#ffffff", 255, 255, 255, 255)]
    [InlineData("#000000", 0, 0, 0, 255)]
    public void Parse_HexRRGGBB_ReturnsCorrectColor(string hex, byte r, byte g, byte b, byte a)
    {
        var color = ImageSharpColorParser.Parse(hex);
        var pixel = color.ToPixel<Rgba32>();
        Assert.Equal(r, pixel.R);
        Assert.Equal(g, pixel.G);
        Assert.Equal(b, pixel.B);
        Assert.Equal(a, pixel.A);
    }

    [Theory]
    [InlineData("#f00", 255, 0, 0, 255)]
    [InlineData("#0f0", 0, 255, 0, 255)]
    [InlineData("#00f", 0, 0, 255, 255)]
    public void Parse_HexRGB_ReturnsCorrectColor(string hex, byte r, byte g, byte b, byte a)
    {
        var color = ImageSharpColorParser.Parse(hex);
        var pixel = color.ToPixel<Rgba32>();
        Assert.Equal(r, pixel.R);
        Assert.Equal(g, pixel.G);
        Assert.Equal(b, pixel.B);
        Assert.Equal(a, pixel.A);
    }

    [Theory]
    [InlineData("#80ff0000", 255, 0, 0, 128)]
    [InlineData("#00000000", 0, 0, 0, 0)]
    [InlineData("#ffffffff", 255, 255, 255, 255)]
    public void Parse_HexAARRGGBB_ReturnsCorrectColor(string hex, byte r, byte g, byte b, byte a)
    {
        var color = ImageSharpColorParser.Parse(hex);
        var pixel = color.ToPixel<Rgba32>();
        Assert.Equal(r, pixel.R);
        Assert.Equal(g, pixel.G);
        Assert.Equal(b, pixel.B);
        Assert.Equal(a, pixel.A);
    }

    [Fact]
    public void Parse_RgbFunction_ReturnsCorrectColor()
    {
        var color = ImageSharpColorParser.Parse("rgb(128, 64, 32)");
        var pixel = color.ToPixel<Rgba32>();
        Assert.Equal(128, pixel.R);
        Assert.Equal(64, pixel.G);
        Assert.Equal(32, pixel.B);
        Assert.Equal(255, pixel.A);
    }

    [Fact]
    public void Parse_RgbaFunction_ReturnsCorrectColor()
    {
        var color = ImageSharpColorParser.Parse("rgba(255, 0, 0, 0.5)");
        var pixel = color.ToPixel<Rgba32>();
        Assert.Equal(255, pixel.R);
        Assert.Equal(0, pixel.G);
        Assert.Equal(0, pixel.B);
        Assert.Equal(128, pixel.A);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("invalid")]
    [InlineData("not-a-color")]
    public void Parse_InvalidInput_ReturnsBlack(string? input)
    {
        var color = ImageSharpColorParser.Parse(input);
        var pixel = color.ToPixel<Rgba32>();
        Assert.Equal(0, pixel.R);
        Assert.Equal(0, pixel.G);
        Assert.Equal(0, pixel.B);
        Assert.Equal(255, pixel.A);
    }

    [Theory]
    [InlineData("#f00", true)]
    [InlineData("#ff0000", true)]
    [InlineData("#80ff0000", true)]
    [InlineData("rgb(255, 0, 0)", true)]
    [InlineData("rgba(255, 0, 0, 0.5)", true)]
    [InlineData(null, false)]
    [InlineData("", false)]
    [InlineData("invalid", false)]
    public void TryParse_ReturnsExpectedSuccess(string? input, bool expectedSuccess)
    {
        var result = ImageSharpColorParser.TryParse(input, out _);
        Assert.Equal(expectedSuccess, result);
    }
}
