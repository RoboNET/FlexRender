#pragma warning disable CS0618 // Testing deprecated TextMeasurer backward compatibility

using FlexRender.Layout;
using FlexRender.Parsing.Ast;
using Xunit;

namespace FlexRender.Tests.Layout;

/// <summary>
/// Tests for LayoutEngine integration with ITextShaper.
/// Uses a mock text shaper to verify that LayoutEngine stores
/// pre-computed text lines on LayoutNode.
/// </summary>
public sealed class LayoutEngineTextShaperTests
{
    /// <summary>
    /// A simple mock shaper that splits text by spaces, wrapping at maxWidth
    /// using a fixed character width of (fontSize * 0.6).
    /// </summary>
    private sealed class MockTextShaper : ITextShaper
    {
        public int CallCount { get; private set; }

        public TextShapingResult ShapeText(TextElement element, float fontSize, float maxWidth)
        {
            CallCount++;

            if (string.IsNullOrEmpty(element.Content))
            {
                return new TextShapingResult(
                    Array.Empty<string>(),
                    new LayoutSize(0f, 0f),
                    0f);
            }

            var charWidth = fontSize * 0.6f;
            var lineHeight = fontSize * 1.2f;
            var lines = new List<string>();

            // Split by explicit newlines first
            var paragraphs = element.Content.Split('\n');
            foreach (var paragraph in paragraphs)
            {
                if (!element.Wrap || charWidth * paragraph.Length <= maxWidth)
                {
                    lines.Add(paragraph);
                    continue;
                }

                // Simple word-wrap
                var words = paragraph.Split(' ');
                var currentLine = "";
                foreach (var word in words)
                {
                    var testLine = string.IsNullOrEmpty(currentLine) ? word : $"{currentLine} {word}";
                    if (charWidth * testLine.Length <= maxWidth)
                    {
                        currentLine = testLine;
                    }
                    else
                    {
                        if (!string.IsNullOrEmpty(currentLine))
                            lines.Add(currentLine);
                        currentLine = word;

                        if (element.MaxLines.HasValue && lines.Count >= element.MaxLines.Value)
                            break;
                    }
                }

                if (!string.IsNullOrEmpty(currentLine))
                    lines.Add(currentLine);

                if (element.MaxLines.HasValue && lines.Count >= element.MaxLines.Value)
                    break;
            }

            // Apply MaxLines
            if (element.MaxLines.HasValue && lines.Count > element.MaxLines.Value)
            {
                lines.RemoveRange(element.MaxLines.Value, lines.Count - element.MaxLines.Value);
            }

            var maxLineWidth = 0f;
            foreach (var line in lines)
            {
                var w = charWidth * line.Length;
                if (w > maxLineWidth) maxLineWidth = w;
            }

            var totalHeight = lines.Count * lineHeight;

            return new TextShapingResult(
                lines,
                new LayoutSize(maxLineWidth, totalHeight),
                lineHeight);
        }
    }

    [Fact]
    public void TextShaper_Property_DefaultsToNull()
    {
        var engine = new LayoutEngine();
        Assert.Null(engine.TextShaper);
    }

    [Fact]
    public void TextShaper_WhenSet_PopulatesTextLinesOnLayoutNode()
    {
        var shaper = new MockTextShaper();
        var engine = new LayoutEngine { TextShaper = shaper };

        var template = new Template
        {
            Canvas = new CanvasSettings { Width = 300 },
            Elements = new List<TemplateElement>
            {
                new TextElement { Content = "Hello World", Size = "16" }
            }
        };

        var root = engine.ComputeLayout(template);
        var textNode = root.Children[0];

        Assert.NotNull(textNode.TextLines);
        Assert.Single(textNode.TextLines); // "Hello World" fits in 300px
        Assert.Equal("Hello World", textNode.TextLines[0]);
        Assert.True(textNode.ComputedLineHeight > 0f);
    }

    [Fact]
    public void TextShaper_WrappedText_PopulatesMultipleLines()
    {
        var shaper = new MockTextShaper();
        var engine = new LayoutEngine { TextShaper = shaper };

        var template = new Template
        {
            Canvas = new CanvasSettings { Width = 80 }, // Narrow: forces wrapping
            Elements = new List<TemplateElement>
            {
                new TextElement { Content = "Hello World Foo Bar", Size = "16", Wrap = true }
            }
        };

        var root = engine.ComputeLayout(template);
        var textNode = root.Children[0];

        Assert.NotNull(textNode.TextLines);
        Assert.True(textNode.TextLines.Count > 1,
            $"Expected multiple lines but got {textNode.TextLines.Count}: [{string.Join(", ", textNode.TextLines)}]");
    }

    [Fact]
    public void TextShaper_EmptyContent_TextLinesIsEmptyNotNull()
    {
        var shaper = new MockTextShaper();
        var engine = new LayoutEngine { TextShaper = shaper };

        var template = new Template
        {
            Canvas = new CanvasSettings { Width = 300 },
            Elements = new List<TemplateElement>
            {
                new TextElement { Content = "", Size = "16" }
            }
        };

        var root = engine.ComputeLayout(template);
        var textNode = root.Children[0];

        // Empty content: TextLines should be empty (not null), since shaper was available
        Assert.NotNull(textNode.TextLines);
        Assert.Empty(textNode.TextLines);
    }

