using FlexRender.Charts;
using Xunit;

namespace FlexRender.Tests.Charts;

/// <summary>
/// Tests for the renderer-agnostic plot-area subdivision math.
/// </summary>
public sealed class ChartLayoutMathTests
{
    [Fact]
    public void ComputePlotArea_NoTitleNoLegend_LeavesAxisGutter()
    {
        var plot = ChartLayout.ComputePlotArea(
            width: 600f, height: 300f,
            hasTitle: false, legend: LegendPosition.None,
            axisGutterLeft: 40f, axisGutterBottom: 24f, titleHeight: 24f, legendExtent: 60f);

        Assert.Equal(40f, plot.Left);
        Assert.Equal(0f, plot.Top);
        Assert.Equal(600f - 40f, plot.Right);
        Assert.Equal(300f - 24f, plot.Bottom);
    }

    [Fact]
    public void ComputePlotArea_WithTitle_ReservesTopBand()
    {
        var plot = ChartLayout.ComputePlotArea(
            width: 600f, height: 300f,
            hasTitle: true, legend: LegendPosition.None,
            axisGutterLeft: 40f, axisGutterBottom: 24f, titleHeight: 24f, legendExtent: 60f);

        Assert.Equal(24f, plot.Top);
    }

    [Fact]
    public void ComputePlotArea_BottomLegend_ReservesBottomBand()
    {
        var plot = ChartLayout.ComputePlotArea(
            width: 600f, height: 300f,
            hasTitle: false, legend: LegendPosition.Bottom,
            axisGutterLeft: 40f, axisGutterBottom: 24f, titleHeight: 24f, legendExtent: 60f);

        Assert.Equal(300f - 24f - 60f, plot.Bottom);
    }

    [Fact]
    public void ComputePlotArea_RightLegend_ReservesRightBand()
    {
        var plot = ChartLayout.ComputePlotArea(
            width: 600f, height: 300f,
            hasTitle: false, legend: LegendPosition.Right,
            axisGutterLeft: 40f, axisGutterBottom: 24f, titleHeight: 24f, legendExtent: 60f);

        Assert.Equal(600f - 40f - 60f, plot.Right);
    }

    [Fact]
    public void ComputePlotArea_DegenerateSize_DoesNotInvert()
    {
        var plot = ChartLayout.ComputePlotArea(
            width: 30f, height: 20f,
            hasTitle: true, legend: LegendPosition.Bottom,
            axisGutterLeft: 40f, axisGutterBottom: 24f, titleHeight: 24f, legendExtent: 60f);

        Assert.True(plot.Right >= plot.Left);
        Assert.True(plot.Bottom >= plot.Top);
    }
}
