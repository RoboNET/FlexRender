using FlexRender.Layout;
using FlexRender.Parsing.Ast;
using Xunit;

namespace FlexRender.Tests.Layout;

public class LayoutContextTests
{
    [Fact]
    public void Constructor_SetsAllValues()
    {
        var context = new LayoutContext(
            containerWidth: 300,
            containerHeight: 400,
            fontSize: 16
        );

        Assert.Equal(300f, context.ContainerWidth);
        Assert.Equal(400f, context.ContainerHeight);
        Assert.Equal(16f, context.FontSize);
    }

    [Fact]
    public void WithSize_ReturnsNewContext()
    {
        var original = new LayoutContext(300, 400, 16);

        var modified = original.WithSize(200, 300);

        Assert.Equal(300f, original.ContainerWidth);
        Assert.Equal(200f, modified.ContainerWidth);
        Assert.Equal(300f, modified.ContainerHeight);
        Assert.Equal(16f, modified.FontSize);
    }

    [Fact]
    public void WithFontSize_ReturnsNewContext()
    {
        var original = new LayoutContext(300, 400, 16);

        var modified = original.WithFontSize(24);

        Assert.Equal(16f, original.FontSize);
        Assert.Equal(24f, modified.FontSize);
    }

    [Fact]
    public void ResolveWidth_CalculatesFromUnit()
    {
        var context = new LayoutContext(300, 400, 16);

        Assert.Equal(100f, context.ResolveWidth("100"));
        Assert.Equal(150f, context.ResolveWidth("50%"));
        Assert.Equal(32f, context.ResolveWidth("2em"));
    }

    [Fact]
    public void ResolveHeight_CalculatesFromUnit()
    {
        var context = new LayoutContext(300, 400, 16);

        Assert.Equal(100f, context.ResolveHeight("100"));
        Assert.Equal(200f, context.ResolveHeight("50%"));
        Assert.Equal(32f, context.ResolveHeight("2em"));
    }

    [Fact]
    public void ResolveWidth_Auto_ReturnsNull()
    {
        var context = new LayoutContext(300, 400, 16);

        Assert.Null(context.ResolveWidth("auto"));
    }

    [Fact]
    public void Constructor_WithIntrinsicSizes_StoresSizes()
    {
        var sizes = new Dictionary<TemplateElement, IntrinsicSize>(ReferenceEqualityComparer.Instance);
        var element = new TextElement { Content = "test" };
        sizes[element] = new IntrinsicSize(10, 100, 20, 200);

        var context = new LayoutContext(300, 400, 16, sizes);

        Assert.NotNull(context.IntrinsicSizes);
        Assert.True(context.IntrinsicSizes.ContainsKey(element));
        Assert.Equal(100f, context.IntrinsicSizes[element].MaxWidth);
    }
}
