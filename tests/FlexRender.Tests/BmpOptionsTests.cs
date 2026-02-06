using Xunit;

namespace FlexRender.Tests;

public sealed class BmpOptionsTests
{
    [Fact]
    public void Default_ColorModeBgra32()
    {
        var options = BmpOptions.Default;

        Assert.Equal(BmpColorMode.Bgra32, options.ColorMode);
    }

    [Fact]
    public void Default_IsSingletonInstance()
    {
        var a = BmpOptions.Default;
        var b = BmpOptions.Default;

        Assert.Same(a, b);
    }

    [Fact]
    public void Init_SetsColorMode()
    {
        var options = new BmpOptions { ColorMode = BmpColorMode.Monochrome1 };

        Assert.Equal(BmpColorMode.Monochrome1, options.ColorMode);
    }

    [Fact]
    public void ValueEquality_EqualInstances()
    {
        var a = new BmpOptions { ColorMode = BmpColorMode.Rgb24 };
        var b = new BmpOptions { ColorMode = BmpColorMode.Rgb24 };

        Assert.Equal(a, b);
    }

    [Fact]
    public void ValueEquality_DifferentInstances()
    {
        var a = new BmpOptions { ColorMode = BmpColorMode.Rgb24 };
        var b = new BmpOptions { ColorMode = BmpColorMode.Monochrome1 };

        Assert.NotEqual(a, b);
    }
}
