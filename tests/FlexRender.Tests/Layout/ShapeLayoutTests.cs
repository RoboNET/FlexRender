using FlexRender.Configuration;
using FlexRender.Layout;
using FlexRender.Parsing.Ast;
using Xunit;

namespace FlexRender.Tests.Layout;

/// <summary>
/// Layout tests for the shape leaf elements (rect, circle, ellipse, draw).
/// </summary>
public sealed class ShapeLayoutTests
{
    private static LayoutEngine CreateEngine() => new(new ResourceLimits());

    [Fact]
    public void Rect_WithExplicitSize_ProducesThatSize()
    {
        var rect = new RectElement { Width = "100", Height = "50", Fill = "#ff0000" };
        var template = new Template
        {
            Canvas = new CanvasSettings { Width = 300, Fixed = FixedDimension.Width }
        };
        template.AddElement(rect);

        var engine = CreateEngine();
        var root = engine.ComputeLayout(template);
        var node = root.Children[0];

        Assert.Equal(100f, node.Width);
        Assert.Equal(50f, node.Height);
    }

    [Fact]
    public void Draw_WithExplicitSize_ProducesThatSize()
    {
        var draw = new DrawElement(new[] { (DrawShape)new DrawLine(0f, 0f, 10f, 10f, "#000", 1f) })
        {
            Width = "400",
            Height = "200"
        };
        var template = new Template
        {
            Canvas = new CanvasSettings { Width = 400, Fixed = FixedDimension.Width }
        };
        template.AddElement(draw);

        var engine = CreateEngine();
        var root = engine.ComputeLayout(template);
        var node = root.Children[0];

        Assert.Equal(400f, node.Width);
        Assert.Equal(200f, node.Height);
    }
}
