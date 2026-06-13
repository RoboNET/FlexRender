using System.Collections.Generic;
using FlexRender.Charts;
using FlexRender.Parsing.Ast;
using Xunit;

namespace FlexRender.Tests.Parsing.Ast;

/// <summary>
/// Tests for the gauge/progress single-value properties on <see cref="ChartElement"/>.
/// </summary>
public sealed class ChartElementGaugeTests
{
    [Fact]
    public void Defaults_ValueMaxLabelAreNull()
    {
        var chart = new ChartElement(ChartType.Gauge, new List<ChartSeries>());
        Assert.Null(chart.Value);
        Assert.Null(chart.Max);
        Assert.Null(chart.ValueLabel);
    }

    [Fact]
    public void CloneWithSubstitution_PreservesValueMaxLabel()
    {
        var chart = new ChartElement(ChartType.Progress, new List<ChartSeries>())
        {
            Value = 72d,
            Max = 100d,
            ValueLabel = "CPU"
        };

        var clone = (ChartElement)chart.CloneWithSubstitution(s => s);

        Assert.Equal(72d, clone.Value);
        Assert.Equal(100d, clone.Max);
        Assert.Equal("CPU", clone.ValueLabel);
    }
}
