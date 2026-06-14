using FlexRender.Rendering;
using SkiaSharp;
using Xunit;

namespace FlexRender.Tests.Rendering;

/// <summary>
/// Tests for the heatmap two-color value-to-color interpolation.
/// </summary>
public sealed class HeatmapColorScaleTests
{
    private static readonly SKColor Low = new(0, 0, 0);       // black
    private static readonly SKColor High = new(200, 100, 50); // warm

    [Fact]
    public void Map_AtMin_ReturnsLowColor()
    {
        var c = HeatmapColorScale.Map(0d, 0d, 10d, Low, High);
        Assert.Equal(Low.Red, c.Red);
        Assert.Equal(Low.Green, c.Green);
        Assert.Equal(Low.Blue, c.Blue);
    }

    [Fact]
    public void Map_AtMax_ReturnsHighColor()
    {
        var c = HeatmapColorScale.Map(10d, 0d, 10d, Low, High);
        Assert.Equal(High.Red, c.Red);
        Assert.Equal(High.Green, c.Green);
        Assert.Equal(High.Blue, c.Blue);
    }

    [Fact]
    public void Map_Midpoint_IsBetweenLowAndHigh()
    {
        var c = HeatmapColorScale.Map(5d, 0d, 10d, Low, High);
        Assert.Equal((byte)100, c.Red);  // (0 + 200) / 2
        Assert.Equal((byte)50, c.Green); // (0 + 100) / 2
        Assert.Equal((byte)25, c.Blue);  // (0 + 50)  / 2
    }

    [Fact]
    public void Map_DegenerateRange_ReturnsHighColor()
    {
        var c = HeatmapColorScale.Map(5d, 5d, 5d, Low, High);
        Assert.Equal(High.Red, c.Red);
        Assert.Equal(High.Green, c.Green);
        Assert.Equal(High.Blue, c.Blue);
    }

    [Fact]
    public void Map_BelowMin_ClampsToLow()
    {
        var c = HeatmapColorScale.Map(-5d, 0d, 10d, Low, High);
        Assert.Equal(Low.Red, c.Red);
    }
}
