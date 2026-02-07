using FlexRender.Configuration;
using FlexRender.Layout;
using FlexRender.Parsing.Ast;
using FlexRender.Svg;
using Xunit;

namespace FlexRender.Tests.Rendering;

/// <summary>
/// Tests that SVG renderer uses pre-computed TextLines from LayoutNode
/// for word-wrap support. Previously, SVG only split on '\n'.
/// </summary>
public sealed class SvgTextWrappingTests : IDisposable
{
    private readonly SvgRender _svgRender;

    public SvgTextWrappingTests()
    {
        // Create SvgRender without Skia backend -- uses ApproximateTextShaper
        _svgRender = new SvgRender(
            new ResourceLimits(),
            new FlexRenderOptions { BaseFontSize = 12f });
    }

    [Fact]
    public async Task RenderToSvg_WrappedText_ProducesMultipleTspanElements()
    {
        var template = new Template
        {
            Canvas = new CanvasSettings { Width = 80 },
            Elements = new List<TemplateElement>
            {
                new TextElement
                {
                    Content = "Hello World Foo Bar Baz",
                    Size = "12",
                    Wrap = true
                }
            }
        };

        var svg = await _svgRender.RenderToSvg(template);

        // Count tspan elements: should have more than 1 due to word wrapping
        var tspanCount = CountOccurrences(svg, "<tspan");
        Assert.True(tspanCount > 1,
            $"Expected multiple <tspan> elements for wrapped text, got {tspanCount}. SVG:\n{svg}");
    }

    [Fact]
    public async Task RenderToSvg_ExplicitNewlines_ProducesMultipleTspanElements()
    {
        var template = new Template
        {
            Canvas = new CanvasSettings { Width = 300 },
            Elements = new List<TemplateElement>
            {
                new TextElement
                {
                    Content = "Line 1\nLine 2\nLine 3",
                    Size = "12"
                }
            }
        };

        var svg = await _svgRender.RenderToSvg(template);

        var tspanCount = CountOccurrences(svg, "<tspan");
        Assert.Equal(3, tspanCount);
    }

    [Fact]
    public async Task RenderToSvg_SingleLineText_HasOneTspan()
    {
        var template = new Template
        {
            Canvas = new CanvasSettings { Width = 300 },
            Elements = new List<TemplateElement>
            {
                new TextElement { Content = "Hi", Size = "12" }
            }
        };

        var svg = await _svgRender.RenderToSvg(template);

        var tspanCount = CountOccurrences(svg, "<tspan");
        Assert.Equal(1, tspanCount);
    }

    [Fact]
    public async Task RenderToSvg_WrappedText_TspanHasDyAttribute()
    {
        var template = new Template
        {
            Canvas = new CanvasSettings { Width = 60 },
            Elements = new List<TemplateElement>
            {
                new TextElement
                {
                    Content = "Word1 Word2 Word3 Word4",
                    Size = "12",
                    Wrap = true
                }
            }
        };

        var svg = await _svgRender.RenderToSvg(template);

        // Second and subsequent tspans should have dy attribute for line spacing
        Assert.Contains("dy=\"", svg);
    }

    private static int CountOccurrences(string text, string pattern)
    {
        var count = 0;
        var index = 0;
        while ((index = text.IndexOf(pattern, index, StringComparison.Ordinal)) != -1)
        {
            count++;
            index += pattern.Length;
        }
        return count;
    }

    public void Dispose()
    {
        _svgRender.Dispose();
    }
}
