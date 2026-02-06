using FlexRender.Layout;
using FlexRender.Parsing.Ast;
using Xunit;

namespace FlexRender.Tests.Layout;

/// <summary>
/// Tests for text direction resolution logic.
/// </summary>
public class DirectionResolutionTests
{
    [Fact]
    public void ResolveDirection_ElementOverride_WinsOverInherited()
    {
        var element = new TextElement { Content = "test", TextDirection = TextDirection.Rtl };
        var result = LayoutHelpers.ResolveDirection(element, TextDirection.Ltr);
        Assert.Equal(TextDirection.Rtl, result);
    }

    [Fact]
    public void ResolveDirection_NullElement_InheritsParent()
    {
        var element = new TextElement { Content = "test" }; // TextDirection is null
        var result = LayoutHelpers.ResolveDirection(element, TextDirection.Rtl);
        Assert.Equal(TextDirection.Rtl, result);
    }

    [Fact]
    public void ResolveDirection_ExplicitLtr_OverridesRtlParent()
    {
        var element = new TextElement { Content = "test", TextDirection = TextDirection.Ltr };
        var result = LayoutHelpers.ResolveDirection(element, TextDirection.Rtl);
        Assert.Equal(TextDirection.Ltr, result);
    }

    [Fact]
    public void ResolveDirection_NullDir_InheritsLtr()
    {
        var element = new FlexElement(); // TextDirection is null
        var result = LayoutHelpers.ResolveDirection(element, TextDirection.Ltr);
        Assert.Equal(TextDirection.Ltr, result);
    }
}
