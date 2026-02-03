using FlexRender.Rendering;
using Xunit;

namespace FlexRender.Tests.Rendering;

public class RotationHelperTests
{
    [Theory]
    [InlineData("none", 0f)]
    [InlineData("None", 0f)]
    [InlineData("NONE", 0f)]
    public void ParseRotation_None_ReturnsZero(string value, float expected)
    {
        var result = RotationHelper.ParseRotation(value);

        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData("right", 90f)]
    [InlineData("Right", 90f)]
    [InlineData("RIGHT", 90f)]
    public void ParseRotation_Right_Returns90(string value, float expected)
    {
        var result = RotationHelper.ParseRotation(value);

        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData("left", 270f)]
    [InlineData("Left", 270f)]
    [InlineData("LEFT", 270f)]
    public void ParseRotation_Left_Returns270(string value, float expected)
    {
        var result = RotationHelper.ParseRotation(value);

        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData("flip", 180f)]
    [InlineData("Flip", 180f)]
    [InlineData("FLIP", 180f)]
    public void ParseRotation_Flip_Returns180(string value, float expected)
    {
        var result = RotationHelper.ParseRotation(value);

        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData("0", 0f)]
    [InlineData("45", 45f)]
    [InlineData("90", 90f)]
    [InlineData("180", 180f)]
    [InlineData("270", 270f)]
    [InlineData("360", 360f)]
    public void ParseRotation_NumericDegrees_ReturnsValue(string value, float expected)
    {
        var result = RotationHelper.ParseRotation(value);

        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData("-45", -45f)]
    [InlineData("-90", -90f)]
    public void ParseRotation_NegativeDegrees_ReturnsValue(string value, float expected)
    {
        var result = RotationHelper.ParseRotation(value);

        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData("45.5", 45.5f)]
    [InlineData("22.75", 22.75f)]
    public void ParseRotation_DecimalDegrees_ReturnsValue(string value, float expected)
    {
        var result = RotationHelper.ParseRotation(value);

        Assert.Equal(expected, result, precision: 2);
    }

    [Theory]
    [InlineData("")]
    [InlineData("invalid")]
    [InlineData("abc")]
    public void ParseRotation_Invalid_ReturnsZero(string value)
    {
        var result = RotationHelper.ParseRotation(value);

        Assert.Equal(0f, result);
    }

    [Fact]
    public void ParseRotation_Null_ReturnsZero()
    {
        var result = RotationHelper.ParseRotation(null);

        Assert.Equal(0f, result);
    }

    [Theory]
    [InlineData(0f, false)]
    [InlineData(45f, true)]
    [InlineData(90f, true)]
    [InlineData(180f, true)]
    [InlineData(-45f, true)]
    public void HasRotation_ChecksNonZero(float degrees, bool expected)
    {
        var result = RotationHelper.HasRotation(degrees);

        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData(90f, true)]
    [InlineData(270f, true)]
    [InlineData(-90f, true)]
    [InlineData(-270f, true)]
    [InlineData(0f, false)]
    [InlineData(45f, false)]
    [InlineData(180f, false)]
    public void SwapsDimensions_ChecksOrthogonal(float degrees, bool expected)
    {
        var result = RotationHelper.SwapsDimensions(degrees);

        Assert.Equal(expected, result);
    }
}
