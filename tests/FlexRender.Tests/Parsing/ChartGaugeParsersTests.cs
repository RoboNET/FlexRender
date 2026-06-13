using FlexRender.Charts;
using FlexRender.Parsing;
using FlexRender.Parsing.Ast;
using Xunit;

namespace FlexRender.Tests.Parsing;

/// <summary>
/// Tests for parsing gauge/progress value/max/label and property validation.
/// </summary>
public sealed class ChartGaugeParsersTests
{
    private readonly TemplateParser _parser = new();

    [Fact]
    public void Parse_Gauge_ReadsValueMaxLabel()
    {
        const string yaml = """
            canvas:
              width: 300
            layout:
              - type: chart
                chart-type: gauge
                width: 300
                height: 200
                value: 72
                max: 100
                label: CPU
            """;

        var template = _parser.Parse(yaml);
        var chart = Assert.IsType<ChartElement>(template.Elements[0]);

        Assert.Equal(ChartType.Gauge, chart.ChartType);
        Assert.Equal(72d, chart.Value);
        Assert.Equal(100d, chart.Value is null ? 0d : chart.Max);
        Assert.Equal("CPU", chart.ValueLabel);
    }

    [Fact]
    public void Parse_Progress_DefaultsMaxLabelToNull()
    {
        const string yaml = """
            canvas:
              width: 300
            layout:
              - type: chart
                chart-type: progress
                width: 300
                height: 80
                value: 40
            """;

        var template = _parser.Parse(yaml);
        var chart = Assert.IsType<ChartElement>(template.Elements[0]);

        Assert.Equal(40d, chart.Value);
        Assert.Null(chart.Max);
        Assert.Null(chart.ValueLabel);
    }

    [Fact]
    public void Parse_GaugeWithKnownProps_DoesNotThrow()
    {
        const string yaml = """
            canvas:
              width: 300
            layout:
              - type: chart
                chart-type: gauge
                width: 300
                height: 200
                value: 50
                max: 80
                label: Disk
                theme: dark
            """;

        var template = _parser.Parse(yaml);
        Assert.IsType<ChartElement>(template.Elements[0]);
    }

    [Fact]
    public void Parse_TypoInValue_Throws()
    {
        const string yaml = """
            canvas:
              width: 300
            layout:
              - type: chart
                chart-type: gauge
                width: 300
                height: 200
                valeu: 50
            """;

        var ex = Assert.Throws<TemplateParseException>(() => _parser.Parse(yaml));
        Assert.Contains("valeu", ex.Message);
    }
}
