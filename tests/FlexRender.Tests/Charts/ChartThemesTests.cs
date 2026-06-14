using FlexRender.Charts;
using Xunit;

namespace FlexRender.Tests.Charts;

/// <summary>
/// Tests for named chart theme resolution.
/// </summary>
public sealed class ChartThemesTests
{
    [Theory]
    [InlineData("light")]
    [InlineData("dark")]
    [InlineData("minimal")]
    public void Resolve_KnownName_ReturnsTheme(string name)
    {
        var theme = ChartThemes.Resolve(name);
        Assert.NotNull(theme);
    }

    [Fact]
    public void Resolve_UnknownName_ReturnsNull()
    {
        Assert.Null(ChartThemes.Resolve("neon"));
    }

    [Fact]
    public void Resolve_IsCaseInsensitive()
    {
        Assert.NotNull(ChartThemes.Resolve("DARK"));
    }

    [Fact]
    public void Default_IsLight()
    {
        Assert.Same(ChartThemes.Resolve("light"), ChartThemes.Default);
    }

    [Fact]
    public void LightTheme_HasNonEmptyColors()
    {
        var theme = ChartThemes.Default;
        Assert.False(string.IsNullOrEmpty(theme.BackgroundColor));
        Assert.False(string.IsNullOrEmpty(theme.GridColor));
        Assert.False(string.IsNullOrEmpty(theme.AxisColor));
        Assert.False(string.IsNullOrEmpty(theme.LabelColor));
        Assert.True(theme.LabelSize > 0f);
        Assert.True(theme.TitleSize > 0f);
        Assert.True(theme.LineWidth > 0f);
        Assert.True(theme.BarCornerRadius >= 0f);
    }
}
