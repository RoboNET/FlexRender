using FlexRender.Parsing;
using FlexRender.Parsing.Ast;
using Xunit;

namespace FlexRender.Tests.Parsing;

/// <summary>
/// Tests for parsing rect/circle/ellipse shape elements from YAML.
/// </summary>
public sealed class ShapeParserTests
{
    private readonly TemplateParser _parser = new();

    [Fact]
    public void Parse_Rect_SolidFillStrokeRadius()
    {
        var yaml = """
            canvas:
              width: 300
            layout:
              - type: rect
                width: 100
                height: 50
                fill: "#4A90D9"
                stroke: "#333333"
                stroke-width: 2
                radius: 4
            """;

        var template = _parser.Parse(yaml);
        var rect = Assert.IsType<RectElement>(template.Elements[0]);

        Assert.Equal("#4A90D9", rect.Fill.Value);
        Assert.Equal("#333333", rect.Stroke.Value);
        Assert.Equal(2f, rect.StrokeWidth.Value);
        Assert.Equal("4", rect.Radius.Value);
        Assert.Equal("100", rect.Width.Value);
        Assert.Equal("50", rect.Height.Value);
    }

    [Fact]
    public void Parse_Rect_GradientObjectFill_ConvertsToCss()
    {
        var yaml = """
            canvas:
              width: 300
            layout:
              - type: rect
                width: 100
                height: 100
                fill:
                  gradient: linear
                  colors: ["#f00", "#00f"]
                  angle: 45
            """;

        var template = _parser.Parse(yaml);
        var rect = Assert.IsType<RectElement>(template.Elements[0]);

        Assert.Equal("linear-gradient(45deg, #f00, #00f)", rect.Fill.Value);
    }

    [Fact]
    public void Parse_Circle_SizeShorthand_SetsWidthAndHeight()
    {
        var yaml = """
            canvas:
              width: 300
            layout:
              - type: circle
                size: 40
                fill: "#e74c3c"
            """;

        var template = _parser.Parse(yaml);
        var circle = Assert.IsType<CircleElement>(template.Elements[0]);

        Assert.Equal("40", circle.Width.Value);
        Assert.Equal("40", circle.Height.Value);
        Assert.Equal("#e74c3c", circle.Fill.Value);
    }

    [Fact]
    public void Parse_Ellipse_WidthHeightFill()
    {
        var yaml = """
            canvas:
              width: 300
            layout:
              - type: ellipse
                width: 120
                height: 60
                fill: "#2ecc71"
            """;

        var template = _parser.Parse(yaml);
        var ellipse = Assert.IsType<EllipseElement>(template.Elements[0]);

        Assert.Equal("120", ellipse.Width.Value);
        Assert.Equal("60", ellipse.Height.Value);
        Assert.Equal("#2ecc71", ellipse.Fill.Value);
    }

    [Fact]
    public void Parse_Rect_UnknownProperty_SuggestsCorrection()
    {
        var yaml = """
            canvas:
              width: 300
            layout:
              - type: rect
                fil: "#fff"
            """;

        var ex = Assert.Throws<TemplateParseException>(() => _parser.Parse(yaml));
        Assert.Contains("'fil'", ex.Message);
        Assert.Contains("fill", ex.Message);
    }
}
