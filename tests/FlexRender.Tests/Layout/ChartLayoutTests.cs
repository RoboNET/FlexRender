using System.Collections.Generic;
using FlexRender.Charts;
using FlexRender.Configuration;
using FlexRender.Layout;
using FlexRender.Parsing.Ast;
using Xunit;

namespace FlexRender.Tests.Layout;

/// <summary>
/// Layout tests for the chart leaf element.
/// </summary>
public sealed class ChartLayoutTests
{
    [Fact]
    public void Chart_WithExplicitSize_ProducesThatSize()
    {
        var chart = new ChartElement(ChartType.Bar, new List<ChartSeries>
        {
            ChartSeries.FromInline("a", new[] { 1d, 2d, 3d })
        })
        {
            Width = "600",
            Height = "300"
        };

        var template = new Template
        {
            Canvas = new CanvasSettings { Width = 800, Fixed = FixedDimension.Width }
        };
        template.AddElement(chart);

        var engine = new LayoutEngine(new ResourceLimits());
        var root = engine.ComputeLayout(template);
        var node = root.Children[0];

        Assert.Equal(600f, node.Width);
        Assert.Equal(300f, node.Height);
    }
}
