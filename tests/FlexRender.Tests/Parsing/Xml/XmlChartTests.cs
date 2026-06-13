using FlexRender.Charts;
using FlexRender.Parsing.Ast;
using FlexRender.Xml;
using Xunit;

namespace FlexRender.Tests.Parsing.Xml;

/// <summary>
/// Tests for the chart element via the XML parser, covering series collection, inline and tuple
/// data, categories, palette, gauge scalars, and heatmap rows/labels. Each case asserts the
/// resulting <see cref="ChartElement"/> matches the equivalent YAML chart AST.
/// </summary>
public class XmlChartTests
{
    private readonly XmlTemplateParser _parser = new();

    [Fact]
    public void Parse_BarChart_SeriesAndCategories()
    {
        const string xml = """
            <flexrender>
              <canvas width="400"/>
              <chart chart-type="bar" title="Sales" legend="bottom">
                <categories>
                  <item>Q1</item><item>Q2</item><item>Q3</item><item>Q4</item>
                </categories>
                <series label="2024" data="12,30,22,48"/>
                <series label="2025" data="18,26,31,40"/>
              </chart>
            </flexrender>
            """;

        var chart = Assert.IsType<ChartElement>(Assert.Single(_parser.Parse(xml).Elements));
        Assert.Equal(ChartType.Bar, chart.ChartType);
        Assert.Equal("Sales", chart.Title);
        Assert.Equal(new[] { "Q1", "Q2", "Q3", "Q4" }, chart.Categories);
        Assert.Equal(2, chart.Series.Count);
        Assert.Equal("2024", chart.Series[0].Label);
        Assert.Equal(new[] { 12d, 30d, 22d, 48d }, chart.Series[0].Data);
        Assert.Equal("2025", chart.Series[1].Label);
        Assert.Equal(new[] { 18d, 26d, 31d, 40d }, chart.Series[1].Data);
    }

    [Fact]
    public void Parse_NamedPalette_Attribute()
    {
        const string xml = """
            <flexrender>
              <canvas width="400"/>
              <chart chart-type="pie" palette="ocean">
                <series data="10,20,30"/>
              </chart>
            </flexrender>
            """;

        var chart = Assert.IsType<ChartElement>(Assert.Single(_parser.Parse(xml).Elements));
        Assert.NotNull(chart.Palette);
    }

    [Fact]
    public void Parse_ExplicitColorPalette_List()
    {
        const string xml = """
            <flexrender>
              <canvas width="400"/>
              <chart chart-type="pie" palette="#ff0000,#00ff00,#0000ff">
                <series data="10,20,30"/>
              </chart>
            </flexrender>
            """;

        var chart = Assert.IsType<ChartElement>(Assert.Single(_parser.Parse(xml).Elements));
        Assert.NotNull(chart.Palette);
        Assert.Equal(new[] { "#ff0000", "#00ff00", "#0000ff" }, chart.Palette!.Colors);
    }

    [Fact]
    public void Parse_ScatterTupleData()
    {
        const string xml = """
            <flexrender>
              <canvas width="400"/>
              <chart chart-type="scatter">
                <series label="pts" points="1,2; 3,4; 5,6"/>
              </chart>
            </flexrender>
            """;

        var chart = Assert.IsType<ChartElement>(Assert.Single(_parser.Parse(xml).Elements));
        Assert.Equal(ChartType.Scatter, chart.ChartType);
        var series = Assert.Single(chart.Series);
        Assert.Equal("pts", series.Label);
        Assert.Equal(3, series.Points.Count);
        Assert.Equal(1d, series.Points[0].X);
        Assert.Equal(2d, series.Points[0].Y);
        Assert.Equal(3d, series.Points[1].X);
        Assert.Equal(4d, series.Points[1].Y);
        Assert.Equal(5d, series.Points[2].X);
        Assert.Equal(6d, series.Points[2].Y);
    }

    [Fact]
    public void Parse_Gauge_ValueAndMax()
    {
        const string xml = """
            <flexrender>
              <canvas width="400"/>
              <chart chart-type="gauge" value="72" max="100" label="CPU"/>
            </flexrender>
            """;

        var chart = Assert.IsType<ChartElement>(Assert.Single(_parser.Parse(xml).Elements));
        Assert.Equal(ChartType.Gauge, chart.ChartType);
        Assert.Equal(72d, chart.Value);
        Assert.Equal(100d, chart.Max);
        Assert.Equal("CPU", chart.ValueLabel);
    }

    [Fact]
    public void Parse_Heatmap_RowsAndLabels()
    {
        const string xml = """
            <flexrender>
              <canvas width="400"/>
              <chart chart-type="heatmap">
                <x-labels>
                  <item>Mon</item><item>Tue</item><item>Wed</item>
                </x-labels>
                <y-labels>
                  <item>AM</item><item>PM</item>
                </y-labels>
                <series data="2,8,4"/>
                <series data="6,1,9"/>
              </chart>
            </flexrender>
            """;

        var chart = Assert.IsType<ChartElement>(Assert.Single(_parser.Parse(xml).Elements));
        Assert.Equal(ChartType.Heatmap, chart.ChartType);
        Assert.Equal(new[] { "Mon", "Tue", "Wed" }, chart.XLabels);
        Assert.Equal(new[] { "AM", "PM" }, chart.YLabels);
        Assert.Equal(2, chart.Series.Count);
        Assert.Equal(new[] { 2d, 8d, 4d }, chart.Series[0].Data);
        Assert.Equal(new[] { 6d, 1d, 9d }, chart.Series[1].Data);
    }
}
