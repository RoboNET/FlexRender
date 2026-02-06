using Xunit;

namespace FlexRender.Tests;

public sealed class JpegOptionsTests
{
    [Fact]
    public void Default_Quality90()
    {
        var options = JpegOptions.Default;

        Assert.Equal(90, options.Quality);
    }

    [Fact]
    public void Default_IsSingletonInstance()
    {
        var a = JpegOptions.Default;
        var b = JpegOptions.Default;

        Assert.Same(a, b);
    }

    [Fact]
    public void Init_SetsQuality()
    {
        var options = new JpegOptions { Quality = 75 };

        Assert.Equal(75, options.Quality);
    }

    [Fact]
    public void ValueEquality_EqualInstances()
    {
        var a = new JpegOptions { Quality = 85 };
        var b = new JpegOptions { Quality = 85 };

        Assert.Equal(a, b);
    }

    [Fact]
    public void ValueEquality_DifferentInstances()
    {
        var a = new JpegOptions { Quality = 80 };
        var b = new JpegOptions { Quality = 90 };

        Assert.NotEqual(a, b);
    }
}
