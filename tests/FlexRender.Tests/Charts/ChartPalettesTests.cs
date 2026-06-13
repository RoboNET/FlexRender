using System.Collections.Generic;
using FlexRender.Charts;
using Xunit;

namespace FlexRender.Tests.Charts;

/// <summary>
/// Tests for named palette resolution and color cycling.
/// </summary>
public sealed class ChartPalettesTests
{
    [Theory]
    [InlineData("default")]
    [InlineData("ocean")]
    [InlineData("sunset")]
    [InlineData("forest")]
    [InlineData("mono")]
    [InlineData("vivid")]
    public void Resolve_KnownName_ReturnsNonEmptyPalette(string name)
    {
        var palette = ChartPalettes.Resolve(name);
        Assert.NotNull(palette);
        Assert.NotEmpty(palette!.Colors);
    }

    [Fact]
    public void Resolve_UnknownName_ReturnsNull()
    {
        Assert.Null(ChartPalettes.Resolve("does-not-exist"));
    }

    [Fact]
    public void Resolve_IsCaseInsensitive()
    {
        Assert.NotNull(ChartPalettes.Resolve("OCEAN"));
    }

    [Fact]
    public void ColorAt_CyclesWhenIndexExceedsCount()
    {
        var palette = new ChartPalette(new[] { "#111111", "#222222" });
        Assert.Equal("#111111", palette.ColorAt(0));
        Assert.Equal("#222222", palette.ColorAt(1));
        Assert.Equal("#111111", palette.ColorAt(2));
    }

    [Fact]
    public void Default_IsNonEmpty()
    {
        Assert.NotEmpty(ChartPalettes.Default.Colors);
    }

    [Fact]
    public void Constructor_NullColors_Throws()
    {
        Assert.Throws<System.ArgumentNullException>(() => new ChartPalette(null!));
    }

    [Fact]
    public void Constructor_EmptyColors_Throws()
    {
        Assert.Throws<System.ArgumentException>(() => new ChartPalette(new List<string>()));
    }
}
