#pragma warning disable CS0618 // Testing deprecated TextMeasurer backward compatibility

using FlexRender.Layout;
using FlexRender.Parsing.Ast;
using Xunit;

namespace FlexRender.Tests.Layout;

/// <summary>
/// Tests that verify consistency between intrinsic text measurement and layout-phase
/// text height allocation. Reproduces a bug where <see cref="LayoutEngine"/> previously used a
/// hardcoded <c>fontSize * 1.4f</c> multiplier for single-line non-wrapping text in
/// <c>LayoutTextElement</c>, while <c>IntrinsicMeasurer.MeasureTextIntrinsic</c> used
/// the real <see cref="LayoutEngine.TextMeasurer"/> delegate. When the font's actual
/// line spacing differed from the 1.4x assumption, the two passes disagreed on text height.
/// </summary>
public sealed class TextHeightConsistencyTests
{
    /// <summary>
    /// The multiplier used by the mock TextMeasurer to simulate a font (e.g., Inter)
    /// where actual line spacing is 1.2x the font size, rather than the hardcoded 1.4x.
    /// </summary>
    private const float ActualFontSpacingMultiplier = 1.2f;

    /// <summary>
    /// The hardcoded multiplier used by LayoutEngine.LayoutTextElement in its fallback
    /// branch for single-line, non-wrapping text.
    /// </summary>
    private const float HardcodedLayoutMultiplier = 1.4f;

    /// <summary>
    /// Base font size used for layout. Matches LayoutEngine default (16px).
    /// </summary>
    private const float BaseFontSize = 16f;

    /// <summary>
    /// A mock TextMeasurer that simulates a font where line spacing is 1.2x the font
    /// size. The returned width is kept deliberately narrow so the text fits on a single
    /// line and does not trigger the wrapping branch in LayoutTextElement.
    /// </summary>
    private static LayoutSize MockTextMeasurer(TextElement text, float fontSize, float maxWidth)
    {
        // Return a narrow width so text never wraps (single-line path)
        var width = fontSize * text.Content.Length * 0.5f;
        // Height uses the "real" font spacing: 1.2x, not the hardcoded 1.4x
        var height = fontSize * ActualFontSpacingMultiplier;
        return new LayoutSize(width, height);
    }

