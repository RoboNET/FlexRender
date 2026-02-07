using FlexRender.Layout;
using FlexRender.Parsing.Ast;
using Xunit;

namespace FlexRender.Tests.Layout;

public sealed class ApproximateTextShaperTests
{
    private readonly ApproximateTextShaper _shaper = new();

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
    public void ShapeText_SingleLine_FitsInWidth()
    {
        var element = new TextElement { Content = "Hi" };
        var result = _shaper.ShapeText(element, 16f, 300f);

        Assert.Single(result.Lines);
        Assert.Equal("Hi", result.Lines[0]);
        Assert.True(result.TotalSize.Width > 0f);
        Assert.True(result.LineHeight > 0f);
    }

    [Fact]
    public void ShapeText_WrappedText_BreaksAtWordBoundary()
    {
        var element = new TextElement
        {
            Content = "Alpha Beta Gamma Delta Epsilon",
            Wrap = true
        };

        // Very narrow: 16 * 0.6 * 5 = 48 chars wide per line
        var result = _shaper.ShapeText(element, 16f, 50f);

        Assert.True(result.Lines.Count > 1,
            $"Expected multiple lines in 50px, got {result.Lines.Count}");
    }

    [Fact]
    public void ShapeText_ExplicitNewlines_Splits()
    {
        var element = new TextElement { Content = "A\nB\nC" };
        var result = _shaper.ShapeText(element, 16f, 300f);

        Assert.Equal(3, result.Lines.Count);
        Assert.Equal("A", result.Lines[0]);
        Assert.Equal("B", result.Lines[1]);
        Assert.Equal("C", result.Lines[2]);
    }

    [Fact]
    public void ShapeText_NoWrap_SingleLine()
    {
        var element = new TextElement
        {
            Content = "A very long text that would normally wrap",
            Wrap = false
        };

        var result = _shaper.ShapeText(element, 16f, 50f);

        Assert.Single(result.Lines);
    }

    [Fact]
    public void ShapeText_MaxLines_Truncates()
    {
        var element = new TextElement
        {
            Content = "Word1 Word2 Word3 Word4 Word5 Word6 Word7 Word8",
            Wrap = true,
            MaxLines = 2
        };

        var result = _shaper.ShapeText(element, 16f, 60f);

        Assert.True(result.Lines.Count <= 2,
            $"Expected at most 2 lines, got {result.Lines.Count}");
    }

    [Fact]
    public void ShapeText_Ellipsis_NoWrap_TruncatesWithEllipsis()
    {
        var element = new TextElement
        {
            Content = "This text is too long to fit",
            Wrap = false,
            Overflow = TextOverflow.Ellipsis
        };

        var result = _shaper.ShapeText(element, 16f, 50f);

        Assert.Single(result.Lines);
        Assert.EndsWith("...", result.Lines[0]);
    }

    [Fact]
    public void ShapeText_VisibleOverflow_NoTruncation()
    {
        var element = new TextElement
        {
            Content = "Long text here",
            Wrap = false,
            Overflow = TextOverflow.Visible
        };

        var result = _shaper.ShapeText(element, 16f, 10f);

        Assert.Single(result.Lines);
        Assert.Equal("Long text here", result.Lines[0]);
    }

    [Fact]
    public void ShapeText_ImplementsITextShaper()
    {
        Assert.IsAssignableFrom<ITextShaper>(_shaper);
    }

    [Fact]
    public void ShapeText_LineHeight_AffectsTotalHeight()
    {
        var element = new TextElement { Content = "Hello", LineHeight = "2.0" };
        var result = _shaper.ShapeText(element, 16f, 300f);

        // fontSize=16, lineHeight multiplier=2.0 -> lineHeight=32, one line -> height=32
        Assert.Equal(32f, result.TotalSize.Height, 0.1f);
    }

    [Fact]
    public void ShapeText_DefaultCharWidthFactor_IsReasonable()
    {
        // The approximate shaper uses fontSize * 0.6 as average char width.
        // "Hello" = 5 chars * 16 * 0.6 = 48px
        var element = new TextElement { Content = "Hello" };
        var result = _shaper.ShapeText(element, 16f, 300f);

        Assert.Equal(48f, result.TotalSize.Width, 0.1f);
    }
}
