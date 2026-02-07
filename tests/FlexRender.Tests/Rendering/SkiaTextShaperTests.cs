using FlexRender.Layout;
using FlexRender.Parsing.Ast;
using FlexRender.Rendering;
using Xunit;

namespace FlexRender.Tests.Rendering;

public sealed class SkiaTextShaperTests
{
    private readonly SkiaTextShaper _shaper;

    public SkiaTextShaperTests()
    {
        var fontManager = new FontManager();
        _shaper = new SkiaTextShaper(fontManager);
    }

    [Fact]
    public void ShapeText_EmptyContent_ReturnsEmptyLines()
    {
        var element = new TextElement { Content = "" };
        var result = _shaper.ShapeText(element, 16f, 300f);

        Assert.Empty(result.Lines);
        Assert.Equal(0f, result.TotalSize.Width);
        Assert.Equal(0f, result.TotalSize.Height);
    }

    [Fact]
    public void ShapeText_SingleLine_ReturnsOneLine()
    {
        var element = new TextElement { Content = "Hello" };
        var result = _shaper.ShapeText(element, 16f, 300f);

        Assert.Single(result.Lines);
        Assert.Equal("Hello", result.Lines[0]);
        Assert.True(result.TotalSize.Width > 0f);
        Assert.True(result.TotalSize.Height > 0f);
        Assert.True(result.LineHeight > 0f);
    }

    [Fact]
    public void ShapeText_ExplicitNewlines_SplitsLines()
    {
        var element = new TextElement { Content = "Line 1\nLine 2\nLine 3" };
        var result = _shaper.ShapeText(element, 16f, 300f);

        Assert.Equal(3, result.Lines.Count);
        Assert.Equal("Line 1", result.Lines[0]);
        Assert.Equal("Line 2", result.Lines[1]);
        Assert.Equal("Line 3", result.Lines[2]);
    }

    [Fact]
    public void ShapeText_WrappedText_BreaksAtWordBoundary()
    {
        var element = new TextElement
        {
            Content = "This is a long text that should wrap to multiple lines",
            Wrap = true
        };

        // Use a very narrow width to force wrapping
        var result = _shaper.ShapeText(element, 16f, 50f);

        Assert.True(result.Lines.Count > 1,
            $"Expected multiple lines but got {result.Lines.Count}");
    }

    [Fact]
    public void ShapeText_NoWrap_SingleLine()
    {
        var element = new TextElement
        {
            Content = "This is a long text that would wrap if wrapping were enabled",
            Wrap = false
        };

        var result = _shaper.ShapeText(element, 16f, 50f);

        Assert.Single(result.Lines);
    }

    [Fact]
    public void ShapeText_MaxLines_TruncatesLines()
    {
        var element = new TextElement
        {
            Content = "Word1 Word2 Word3 Word4 Word5 Word6 Word7 Word8 Word9 Word10",
            Wrap = true,
            MaxLines = 2
        };

        var result = _shaper.ShapeText(element, 16f, 80f);

        Assert.True(result.Lines.Count <= 2,
            $"Expected at most 2 lines but got {result.Lines.Count}");
    }

    [Fact]
    public void ShapeText_Ellipsis_AddsEllipsisToTruncatedLine()
    {
        var element = new TextElement
        {
            Content = "This is a very long text that will definitely be truncated",
            Wrap = false,
            Overflow = TextOverflow.Ellipsis
        };

        var result = _shaper.ShapeText(element, 16f, 80f);

        Assert.Single(result.Lines);
        Assert.EndsWith("...", result.Lines[0]);
    }

    [Fact]
    public void ShapeText_VisibleOverflow_NoTruncation()
    {
        var element = new TextElement
        {
            Content = "Long text",
            Wrap = false,
            Overflow = TextOverflow.Visible
        };

        // With Visible overflow and no wrap, text should not be truncated
        var result = _shaper.ShapeText(element, 16f, 10f);

        Assert.Single(result.Lines);
        Assert.Equal("Long text", result.Lines[0]);
    }

    [Fact]
    public void ShapeText_LineHeight_AffectsTotalHeight()
    {
        var element = new TextElement { Content = "Hello\nWorld", LineHeight = "2.0" };
        var result = _shaper.ShapeText(element, 16f, 300f);

        // With lineHeight=2.0 multiplier, each line takes 32px, total = 64px
        Assert.Equal(2, result.Lines.Count);
        Assert.True(result.TotalSize.Height >= 60f,
            $"With 2.0x line-height, total height should be >= 60, got {result.TotalSize.Height}");
    }

    [Fact]
    public void ShapeText_ImplementsITextShaper()
    {
        Assert.IsAssignableFrom<ITextShaper>(_shaper);
    }
}