    /// <summary>
    /// Reproduces the text height inconsistency bug: three text elements in a flex
    /// column with gap=4 and align=center. The IntrinsicMeasurer measured text height
    /// using the TextMeasurer (1.2x), but LayoutTextElement previously fell into the hardcoded
    /// 1.4x fallback for single-line text. This test asserts that the layout-allocated
    /// height for each text child matches the TextMeasurer result, NOT fontSize * 1.4.
    ///
    /// Expected: each text element's layout height equals fontSize * 1.2 (from measurer).
    /// Before fix: each text element's layout height equalled fontSize * 1.4 (hardcoded).
    /// </summary>
    [Fact]
    public void LayoutTextElement_SingleLineNoWrap_HeightMatchesTextMeasurer()
    {
        // Arrange: three text elements in a column flex, simulating an invoice header
        var companyName = new TextElement
        {
            Content = "Company Name",
            Size = "1.4em",
            Font = "bold"
        };
        var invoiceNumber = new TextElement
        {
            Content = "Invoice #INV-2026-0042",
            Size = "0.9em"
        };
        var date = new TextElement
        {
            Content = "February 7, 2026",
            Size = "0.8em"
        };

        var flex = new FlexElement
        {
            Direction = FlexDirection.Column,
            Gap = "4",
            Align = AlignItems.Center
        };
        flex.AddChild(companyName);
        flex.AddChild(invoiceNumber);
        flex.AddChild(date);

        var template = new Template
        {
            Canvas = new CanvasSettings
            {
                Fixed = FixedDimension.Width,
                Width = 300
            },
            Elements = new List<TemplateElement> { flex }
        };

        var engine = new LayoutEngine { TextMeasurer = MockTextMeasurer };

        // Act
        var root = engine.ComputeLayout(template);
        var flexNode = root.Children[0];

        // Resolve expected heights from the mock TextMeasurer
        var fontSize1 = FontSizeResolver.Resolve(companyName.Size, BaseFontSize);
        var fontSize2 = FontSizeResolver.Resolve(invoiceNumber.Size, BaseFontSize);
        var fontSize3 = FontSizeResolver.Resolve(date.Size, BaseFontSize);

        var expectedHeight1 = fontSize1 * ActualFontSpacingMultiplier;
        var expectedHeight2 = fontSize2 * ActualFontSpacingMultiplier;
        var expectedHeight3 = fontSize3 * ActualFontSpacingMultiplier;

        // Also compute the WRONG heights that the bug produces
        var bugHeight1 = fontSize1 * HardcodedLayoutMultiplier;
        var bugHeight2 = fontSize2 * HardcodedLayoutMultiplier;
        var bugHeight3 = fontSize3 * HardcodedLayoutMultiplier;

        // Sanity: confirm that 1.2x and 1.4x produce meaningfully different values
        Assert.NotEqual(expectedHeight1, bugHeight1, 0.01f);
        Assert.NotEqual(expectedHeight2, bugHeight2, 0.01f);
        Assert.NotEqual(expectedHeight3, bugHeight3, 0.01f);

        // Assert: layout-allocated heights should match the TextMeasurer (1.2x),
        // not the hardcoded fallback (1.4x).
        // Before the fix, this failed because LayoutTextElement used 1.4x instead of TextMeasurer.
        Assert.Equal(expectedHeight1, flexNode.Children[0].Height, 0.1f);
        Assert.Equal(expectedHeight2, flexNode.Children[1].Height, 0.1f);
        Assert.Equal(expectedHeight3, flexNode.Children[2].Height, 0.1f);
    }

    /// <summary>
    /// Verifies that children in a column flex do not overlap when text height is
    /// correctly measured. Before the fix, each child was allocated more height than
    /// measured (1.4x vs 1.2x), so the Y positions were spaced further apart than
    /// necessary. While that particular direction of the mismatch did not cause
    /// visual overlap (it caused excess spacing), the test documents the invariant
    /// that should always hold: child[n+1].Y >= child[n].Y + child[n].Height.
    /// </summary>
    [Fact]
    public void LayoutTextElement_FlexColumnWithGap_ChildrenDoNotOverlap()
    {
        // Arrange
        var text1 = new TextElement { Content = "Company Name", Size = "1.4em", Font = "bold" };
        var text2 = new TextElement { Content = "Invoice #INV-2026-0042", Size = "0.9em" };
        var text3 = new TextElement { Content = "February 7, 2026", Size = "0.8em" };

        var flex = new FlexElement
        {
            Direction = FlexDirection.Column,
            Gap = "4",
            Align = AlignItems.Center
        };
        flex.AddChild(text1);
        flex.AddChild(text2);
        flex.AddChild(text3);

        var template = new Template
        {
            Canvas = new CanvasSettings
            {
                Fixed = FixedDimension.Width,
                Width = 300
            },
            Elements = new List<TemplateElement> { flex }
        };

        var engine = new LayoutEngine { TextMeasurer = MockTextMeasurer };

        // Act
        var root = engine.ComputeLayout(template);
        var flexNode = root.Children[0];

        // Assert: no child should overlap with the next child
        for (var i = 0; i < flexNode.Children.Count - 1; i++)
        {
            var current = flexNode.Children[i];
            var next = flexNode.Children[i + 1];
            Assert.True(
                next.Y >= current.Y + current.Height,
                $"Child {i + 1} (Y={next.Y:F2}) overlaps with child {i} " +
                $"(Y={current.Y:F2}, Height={current.Height:F2}, Bottom={current.Y + current.Height:F2})");
        }
    }

