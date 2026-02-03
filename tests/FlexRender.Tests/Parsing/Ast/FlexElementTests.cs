using FlexRender.Layout;
using FlexRender.Parsing.Ast;
using Xunit;

namespace FlexRender.Tests.Parsing.Ast;

public class FlexElementTests
{
    [Fact]
    public void Type_IsFlex()
    {
        var flex = new FlexElement();

        Assert.Equal(ElementType.Flex, flex.Type);
    }

    [Fact]
    public void Default_HasCorrectValues()
    {
        var flex = new FlexElement();

        Assert.Equal(FlexDirection.Column, flex.Direction);
        Assert.Equal(FlexWrap.NoWrap, flex.Wrap);
        Assert.Equal("0", flex.Gap);
        Assert.Equal("0", flex.Padding);
        Assert.Equal(JustifyContent.Start, flex.Justify);
        Assert.Equal(AlignItems.Stretch, flex.Align);
        Assert.Equal(AlignContent.Stretch, flex.AlignContent);
        Assert.Empty(flex.Children);
    }

    [Fact]
    public void FlexProperties_DefaultValues()
    {
        var flex = new FlexElement();

        Assert.Equal(0f, flex.Grow);
        Assert.Equal(1f, flex.Shrink);
        Assert.Equal("auto", flex.Basis);
        Assert.Equal(AlignSelf.Auto, flex.AlignSelf);
        Assert.Equal(0, flex.Order);
    }

    [Fact]
    public void Children_CanAddElements()
    {
        var flex = new FlexElement();
        flex.AddChild(new TextElement { Content = "Child 1" });
        flex.AddChild(new TextElement { Content = "Child 2" });

        Assert.Equal(2, flex.Children.Count);
    }

    [Fact]
    public void Children_CanSetFromList()
    {
        var flex = new FlexElement();
        var children = new List<TemplateElement>
        {
            new TextElement { Content = "Child 1" },
            new TextElement { Content = "Child 2" }
        };

        flex.Children = children;

        Assert.Equal(2, flex.Children.Count);
    }

    [Fact]
    public void Children_IsReadOnly_CannotModifyDirectly()
    {
        var flex = new FlexElement();
        flex.AddChild(new TextElement { Content = "Child 1" });

        // The returned list is IReadOnlyList, so it should not be modifiable
        Assert.IsAssignableFrom<IReadOnlyList<TemplateElement>>(flex.Children);
    }

    [Fact]
    public void AddChild_NullElement_ThrowsArgumentNullException()
    {
        var flex = new FlexElement();

        Assert.Throws<ArgumentNullException>(() => flex.AddChild(null!));
    }
}
