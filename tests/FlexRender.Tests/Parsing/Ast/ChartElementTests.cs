using System.Collections.Generic;
using FlexRender.Charts;
using FlexRender.Parsing.Ast;
using Xunit;

namespace FlexRender.Tests.Parsing.Ast;

/// <summary>
/// Tests for the <see cref="ChartElement"/> AST class.
/// </summary>
public sealed class ChartElementTests
{
    private static ChartElement MakeChart()
    {
        var series = new List<ChartSeries>
        {
            ChartSeries.FromInline("2024", new[] { 12d, 30d, 22d, 48d })
        };
        return new ChartElement(ChartType.Bar, series)
        {
            Categories = new[] { "Q1", "Q2", "Q3", "Q4" },
            Width = "600",
            Height = "300",
            Legend = LegendPosition.Bottom,
            Title = "Revenue"
        };
    }

    [Fact]
    public void Type_IsChart()
    {
        Assert.Equal(ElementType.Chart, MakeChart().Type);
    }

    [Fact]
    public void ChartType_AndSeries_AreExposed()
    {
        var chart = MakeChart();
        Assert.Equal(ChartType.Bar, chart.ChartType);
        Assert.Single(chart.Series);
        Assert.Equal(4, chart.Categories.Count);
    }

    [Fact]
    public void Defaults_AreSensible()
    {
        var chart = new ChartElement(ChartType.Line, new List<ChartSeries>());
        Assert.Empty(chart.Series);
        Assert.Empty(chart.Categories);
        Assert.Null(chart.Title);
        Assert.False(chart.Horizontal);
        Assert.False(chart.Stacked);
        Assert.False(chart.Smooth);
        Assert.False(chart.ShowPoints);
        Assert.Equal(PieLabelMode.Percent, chart.PieLabels);
    }

    [Fact]
    public void CloneWithSubstitution_PreservesChartState()
    {
        var clone = (ChartElement)MakeChart().CloneWithSubstitution(s => s);

        Assert.Equal(ChartType.Bar, clone.ChartType);
        Assert.Single(clone.Series);
        Assert.Equal("Revenue", clone.Title);
        Assert.Equal("600", clone.Width.Value);
        Assert.Equal(LegendPosition.Bottom, clone.Legend);
    }

    [Fact]
    public void Constructor_NullSeries_Throws()
    {
        Assert.Throws<System.ArgumentNullException>(() => new ChartElement(ChartType.Bar, null!));
    }
}
