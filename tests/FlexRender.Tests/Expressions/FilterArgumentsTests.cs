using FlexRender.TemplateEngine;
using Xunit;

namespace FlexRender.Tests.Expressions;

/// <summary>
/// Tests for the <see cref="FilterArguments"/> class that holds parsed filter arguments.
/// </summary>
public sealed class FilterArgumentsTests
{
    [Fact]
    public void Positional_ReturnsPositionalArgument()
    {
        var args = new FilterArguments(new StringValue("30"), new Dictionary<string, TemplateValue?>());

        Assert.NotNull(args.Positional);
        var str = Assert.IsType<StringValue>(args.Positional);
        Assert.Equal("30", str.Value);
    }

    [Fact]
    public void Positional_WhenNull_ReturnsNull()
    {
        var args = new FilterArguments(null, new Dictionary<string, TemplateValue?>());

        Assert.Null(args.Positional);
    }

    [Fact]
    public void GetNamed_ExistingKey_ReturnsValue()
    {
        var named = new Dictionary<string, TemplateValue?>
        {
            ["suffix"] = new StringValue("px")
        };
        var args = new FilterArguments(null, named);

        var result = args.GetNamed("suffix", NullValue.Instance);

        var str = Assert.IsType<StringValue>(result);
        Assert.Equal("px", str.Value);
    }

    [Fact]
    public void GetNamed_MissingKey_ReturnsDefault()
    {
        var args = new FilterArguments(null, new Dictionary<string, TemplateValue?>());

        var result = args.GetNamed("missing", new StringValue("default"));

        var str = Assert.IsType<StringValue>(result);
        Assert.Equal("default", str.Value);
    }

    [Fact]
    public void HasFlag_PresentFlag_ReturnsTrue()
    {
        var named = new Dictionary<string, TemplateValue?>
        {
            ["fromEnd"] = null
        };
        var args = new FilterArguments(null, named);

        Assert.True(args.HasFlag("fromEnd"));
    }

    [Fact]
    public void HasFlag_AbsentFlag_ReturnsFalse()
    {
        var args = new FilterArguments(null, new Dictionary<string, TemplateValue?>());

        Assert.False(args.HasFlag("fromEnd"));
    }

    [Fact]
    public void HasFlag_KeyWithValue_ReturnsFalse()
    {
        var named = new Dictionary<string, TemplateValue?>
        {
            ["suffix"] = new StringValue("px")
        };
        var args = new FilterArguments(null, named);

        Assert.False(args.HasFlag("suffix"));
    }

    [Fact]
    public void Empty_HasNoPositionalOrNamed()
    {
        var args = FilterArguments.Empty;

        Assert.Null(args.Positional);
        Assert.False(args.HasFlag("anything"));
        Assert.IsType<NullValue>(args.GetNamed("anything", NullValue.Instance));
    }
}
