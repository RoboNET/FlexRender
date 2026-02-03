using FlexRender.Layout;
using Xunit;

namespace FlexRender.Tests.Layout;

public sealed class LineHeightResolverTests
{
    [Fact]
    public void Resolve_EmptyString_ReturnsDefault()
    {
        var result = LineHeightResolver.Resolve("", 20f, 28f);
        Assert.Equal(28f, result);
    }

    [Fact]
    public void Resolve_Null_ReturnsDefault()
    {
        var result = LineHeightResolver.Resolve(null, 20f, 28f);
        Assert.Equal(28f, result);
    }

    [Fact]
    public void Resolve_PlainMultiplier_ReturnsMultiplied()
    {
        var result = LineHeightResolver.Resolve("1.5", 20f, 28f);
        Assert.Equal(30f, result);
    }

    [Fact]
    public void Resolve_PxUnit_ReturnsAbsolute()
    {
        var result = LineHeightResolver.Resolve("24px", 20f, 28f);
        Assert.Equal(24f, result);
    }

    [Fact]
    public void Resolve_EmUnit_ReturnsRelativeToFontSize()
    {
        var result = LineHeightResolver.Resolve("2em", 20f, 28f);
        Assert.Equal(40f, result);
    }

    [Fact]
    public void Resolve_NegativeMultiplier_ClampedToZero()
    {
        var result = LineHeightResolver.Resolve("-1.5", 20f, 28f);
        Assert.Equal(0f, result);
    }

    [Fact]
    public void Resolve_NegativePx_ClampedToZero()
    {
        var result = LineHeightResolver.Resolve("-10px", 20f, 28f);
        Assert.Equal(0f, result);
    }

    [Fact]
    public void Resolve_NegativeEm_ClampedToZero()
    {
        var result = LineHeightResolver.Resolve("-2em", 20f, 28f);
        Assert.Equal(0f, result);
    }

    [Fact]
    public void Resolve_ZeroMultiplier_ReturnsZero()
    {
        var result = LineHeightResolver.Resolve("0", 20f, 28f);
        Assert.Equal(0f, result);
    }

    [Fact]
    public void Resolve_ZeroPx_ReturnsZero()
    {
        var result = LineHeightResolver.Resolve("0px", 20f, 28f);
        Assert.Equal(0f, result);
    }

    [Fact]
    public void Resolve_ZeroEm_ReturnsZero()
    {
        var result = LineHeightResolver.Resolve("0em", 20f, 28f);
        Assert.Equal(0f, result);
    }

    [Fact]
    public void Resolve_InvalidString_ReturnsDefault()
    {
        var result = LineHeightResolver.Resolve("abc", 20f, 28f);
        Assert.Equal(28f, result);
    }

    [Fact]
    public void Resolve_WhitespaceAroundValue_TrimsAndParses()
    {
        var result = LineHeightResolver.Resolve("  1.5  ", 20f, 28f);
        Assert.Equal(30f, result);
    }

    [Fact]
    public void Resolve_WhitespaceAroundPxValue_TrimsAndParses()
    {
        var result = LineHeightResolver.Resolve("  24px  ", 20f, 28f);
        Assert.Equal(24f, result);
    }

    [Theory]
    [InlineData("1.0", 16f, 22.4f, 16f)]
    [InlineData("2.0", 10f, 14f, 20f)]
    [InlineData("30px", 16f, 22.4f, 30f)]
    [InlineData("1.5em", 20f, 28f, 30f)]
    public void Resolve_VariousInputs_ReturnsExpected(string lineHeight, float fontSize, float defaultLh, float expected)
    {
        var result = LineHeightResolver.Resolve(lineHeight, fontSize, defaultLh);
        Assert.Equal(expected, result, 0.01f);
    }

    [Theory]
    [InlineData("-5px", 20f, 28f)]
    [InlineData("-100px", 10f, 14f)]
    [InlineData("-3em", 16f, 22.4f)]
    [InlineData("-2.5", 20f, 28f)]
    public void Resolve_NegativeValues_AlwaysClampedToZero(string lineHeight, float fontSize, float defaultLh)
    {
        var result = LineHeightResolver.Resolve(lineHeight, fontSize, defaultLh);
        Assert.Equal(0f, result);
    }
}
