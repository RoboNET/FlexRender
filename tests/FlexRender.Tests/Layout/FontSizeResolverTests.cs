using FlexRender.Layout;
using Xunit;

namespace FlexRender.Tests.Layout;

public sealed class FontSizeResolverTests
{
    [Fact]
    public void Resolve_PlainNumber_ReturnAsPixels()
    {
        Assert.Equal(16f, FontSizeResolver.Resolve("16", 12f));
    }

    [Fact]
    public void Resolve_PxUnit_ReturnsAbsolute()
    {
        Assert.Equal(48f, FontSizeResolver.Resolve("48px", 12f));
    }

    [Fact]
    public void Resolve_EmUnit_MultipliesBaseFontSize()
    {
        Assert.Equal(18f, FontSizeResolver.Resolve("1.5em", 12f));
    }

    [Fact]
    public void Resolve_Percentage_MultipliesBaseFontSize()
    {
        Assert.Equal(18f, FontSizeResolver.Resolve("150%", 12f));
    }

    [Fact]
    public void Resolve_Null_ReturnsBaseFontSize()
    {
        Assert.Equal(12f, FontSizeResolver.Resolve(null, 12f));
    }

    [Fact]
    public void Resolve_Empty_ReturnsBaseFontSize()
    {
        Assert.Equal(12f, FontSizeResolver.Resolve("", 12f));
    }

    [Fact]
    public void Resolve_Invalid_ReturnsBaseFontSize()
    {
        Assert.Equal(12f, FontSizeResolver.Resolve("abc", 12f));
    }

    [Fact]
    public void Resolve_WhitespaceAround_TrimsAndParses()
    {
        Assert.Equal(16f, FontSizeResolver.Resolve("  16px  ", 12f));
    }

    [Theory]
    [InlineData("1em", 12f, 12f)]
    [InlineData("1em", 16f, 16f)]
    [InlineData("2em", 12f, 24f)]
    [InlineData("100%", 12f, 12f)]
    [InlineData("200%", 16f, 32f)]
    [InlineData("48px", 12f, 48f)]
    [InlineData("48px", 16f, 48f)]
    [InlineData("48", 12f, 48f)]
    public void Resolve_VariousInputs_ReturnsExpected(string size, float baseFontSize, float expected)
    {
        Assert.Equal(expected, FontSizeResolver.Resolve(size, baseFontSize), 0.01f);
    }
}
