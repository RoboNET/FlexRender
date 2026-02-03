using FlexRender.Layout;
using FlexRender.Parsing.Ast;
using Xunit;

namespace FlexRender.Tests.Parsing.Ast;

public class TextElementFlexTests
{
    [Fact]
    public void TextElement_HasFlexItemProperties()
    {
        var text = new TextElement();

        Assert.Equal(0f, text.Grow);
        Assert.Equal(1f, text.Shrink);
        Assert.Equal("auto", text.Basis);
        Assert.Equal(AlignSelf.Auto, text.AlignSelf);
        Assert.Equal(0, text.Order);
    }

    [Fact]
    public void TextElement_HasWidthAndHeight()
    {
        var text = new TextElement
        {
            Width = "50%",
            Height = "100"
        };

        Assert.Equal("50%", text.Width);
        Assert.Equal("100", text.Height);
    }
}
