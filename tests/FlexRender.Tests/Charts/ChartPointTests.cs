using FlexRender.Charts;
using Xunit;

namespace FlexRender.Tests.Charts;

/// <summary>
/// Tests for the <see cref="ChartPoint"/> XY(R) tuple struct.
/// </summary>
public sealed class ChartPointTests
{
    [Fact]
    public void Constructor_StoresXYR()
    {
        var p = new ChartPoint(1.5d, 2.5d, 3.5d);
        Assert.Equal(1.5d, p.X);
        Assert.Equal(2.5d, p.Y);
        Assert.Equal(3.5d, p.R);
    }

    [Fact]
    public void Xy_DefaultsRadiusToZero()
    {
        var p = ChartPoint.Xy(4d, 5d);
        Assert.Equal(4d, p.X);
        Assert.Equal(5d, p.Y);
        Assert.Equal(0d, p.R);
    }
}
