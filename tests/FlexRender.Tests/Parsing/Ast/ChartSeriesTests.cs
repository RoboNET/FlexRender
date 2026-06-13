using System;
using FlexRender.Parsing.Ast;
using Xunit;

namespace FlexRender.Tests.Parsing.Ast;

/// <summary>
/// Tests for the <see cref="ChartSeries"/> DTO.
/// </summary>
public sealed class ChartSeriesTests
{
    [Fact]
    public void InlineData_StoresLabelAndValues()
    {
        var series = ChartSeries.FromInline("2024", new[] { 12d, 30d, 22d, 48d });

        Assert.Equal("2024", series.Label);
        Assert.Null(series.DataExpression);
        Assert.Equal(new[] { 12d, 30d, 22d, 48d }, series.Data);
    }

    [Fact]
    public void Expression_StoresExpressionAndEmptyData()
    {
        var series = ChartSeries.FromExpression("Sales", "{{ sales }}");

        Assert.Equal("Sales", series.Label);
        Assert.Equal("{{ sales }}", series.DataExpression);
        Assert.Empty(series.Data);
    }

    [Fact]
    public void WithData_ReplacesDataKeepingLabel()
    {
        var series = ChartSeries.FromExpression("Sales", "{{ sales }}");
        var resolved = series.WithData(new[] { 1d, 2d, 3d });

        Assert.Equal("Sales", resolved.Label);
        Assert.Equal("{{ sales }}", resolved.DataExpression);
        Assert.Equal(new[] { 1d, 2d, 3d }, resolved.Data);
    }

    [Fact]
    public void FromInline_NullData_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => ChartSeries.FromInline("x", null!));
    }
}
