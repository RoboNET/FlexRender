using FlexRender.Parsing;
using Xunit;

namespace FlexRender.Tests.Parsing;

/// <summary>
/// Tests for chart property validation and typo suggestions.
/// </summary>
public sealed class ChartKnownPropertiesTests
{
    private readonly TemplateParser _parser = new();

    [Fact]
    public void Parse_TypoInChartType_SuggestsCorrection()
    {
        const string yaml = """
            canvas:
              width: 600
            layout:
              - type: chart
                chart-typ: bar
                width: 600
                height: 300
                series:
                  - data: [1, 2]
            """;

        var ex = Assert.Throws<TemplateParseException>(() => _parser.Parse(yaml));
        Assert.Contains("chart-type", ex.Message);
    }

    [Fact]
    public void Parse_UnknownChartProperty_Throws()
    {
        const string yaml = """
            canvas:
              width: 600
            layout:
              - type: chart
                chart-type: bar
                width: 600
                height: 300
                bogus: 1
                series:
                  - data: [1, 2]
            """;

        var ex = Assert.Throws<TemplateParseException>(() => _parser.Parse(yaml));
        Assert.Contains("bogus", ex.Message);
    }

    [Fact]
    public void Parse_AllKnownChartProps_DoesNotThrow()
    {
        const string yaml = """
            canvas:
              width: 600
            layout:
              - type: chart
                chart-type: bar
                width: 600
                height: 300
                categories: [A, B]
                series:
                  - label: x
                    data: [1, 2]
                palette: ocean
                theme: dark
                legend: top
                title: T
                horizontal: true
                stacked: true
                smooth: false
                points: false
                labels: percent
            """;

        var template = _parser.Parse(yaml);
        Assert.NotNull(template);
    }
}
