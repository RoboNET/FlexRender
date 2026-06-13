using System.Text;
using FlexRender.Configuration;
using FlexRender.Parsing;
using FlexRender.Xml;
using Xunit;

namespace FlexRender.Tests.Parsing.Xml;

/// <summary>
/// Tests that ResourceLimits are enforced by the XML parser via the shared TemplateParser.
/// </summary>
public class XmlResourceLimitTests
{
    [Fact]
    public void Parse_TooManyShapes_Throws()
    {
        var limits = new ResourceLimits { MaxShapesPerDraw = 2 };
        var parser = new XmlTemplateParser(limits);

        var sb = new StringBuilder();
        sb.Append("<flexrender><canvas width=\"300\"/><draw><shapes>");
        for (var i = 0; i < 3; i++)
        {
            sb.Append("<rect x=\"0\" y=\"0\" width=\"1\" height=\"1\"/>");
        }
        sb.Append("</shapes></draw></flexrender>");

        var ex = Assert.Throws<TemplateParseException>(() => parser.Parse(sb.ToString()));
        Assert.Contains("exceeds the maximum", ex.Message, System.StringComparison.Ordinal);
    }

    [Fact]
    public void Parse_TooManySeries_Throws()
    {
        var limits = new ResourceLimits { MaxSeriesPerChart = 1 };
        var parser = new XmlTemplateParser(limits);

        const string xml = """
            <flexrender>
              <canvas width="300"/>
              <chart chart-type="bar">
                <series label="a" data="1,2"/>
                <series label="b" data="3,4"/>
              </chart>
            </flexrender>
            """;

        var ex = Assert.Throws<TemplateParseException>(() => parser.Parse(xml));
        Assert.Contains("exceeds the maximum", ex.Message, System.StringComparison.Ordinal);
    }
}
