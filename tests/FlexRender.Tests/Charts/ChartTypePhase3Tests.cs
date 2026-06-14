using System;
using FlexRender.Charts;
using Xunit;

namespace FlexRender.Tests.Charts;

/// <summary>
/// Sanity tests for the Phase-3 chart type enum members.
/// </summary>
public sealed class ChartTypePhase3Tests
{
    [Theory]
    [InlineData("Scatter")]
    [InlineData("Bubble")]
    [InlineData("Gauge")]
    [InlineData("Progress")]
    [InlineData("Sparkline")]
    public void ChartType_HasAllPhase3Members(string name)
    {
        Assert.True(Enum.TryParse<ChartType>(name, out _));
    }

    [Theory]
    [InlineData("Bar")]
    [InlineData("Line")]
    [InlineData("Area")]
    [InlineData("Pie")]
    [InlineData("Donut")]
    public void ChartType_StillHasPhase2Members(string name)
    {
        Assert.True(Enum.TryParse<ChartType>(name, out _));
    }
}
