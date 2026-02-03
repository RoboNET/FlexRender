using FlexRender.Layout.Units;
using Xunit;

namespace FlexRender.Tests.Layout.Units;

public sealed class PaddingValuesTests
{
    [Fact]
    public void Uniform_CreatesEqualSides()
    {
        var p = PaddingValues.Uniform(10f);

        Assert.Equal(10f, p.Top);
        Assert.Equal(10f, p.Right);
        Assert.Equal(10f, p.Bottom);
        Assert.Equal(10f, p.Left);
    }

    [Fact]
    public void Constructor_SetsAllSides()
    {
        var p = new PaddingValues(1f, 2f, 3f, 4f);

        Assert.Equal(1f, p.Top);
        Assert.Equal(2f, p.Right);
        Assert.Equal(3f, p.Bottom);
        Assert.Equal(4f, p.Left);
    }

    [Fact]
    public void HorizontalSum_ReturnsLeftPlusRight()
    {
        var p = new PaddingValues(10f, 20f, 30f, 40f);

        Assert.Equal(60f, p.Horizontal);
    }

    [Fact]
    public void VerticalSum_ReturnsTopPlusBottom()
    {
        var p = new PaddingValues(10f, 20f, 30f, 40f);

        Assert.Equal(40f, p.Vertical);
    }

    [Fact]
    public void Zero_AllSidesAreZero()
    {
        var p = PaddingValues.Zero;

        Assert.Equal(0f, p.Top);
        Assert.Equal(0f, p.Right);
        Assert.Equal(0f, p.Bottom);
        Assert.Equal(0f, p.Left);
    }

    [Fact]
    public void Equality_SameValues_AreEqual()
    {
        var a = new PaddingValues(1f, 2f, 3f, 4f);
        var b = new PaddingValues(1f, 2f, 3f, 4f);

        Assert.Equal(a, b);
    }

    [Fact]
    public void Equality_DifferentValues_AreNotEqual()
    {
        var a = new PaddingValues(1f, 2f, 3f, 4f);
        var b = new PaddingValues(4f, 3f, 2f, 1f);

        Assert.NotEqual(a, b);
    }

    [Fact]
    public void ClampNegatives_ClampsNegativeValuesToZero()
    {
        var p = new PaddingValues(-5f, 10f, -3f, 20f);

        var clamped = p.ClampNegatives();

        Assert.Equal(0f, clamped.Top);
        Assert.Equal(10f, clamped.Right);
        Assert.Equal(0f, clamped.Bottom);
        Assert.Equal(20f, clamped.Left);
    }

    [Fact]
    public void ClampNegatives_PositiveValues_Unchanged()
    {
        var p = new PaddingValues(1f, 2f, 3f, 4f);

        var clamped = p.ClampNegatives();

        Assert.Equal(p, clamped);
    }
}
