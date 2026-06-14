using FlexRender.Charts;
using FlexRender.Parsing;
using FlexRender.Parsing.Ast;
using Xunit;

namespace FlexRender.Tests.Parsing;

/// <summary>
/// Tests for parsing scatter/bubble tuple series and the updated chart-type validation.
/// </summary>
public sealed class ChartScatterParsersTests
{
    private readonly TemplateParser _parser = new();

    [Fact]
    public void Parse_ScatterTuples_ProducesPoints()
    {
        const string yaml = """
            canvas:
              width: 400
            layout:
              - type: chart
                chart-type: scatter
                width: 400
                height: 300
                series:
                  - label: cloud
                    data: [[1, 10], [2, 25], [3, 18]]
            """;

        var template = _parser.Parse(yaml);
        var chart = Assert.IsType<ChartElement>(template.Elements[0]);

        Assert.Equal(ChartType.Scatter, chart.ChartType);
        var pts = chart.Series[0].Points;
        Assert.Equal(3, pts.Count);
        Assert.Equal(1d, pts[0].X);
        Assert.Equal(10d, pts[0].Y);
        Assert.Equal(0d, pts[0].R);
        Assert.Empty(chart.Series[0].Data);
    }

    [Fact]
    public void Parse_BubbleTriples_ProducesPointsWithRadius()
    {
        const string yaml = """
            canvas:
              width: 400
            layout:
              - type: chart
                chart-type: bubble
                width: 400
                height: 300
                series:
                  - data: [[1, 10, 5], [2, 25, 12]]
            """;

        var template = _parser.Parse(yaml);
        var chart = Assert.IsType<ChartElement>(template.Elements[0]);

        var pts = chart.Series[0].Points;
        Assert.Equal(2, pts.Count);
        Assert.Equal(5d, pts[0].R);
        Assert.Equal(12d, pts[1].R);
    }

    [Fact]
    public void Parse_ScatterTupleWrongArity_Throws()
    {
        const string yaml = """
            canvas:
              width: 400
            layout:
              - type: chart
                chart-type: scatter
                width: 400
                height: 300
                series:
                  - label: bad
                    data: [[1, 2, 3]]
            """;

        var ex = Assert.Throws<TemplateParseException>(() => _parser.Parse(yaml));
        Assert.Contains("bad", ex.Message);
    }

    [Fact]
    public void Parse_ScatterTupleNonNumeric_Throws()
    {
        const string yaml = """
            canvas:
              width: 400
            layout:
              - type: chart
                chart-type: scatter
                width: 400
                height: 300
                series:
                  - data: [[1, "x"]]
            """;

        Assert.Throws<TemplateParseException>(() => _parser.Parse(yaml));
    }

    [Fact]
    public void Parse_UnknownChartType_ErrorListsNewTypes()
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
        Assert.Contains("scatter", ex.Message);
    }
}
