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

    [Fact]
    public void Compute_SinglePointPositive_AnchorsAtZeroWithRange()
    {
        var scale = AxisScale.Compute(42d, 42d, targetTicks: 5);

        Assert.Equal(0d, scale.Min);
        Assert.True(scale.Max >= 42d);
        Assert.True(scale.Step > 0d);
        Assert.True(scale.Ticks.Count >= 2);
    }

    [Fact]
    public void Compute_AllZeroValues_ProducesUnitRange()
    {
        var scale = AxisScale.Compute(0d, 0d, targetTicks: 5);

        Assert.Equal(0d, scale.Min);
        Assert.True(scale.Max > 0d);
        Assert.True(scale.Step > 0d);
    }

    [Fact]
    public void Compute_SinglePointNegative_AnchorsMaxAtZero()
    {
        var scale = AxisScale.Compute(-7d, -7d, targetTicks: 5);

        Assert.True(scale.Min <= -7d);
        Assert.Equal(0d, scale.Max);
    }

    [Fact]
    public void Compute_NonFiniteInputs_FallBackToUnitRange()
    {
        var scale = AxisScale.Compute(double.NaN, double.PositiveInfinity, targetTicks: 5);

        Assert.True(double.IsFinite(scale.Min));
        Assert.True(double.IsFinite(scale.Max));
        Assert.True(scale.Step > 0d);
        Assert.True(scale.Max > scale.Min);
    }

    [Fact]
    public void Compute_TicksAreMonotonicAndEvenlySpaced()
    {
        var scale = AxisScale.Compute(0d, 95d, targetTicks: 5);

        for (var i = 1; i < scale.Ticks.Count; i++)
        {
            var delta = scale.Ticks[i] - scale.Ticks[i - 1];
            Assert.True(System.Math.Abs(delta - scale.Step) < 1e-9, $"Tick spacing {delta} != step {scale.Step}");
        }
    }
}
