using FlexRender.Parsing.Ast;
using FlexRender.Xml;
using Xunit;

namespace FlexRender.Tests.Parsing.Xml;

/// <summary>
/// Tests for shape elements (rect, circle, ellipse, draw) via the XML parser.
/// </summary>
public class XmlShapeTests
{
    private readonly XmlTemplateParser _parser = new();

    [Fact]
    public void Parse_RectCircleEllipse()
    {
        const string xml = """
            <flexrender>
              <canvas width="300"/>
              <rect fill="#4A90D9" stroke="#000000" stroke-width="2" radius="6" width="100" height="40"/>
              <circle fill="#ff0000" size="50"/>
              <ellipse fill="#00ff00" width="80" height="40"/>
            </flexrender>
            """;

        var elements = _parser.Parse(xml).Elements;
        var rect = Assert.IsType<RectElement>(elements[0]);
        Assert.Equal("#4A90D9", rect.Fill.Value);
        Assert.Equal(2f, rect.StrokeWidth.Value);

        Assert.IsType<CircleElement>(elements[1]);
        Assert.IsType<EllipseElement>(elements[2]);
    }

    [Fact]
    public void Parse_Draw_WithShapes()
    {
        const string xml = """
            <flexrender>
              <canvas width="300"/>
              <draw width="200" height="100">
                <shapes>
                  <line x1="0" y1="0" x2="100" y2="100" stroke="#000" stroke-width="2"/>
                  <rect x="10" y="10" width="50" height="30" fill="#eee"/>
                  <circle cx="80" cy="80" r="15" fill="#f00"/>
                  <polyline points="10,10; 50,50; 90,10" stroke="#00f" stroke-width="1"/>
                </shapes>
              </draw>
            </flexrender>
            """;

        var draw = Assert.IsType<DrawElement>(Assert.Single(_parser.Parse(xml).Elements));
        Assert.Equal(4, draw.Shapes.Count);
        Assert.IsType<DrawLine>(draw.Shapes[0]);
        Assert.IsType<DrawRect>(draw.Shapes[1]);
        Assert.IsType<DrawCircle>(draw.Shapes[2]);
        Assert.IsType<DrawPolyline>(draw.Shapes[3]);
    }
}
