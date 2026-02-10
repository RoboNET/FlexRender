using FlexRender.Rendering;
using Xunit;

namespace FlexRender.Tests.Rendering;

/// <summary>
/// Tests for <see cref="BoxShadowParser"/> which parses box-shadow strings
/// in the format "offsetX offsetY blurRadius color".
/// </summary>
public sealed class BoxShadowParserTests
{
    [Fact]
    public void TryParse_ValidShadow_ReturnsTrue()
    {
        var ok = BoxShadowParser.TryParse("4 4 8 #333333", out var shadow);

        Assert.True(ok);
        Assert.NotNull(shadow);
        Assert.Equal(4f, shadow.OffsetX);
        Assert.Equal(4f, shadow.OffsetY);
        Assert.Equal(8f, shadow.BlurRadius);
    }

    [Fact]
    public void TryParse_ZeroOffset_ReturnsTrue()
    {
        var ok = BoxShadowParser.TryParse("0 2 6 #00000080", out var shadow);

        Assert.True(ok);
        Assert.NotNull(shadow);
        Assert.Equal(0f, shadow.OffsetX);
        Assert.Equal(2f, shadow.OffsetY);
        Assert.Equal(6f, shadow.BlurRadius);
    }

    [Fact]
    public void TryParse_RgbaColor_ReturnsTrue()
    {
        var ok = BoxShadowParser.TryParse("4 4 8 rgba(0,0,0,0.3)", out var shadow);

        Assert.True(ok);
        Assert.NotNull(shadow);
        Assert.Equal(4f, shadow.OffsetX);
        Assert.Equal(4f, shadow.OffsetY);
        Assert.Equal(8f, shadow.BlurRadius);
    }

    [Fact]
    public void TryParse_NegativeOffset_ReturnsTrue()
    {
        var ok = BoxShadowParser.TryParse("-2 -3 4 #000000", out var shadow);

        Assert.True(ok);
        Assert.NotNull(shadow);
        Assert.Equal(-2f, shadow.OffsetX);
        Assert.Equal(-3f, shadow.OffsetY);
        Assert.Equal(4f, shadow.BlurRadius);
    }

    [Fact]
    public void TryParse_NegativeBlurRadius_ReturnsFalse()
    {
        var ok = BoxShadowParser.TryParse("4 4 -8 #333333", out var shadow);

        Assert.False(ok);
        Assert.Null(shadow);
    }

    [Fact]
    public void TryParse_Null_ReturnsFalse()
    {
        var ok = BoxShadowParser.TryParse(null, out var shadow);

        Assert.False(ok);
        Assert.Null(shadow);
    }

    [Fact]
    public void TryParse_EmptyString_ReturnsFalse()
    {
        var ok = BoxShadowParser.TryParse("", out var shadow);

        Assert.False(ok);
        Assert.Null(shadow);
    }

    [Fact]
    public void TryParse_WhitespaceOnly_ReturnsFalse()
    {
        var ok = BoxShadowParser.TryParse("   ", out var shadow);

        Assert.False(ok);
        Assert.Null(shadow);
    }

    [Fact]
    public void TryParse_TooFewTokens_ReturnsFalse()
    {
        var ok = BoxShadowParser.TryParse("4 4", out var shadow);

        Assert.False(ok);
        Assert.Null(shadow);
    }

    [Fact]
    public void TryParse_InvalidColor_ReturnsFalse()
    {
        var ok = BoxShadowParser.TryParse("4 4 8 notacolor", out var shadow);

        Assert.False(ok);
        Assert.Null(shadow);
    }

    [Fact]
    public void TryParse_FloatValues_ReturnsCorrectValues()
    {
        var ok = BoxShadowParser.TryParse("1.5 2.5 3.5 #FF0000", out var shadow);

        Assert.True(ok);
        Assert.NotNull(shadow);
        Assert.Equal(1.5f, shadow.OffsetX);
        Assert.Equal(2.5f, shadow.OffsetY);
        Assert.Equal(3.5f, shadow.BlurRadius);
    }

    [Fact]
    public void TryParse_ZeroBlurRadius_ReturnsTrue()
    {
        var ok = BoxShadowParser.TryParse("2 2 0 #000000", out var shadow);

        Assert.True(ok);
        Assert.NotNull(shadow);
        Assert.Equal(0f, shadow.BlurRadius);
    }
}
