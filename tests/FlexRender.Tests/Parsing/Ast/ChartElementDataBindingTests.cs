using System.Collections.Generic;
using FlexRender;
using FlexRender.Charts;
using FlexRender.Parsing.Ast;
using FlexRender.TemplateEngine;
using Xunit;

namespace FlexRender.Tests.Parsing.Ast;

/// <summary>
/// Tests for resolving data-bound chart series against the data context.
/// </summary>
public sealed class ChartElementDataBindingTests
{
    private static string PassthroughResolver(string raw, ObjectValue data) => raw;

    [Fact]
    public void ResolveExpressions_BoundSeries_ResolvesArrayFromContext()
    {
        var chart = new ChartElement(ChartType.Bar, new List<ChartSeries>
        {
            ChartSeries.FromExpression("Sales", "{{ sales }}")
        });

        var data = new ObjectValue
        {
            ["sales"] = new ArrayValue(new TemplateValue[]
            {
                new NumberValue(12m), new NumberValue(30m), new NumberValue(22m)
            })
        };

        chart.ResolveExpressions(PassthroughResolver, data);

        Assert.Equal(new[] { 12d, 30d, 22d }, chart.Series[0].Data);
    }

    [Fact]
    public void ResolveExpressions_InlineSeries_Unchanged()
    {
        var chart = new ChartElement(ChartType.Bar, new List<ChartSeries>
        {
            ChartSeries.FromInline("a", new[] { 1d, 2d, 3d })
        });

        chart.ResolveExpressions(PassthroughResolver, new ObjectValue());

        Assert.Equal(new[] { 1d, 2d, 3d }, chart.Series[0].Data);
    }

    [Fact]
    public void ResolveExpressions_MissingPath_ResolvesToEmptyData()
    {
        var chart = new ChartElement(ChartType.Line, new List<ChartSeries>
        {
            ChartSeries.FromExpression("x", "{{ nothere }}")
        });

        chart.ResolveExpressions(PassthroughResolver, new ObjectValue());

        Assert.Empty(chart.Series[0].Data);
    }

    [Fact]
    public void ResolveExpressions_NonNumericArrayItem_Throws()
    {
        var chart = new ChartElement(ChartType.Bar, new List<ChartSeries>
        {
            ChartSeries.FromExpression("Sales", "{{ sales }}")
        });

        var data = new ObjectValue
        {
            ["sales"] = new ArrayValue(new TemplateValue[]
            {
                new NumberValue(12m), new StringValue("oops")
            })
        };

        var ex = Assert.Throws<TemplateEngineException>(() => chart.ResolveExpressions(PassthroughResolver, data));
        Assert.Contains("Sales", ex.Message);
    }
}
