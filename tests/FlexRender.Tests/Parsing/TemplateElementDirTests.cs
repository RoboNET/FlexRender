using FlexRender.Layout;
using FlexRender.Parsing.Ast;
using Xunit;

namespace FlexRender.Tests.Parsing;

public class TemplateElementDirTests
{
    [Fact]
    public void TemplateElement_Dir_DefaultsToNull()
    {
        var element = new TextElement { Content = "test" };
        Assert.Null(element.TextDirection.Value);
    }

    [Fact]
    public void TemplateElement_Dir_CanBeSetToRtl()
    {
        var element = new TextElement { Content = "test", TextDirection = TextDirection.Rtl };
        Assert.Equal(TextDirection.Rtl, element.TextDirection);
    }

    [Fact]
    public void TemplateElement_Dir_CanBeSetToLtr()
    {
        var element = new FlexElement { TextDirection = TextDirection.Ltr };
        Assert.Equal(TextDirection.Ltr, element.TextDirection);
    }

    [Fact]
    public void TemplateElement_Dir_NullMeansInherit()
    {
        var element = new TextElement { Content = "test" };
        // Null means "inherit from parent/canvas"
        Assert.Null(element.TextDirection.Value);
    }
}
