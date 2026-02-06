using FlexRender.Layout.Units;
using Xunit;

namespace FlexRender.Tests.Layout.Units;

/// <summary>
/// Tests for <see cref="PaddingParser.ParseMargin"/> method.
/// Verifies CSS margin shorthand parsing with auto support.
/// </summary>
public sealed class PaddingParserMarginTests
{
    // ────────────────────────────────────────────────────────────────
    // Single value
    // ────────────────────────────────────────────────────────────────

    [Fact]
    public void ParseMargin_Auto_ReturnsAllAuto()
    {
        var result = PaddingParser.ParseMargin("auto", 200f, 16f);

        Assert.True(result.Top.IsAuto);
        Assert.True(result.Right.IsAuto);
        Assert.True(result.Bottom.IsAuto);
        Assert.True(result.Left.IsAuto);
        Assert.True(result.HasAuto);
    }

    [Fact]
    public void ParseMargin_Fixed_ReturnsAllFixed()
    {
        var result = PaddingParser.ParseMargin("20", 200f, 16f);

        Assert.False(result.HasAuto);
        Assert.Equal(20f, result.Top.ResolvedPixels, 1);
        Assert.Equal(20f, result.Right.ResolvedPixels, 1);
        Assert.Equal(20f, result.Bottom.ResolvedPixels, 1);
        Assert.Equal(20f, result.Left.ResolvedPixels, 1);
    }

    // ────────────────────────────────────────────────────────────────
    // Two values
    // ────────────────────────────────────────────────────────────────

    [Fact]
    public void ParseMargin_MixedAutoAndFixed_TwoValues()
    {
        // "0 auto" => top=0, right=auto, bottom=0, left=auto
        var result = PaddingParser.ParseMargin("0 auto", 200f, 16f);

        Assert.False(result.Top.IsAuto);
        Assert.Equal(0f, result.Top.ResolvedPixels);

        Assert.True(result.Right.IsAuto);
        Assert.True(result.Left.IsAuto);

        Assert.False(result.Bottom.IsAuto);
        Assert.Equal(0f, result.Bottom.ResolvedPixels);
    }

    [Fact]
    public void ParseMargin_AutoVertical_FixedHorizontal()
    {
        // "auto 10" => top=auto, right=10, bottom=auto, left=10
        var result = PaddingParser.ParseMargin("auto 10", 200f, 16f);

        Assert.True(result.Top.IsAuto);
        Assert.True(result.Bottom.IsAuto);
        Assert.False(result.Right.IsAuto);
        Assert.Equal(10f, result.Right.ResolvedPixels, 1);
        Assert.False(result.Left.IsAuto);
        Assert.Equal(10f, result.Left.ResolvedPixels, 1);
    }

    // ────────────────────────────────────────────────────────────────
    // Four values
    // ────────────────────────────────────────────────────────────────

    [Fact]
    public void ParseMargin_FourValues_WithAuto()
    {
        // "0 0 0 auto" => top=0, right=0, bottom=0, left=auto
        var result = PaddingParser.ParseMargin("0 0 0 auto", 200f, 16f);

        Assert.False(result.Top.IsAuto);
        Assert.False(result.Right.IsAuto);
        Assert.False(result.Bottom.IsAuto);
        Assert.True(result.Left.IsAuto);
        Assert.True(result.HasAuto);
    }

    [Fact]
    public void ParseMargin_FourValues_MultipleAuto()
    {
        // "auto 0 auto 0" => top=auto, right=0, bottom=auto, left=0
        var result = PaddingParser.ParseMargin("auto 0 auto 0", 200f, 16f);

        Assert.True(result.Top.IsAuto);
        Assert.False(result.Right.IsAuto);
        Assert.True(result.Bottom.IsAuto);
        Assert.False(result.Left.IsAuto);
    }

    // ────────────────────────────────────────────────────────────────
    // Three values
    // ────────────────────────────────────────────────────────────────

    [Fact]
    public void ParseMargin_ThreeValues_WithAuto()
    {
        // "0 auto 10" => top=0, right=auto, bottom=10, left=auto
        var result = PaddingParser.ParseMargin("0 auto 10", 200f, 16f);

        Assert.False(result.Top.IsAuto);
        Assert.Equal(0f, result.Top.ResolvedPixels);
        Assert.True(result.Right.IsAuto);
        Assert.False(result.Bottom.IsAuto);
        Assert.Equal(10f, result.Bottom.ResolvedPixels, 1);
        Assert.True(result.Left.IsAuto);
    }

    // ────────────────────────────────────────────────────────────────
    // Edge cases
    // ────────────────────────────────────────────────────────────────

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void ParseMargin_Null_ReturnsZero(string? input)
    {
        var result = PaddingParser.ParseMargin(input, 200f, 16f);

        Assert.Equal(MarginValues.Zero, result);
    }

    [Fact]
    public void ParseMargin_FixedPixelValues_MatchesFixedParse()
    {
        // When no "auto" tokens, margin parsing should produce equivalent fixed values
        var result = PaddingParser.ParseMargin("10 20 30 40", 200f, 16f);

        Assert.Equal(10f, result.Top.ResolvedPixels, 1);
        Assert.Equal(20f, result.Right.ResolvedPixels, 1);
        Assert.Equal(30f, result.Bottom.ResolvedPixels, 1);
        Assert.Equal(40f, result.Left.ResolvedPixels, 1);
        Assert.False(result.HasAuto);
    }

    [Fact]
    public void ParseMargin_CaseInsensitiveAuto()
    {
        var result = PaddingParser.ParseMargin("AUTO", 200f, 16f);

        Assert.True(result.Top.IsAuto);
        Assert.True(result.Right.IsAuto);
        Assert.True(result.Bottom.IsAuto);
        Assert.True(result.Left.IsAuto);
    }

    [Fact]
    public void ParseMargin_WithUnits_ResolvesCorrectly()
    {
        // "10px auto 5% auto" with parentSize=200 => top=10, right=auto, bottom=10, left=auto
        var result = PaddingParser.ParseMargin("10px auto 5% auto", 200f, 16f);

        Assert.Equal(10f, result.Top.ResolvedPixels, 1);
        Assert.True(result.Right.IsAuto);
        Assert.Equal(10f, result.Bottom.ResolvedPixels, 1);
        Assert.True(result.Left.IsAuto);
    }
}
