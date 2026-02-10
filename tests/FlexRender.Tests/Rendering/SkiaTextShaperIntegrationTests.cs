using FlexRender.Parsing.Ast;
using FlexRender.Rendering;
using Xunit;

namespace FlexRender.Tests.Rendering;

/// <summary>
/// Integration tests verifying that SkiaRenderer uses SkiaTextShaper
/// and populates TextLines on LayoutNode during layout.
/// </summary>
public sealed class SkiaTextShaperIntegrationTests : IDisposable
{
    private readonly SkiaRenderer _renderer;

    public SkiaTextShaperIntegrationTests()
    {
        _renderer = new SkiaRenderer();
    }

    [Fact]
    public void ComputeLayout_PopulatesTextLinesFromSkiaTextShaper()
    {
        var template = new Template
        {
            Canvas = new CanvasSettings { Width = 300 },
            Elements = new List<TemplateElement>
            {
                new TextElement { Content = "Hello World", Size = "16" }
            }
        };

        var root = _renderer.ComputeLayout(template, new ObjectValue());
        var textNode = root.Children[0];

        Assert.NotNull(textNode.TextLines);
        Assert.True(textNode.TextLines.Count >= 1);
        Assert.True(textNode.ComputedLineHeight > 0f);
    }

    [Fact]
    public void ComputeLayout_WrappedText_ProducesMultipleLines()
    {
        var template = new Template
        {
            Canvas = new CanvasSettings { Width = 60 }, // Very narrow
            Elements = new List<TemplateElement>
            {
                new TextElement
                {
                    Content = "This is a long text that should wrap",
                    Size = "16",
                    Wrap = true
                }
            }
        };

        var root = _renderer.ComputeLayout(template, new ObjectValue());
        var textNode = root.Children[0];

        Assert.NotNull(textNode.TextLines);
        Assert.True(textNode.TextLines.Count > 1,
            $"Expected multiple lines in 60px container, got {textNode.TextLines.Count}");
    }

    public void Dispose()
    {
        _renderer.Dispose();
    }
}
