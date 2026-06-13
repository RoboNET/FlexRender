using FlexRender.Configuration;
using FlexRender.Parsing;
using FlexRender.Parsing.Ast;
using Xunit;

namespace FlexRender.Tests.Parsing;

/// <summary>
/// Tests for parsing the 'draw' element and its absolute-coordinate shapes.
/// </summary>
public sealed class DrawParserTests
{
    private readonly TemplateParser _parser = new();

    [Fact]
    public void Parse_Draw_AllShapeKinds()
    {
        var yaml = """
            canvas:
              width: 400
            layout:
              - type: draw
                width: 400
                height: 200
                shapes:
                  - line: {x1: 0, y1: 100, x2: 400, y2: 50, stroke: "#333", stroke-width: 2}
                  - polyline: {points: [[0, 10], [50, 40], [100, 20]], stroke: "#4A90D9"}
                  - rect: {x: 10, y: 10, width: 80, height: 40, fill: "#eee", radius: 4}
                  - circle: {cx: 200, cy: 75, r: 30, fill: "#e74c3c"}
                  - path: {d: "M 0 0 L 100 50 Q 150 0 200 50 Z", fill: "#2ecc71"}
            """;

        var template = _parser.Parse(yaml);
        var draw = Assert.IsType<DrawElement>(template.Elements[0]);

        Assert.Equal(5, draw.Shapes.Count);

        var line = Assert.IsType<DrawLine>(draw.Shapes[0]);
        Assert.Equal(0f, line.X1);
        Assert.Equal(400f, line.X2);
        Assert.Equal("#333", line.Stroke);
        Assert.Equal(2f, line.StrokeWidth);

        var polyline = Assert.IsType<DrawPolyline>(draw.Shapes[1]);
        Assert.Equal(3, polyline.Points.Count);
        Assert.Equal(50f, polyline.Points[1].X);
        Assert.Equal(40f, polyline.Points[1].Y);

        var rect = Assert.IsType<DrawRect>(draw.Shapes[2]);
        Assert.Equal(80f, rect.Width);
        Assert.Equal("#eee", rect.Fill);
        Assert.Equal(4f, rect.Radius);

        var circle = Assert.IsType<DrawCircle>(draw.Shapes[3]);
        Assert.Equal(200f, circle.Cx);
        Assert.Equal(30f, circle.R);

        var path = Assert.IsType<DrawPath>(draw.Shapes[4]);
        Assert.Equal("#2ecc71", path.Fill);
        Assert.True(path.Commands.Count >= 4);
    }

    [Fact]
    public void Parse_Draw_NoShapes_ProducesEmptyList()
    {
        var yaml = """
            canvas:
              width: 400
            layout:
              - type: draw
                width: 400
                height: 200
            """;

        var template = _parser.Parse(yaml);
        var draw = Assert.IsType<DrawElement>(template.Elements[0]);
        Assert.Empty(draw.Shapes);
    }

    [Fact]
    public void Parse_Draw_MalformedPath_ThrowsWithCommand()
    {
        var yaml = """
            canvas:
              width: 400
            layout:
              - type: draw
                width: 400
                height: 200
                shapes:
                  - path: {d: "M 0 0 X 1 1"}
            """;

        var ex = Assert.Throws<TemplateParseException>(() => _parser.Parse(yaml));
        Assert.Contains("'X'", ex.Message);
    }

    [Fact]
    public void Parse_Draw_ExceedsShapeLimit_Throws()
    {
        var limits = new ResourceLimits { MaxShapesPerDraw = 2 };
        var parser = new TemplateParser(limits);

        var yaml = """
            canvas:
              width: 400
            layout:
              - type: draw
                width: 400
                height: 200
                shapes:
                  - line: {x1: 0, y1: 0, x2: 1, y2: 1}
                  - line: {x1: 0, y1: 0, x2: 1, y2: 1}
                  - line: {x1: 0, y1: 0, x2: 1, y2: 1}
            """;

        var ex = Assert.Throws<TemplateParseException>(() => parser.Parse(yaml));
        Assert.Contains("shapes", ex.Message);
    }

    [Fact]
    public void Parse_Draw_UnknownProperty_Throws()
    {
        var yaml = """
            canvas:
              width: 400
            layout:
              - type: draw
                width: 400
                shaps: []
            """;

        Assert.Throws<TemplateParseException>(() => _parser.Parse(yaml));
    }
}
