using FlexRender.Layout;
using FlexRender.Parsing.Ast;
using Xunit;

namespace FlexRender.Tests.Layout;

public sealed class LayoutNodeTextLinesTests
{
    [Fact]
    public void LayoutNode_TextLines_DefaultsToNull()
    {
        var node = new LayoutNode(new TextElement { Content = "Hello" }, 0, 0, 100, 20);

        Assert.Null(node.TextLines);
    }

    [Fact]
    public void LayoutNode_TextLines_CanBeSet()
    {
        var node = new LayoutNode(new TextElement { Content = "Hello World" }, 0, 0, 100, 40);
        var lines = new List<string> { "Hello", "World" };
        node.TextLines = lines;

        Assert.Equal(2, node.TextLines!.Count);
        Assert.Equal("Hello", node.TextLines[0]);
        Assert.Equal("World", node.TextLines[1]);
    }

    [Fact]
    public void LayoutNode_ComputedLineHeight_DefaultsToZero()
    {
        var node = new LayoutNode(new TextElement { Content = "Hello" }, 0, 0, 100, 20);

        Assert.Equal(0f, node.ComputedLineHeight);
    }

    [Fact]
    public void LayoutNode_ComputedLineHeight_CanBeSet()
    {
        var node = new LayoutNode(new TextElement { Content = "Hello" }, 0, 0, 100, 20);
        node.ComputedLineHeight = 18.5f;

        Assert.Equal(18.5f, node.ComputedLineHeight);
    }

    [Fact]
    public void LayoutNode_NonTextElement_TextLinesRemainsNull()
    {
        var node = new LayoutNode(new FlexElement(), 0, 0, 100, 100);

        Assert.Null(node.TextLines);
        Assert.Equal(0f, node.ComputedLineHeight);
    }
}
