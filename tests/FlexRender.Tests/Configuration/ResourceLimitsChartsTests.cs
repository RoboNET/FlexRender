using System;
using FlexRender.Configuration;
using Xunit;

namespace FlexRender.Tests.Configuration;

/// <summary>
/// Tests for the chart-related resource limits.
/// </summary>
public sealed class ResourceLimitsChartsTests
{
    [Fact]
    public void MaxSeriesPerChart_DefaultsTo50()
    {
        var limits = new ResourceLimits();
        Assert.Equal(50, limits.MaxSeriesPerChart);
    }

    [Fact]
    public void MaxDataPointsPerSeries_DefaultsTo10000()
    {
        var limits = new ResourceLimits();
        Assert.Equal(10000, limits.MaxDataPointsPerSeries);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void MaxSeriesPerChart_RejectsNonPositive(int value)
    {
        var limits = new ResourceLimits();
        Assert.Throws<ArgumentOutOfRangeException>(() => limits.MaxSeriesPerChart = value);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void MaxDataPointsPerSeries_RejectsNonPositive(int value)
    {
        var limits = new ResourceLimits();
        Assert.Throws<ArgumentOutOfRangeException>(() => limits.MaxDataPointsPerSeries = value);
    }
}
