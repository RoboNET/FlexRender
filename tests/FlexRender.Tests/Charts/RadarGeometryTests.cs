using System;
using FlexRender.Charts;
using Xunit;

namespace FlexRender.Tests.Charts;

/// <summary>
/// Tests for radar spoke geometry: angles start at the top and advance clockwise.
/// </summary>
public sealed class RadarGeometryTests
{
    [Fact]
    public void SpokeAngle_FirstSpoke_PointsUp()
    {
        // Spoke 0 of 4 is straight up: -90 degrees == -PI/2 radians.
        var angle = RadarGeometry.SpokeAngle(0, 4);
        Assert.Equal(-MathF.PI / 2f, angle, 4);
    }

    [Fact]
    public void SpokeAngle_SecondOfFour_PointsRight()
    {
        // Spoke 1 of 4 is to the right: 0 radians.
        var angle = RadarGeometry.SpokeAngle(1, 4);
        Assert.Equal(0f, angle, 4);
    }

    [Fact]
    public void Project_ZeroFraction_ReturnsCenter()
    {
        var p = RadarGeometry.Project(centerX: 100f, centerY: 100f, radius: 50f, spokeIndex: 0, spokeCount: 4, fraction: 0f);
        Assert.Equal(100f, p.X, 3);
        Assert.Equal(100f, p.Y, 3);
    }

    [Fact]
    public void Project_FullFractionFirstSpoke_ReachesTopEdge()
    {
        var p = RadarGeometry.Project(centerX: 100f, centerY: 100f, radius: 50f, spokeIndex: 0, spokeCount: 4, fraction: 1f);
        Assert.Equal(100f, p.X, 3);   // straight up: x unchanged
        Assert.Equal(50f, p.Y, 3);    // y = 100 - 50
    }
}
