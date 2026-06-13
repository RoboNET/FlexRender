using System;
using System.Collections.Generic;
using FlexRender.Charts;
using FlexRender.Parsing.Ast;
using Xunit;

namespace FlexRender.Tests.Parsing.Ast;

/// <summary>
/// Tests for the heatmap label/cell-value properties on <see cref="ChartElement"/>.
/// </summary>
public sealed class ChartElementHeatmapTests
{
    [Fact]
    public void Defaults_LabelsEmpty_CellValuesFalse()
    {
        var chart = new ChartElement(ChartType.Heatmap, new List<ChartSeries>());
        Assert.Empty(chart.XLabels);
        Assert.Empty(chart.YLabels);
        Assert.False(chart.ShowCellValues);
    }

    [Fact]
    public void CloneWithSubstitution_PreservesLabelsAndCellValues()
    {
        var chart = new ChartElement(ChartType.Heatmap, new List<ChartSeries>())
        {
            XLabels = new[] { "Mon", "Tue" },
            YLabels = new[] { "AM", "PM" },
            ShowCellValues = true
        };

        var clone = (ChartElement)chart.CloneWithSubstitution(s => s);

        Assert.Equal(new[] { "Mon", "Tue" }, clone.XLabels);
        Assert.Equal(new[] { "AM", "PM" }, clone.YLabels);
        Assert.True(clone.ShowCellValues);
    }
}
