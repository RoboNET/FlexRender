using FlexRender.Layout.Units;
using Xunit;

namespace FlexRender.Tests.Layout.Units;

public class UnitParserTests
{
    [Theory]
    [InlineData("100", UnitType.Pixels, 100f)]
    [InlineData("50.5", UnitType.Pixels, 50.5f)]
    [InlineData("0", UnitType.Pixels, 0f)]
    public void Parse_NumberOnly_ReturnsPixels(string input, UnitType expectedType, float expectedValue)
    {
        var unit = UnitParser.Parse(input);

        Assert.Equal(expectedType, unit.Type);
        Assert.Equal(expectedValue, unit.Value);
    }

    [Theory]
    [InlineData("50%", UnitType.Percent, 50f)]
    [InlineData("100%", UnitType.Percent, 100f)]
    [InlineData("33.33%", UnitType.Percent, 33.33f)]
    public void Parse_Percent_ReturnsPercent(string input, UnitType expectedType, float expectedValue)
    {
        var unit = UnitParser.Parse(input);

        Assert.Equal(expectedType, unit.Type);
        Assert.Equal(expectedValue, unit.Value, 2);
    }

    [Theory]
    [InlineData("1em", UnitType.Em, 1f)]
    [InlineData("1.5em", UnitType.Em, 1.5f)]
    [InlineData("2.25em", UnitType.Em, 2.25f)]
    public void Parse_Em_ReturnsEm(string input, UnitType expectedType, float expectedValue)
    {
        var unit = UnitParser.Parse(input);

        Assert.Equal(expectedType, unit.Type);
        Assert.Equal(expectedValue, unit.Value);
    }

    [Theory]
    [InlineData("auto")]
    [InlineData("Auto")]
    [InlineData("AUTO")]
    public void Parse_Auto_ReturnsAuto(string input)
    {
        var unit = UnitParser.Parse(input);

        Assert.Equal(UnitType.Auto, unit.Type);
    }

    [Fact]
    public void Parse_EmptyString_ReturnsAuto()
    {
        var unit = UnitParser.Parse("");

        Assert.Equal(UnitType.Auto, unit.Type);
    }

    [Fact]
    public void Parse_Null_ReturnsAuto()
    {
        var unit = UnitParser.Parse(null);

        Assert.Equal(UnitType.Auto, unit.Type);
    }

    [Fact]
    public void Parse_WithWhitespace_TrimsAndParses()
    {
        var unit = UnitParser.Parse("  50%  ");

        Assert.Equal(UnitType.Percent, unit.Type);
        Assert.Equal(50f, unit.Value);
    }

    [Fact]
    public void TryParse_ValidInput_ReturnsTrueAndUnit()
    {
        var success = UnitParser.TryParse("100px", out var unit);

        Assert.True(success);
        Assert.Equal(UnitType.Pixels, unit.Type);
        Assert.Equal(100f, unit.Value);
    }

    [Fact]
    public void TryParse_InvalidInput_ReturnsFalse()
    {
        var success = UnitParser.TryParse("invalid", out var unit);

        Assert.False(success);
        Assert.Equal(Unit.Auto, unit);
    }

    [Theory]
    [InlineData("100px", UnitType.Pixels, 100f)]
    [InlineData("50px", UnitType.Pixels, 50f)]
    public void Parse_ExplicitPx_ReturnsPixels(string input, UnitType expectedType, float expectedValue)
    {
        var unit = UnitParser.Parse(input);

        Assert.Equal(expectedType, unit.Type);
        Assert.Equal(expectedValue, unit.Value);
    }
}
