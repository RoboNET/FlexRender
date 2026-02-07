using FlexRender.Providers;
using Xunit;

namespace FlexRender.Tests.Providers;

/// <summary>
/// Tests for the <see cref="ContentResult"/> record struct.
/// </summary>
public sealed class ContentResultTests
{
    [Fact]
    public void Constructor_WithValidArgs_StoresValues()
    {
        var pngBytes = new byte[] { 137, 80, 78, 71 }; // PNG magic bytes
        var result = new ContentResult(pngBytes, 100, 200);

        Assert.Same(pngBytes, result.PngBytes);
        Assert.Equal(100, result.Width);
        Assert.Equal(200, result.Height);
    }

    [Fact]
    public void Equality_SameValues_AreEqual()
    {
        var bytes = new byte[] { 1, 2, 3 };
        var a = new ContentResult(bytes, 50, 50);
        var b = new ContentResult(bytes, 50, 50);

        Assert.Equal(a, b);
    }

    [Fact]
    public void Equality_DifferentDimensions_AreNotEqual()
    {
        var bytes = new byte[] { 1, 2, 3 };
        var a = new ContentResult(bytes, 50, 50);
        var b = new ContentResult(bytes, 100, 100);

        Assert.NotEqual(a, b);
    }
}
