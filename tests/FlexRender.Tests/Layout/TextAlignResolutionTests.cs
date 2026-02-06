using FlexRender.Layout;
using FlexRender.Parsing.Ast;
using Xunit;

namespace FlexRender.Tests.Layout;

/// <summary>
/// Tests for logical text alignment resolution (Start/End based on direction).
/// </summary>
public sealed class TextAlignResolutionTests
{
    [Fact]
    public void TextAlign_Start_EnumValueExists()
    {
        var align = TextAlign.Start;
        Assert.Equal(TextAlign.Start, align);
    }

    [Fact]
    public void TextAlign_End_EnumValueExists()
    {
        var align = TextAlign.End;
        Assert.Equal(TextAlign.End, align);
    }

    [Fact]
    public void ResolveLogicalAlign_Start_Ltr_ReturnsLeft()
    {
        var result = LayoutHelpers.ResolveLogicalAlign(TextAlign.Start, TextDirection.Ltr);
        Assert.Equal(TextAlign.Left, result);
    }

    [Fact]
    public void ResolveLogicalAlign_Start_Rtl_ReturnsRight()
    {
        var result = LayoutHelpers.ResolveLogicalAlign(TextAlign.Start, TextDirection.Rtl);
        Assert.Equal(TextAlign.Right, result);
    }

    [Fact]
    public void ResolveLogicalAlign_End_Ltr_ReturnsRight()
    {
        var result = LayoutHelpers.ResolveLogicalAlign(TextAlign.End, TextDirection.Ltr);
        Assert.Equal(TextAlign.Right, result);
    }

    [Fact]
    public void ResolveLogicalAlign_End_Rtl_ReturnsLeft()
    {
        var result = LayoutHelpers.ResolveLogicalAlign(TextAlign.End, TextDirection.Rtl);
        Assert.Equal(TextAlign.Left, result);
    }

    [Fact]
    public void ResolveLogicalAlign_Left_Rtl_StaysLeft()
    {
        var result = LayoutHelpers.ResolveLogicalAlign(TextAlign.Left, TextDirection.Rtl);
        Assert.Equal(TextAlign.Left, result);
    }

    [Fact]
    public void ResolveLogicalAlign_Right_Ltr_StaysRight()
    {
        var result = LayoutHelpers.ResolveLogicalAlign(TextAlign.Right, TextDirection.Ltr);
        Assert.Equal(TextAlign.Right, result);
    }

    [Fact]
    public void ResolveLogicalAlign_Center_Rtl_StaysCenter()
    {
        var result = LayoutHelpers.ResolveLogicalAlign(TextAlign.Center, TextDirection.Rtl);
        Assert.Equal(TextAlign.Center, result);
    }
}
