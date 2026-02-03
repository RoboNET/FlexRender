using FlexRender.Layout;
using FlexRender.Layout.Units;
using Xunit;

namespace FlexRender.Tests.Layout;

public class IntrinsicSizeTests
{
    [Fact]
    public void Default_AllZeros()
    {
        var size = new IntrinsicSize();

        Assert.Equal(0f, size.MinWidth);
        Assert.Equal(0f, size.MaxWidth);
        Assert.Equal(0f, size.MinHeight);
        Assert.Equal(0f, size.MaxHeight);
    }

    [Fact]
    public void Constructor_SetsAllFields()
    {
        var size = new IntrinsicSize(10f, 100f, 20f, 200f);

        Assert.Equal(10f, size.MinWidth);
        Assert.Equal(100f, size.MaxWidth);
        Assert.Equal(20f, size.MinHeight);
        Assert.Equal(200f, size.MaxHeight);
    }

    [Fact]
    public void WithPadding_AddsPaddingToAllDimensions()
    {
        var size = new IntrinsicSize(10f, 100f, 20f, 200f);

        var padded = size.WithPadding(5f);

        Assert.Equal(20f, padded.MinWidth);   // 10 + 5*2
        Assert.Equal(110f, padded.MaxWidth);   // 100 + 5*2
        Assert.Equal(30f, padded.MinHeight);   // 20 + 5*2
        Assert.Equal(210f, padded.MaxHeight);  // 200 + 5*2
    }

    [Fact]
    public void WithMargin_AddsMarginToAllDimensions()
    {
        var size = new IntrinsicSize(10f, 100f, 20f, 200f);

        var margined = size.WithMargin(3f);

        Assert.Equal(16f, margined.MinWidth);   // 10 + 3*2
        Assert.Equal(106f, margined.MaxWidth);   // 100 + 3*2
        Assert.Equal(26f, margined.MinHeight);   // 20 + 3*2
        Assert.Equal(206f, margined.MaxHeight);  // 200 + 3*2
    }

    [Fact]
    public void Equality_SameValues_AreEqual()
    {
        var a = new IntrinsicSize(10f, 100f, 20f, 200f);
        var b = new IntrinsicSize(10f, 100f, 20f, 200f);

        Assert.Equal(a, b);
    }

    [Fact]
    public void WithPadding_PaddingValues_AddsNonUniformPadding()
    {
        var size = new IntrinsicSize(10f, 100f, 20f, 200f);
        var padding = new PaddingValues(5f, 10f, 15f, 20f);

        var padded = size.WithPadding(padding);

        // Width: left + right = 20 + 10 = 30
        Assert.Equal(40f, padded.MinWidth);    // 10 + 30
        Assert.Equal(130f, padded.MaxWidth);   // 100 + 30
        // Height: top + bottom = 5 + 15 = 20
        Assert.Equal(40f, padded.MinHeight);   // 20 + 20
        Assert.Equal(220f, padded.MaxHeight);  // 200 + 20
    }

    [Fact]
    public void WithPadding_UniformPaddingValues_MatchesScalarOverload()
    {
        var size = new IntrinsicSize(10f, 100f, 20f, 200f);

        var fromScalar = size.WithPadding(5f);
        var fromStruct = size.WithPadding(PaddingValues.Uniform(5f));

        Assert.Equal(fromScalar, fromStruct);
    }
}
