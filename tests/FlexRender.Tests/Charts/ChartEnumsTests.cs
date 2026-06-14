using FlexRender.Charts;
using Xunit;

namespace FlexRender.Tests.Charts;

/// <summary>
/// Sanity tests for the chart enums.
/// </summary>
public sealed class ChartEnumsTests
{
    [Theory]
    [InlineData("Bar")]
    [InlineData("Line")]
    [InlineData("Area")]
    [InlineData("Pie")]
    [InlineData("Donut")]
    public void ChartType_HasAllPhase2Members(string name)
    {
        Assert.True(System.Enum.TryParse<ChartType>(name, out _));
    }

    [Theory]
    [InlineData("Top")]
    [InlineData("Bottom")]
    [InlineData("Left")]
    [InlineData("Right")]
    [InlineData("None")]
    public void LegendPosition_HasAllMembers(string name)
    {
        Assert.True(System.Enum.TryParse<LegendPosition>(name, out _));
    }

    [Theory]
    [InlineData("Percent")]
    [InlineData("Value")]
    [InlineData("None")]
    public void PieLabelMode_HasAllMembers(string name)
    {
        Assert.True(System.Enum.TryParse<PieLabelMode>(name, out _));
    }
}
