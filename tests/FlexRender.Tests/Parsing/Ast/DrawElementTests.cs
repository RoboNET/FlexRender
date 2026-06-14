using System.Collections.Generic;
using FlexRender.Parsing.Ast;
using Xunit;

namespace FlexRender.Tests.Parsing.Ast;

/// <summary>
/// Tests for the <see cref="DrawElement"/> AST class.
/// </summary>
public sealed class DrawElementTests
{
    [Fact]
    public void Type_IsDraw()
    {
        var draw = new DrawElement(new List<DrawShape>());
        Assert.Equal(ElementType.Draw, draw.Type);
    }

    [Fact]
    public void Shapes_AreExposedInOrder()
    {
        var shapes = new List<DrawShape>
        {
            new DrawLine(0f, 0f, 10f, 10f, "#000000", 1f),
            new DrawCircle(5f, 5f, 3f, "#ff0000", null, 0f)
        };
        var draw = new DrawElement(shapes) { Width = "400", Height = "200" };

        Assert.Equal(2, draw.Shapes.Count);
        Assert.IsType<DrawLine>(draw.Shapes[0]);
        Assert.IsType<DrawCircle>(draw.Shapes[1]);
        Assert.Equal("400", draw.Width.Value);
    }

    [Fact]
    public void CloneWithSubstitution_PreservesShapes()
    {
        var shapes = new List<DrawShape> { new DrawLine(0f, 0f, 10f, 10f, "#000000", 1f) };
        var draw = new DrawElement(shapes) { Width = "400", Height = "200" };

        var clone = (DrawElement)draw.CloneWithSubstitution(s => s);

        Assert.Single(clone.Shapes);
        Assert.Equal("400", clone.Width.Value);
        Assert.Equal("200", clone.Height.Value);
    }

    [Fact]
    public void Constructor_NullShapes_Throws()
    {
        Assert.Throws<System.ArgumentNullException>(() => new DrawElement(null!));
    }
}
