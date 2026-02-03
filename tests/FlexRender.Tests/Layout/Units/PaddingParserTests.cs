using FlexRender.Layout.Units;
using Xunit;

namespace FlexRender.Tests.Layout.Units;

public sealed class PaddingParserTests
{
    // ============================================
    // Single value (backward compatible)
    // ============================================

    [Theory]
    [InlineData("0", 0f, 0f, 0f, 0f)]
    [InlineData("20", 20f, 20f, 20f, 20f)]
    [InlineData("10.5", 10.5f, 10.5f, 10.5f, 10.5f)]
    [InlineData("20px", 20f, 20f, 20f, 20f)]
    public void Parse_SingleValue_UniformPadding(string input, float top, float right, float bottom, float left)
    {
        var result = PaddingParser.Parse(input, 200f, 16f);

        Assert.Equal(top, result.Top, 1);
        Assert.Equal(right, result.Right, 1);
        Assert.Equal(bottom, result.Bottom, 1);
        Assert.Equal(left, result.Left, 1);
    }

    // ============================================
    // Two values: vertical horizontal
    // ============================================

    [Theory]
    [InlineData("20 40", 20f, 40f, 20f, 40f)]
    [InlineData("10 30", 10f, 30f, 10f, 30f)]
    [InlineData("0 15", 0f, 15f, 0f, 15f)]
    public void Parse_TwoValues_VerticalHorizontal(string input, float top, float right, float bottom, float left)
    {
        var result = PaddingParser.Parse(input, 200f, 16f);

        Assert.Equal(top, result.Top, 1);
        Assert.Equal(right, result.Right, 1);
        Assert.Equal(bottom, result.Bottom, 1);
        Assert.Equal(left, result.Left, 1);
    }

    // ============================================
    // Three values: top horizontal bottom
    // ============================================

    [Theory]
    [InlineData("20 40 30", 20f, 40f, 30f, 40f)]
    [InlineData("10 0 5", 10f, 0f, 5f, 0f)]
    public void Parse_ThreeValues_TopHorizontalBottom(string input, float top, float right, float bottom, float left)
    {
        var result = PaddingParser.Parse(input, 200f, 16f);

        Assert.Equal(top, result.Top, 1);
        Assert.Equal(right, result.Right, 1);
        Assert.Equal(bottom, result.Bottom, 1);
        Assert.Equal(left, result.Left, 1);
    }

    // ============================================
    // Four values: top right bottom left
    // ============================================

    [Theory]
    [InlineData("20 40 30 10", 20f, 40f, 30f, 10f)]
    [InlineData("1 2 3 4", 1f, 2f, 3f, 4f)]
    [InlineData("0 0 0 0", 0f, 0f, 0f, 0f)]
    public void Parse_FourValues_TopRightBottomLeft(string input, float top, float right, float bottom, float left)
    {
        var result = PaddingParser.Parse(input, 200f, 16f);

        Assert.Equal(top, result.Top, 1);
        Assert.Equal(right, result.Right, 1);
        Assert.Equal(bottom, result.Bottom, 1);
        Assert.Equal(left, result.Left, 1);
    }

    // ============================================
    // Unit types in multi-value
    // ============================================

    [Fact]
    public void Parse_TwoValues_WithPxUnits()
    {
        var result = PaddingParser.Parse("10px 20px", 200f, 16f);

        Assert.Equal(10f, result.Top, 1);
        Assert.Equal(20f, result.Right, 1);
        Assert.Equal(10f, result.Bottom, 1);
        Assert.Equal(20f, result.Left, 1);
    }

    [Fact]
    public void Parse_TwoValues_WithPercentUnits()
    {
        // parentSize = 200, so 10% = 20, 5% = 10
        var result = PaddingParser.Parse("10% 5%", 200f, 16f);

        Assert.Equal(20f, result.Top, 1);
        Assert.Equal(10f, result.Right, 1);
        Assert.Equal(20f, result.Bottom, 1);
        Assert.Equal(10f, result.Left, 1);
    }

    [Fact]
    public void Parse_FourValues_MixedUnits()
    {
        // 10px, 1em (=16), 5% of 200 (=10), 20px
        var result = PaddingParser.Parse("10px 1em 5% 20", 200f, 16f);

        Assert.Equal(10f, result.Top, 1);
        Assert.Equal(16f, result.Right, 1);
        Assert.Equal(10f, result.Bottom, 1);
        Assert.Equal(20f, result.Left, 1);
    }

    // ============================================
    // Edge cases
    // ============================================

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Parse_NullOrEmpty_ReturnsZero(string? input)
    {
        var result = PaddingParser.Parse(input, 200f, 16f);

        Assert.Equal(PaddingValues.Zero, result);
    }

    [Fact]
    public void Parse_ExtraWhitespace_HandledCorrectly()
    {
        var result = PaddingParser.Parse("  20   40  ", 200f, 16f);

        Assert.Equal(20f, result.Top, 1);
        Assert.Equal(40f, result.Right, 1);
        Assert.Equal(20f, result.Bottom, 1);
        Assert.Equal(40f, result.Left, 1);
    }

    [Fact]
    public void Parse_FiveOrMoreValues_UsesFirstFour()
    {
        // More than 4 values -- only first 4 are used (graceful degradation)
        var result = PaddingParser.Parse("1 2 3 4 5", 200f, 16f);

        Assert.Equal(1f, result.Top, 1);
        Assert.Equal(2f, result.Right, 1);
        Assert.Equal(3f, result.Bottom, 1);
        Assert.Equal(4f, result.Left, 1);
    }

    // ============================================
    // Absolute parsing (no layout context)
    // ============================================

    [Theory]
    [InlineData("20", 20f, 20f, 20f, 20f)]
    [InlineData("10 30", 10f, 30f, 10f, 30f)]
    [InlineData("10 20 30 40", 10f, 20f, 30f, 40f)]
    public void ParseAbsolute_PixelValues_ReturnsCorrectValues(string input, float top, float right, float bottom, float left)
    {
        var result = PaddingParser.ParseAbsolute(input);

        Assert.Equal(top, result.Top, 1);
        Assert.Equal(right, result.Right, 1);
        Assert.Equal(bottom, result.Bottom, 1);
        Assert.Equal(left, result.Left, 1);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void ParseAbsolute_NullOrEmpty_ReturnsZero(string? input)
    {
        var result = PaddingParser.ParseAbsolute(input);

        Assert.Equal(PaddingValues.Zero, result);
    }

    [Fact]
    public void ParseAbsolute_PercentValue_ResolvesToZero()
    {
        // ParseAbsolute uses parentSize=0, so 50% of 0 = 0
        var result = PaddingParser.ParseAbsolute("50%");

        Assert.Equal(0f, result.Top, 1);
        Assert.Equal(0f, result.Right, 1);
        Assert.Equal(0f, result.Bottom, 1);
        Assert.Equal(0f, result.Left, 1);
    }

    [Fact]
    public void ParseAbsolute_EmValue_ResolvesUsingDefaultFontSize()
    {
        // ParseAbsolute uses fontSize=16, so 2em = 32
        var result = PaddingParser.ParseAbsolute("2em");

        Assert.Equal(32f, result.Top, 1);
        Assert.Equal(32f, result.Right, 1);
        Assert.Equal(32f, result.Bottom, 1);
        Assert.Equal(32f, result.Left, 1);
    }
}
