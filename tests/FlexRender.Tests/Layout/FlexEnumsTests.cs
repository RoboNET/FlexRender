using FlexRender.Layout;
using Xunit;

namespace FlexRender.Tests.Layout;

public class FlexEnumsTests
{
    [Fact]
    public void FlexDirection_HasRowAndColumn()
    {
        Assert.Equal(0, (int)FlexDirection.Row);
        Assert.Equal(1, (int)FlexDirection.Column);
    }

    [Fact]
    public void FlexWrap_HasAllValues()
    {
        Assert.Equal(0, (int)FlexWrap.NoWrap);
        Assert.Equal(1, (int)FlexWrap.Wrap);
        Assert.Equal(2, (int)FlexWrap.WrapReverse);
    }

    [Fact]
    public void JustifyContent_HasAllValues()
    {
        Assert.Equal(0, (int)JustifyContent.Start);
        Assert.Equal(1, (int)JustifyContent.Center);
        Assert.Equal(2, (int)JustifyContent.End);
        Assert.Equal(3, (int)JustifyContent.SpaceBetween);
        Assert.Equal(4, (int)JustifyContent.SpaceAround);
        Assert.Equal(5, (int)JustifyContent.SpaceEvenly);
    }

    [Fact]
    public void AlignItems_HasAllValues()
    {
        Assert.Equal(0, (int)AlignItems.Start);
        Assert.Equal(1, (int)AlignItems.Center);
        Assert.Equal(2, (int)AlignItems.End);
        Assert.Equal(3, (int)AlignItems.Stretch);
        Assert.Equal(4, (int)AlignItems.Baseline);
    }

    [Fact]
    public void AlignContent_HasAllValues()
    {
        Assert.Equal(0, (int)AlignContent.Start);
        Assert.Equal(1, (int)AlignContent.Center);
        Assert.Equal(2, (int)AlignContent.End);
        Assert.Equal(3, (int)AlignContent.Stretch);
        Assert.Equal(4, (int)AlignContent.SpaceBetween);
        Assert.Equal(5, (int)AlignContent.SpaceAround);
    }

    [Fact]
    public void AlignSelf_HasAutoValue()
    {
        Assert.Equal(0, (int)AlignSelf.Auto);
        Assert.Equal(1, (int)AlignSelf.Start);
        Assert.Equal(2, (int)AlignSelf.Center);
        Assert.Equal(3, (int)AlignSelf.End);
        Assert.Equal(4, (int)AlignSelf.Stretch);
        Assert.Equal(5, (int)AlignSelf.Baseline);
    }

    [Fact]
    public void TextDirection_Ltr_HasValueZero()
    {
        Assert.Equal(0, (int)TextDirection.Ltr);
    }

    [Fact]
    public void TextDirection_Rtl_HasValueOne()
    {
        Assert.Equal(1, (int)TextDirection.Rtl);
    }
}
