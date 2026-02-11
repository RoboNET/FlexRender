using FlexRender.Parsing.Ast;
using Xunit;

namespace FlexRender.Tests.Parsing.Ast;

public class TemplateElementTests
{
    [Fact]
    public void Background_DefaultsToNull()
    {
        var element = new TextElement();
        Assert.Null(element.Background.Value);
    }

    [Fact]
    public void Background_CanBeSet()
    {
        var element = new TextElement { Background = "#ff0000" };
        Assert.Equal("#ff0000", element.Background);
    }

    [Fact]
    public void Background_WorksOnFlexElement()
    {
        var element = new FlexElement { Background = "#000000" };
        Assert.Equal("#000000", element.Background);
    }

    [Fact]
    public void Background_WorksOnImageElement()
    {
        var element = new ImageElement { Background = "#ffffff" };
        Assert.Equal("#ffffff", element.Background);
    }

    [Fact]
    public void Padding_DefaultsToZero()
    {
        var element = new TextElement();
        Assert.Equal("0", element.Padding);
    }

    [Fact]
    public void Padding_CanBeSet()
    {
        var element = new TextElement { Padding = "10px" };
        Assert.Equal("10px", element.Padding);
    }

    [Fact]
    public void Padding_WorksOnImageElement()
    {
        var element = new ImageElement { Padding = "5" };
        Assert.Equal("5", element.Padding);
    }

    [Fact]
    public void Padding_WorksOnFlexElement()
    {
        var element = new FlexElement { Padding = "12" };
        Assert.Equal("12", element.Padding);
    }

    [Fact]
    public void Margin_DefaultsToZero()
    {
        var element = new TextElement();
        Assert.Equal("0", element.Margin);
    }

    [Fact]
    public void Margin_CanBeSet()
    {
        var element = new TextElement { Margin = "10px" };
        Assert.Equal("10px", element.Margin);
    }

    [Fact]
    public void Margin_WorksOnFlexElement()
    {
        var element = new FlexElement { Margin = "8" };
        Assert.Equal("8", element.Margin);
    }

    [Fact]
    public void Margin_WorksOnImageElement()
    {
        var element = new ImageElement { Margin = "5" };
        Assert.Equal("5", element.Margin);
    }
}
