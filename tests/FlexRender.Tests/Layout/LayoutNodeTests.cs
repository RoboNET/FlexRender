using FlexRender.Layout;
using FlexRender.Parsing.Ast;
using Xunit;

namespace FlexRender.Tests.Layout;

public class LayoutNodeTests
{
    [Fact]
    public void Constructor_SetsAllValues()
    {
        var element = new TextElement { Content = "Test" };
        var node = new LayoutNode(element, x: 10, y: 20, width: 100, height: 50);

        Assert.Same(element, node.Element);
        Assert.Equal(10f, node.X);
        Assert.Equal(20f, node.Y);
        Assert.Equal(100f, node.Width);
        Assert.Equal(50f, node.Height);
    }

    [Fact]
    public void Right_ReturnsXPlusWidth()
    {
        var node = new LayoutNode(new TextElement(), x: 10, y: 0, width: 100, height: 0);

        Assert.Equal(110f, node.Right);
    }

    [Fact]
    public void Bottom_ReturnsYPlusHeight()
    {
        var node = new LayoutNode(new TextElement(), x: 0, y: 20, width: 0, height: 50);

        Assert.Equal(70f, node.Bottom);
    }

    [Fact]
    public void Children_InitiallyEmpty()
    {
        var node = new LayoutNode(new TextElement(), 0, 0, 100, 100);

        Assert.Empty(node.Children);
    }

    [Fact]
    public void AddChild_AddsToChildren()
    {
        var parent = new LayoutNode(new TextElement(), 0, 0, 200, 200);
        var child = new LayoutNode(new TextElement(), 10, 10, 50, 50);

        parent.AddChild(child);

        Assert.Single(parent.Children);
        Assert.Same(child, parent.Children[0]);
    }

    [Fact]
    public void Bounds_ReturnsRectangle()
    {
        var node = new LayoutNode(new TextElement(), x: 10, y: 20, width: 100, height: 50);

        var bounds = node.Bounds;

        Assert.Equal(10f, bounds.X);
        Assert.Equal(20f, bounds.Y);
        Assert.Equal(100f, bounds.Width);
        Assert.Equal(50f, bounds.Height);
    }
}
