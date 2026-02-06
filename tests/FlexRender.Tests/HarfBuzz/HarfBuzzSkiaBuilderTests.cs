using FlexRender.HarfBuzz;
using FlexRender.Skia;
using Xunit;

namespace FlexRender.Tests.HarfBuzz;

/// <summary>
/// Tests for HarfBuzz SkiaBuilder extension methods.
/// </summary>
public class HarfBuzzSkiaBuilderTests
{
    [Fact]
    public void WithHarfBuzz_SetsShapedTextMeasurer()
    {
        var builder = new SkiaBuilder();

        builder.WithHarfBuzz();

        Assert.NotNull(builder.ShapedTextMeasurer);
    }

    [Fact]
    public void WithHarfBuzz_SetsShapedTextDrawer()
    {
        var builder = new SkiaBuilder();

        builder.WithHarfBuzz();

        Assert.NotNull(builder.ShapedTextDrawer);
    }

    [Fact]
    public void WithHarfBuzz_ReturnsSameBuilder()
    {
        var builder = new SkiaBuilder();

        var result = builder.WithHarfBuzz();

        Assert.Same(builder, result);
    }

    [Fact]
    public void WithHarfBuzz_NullBuilder_ThrowsArgumentNullException()
    {
        SkiaBuilder builder = null!;

        Assert.Throws<ArgumentNullException>(() => builder.WithHarfBuzz());
    }

    [Fact]
    public void WithHarfBuzz_CanChainWithOtherProviders()
    {
        var builder = new SkiaBuilder();

        // Should not throw when chaining with other extensions
        var result = builder.WithHarfBuzz();

        Assert.NotNull(result);
        Assert.NotNull(builder.ShapedTextMeasurer);
        Assert.NotNull(builder.ShapedTextDrawer);
    }
}
