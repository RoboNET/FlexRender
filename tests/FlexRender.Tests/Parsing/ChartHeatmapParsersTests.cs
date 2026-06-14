using FlexRender.Charts;
using FlexRender.Parsing;
using FlexRender.Parsing.Ast;
using Xunit;

namespace FlexRender.Tests.Parsing;

/// <summary>
/// Tests for parsing heatmap labels / cell-values, radar series, and the updated chart-type validation.
/// </summary>
public sealed class ChartHeatmapParsersTests
{
    private readonly TemplateParser _parser = new();

    [Fact]
    public void Parse_Heatmap_ReadsLabelsAndCellValues()
    {
        const string yaml = """
            canvas:
              width: 400
            layout:
              - type: chart
                chart-type: heatmap
                width: 400
                height: 300
                x-labels: [Mon, Tue, Wed]
                y-labels: [AM, PM]
                cell-values: true
                series:
                  - data: [1, 2, 3]
                  - data: [4, 5, 6]
            """;

        var template = _parser.Parse(yaml);
        var chart = Assert.IsType<ChartElement>(template.Elements[0]);

        Assert.Equal(ChartType.Heatmap, chart.ChartType);
        Assert.Equal(new[] { "Mon", "Tue", "Wed" }, chart.XLabels);
        Assert.Equal(new[] { "AM", "PM" }, chart.YLabels);
        Assert.True(chart.ShowCellValues);
        Assert.Equal(2, chart.Series.Count);
        Assert.Equal(3, chart.Series[0].Data.Count);
    }

    [Fact]
    public void Parse_Heatmap_DefaultsLabelsEmptyCellValuesFalse()
    {
        const string yaml = """
            canvas:
              width: 400
            layout:
              - type: chart
                chart-type: heatmap
                width: 400
                height: 300
                series:
                  - data: [1, 2]
            """;

        var template = _parser.Parse(yaml);
        var chart = Assert.IsType<ChartElement>(template.Elements[0]);

        Assert.Empty(chart.XLabels);
        Assert.Empty(chart.YLabels);
        Assert.False(chart.ShowCellValues);
    }

    [Fact]
    public void Parse_Radar_ReadsCategoriesAndSeries()
    {
        const string yaml = """
            canvas:
              width: 400
            layout:
              - type: chart
                chart-type: radar
                width: 400
                height: 400
                categories: [Speed, Power, Range, Agility, Armor]
                series:
                  - label: A
                    data: [4, 3, 5, 2, 4]
                  - label: B
                    data: [3, 5, 2, 4, 3]
            """;

        var template = _parser.Parse(yaml);
        var chart = Assert.IsType<ChartElement>(template.Elements[0]);

        Assert.Equal(ChartType.Radar, chart.ChartType);
        Assert.Equal(5, chart.Categories.Count);
        Assert.Equal(2, chart.Series.Count);
        Assert.Equal(5, chart.Series[0].Data.Count);
    }

    [Fact]
    public void Parse_TypoInXLabels_Throws()
    {
        const string yaml = """
            canvas:
              width: 400
            layout:
              - type: chart
                chart-type: heatmap
                width: 400
                height: 300
                x-labesl: [Mon, Tue]
                series:
                  - data: [1, 2]
            """;

        var ex = Assert.Throws<TemplateParseException>(() => _parser.Parse(yaml));
        Assert.Contains("x-labesl", ex.Message);
    }

    [Fact]
    public void Parse_UnknownChartType_ErrorListsHeatmapAndRadar()
    {
        const string yaml = """
            canvas:
              width: 400
            layout:
              - type: chart
                chart-type: nope
                width: 400
                height: 300
                series:
                  - data: [1, 2]
            """;

        var ex = Assert.Throws<TemplateParseException>(() => _parser.Parse(yaml));
        Assert.Contains("heatmap", ex.Message);
        Assert.Contains("radar", ex.Message);
    }
}
