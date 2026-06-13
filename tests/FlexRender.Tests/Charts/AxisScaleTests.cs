using System.Collections.Generic;
using FlexRender.Charts;
using Xunit;

namespace FlexRender.Tests.Charts;

/// <summary>
/// Tests for the renderer-agnostic nice-tick axis scaling math.
/// </summary>
public sealed class AxisScaleTests
{
    [Fact]
    public void Compute_SimplePositiveRange_ProducesNiceBounds()
    {
        var scale = AxisScale.Compute(0d, 48d, targetTicks: 5);

        Assert.Equal(0d, scale.Min);
        Assert.Equal(50d, scale.Max);
        Assert.Equal(10d, scale.Step);
        Assert.Equal(new[] { 0d, 10d, 20d, 30d, 40d, 50d }, scale.Ticks);
    }

    [Fact]
    public void Compute_RangeNotStartingAtZero_StillIncludesZeroForBars()
    {
        var scale = AxisScale.Compute(12d, 48d, targetTicks: 5);

        Assert.Equal(0d, scale.Min);
        Assert.True(scale.Max >= 48d);
    }

    [Fact]
    public void Compute_NegativeOnly_ClampsMaxToZero()
    {
        var scale = AxisScale.Compute(-80d, -10d, targetTicks: 5);

        Assert.True(scale.Min <= -80d);
        Assert.Equal(0d, scale.Max);
    }

    [Fact]
    public void Compute_CrossingZero_KeepsBothSides()
    {
        var scale = AxisScale.Compute(-30d, 70d, targetTicks: 5);

        Assert.True(scale.Min <= -30d);
        Assert.True(scale.Max >= 70d);
        Assert.Contains(0d, scale.Ticks);
    }
}
