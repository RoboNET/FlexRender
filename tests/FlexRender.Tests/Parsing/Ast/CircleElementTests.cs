using FlexRender.Parsing.Ast;
using Xunit;

namespace FlexRender.Tests.Parsing.Ast;

/// <summary>
/// Tests for the <see cref="CircleElement"/> AST class.
/// </summary>
public sealed class CircleElementTests
{
    [Fact]
    public void Type_IsCircle()
    {
        var circle = new CircleElement();
        Assert.Equal(ElementType.Circle, circle.Type);
    }

    [Fact]
    public void CloneWithSubstitution_CopiesShapeProperties()
    {
        var circle = new CircleElement
        {
            Fill = "#e74c3c",
            Stroke = "#000000",
            StrokeWidth = 1.5f,
            Width = "40",
            Height = "40"
        };

        var clone = (CircleElement)circle.CloneWithSubstitution(s => s);

        Assert.Equal("#e74c3c", clone.Fill.Value);
        Assert.Equal("#000000", clone.Stroke.Value);
        Assert.Equal(1.5f, clone.StrokeWidth.Value);
        Assert.Equal("40", clone.Width.Value);
        Assert.Equal("40", clone.Height.Value);
    }
}