    /// <summary>
    /// Parameterized test that verifies the height consistency bug across different
    /// font size specifications. For each font size, the layout-allocated height should
    /// match the TextMeasurer result (1.2x), not the hardcoded 1.4x fallback.
    /// </summary>
    [Theory]
    [InlineData("16px", 16f)]
    [InlineData("1.4em", 22.4f)]
    [InlineData("0.9em", 14.4f)]
    [InlineData("0.8em", 12.8f)]
    [InlineData("24", 24f)]
    public void LayoutTextElement_SingleLineHeight_MatchesMeasuredHeight(string sizeSpec, float expectedFontSize)
    {
        // Arrange
        var text = new TextElement
        {
            Content = "Sample text that fits on one line",
            Size = sizeSpec
        };

        var flex = new FlexElement { Direction = FlexDirection.Column };
        flex.AddChild(text);

        var template = new Template
        {
            Canvas = new CanvasSettings
            {
                Fixed = FixedDimension.Width,
                Width = 600 // Wide enough that text never wraps
            },
            Elements = new List<TemplateElement> { flex }
        };

        var engine = new LayoutEngine { TextMeasurer = MockTextMeasurer };

        // Act
        var root = engine.ComputeLayout(template);
        var textNode = root.Children[0].Children[0];

        // Verify font size resolution is correct
        var resolvedFontSize = FontSizeResolver.Resolve(sizeSpec, BaseFontSize);
        Assert.Equal(expectedFontSize, resolvedFontSize, 0.01f);

        // The TextMeasurer returns height = fontSize * 1.2
        var measuredHeight = resolvedFontSize * ActualFontSpacingMultiplier;
        // The buggy code uses height = fontSize * 1.4
        var buggyHeight = resolvedFontSize * HardcodedLayoutMultiplier;

        // Assert: layout height should match the measured height, not the hardcoded one.
        // This previously failed because LayoutTextElement ignored TextMeasurer for single-line text.
        Assert.Equal(
            measuredHeight,
            textNode.Height,
            0.1f);

        // Double-check: the buggy value should differ from the expected value
        Assert.True(
            Math.Abs(buggyHeight - measuredHeight) > 1f,
            $"Test setup error: buggy height ({buggyHeight:F2}) should differ meaningfully " +
            $"from measured height ({measuredHeight:F2})");
    }

    /// <summary>
    /// Verifies that intrinsic measurement and layout-phase measurement produce
    /// consistent heights. The IntrinsicMeasurer uses TextMeasurer directly, and
    /// LayoutTextElement now also uses TextMeasurer (previously it fell back to the
    /// hardcoded 1.4x multiplier for single-line text). These two passes should agree on text height.
    /// </summary>
    [Fact]
    public void IntrinsicAndLayout_TextHeight_AreConsistent()
    {
        // Arrange
        var text = new TextElement
        {
            Content = "Hello World",
            Size = "1.4em"
        };

        var engine = new LayoutEngine { TextMeasurer = MockTextMeasurer };

        // Act: measure intrinsic
        var intrinsics = engine.MeasureAllIntrinsics(text);
        var intrinsicSize = intrinsics[text];

        // Act: run full layout with a flex column wrapper
        var flex = new FlexElement { Direction = FlexDirection.Column };
        flex.AddChild(text);

        var template = new Template
        {
            Canvas = new CanvasSettings
            {
                Fixed = FixedDimension.Width,
                Width = 600
            },
            Elements = new List<TemplateElement> { flex }
        };

        var root = engine.ComputeLayout(template);
        var textNode = root.Children[0].Children[0];

        // Assert: intrinsic height and layout height should be the same.
        // The intrinsic pass uses TextMeasurer (height = fontSize * 1.2),
        // and the layout pass now also uses TextMeasurer instead of the hardcoded fallback.
        // This previously failed due to the inconsistency between the two passes.
        Assert.Equal(
            intrinsicSize.MaxHeight,
            textNode.Height,
            0.1f);
    }
}
