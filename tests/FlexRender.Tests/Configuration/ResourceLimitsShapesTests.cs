using System;
using FlexRender.Configuration;
using Xunit;

namespace FlexRender.Tests.Configuration;

/// <summary>
/// Tests for the <see cref="ResourceLimits.MaxShapesPerDraw"/> limit.
/// </summary>
public sealed class ResourceLimitsShapesTests
{
    [Fact]
    public void MaxShapesPerDraw_DefaultsTo1000()
    {
        var limits = new ResourceLimits();
        Assert.Equal(1000, limits.MaxShapesPerDraw);
    }

    [Fact]
    public void MaxShapesPerDraw_AcceptsPositiveValue()
    {
        var limits = new ResourceLimits { MaxShapesPerDraw = 50 };
        Assert.Equal(50, limits.MaxShapesPerDraw);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void MaxShapesPerDraw_RejectsNonPositive(int value)
    {
        var limits = new ResourceLimits();
        Assert.Throws<ArgumentOutOfRangeException>(() => limits.MaxShapesPerDraw = value);
    }
}
