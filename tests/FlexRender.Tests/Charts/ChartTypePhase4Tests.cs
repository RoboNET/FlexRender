using System;
using FlexRender.Charts;
using Xunit;

namespace FlexRender.Tests.Charts;

/// <summary>
/// Sanity tests for the Phase-4 chart type enum members.
/// </summary>
public sealed class ChartTypePhase4Tests
{
    [Theory]
    [InlineData("Heatmap")]
    [InlineData("Radar")]
    public void ChartType_HasAllPhase4Members(string name)
    {
        Assert.True(Enum.TryParse<ChartType>(name, out _));
    }

    [Theory]
    [InlineData("Bar")]
    [InlineData("Scatter")]
    [InlineData("Gauge")]
    [InlineData("Sparkline")]
    public void ChartType_StillHasEarlierMembers(string name)
    {
        Assert.True(Enum.TryParse<ChartType>(name, out _));
    }
}
