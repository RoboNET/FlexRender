using FlexRender.Rendering;
using SkiaSharp;
using Xunit;

namespace FlexRender.Tests.Rendering;

public class ColorParserTests
{
    [Theory]
    [InlineData("#ffffff", 255, 255, 255, 255)]
    [InlineData("#FFFFFF", 255, 255, 255, 255)]
    [InlineData("#000000", 255, 0, 0, 0)]
    [InlineData("#ff0000", 255, 255, 0, 0)]
    [InlineData("#00ff00", 255, 0, 255, 0)]
    [InlineData("#0000ff", 255, 0, 0, 255)]
    public void Parse_SixDigitHex_ReturnsCorrectColor(string hex, byte alpha, byte red, byte green, byte blue)
    {
        var color = ColorParser.Parse(hex);

        Assert.Equal(alpha, color.Alpha);
        Assert.Equal(red, color.Red);
        Assert.Equal(green, color.Green);
        Assert.Equal(blue, color.Blue);
    }

    [Theory]
    [InlineData("#fff", 255, 255, 255, 255)]
    [InlineData("#FFF", 255, 255, 255, 255)]
    [InlineData("#000", 255, 0, 0, 0)]
    [InlineData("#f00", 255, 255, 0, 0)]
    [InlineData("#0f0", 255, 0, 255, 0)]
    [InlineData("#00f", 255, 0, 0, 255)]
    public void Parse_ThreeDigitHex_ReturnsCorrectColor(string hex, byte alpha, byte red, byte green, byte blue)
    {
        var color = ColorParser.Parse(hex);

        Assert.Equal(alpha, color.Alpha);
        Assert.Equal(red, color.Red);
        Assert.Equal(green, color.Green);
        Assert.Equal(blue, color.Blue);
    }

    [Theory]
    [InlineData("#ff000000", 255, 0, 0, 0)]
    [InlineData("#80ffffff", 128, 255, 255, 255)]
    [InlineData("#00ff0000", 0, 255, 0, 0)]
    public void Parse_EightDigitHex_IncludesAlpha(string hex, byte alpha, byte red, byte green, byte blue)
    {
        var color = ColorParser.Parse(hex);

        Assert.Equal(alpha, color.Alpha);
        Assert.Equal(red, color.Red);
        Assert.Equal(green, color.Green);
        Assert.Equal(blue, color.Blue);
    }

    [Theory]
    [InlineData("#f000", 255, 0, 0, 0)]
    [InlineData("#8fff", 136, 255, 255, 255)]
    public void Parse_FourDigitHex_IncludesAlpha(string hex, byte alpha, byte red, byte green, byte blue)
    {
        var color = ColorParser.Parse(hex);

        Assert.Equal(alpha, color.Alpha);
        Assert.Equal(red, color.Red);
        Assert.Equal(green, color.Green);
        Assert.Equal(blue, color.Blue);
    }

    [Theory]
    [InlineData("ffffff")]
    [InlineData("invalid")]
    [InlineData("#gg0000")]
    [InlineData("#12345")]
    [InlineData("")]
    public void Parse_InvalidHex_ReturnsBlack(string hex)
    {
        var color = ColorParser.Parse(hex);

        Assert.Equal(SKColors.Black, color);
    }

    [Fact]
    public void Parse_Null_ReturnsBlack()
    {
        var color = ColorParser.Parse(null);

        Assert.Equal(SKColors.Black, color);
    }

    [Fact]
    public void TryParse_ValidHex_ReturnsTrueAndColor()
    {
        var success = ColorParser.TryParse("#ff0000", out var color);

        Assert.True(success);
        Assert.Equal(255, color.Red);
        Assert.Equal(0, color.Green);
        Assert.Equal(0, color.Blue);
    }

    [Fact]
    public void TryParse_InvalidHex_ReturnsFalse()
    {
        var success = ColorParser.TryParse("invalid", out var color);

        Assert.False(success);
        Assert.Equal(default, color);
    }

    [Theory]
    [InlineData("rgba(255, 255, 255, 0.05)", 13, 255, 255, 255)]
    [InlineData("rgba(255, 255, 255, 0.8)", 204, 255, 255, 255)]
    [InlineData("rgba(255, 0, 0, 1.0)", 255, 255, 0, 0)]
    [InlineData("rgba(0, 0, 0, 0.0)", 0, 0, 0, 0)]
    [InlineData("rgba(128, 64, 32, 0.5)", 128, 128, 64, 32)]
    public void Parse_Rgba_WithAlpha_ReturnsCorrectColor(string input, byte alpha, byte red, byte green, byte blue)
    {
        var color = ColorParser.Parse(input);

        Assert.Equal(alpha, color.Alpha);
        Assert.Equal(red, color.Red);
        Assert.Equal(green, color.Green);
        Assert.Equal(blue, color.Blue);
    }

    [Theory]
    [InlineData("rgb(255, 0, 0)", 255, 255, 0, 0)]
    [InlineData("rgb(0, 255, 0)", 255, 0, 255, 0)]
    [InlineData("rgb(0, 0, 255)", 255, 0, 0, 255)]
    [InlineData("rgb(0, 0, 0)", 255, 0, 0, 0)]
    [InlineData("rgb(255, 255, 255)", 255, 255, 255, 255)]
    public void Parse_Rgb_ReturnsCorrectColor(string input, byte alpha, byte red, byte green, byte blue)
    {
        var color = ColorParser.Parse(input);

        Assert.Equal(alpha, color.Alpha);
        Assert.Equal(red, color.Red);
        Assert.Equal(green, color.Green);
        Assert.Equal(blue, color.Blue);
    }

    [Theory]
    [InlineData("rgba(255,255,255,0.6)", 153, 255, 255, 255)]
    [InlineData("rgba(0,0,0,0.5)", 128, 0, 0, 0)]
    [InlineData("rgb(128,64,32)", 255, 128, 64, 32)]
    public void Parse_Rgba_NoSpaces_Works(string input, byte alpha, byte red, byte green, byte blue)
    {
        var color = ColorParser.Parse(input);

        Assert.Equal(alpha, color.Alpha);
        Assert.Equal(red, color.Red);
        Assert.Equal(green, color.Green);
        Assert.Equal(blue, color.Blue);
    }

    [Theory]
    [InlineData("rgba(256, 0, 0, 1.0)")]
    [InlineData("rgba(0, 256, 0, 1.0)")]
    [InlineData("rgba(0, 0, 256, 1.0)")]
    [InlineData("rgba(255, 255, 255, 1.1)")]
    [InlineData("rgba(-1, 0, 0, 1.0)")]
    [InlineData("rgba(0, 0, 0, -0.1)")]
    public void Parse_Rgba_OutOfRange_ReturnsBlack(string input)
    {
        var color = ColorParser.Parse(input);

        Assert.Equal(SKColors.Black, color);
    }

    [Fact]
    public void TryParse_Rgba_Valid_ReturnsTrueAndColor()
    {
        var success = ColorParser.TryParse("rgba(255, 128, 0, 0.5)", out var color);

        Assert.True(success);
        Assert.Equal(255, color.Red);
        Assert.Equal(128, color.Green);
        Assert.Equal(0, color.Blue);
        Assert.Equal(128, color.Alpha);
    }
}
