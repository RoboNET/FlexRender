using FlexRender.Layout;
using Xunit;

namespace FlexRender.Tests.Layout;

public sealed class LayoutSizeTests
{
    [Fact]
    public void Constructor_SetsWidthAndHeight()
    {
        var size = new LayoutSize(100f, 200f);
        Assert.Equal(100f, size.Width);
        Assert.Equal(200f, size.Height);
    }

    [Fact]
    public void Default_IsZero()
    {
        var size = default(LayoutSize);
        Assert.Equal(0f, size.Width);
        Assert.Equal(0f, size.Height);
    }

    [Fact]
    public void Equality_SameValues_AreEqual()
    {
        var a = new LayoutSize(10f, 20f);
        var b = new LayoutSize(10f, 20f);
        Assert.Equal(a, b);
        Assert.True(a == b);
    }

    [Fact]
    public void Equality_DifferentValues_AreNotEqual()
    {
        var a = new LayoutSize(10f, 20f);
        var b = new LayoutSize(30f, 40f);
        Assert.NotEqual(a, b);
        Assert.True(a != b);
    }
}
