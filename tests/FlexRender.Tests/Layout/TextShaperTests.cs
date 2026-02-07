using FlexRender.Layout;
using FlexRender.Parsing.Ast;
using Xunit;

namespace FlexRender.Tests.Layout;

public sealed class TextShaperTests
{
    [Fact]
    public void TextShapingResult_StoresLinesAndMetrics()
    {
        var lines = new List<string> { "Hello", "World" };
        var result = new TextShapingResult(lines, new LayoutSize(100f, 40f), 20f);

        Assert.Equal(2, result.Lines.Count);
        Assert.Equal("Hello", result.Lines[0]);
        Assert.Equal("World", result.Lines[1]);
        Assert.Equal(100f, result.TotalSize.Width);
        Assert.Equal(40f, result.TotalSize.Height);
        Assert.Equal(20f, result.LineHeight);
    }

    [Fact]
    public void TextShapingResult_EmptyLines_IsValid()
    {
        var result = new TextShapingResult(
            Array.Empty<string>(),
            new LayoutSize(0f, 0f),
            0f);

        Assert.Empty(result.Lines);
        Assert.Equal(0f, result.TotalSize.Width);
        Assert.Equal(0f, result.TotalSize.Height);
    }

    [Fact]
    public void TextShapingResult_SingleLine_IsValid()
    {
        var result = new TextShapingResult(
            new[] { "Single line" },
            new LayoutSize(80f, 20f),
            20f);

        Assert.Single(result.Lines);
        Assert.Equal("Single line", result.Lines[0]);
        Assert.Equal(80f, result.TotalSize.Width);
        Assert.Equal(20f, result.TotalSize.Height);
    }
}
