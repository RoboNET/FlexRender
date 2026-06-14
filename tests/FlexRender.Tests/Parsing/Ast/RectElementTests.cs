using FlexRender.Parsing.Ast;
using Xunit;

namespace FlexRender.Tests.Parsing.Ast;

/// <summary>
/// Tests for the <see cref="RectElement"/> AST class.
/// </summary>
public sealed class RectElementTests
{
    [Fact]
    public void Type_IsRect()
    {
        var rect = new RectElement();
        Assert.Equal(ElementType.Rect, rect.Type);
    }

    [Fact]
    public void Defaults_AreEmpty()
    {
        var rect = new RectElement();
        Assert.Null(rect.Fill.Value);
        Assert.Null(rect.Stroke.Value);
        Assert.Equal(0f, rect.StrokeWidth.Value);
        Assert.Null(rect.Radius.Value);
    }

    [Fact]
    public void CloneWithSubstitution_CopiesShapeProperties()
    {
        var rect = new RectElement
        {
            Fill = "#4A90D9",
            Stroke = "#333333",
            StrokeWidth = 2f,
            Radius = "4",
            Width = "100",
            Height = "50"
        };

        var clone = (RectElement)rect.CloneWithSubstitution(s => s);

        Assert.Equal("#4A90D9", clone.Fill.Value);
        Assert.Equal("#333333", clone.Stroke.Value);
        Assert.Equal(2f, clone.StrokeWidth.Value);
        Assert.Equal("4", clone.Radius.Value);
        Assert.Equal("100", clone.Width.Value);
        Assert.Equal("50", clone.Height.Value);
    }
}
