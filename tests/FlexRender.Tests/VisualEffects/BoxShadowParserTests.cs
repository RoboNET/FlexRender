using FlexRender.Rendering;
using SkiaSharp;
using Xunit;

namespace FlexRender.Tests.VisualEffects;

/// <summary>
/// Tests for BoxShadowParser and BoxShadowValues.
/// </summary>
public sealed class BoxShadowParserTests
{
    // === Successful parsing ===

    [Fact]
    public void TryParse_ValidShadow_ReturnsTrue()
    {
        var success = BoxShadowParser.TryParse("4 4 8 rgba(0,0,0,0.3)", out var result);

        Assert.True(success);
        Assert.NotNull(result);
        Assert.Equal(4f, result.OffsetX);
        Assert.Equal(4f, result.OffsetY);
        Assert.Equal(8f, result.BlurRadius);
    }

    [Fact]
    public void TryParse_ValidShadowWithHexColor_ParsesColor()
    {
        var success = BoxShadowParser.TryParse("2 2 4 #000000", out var result);

        Assert.True(success);
        Assert.NotNull(result);
        Assert.Equal(2f, result.OffsetX);
        Assert.Equal(2f, result.OffsetY);
        Assert.Equal(4f, result.BlurRadius);
        Assert.Equal(SKColors.Black, result.Color);
    }

    [Fact]
    public void TryParse_NegativeOffsets_Parses()
    {
        var success = BoxShadowParser.TryParse("-4 -4 8 #000000", out var result);

        Assert.True(success);
        Assert.NotNull(result);
        Assert.Equal(-4f, result.OffsetX);
        Assert.Equal(-4f, result.OffsetY);
        Assert.Equal(8f, result.BlurRadius);
    }

    [Fact]
    public void TryParse_ZeroBlurRadius_Parses()
    {
        var success = BoxShadowParser.TryParse("4 4 0 #000000", out var result);

        Assert.True(success);
        Assert.NotNull(result);
        Assert.Equal(0f, result.BlurRadius);
    }

    [Fact]
    public void TryParse_RgbaColor_ParsesAlpha()
    {
        var success = BoxShadowParser.TryParse("4 4 8 rgba(0,0,0,0.3)", out var result);

        Assert.True(success);
        Assert.NotNull(result);
        // Alpha should be around 0.3 * 255 = 76-77
        Assert.True(result.Color.Alpha < 100);
        Assert.True(result.Color.Alpha > 50);
    }

    [Fact]
    public void TryParse_LargeBlurRadius_Parses()
    {
        var success = BoxShadowParser.TryParse("0 0 20 #333333", out var result);

        Assert.True(success);
        Assert.NotNull(result);
        Assert.Equal(20f, result.BlurRadius);
    }

    [Theory]
    [InlineData("1 2 3 #ff0000")]
    [InlineData("0 0 0 #000000")]
    [InlineData("10 10 20 rgba(255,0,0,0.5)")]
    public void TryParse_VariousValidInputs_ReturnsTrue(string input)
    {
        var success = BoxShadowParser.TryParse(input, out var result);

        Assert.True(success);
        Assert.NotNull(result);
    }

    // === Null/empty input ===

    [Fact]
    public void TryParse_Null_ReturnsFalse()
    {
        var success = BoxShadowParser.TryParse(null, out var result);

        Assert.False(success);
        Assert.Null(result);
    }

    [Fact]
    public void TryParse_EmptyString_ReturnsFalse()
    {
        var success = BoxShadowParser.TryParse("", out var result);

        Assert.False(success);
        Assert.Null(result);
    }

    [Fact]
    public void TryParse_WhitespaceOnly_ReturnsFalse()
    {
        var success = BoxShadowParser.TryParse("   ", out var result);

        Assert.False(success);
        Assert.Null(result);
    }

    // === Invalid input ===

    [Theory]
    [InlineData("invalid")]
    [InlineData("4 4")]
    [InlineData("4 4 8")]
    public void TryParse_IncompleteInput_ReturnsFalse(string input)
    {
        var success = BoxShadowParser.TryParse(input, out var result);

        Assert.False(success);
        Assert.Null(result);
    }

    // === TemplateElement.BoxShadow property ===

    [Fact]
    public void BoxShadow_DefaultValue_IsNull()
    {
        var element = new FlexRender.Parsing.Ast.FlexElement();

        Assert.Null(element.BoxShadow.Value);
    }

    [Fact]
    public void BoxShadow_CanBeSet()
    {
        var element = new FlexRender.Parsing.Ast.FlexElement
        {
            BoxShadow = "4 4 8 rgba(0,0,0,0.3)"
        };

        Assert.Equal("4 4 8 rgba(0,0,0,0.3)", element.BoxShadow);
    }
}
