using FlexRender.Parsing.Ast;
using Xunit;

namespace FlexRender.Tests.Parsing.Ast;

/// <summary>
/// Tests for the <see cref="EllipseElement"/> AST class.
/// </summary>
public sealed class EllipseElementTests
{
    [Fact]
    public void Type_IsEllipse()
    {
        var ellipse = new EllipseElement();
        Assert.Equal(ElementType.Ellipse, ellipse.Type);
    }

    [Fact]
    public void CloneWithSubstitution_CopiesShapeProperties()
    {
        var ellipse = new EllipseElement
        {
            Fill = "#2ecc71",
            Stroke = "#111111",
            StrokeWidth = 3f,
            Width = "120",
            Height = "60"
        };

        var clone = (EllipseElement)ellipse.CloneWithSubstitution(s => s);

        Assert.Equal("#2ecc71", clone.Fill.Value);
        Assert.Equal("#111111", clone.Stroke.Value);
        Assert.Equal(3f, clone.StrokeWidth.Value);
        Assert.Equal("120", clone.Width.Value);
        Assert.Equal("60", clone.Height.Value);
    }
}