    [Fact]
    public void TextShaper_NodeSizeMatchesShaperResult()
    {
        var shaper = new MockTextShaper();
        var engine = new LayoutEngine { TextShaper = shaper };

        var template = new Template
        {
            Canvas = new CanvasSettings { Width = 300 },
            Elements = new List<TemplateElement>
            {
                new TextElement { Content = "Hello", Size = "16" }
            }
        };

        var root = engine.ComputeLayout(template);
        var textNode = root.Children[0];

        // fontSize=16, lineHeight=16*1.2=19.2
        Assert.Equal(19.2f, textNode.Height, 0.1f);
        Assert.Equal(19.2f, textNode.ComputedLineHeight, 0.1f);
    }

    [Fact]
    public void TextShaper_FallbackToTextMeasurer_WhenNoTextShaper()
    {
        // When TextShaper is null but TextMeasurer is set, LayoutEngine should
        // use TextMeasurer (backward compatibility) and TextLines should be null.
        var engine = new LayoutEngine
        {
            TextMeasurer = (element, fontSize, maxWidth) =>
                new LayoutSize(50f, fontSize * 1.4f)
        };

        var template = new Template
        {
            Canvas = new CanvasSettings { Width = 300 },
            Elements = new List<TemplateElement>
            {
                new TextElement { Content = "Hello", Size = "16" }
            }
        };

        var root = engine.ComputeLayout(template);
        var textNode = root.Children[0];

        // TextMeasurer provides height, TextLines stays null (no shaper)
        Assert.Null(textNode.TextLines);
        Assert.Equal(22.4f, textNode.Height, 0.1f); // 16 * 1.4
    }

    [Fact]
    public void TextShaper_TakesPrecedenceOverTextMeasurer()
    {
        // When both are set, TextShaper should take precedence
        var shaper = new MockTextShaper();
        var engine = new LayoutEngine
        {
            TextShaper = shaper,
            TextMeasurer = (element, fontSize, maxWidth) =>
                new LayoutSize(50f, fontSize * 2.0f) // Different height than shaper
        };

        var template = new Template
        {
            Canvas = new CanvasSettings { Width = 300 },
            Elements = new List<TemplateElement>
            {
                new TextElement { Content = "Hello", Size = "16" }
            }
        };

        var root = engine.ComputeLayout(template);
        var textNode = root.Children[0];

        // TextShaper gives height = 16*1.2 = 19.2, not TextMeasurer's 16*2.0 = 32
        Assert.NotNull(textNode.TextLines);
        Assert.Equal(19.2f, textNode.Height, 0.1f);
        Assert.True(shaper.CallCount > 0, "TextShaper should have been called");
    }

    [Fact]
    public void TextShaper_NonTextElements_TextLinesStaysNull()
    {
        var shaper = new MockTextShaper();
        var engine = new LayoutEngine { TextShaper = shaper };

        var template = new Template
        {
            Canvas = new CanvasSettings { Width = 300 },
            Elements = new List<TemplateElement>
            {
                new FlexElement
                {
                    Direction = FlexDirection.Column,
                    Children = new List<TemplateElement>
                    {
                        new SeparatorElement { Thickness = 2f }
                    }
                }
            }
        };

        var root = engine.ComputeLayout(template);
        var flexNode = root.Children[0];
        var separatorNode = flexNode.Children[0];

        Assert.Null(flexNode.TextLines);
        Assert.Null(separatorNode.TextLines);
    }

    [Fact]
    public void TextShaper_TextWithNewlines_SplitsCorrectly()
    {
        var shaper = new MockTextShaper();
        var engine = new LayoutEngine { TextShaper = shaper };

        var template = new Template
        {
            Canvas = new CanvasSettings { Width = 300 },
            Elements = new List<TemplateElement>
            {
                new TextElement { Content = "Line 1\nLine 2\nLine 3", Size = "16" }
            }
        };

        var root = engine.ComputeLayout(template);
        var textNode = root.Children[0];

        Assert.NotNull(textNode.TextLines);
        Assert.Equal(3, textNode.TextLines.Count);
        Assert.Equal("Line 1", textNode.TextLines[0]);
        Assert.Equal("Line 2", textNode.TextLines[1]);
        Assert.Equal("Line 3", textNode.TextLines[2]);
    }

    [Fact]
    public void TextShaper_ExplicitHeight_OverridesShapedHeight()
    {
        var shaper = new MockTextShaper();
        var engine = new LayoutEngine { TextShaper = shaper };

        var template = new Template
        {
            Canvas = new CanvasSettings { Width = 300 },
            Elements = new List<TemplateElement>
            {
                new TextElement { Content = "Hello", Size = "16", Height = "50" }
            }
        };

        var root = engine.ComputeLayout(template);
        var textNode = root.Children[0];

        // Explicit Height should override the shaped height
        Assert.Equal(50f, textNode.Height, 0.1f);
        // But TextLines should still be populated
        Assert.NotNull(textNode.TextLines);
    }
}
