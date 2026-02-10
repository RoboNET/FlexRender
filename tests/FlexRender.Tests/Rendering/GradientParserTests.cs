using FlexRender.Rendering;
using Xunit;

namespace FlexRender.Tests.Rendering;

/// <summary>
/// Tests for <see cref="GradientParser"/> which parses CSS-like gradient strings
/// into <see cref="GradientDefinition"/> values.
/// </summary>
public sealed class GradientParserTests
{
    // === IsGradient ===

    [Theory]
    [InlineData("linear-gradient(to right, #ff0000, #0000ff)")]
    [InlineData("radial-gradient(#ffffff, #000000)")]
    [InlineData("LINEAR-GRADIENT(to bottom, #fff, #000)")]
    [InlineData("RADIAL-GRADIENT(#fff, #000)")]
    public void IsGradient_ValidGradient_ReturnsTrue(string input)
    {
        Assert.True(GradientParser.IsGradient(input));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("#FF0000")]
    [InlineData("red")]
    [InlineData("not-a-gradient(#fff, #000)")]
    public void IsGradient_NonGradient_ReturnsFalse(string? input)
    {
        Assert.False(GradientParser.IsGradient(input));
    }

    // === TryParse linear-gradient ===

    [Fact]
    public void TryParse_LinearGradientToRight_ReturnsCorrectAngle()
    {
        var ok = GradientParser.TryParse("linear-gradient(to right, #ff0000, #0000ff)", out var gradient);

        Assert.True(ok);
        Assert.NotNull(gradient);
        Assert.Equal(GradientType.Linear, gradient.Type);
        Assert.Equal(90f, gradient.AngleDegrees);
        Assert.Equal(2, gradient.Stops.Count);
    }

    [Fact]
    public void TryParse_LinearGradientToBottom_ReturnsDefaultAngle()
    {
        var ok = GradientParser.TryParse("linear-gradient(to bottom, #ff0000, #0000ff)", out var gradient);

        Assert.True(ok);
        Assert.NotNull(gradient);
        Assert.Equal(180f, gradient.AngleDegrees);
    }

    [Fact]
    public void TryParse_LinearGradientToTop_Returns0Degrees()
    {
        var ok = GradientParser.TryParse("linear-gradient(to top, #ff0000, #0000ff)", out var gradient);

        Assert.True(ok);
        Assert.NotNull(gradient);
        Assert.Equal(0f, gradient.AngleDegrees);
    }

    [Fact]
    public void TryParse_LinearGradientToLeft_Returns270Degrees()
    {
        var ok = GradientParser.TryParse("linear-gradient(to left, #ff0000, #0000ff)", out var gradient);

        Assert.True(ok);
        Assert.NotNull(gradient);
        Assert.Equal(270f, gradient.AngleDegrees);
    }

    [Fact]
    public void TryParse_LinearGradientWithDegrees_ParsesAngle()
    {
        var ok = GradientParser.TryParse("linear-gradient(45deg, #ff0000, #00ff00, #0000ff)", out var gradient);

        Assert.True(ok);
        Assert.NotNull(gradient);
        Assert.Equal(45f, gradient.AngleDegrees);
        Assert.Equal(3, gradient.Stops.Count);
    }

    [Fact]
    public void TryParse_LinearGradientNoDirection_DefaultsToBottom()
    {
        var ok = GradientParser.TryParse("linear-gradient(#ff0000, #0000ff)", out var gradient);

        Assert.True(ok);
        Assert.NotNull(gradient);
        Assert.Equal(GradientType.Linear, gradient.Type);
        // Default direction is "to bottom" = 180 degrees
        Assert.Equal(180f, gradient.AngleDegrees);
        Assert.Equal(2, gradient.Stops.Count);
    }

    [Fact]
    public void TryParse_LinearGradientWithPositions_ParsesPositions()
    {
        var ok = GradientParser.TryParse("linear-gradient(to right, #ff0000 0%, #00ff00 50%, #0000ff 100%)", out var gradient);

        Assert.True(ok);
        Assert.NotNull(gradient);
        Assert.Equal(3, gradient.Stops.Count);
        Assert.Equal(0f, gradient.Stops[0].Position);
        Assert.Equal(0.5f, gradient.Stops[1].Position);
        Assert.Equal(1f, gradient.Stops[2].Position);
    }

    // === TryParse radial-gradient ===

    [Fact]
    public void TryParse_RadialGradient_ReturnsCorrectType()
    {
        var ok = GradientParser.TryParse("radial-gradient(#ffffff, #000000)", out var gradient);

        Assert.True(ok);
        Assert.NotNull(gradient);
        Assert.Equal(GradientType.Radial, gradient.Type);
        Assert.Equal(0f, gradient.AngleDegrees);
        Assert.Equal(2, gradient.Stops.Count);
    }

    [Fact]
    public void TryParse_RadialGradientWithPositions_ParsesStops()
    {
        var ok = GradientParser.TryParse("radial-gradient(#ffffff 0%, #888888 50%, #000000 100%)", out var gradient);

        Assert.True(ok);
        Assert.NotNull(gradient);
        Assert.Equal(3, gradient.Stops.Count);
    }

    // === TryParse failures ===

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("#FF0000")]
    [InlineData("linear-gradient()")]
    [InlineData("linear-gradient(#ff0000)")]
    public void TryParse_Invalid_ReturnsFalse(string? input)
    {
        var ok = GradientParser.TryParse(input, out var gradient);

        Assert.False(ok);
        Assert.Null(gradient);
    }

    // === Corner direction keywords ===

    [Theory]
    [InlineData("to top right", 45f)]
    [InlineData("to right top", 45f)]
    [InlineData("to bottom right", 135f)]
    [InlineData("to right bottom", 135f)]
    [InlineData("to bottom left", 225f)]
    [InlineData("to left bottom", 225f)]
    [InlineData("to top left", 315f)]
    [InlineData("to left top", 315f)]
    public void TryParse_LinearGradientCornerDirections_ReturnsCorrectAngle(string direction, float expectedAngle)
    {
        var ok = GradientParser.TryParse($"linear-gradient({direction}, #ff0000, #0000ff)", out var gradient);

        Assert.True(ok);
        Assert.NotNull(gradient);
        Assert.Equal(expectedAngle, gradient.AngleDegrees);
    }

    // === Color stop without position ===

    [Fact]
    public void TryParse_StopsWithoutPositions_PositionIsNull()
    {
        var ok = GradientParser.TryParse("linear-gradient(to right, #ff0000, #0000ff)", out var gradient);

        Assert.True(ok);
        Assert.NotNull(gradient);
        Assert.Null(gradient.Stops[0].Position);
        Assert.Null(gradient.Stops[1].Position);
    }
}
