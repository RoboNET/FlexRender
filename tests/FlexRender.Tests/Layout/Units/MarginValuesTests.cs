using FlexRender.Layout.Units;
using Xunit;

namespace FlexRender.Tests.Layout.Units;

/// <summary>
/// Tests for <see cref="MarginValue"/> and <see cref="MarginValues"/> types.
/// </summary>
public sealed class MarginValuesTests
{
    // ────────────────────────────────────────────────────────────────
    // MarginValue Tests
    // ────────────────────────────────────────────────────────────────

    [Fact]
    public void MarginValue_Auto_HasNoPixels()
    {
        var auto = MarginValue.Auto;

        Assert.True(auto.IsAuto);
        Assert.Null(auto.Pixels);
        Assert.Equal(0f, auto.ResolvedPixels);
    }

    [Fact]
    public void MarginValue_Fixed_HasPixels()
    {
        var fixed10 = MarginValue.Fixed(10f);

        Assert.False(fixed10.IsAuto);
        Assert.Equal(10f, fixed10.Pixels);
        Assert.Equal(10f, fixed10.ResolvedPixels);
    }

    [Fact]
    public void MarginValue_Fixed_ZeroPixels()
    {
        var zero = MarginValue.Fixed(0f);

        Assert.False(zero.IsAuto);
        Assert.Equal(0f, zero.Pixels);
        Assert.Equal(0f, zero.ResolvedPixels);
    }

    // ────────────────────────────────────────────────────────────────
    // MarginValues HasAuto Tests
    // ────────────────────────────────────────────────────────────────

    [Fact]
    public void MarginValues_HasAuto_TrueWhenAnyAuto()
    {
        var margin = new MarginValues(
            MarginValue.Fixed(0), MarginValue.Auto,
            MarginValue.Fixed(0), MarginValue.Fixed(0));

        Assert.True(margin.HasAuto);
    }

    [Fact]
    public void MarginValues_HasAuto_FalseWhenAllFixed()
    {
        var margin = new MarginValues(
            MarginValue.Fixed(10), MarginValue.Fixed(20),
            MarginValue.Fixed(10), MarginValue.Fixed(20));

        Assert.False(margin.HasAuto);
    }

    // ────────────────────────────────────────────────────────────────
    // MainAxisAutoCount Tests
    // ────────────────────────────────────────────────────────────────

    [Fact]
    public void MarginValues_MainAxisAutoCount_Row_CountsLeftRight()
    {
        // Row: main axis is horizontal (Left/Right)
        var margin = new MarginValues(
            MarginValue.Fixed(0), MarginValue.Auto,
            MarginValue.Fixed(0), MarginValue.Auto);

        Assert.Equal(2, margin.MainAxisAutoCount(isColumn: false));
    }

    [Fact]
    public void MarginValues_MainAxisAutoCount_Row_OnlyLeft()
    {
        var margin = new MarginValues(
            MarginValue.Fixed(0), MarginValue.Fixed(0),
            MarginValue.Fixed(0), MarginValue.Auto);

        Assert.Equal(1, margin.MainAxisAutoCount(isColumn: false));
    }

    [Fact]
    public void MarginValues_MainAxisAutoCount_Column_CountsTopBottom()
    {
        // Column: main axis is vertical (Top/Bottom)
        var margin = new MarginValues(
            MarginValue.Auto, MarginValue.Fixed(0),
            MarginValue.Auto, MarginValue.Fixed(0));

        Assert.Equal(2, margin.MainAxisAutoCount(isColumn: true));
    }

    // ────────────────────────────────────────────────────────────────
    // CrossAxisAutoCount Tests
    // ────────────────────────────────────────────────────────────────

    [Fact]
    public void MarginValues_CrossAxisAutoCount_Row_CountsTopBottom()
    {
        // Row: cross axis is vertical (Top/Bottom)
        var margin = new MarginValues(
            MarginValue.Auto, MarginValue.Fixed(0),
            MarginValue.Auto, MarginValue.Fixed(0));

        Assert.Equal(2, margin.CrossAxisAutoCount(isColumn: false));
    }

    [Fact]
    public void MarginValues_CrossAxisAutoCount_Column_CountsLeftRight()
    {
        // Column: cross axis is horizontal (Left/Right)
        var margin = new MarginValues(
            MarginValue.Fixed(0), MarginValue.Auto,
            MarginValue.Fixed(0), MarginValue.Auto);

        Assert.Equal(2, margin.CrossAxisAutoCount(isColumn: true));
    }

    // ────────────────────────────────────────────────────────────────
    // Zero Tests
    // ────────────────────────────────────────────────────────────────

    [Fact]
    public void MarginValues_Zero_AllFixedAtZero()
    {
        var zero = MarginValues.Zero;

        Assert.False(zero.HasAuto);
        Assert.Equal(0f, zero.Top.ResolvedPixels);
        Assert.Equal(0f, zero.Right.ResolvedPixels);
        Assert.Equal(0f, zero.Bottom.ResolvedPixels);
        Assert.Equal(0f, zero.Left.ResolvedPixels);
    }
}
