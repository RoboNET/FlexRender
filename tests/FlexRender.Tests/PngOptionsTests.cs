using Xunit;

namespace FlexRender.Tests;

public sealed class PngOptionsTests
{
    [Fact]
    public void Default_CompressionLevel100()
    {
        var options = PngOptions.Default;

        Assert.Equal(100, options.CompressionLevel);
    }

    [Fact]
    public void Default_IsSingletonInstance()
    {
        var a = PngOptions.Default;
        var b = PngOptions.Default;

        Assert.Same(a, b);
    }

    [Fact]
    public void Init_SetsCompressionLevel()
    {
        var options = new PngOptions { CompressionLevel = 50 };

        Assert.Equal(50, options.CompressionLevel);
    }

    [Fact]
    public void ValueEquality_EqualInstances()
    {
        var a = new PngOptions { CompressionLevel = 75 };
        var b = new PngOptions { CompressionLevel = 75 };

        Assert.Equal(a, b);
    }

    [Fact]
    public void ValueEquality_DifferentInstances()
    {
        var a = new PngOptions { CompressionLevel = 50 };
        var b = new PngOptions { CompressionLevel = 75 };

        Assert.NotEqual(a, b);
    }
}
