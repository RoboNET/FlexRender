using FlexRender;
using FlexRender.Charts;
using FlexRender.Configuration;
using FlexRender.Parsing;
using FlexRender.Parsing.Ast;
using FlexRender.TemplateEngine;
using Xunit;

namespace FlexRender.Tests.Parsing;

/// <summary>
/// Tests for parsing the chart element from YAML.
/// </summary>
public sealed class ChartParsersTests
{
    private readonly TemplateParser _parser = new();

    [Fact]
    public void Parse_BarChartWithInlineSeries_ProducesChartElement()
    {
        const string yaml = """
            canvas:
              width: 600
            layout:
              - type: chart
                chart-type: bar
                width: 600
                height: 300
                categories: [Q1, Q2, Q3, Q4]
                series:
                  - label: "2024"
                    data: [12, 30, 22, 48]
                palette: ocean
                legend: bottom
                title: Revenue
            """;

        var template = _parser.Parse(yaml);
        var chart = Assert.IsType<ChartElement>(template.Elements[0]);

        Assert.Equal(ChartType.Bar, chart.ChartType);
        Assert.Equal(new[] { "Q1", "Q2", "Q3", "Q4" }, chart.Categories);
        Assert.Single(chart.Series);
        Assert.Equal("2024", chart.Series[0].Label);
        Assert.Equal(new[] { 12d, 30d, 22d, 48d }, chart.Series[0].Data);
        Assert.NotNull(chart.Palette);
        Assert.Equal(LegendPosition.Bottom, chart.Legend);
        Assert.Equal("Revenue", chart.Title);
    }

    [Fact]
    public void Parse_SeriesWithExpression_StoresExpressionNotData()
    {
        const string yaml = """
            canvas:
              width: 600
            layout:
              - type: chart
                chart-type: line
                width: 600
                height: 300
                series:
                  - label: Sales
                    data: "{{ sales }}"
            """;

        var template = _parser.Parse(yaml);
        var chart = Assert.IsType<ChartElement>(template.Elements[0]);

        Assert.Equal("{{ sales }}", chart.Series[0].DataExpression);
        Assert.Empty(chart.Series[0].Data);
    }

    [Fact]
    public void Parse_ExplicitColorListPalette_IsAccepted()
    {
        const string yaml = """
            canvas:
              width: 400
            layout:
              - type: chart
                chart-type: pie
                width: 400
                height: 400
                categories: [A, B, C]
                series:
                  - data: [10, 20, 30]
                palette: ["#264653", "#2a9d8f", "#e9c46a"]
            """;

        var template = _parser.Parse(yaml);
        var chart = Assert.IsType<ChartElement>(template.Elements[0]);

        Assert.NotNull(chart.Palette);
        Assert.Equal("#264653", chart.Palette!.ColorAt(0));
    }

    [Fact]
    public void Parse_BarTypeSpecificProps_AreApplied()
    {
        const string yaml = """
            canvas:
              width: 600
            layout:
              - type: chart
                chart-type: bar
                width: 600
                height: 300
                horizontal: true
                stacked: true
                series:
                  - data: [1, 2, 3]
            """;

        var template = _parser.Parse(yaml);
        var chart = Assert.IsType<ChartElement>(template.Elements[0]);

        Assert.True(chart.Horizontal);
        Assert.True(chart.Stacked);
    }

    [Fact]
    public void Parse_UnknownChartType_Throws()
    {
        const string yaml = """
            canvas:
              width: 600
            layout:
              - type: chart
                chart-type: spider
                width: 600
                height: 300
                series:
                  - data: [1, 2]
            """;

        var ex = Assert.Throws<TemplateParseException>(() => _parser.Parse(yaml));
        Assert.Contains("chart-type", ex.Message);
    }

    [Fact]
    public void Parse_BoundSeriesResolvingOverLimit_ThrowsOnResolveExpressions()
    {
        var limits = new ResourceLimits { MaxDataPointsPerSeries = 2 };
        var parser = new TemplateParser(limits);

        const string yaml = """
            canvas:
              width: 600
            layout:
              - type: chart
                chart-type: bar
                width: 600
                height: 300
                series:
                  - label: Big
                    data: "{{ big }}"
            """;

        var template = parser.Parse(yaml);
        var chart = Assert.IsType<ChartElement>(template.Elements[0]);

        var data = new ObjectValue
        {
            ["big"] = new ArrayValue(new TemplateValue[]
            {
                new NumberValue(1m), new NumberValue(2m), new NumberValue(3m)
            })
        };

        var ex = Assert.Throws<TemplateEngineException>(
            () => chart.ResolveExpressions(static (raw, _) => raw, data));
        Assert.Contains("maximum", ex.Message);
        Assert.Contains("Big", ex.Message);
    }
}
