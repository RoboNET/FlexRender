using System;
using System.Collections.Generic;
using FlexRender.Charts;
using FlexRender.Parsing.Ast;
using Xunit;

namespace FlexRender.Tests.Parsing.Ast;

/// <summary>
/// Tests for the tuple-point representation on <see cref="ChartSeries"/>.
/// </summary>
public sealed class ChartSeriesPointsTests
{
    [Fact]
    public void FromPoints_StoresLabelAndPoints_DataEmpty()
    {
        var points = new List<ChartPoint> { ChartPoint.Xy(1d, 2d), ChartPoint.Xy(3d, 4d) };
        var series = ChartSeries.FromPoints("xy", points);

        Assert.Equal("xy", series.Label);
        Assert.Equal(2, series.Points.Count);
        Assert.Equal(1d, series.Points[0].X);
        Assert.Empty(series.Data);
        Assert.Null(series.DataExpression);
    }

    [Fact]
    public void FromInline_LeavesPointsEmpty()
    {
        var series = ChartSeries.FromInline("flat", new[] { 1d, 2d, 3d });
        Assert.Empty(series.Points);
        Assert.Equal(3, series.Data.Count);
    }

    [Fact]
    public void FromPoints_NullPoints_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => ChartSeries.FromPoints("x", null!));
    }
}
