using FlexRender.Parsing;
using FlexRender.Parsing.Ast;
using FlexRender.Xml;
using Xunit;

namespace FlexRender.Tests.Parsing.Xml;

/// <summary>
/// Cross-checks that XML and YAML parsers produce equivalent ASTs for the same template.
/// </summary>
public class XmlYamlEquivalenceTests
{
    private const string Yaml = """
        canvas:
          width: 400
        layout:
          - type: flex
            direction: row
            gap: 8
            children:
              - type: text
                content: "Quarterly sales"
                size: 1.2em
              - type: chart
                chart-type: bar
                categories: [Q1, Q2, Q3, Q4]
                series:
                  - label: "2024"
                    data: [12, 30, 22, 48]
                  - label: "2025"
                    data: [18, 26, 31, 40]
        """;

    private const string Xml = """
        <flexrender>
          <canvas width="400"/>
          <flex direction="row" gap="8">
            <text size="1.2em">Quarterly sales</text>
            <chart chart-type="bar">
              <categories>
                <item>Q1</item><item>Q2</item><item>Q3</item><item>Q4</item>
              </categories>
              <series label="2024" data="12,30,22,48"/>
              <series label="2025" data="18,26,31,40"/>
            </chart>
          </flex>
        </flexrender>
        """;

    [Fact]
    public void XmlAndYaml_ProduceEquivalentAst()
    {
        var fromYaml = new TemplateParser().Parse(Yaml);
        var fromXml = new XmlTemplateParser().Parse(Xml);

        Assert.Equal(fromYaml.Canvas.Width, fromXml.Canvas.Width);

        var yamlFlex = Assert.IsType<FlexElement>(Assert.Single(fromYaml.Elements));
        var xmlFlex = Assert.IsType<FlexElement>(Assert.Single(fromXml.Elements));
        Assert.Equal(yamlFlex.Direction, xmlFlex.Direction);
        Assert.Equal(yamlFlex.Children.Count, xmlFlex.Children.Count);

        var yamlText = Assert.IsType<TextElement>(yamlFlex.Children[0]);
        var xmlText = Assert.IsType<TextElement>(xmlFlex.Children[0]);
        Assert.Equal(yamlText.Content, xmlText.Content);
        Assert.Equal(yamlText.Size.Value, xmlText.Size.Value);

        var yamlChart = Assert.IsType<ChartElement>(yamlFlex.Children[1]);
        var xmlChart = Assert.IsType<ChartElement>(xmlFlex.Children[1]);
        Assert.Equal(yamlChart.ChartType, xmlChart.ChartType);
        Assert.Equal(yamlChart.Categories, xmlChart.Categories);
        Assert.Equal(yamlChart.Series.Count, xmlChart.Series.Count);
        Assert.Equal(yamlChart.Series[0].Label, xmlChart.Series[0].Label);
        Assert.Equal(yamlChart.Series[0].Data, xmlChart.Series[0].Data);
        Assert.Equal(yamlChart.Series[1].Data, xmlChart.Series[1].Data);
    }
}
