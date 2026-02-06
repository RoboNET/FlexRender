using FlexRender.Layout;
using FlexRender.Parsing.Ast;
using Xunit;

namespace FlexRender.Tests.Layout;

/// <summary>
/// Tests for padding and margin support on all element types.
/// </summary>
public class LayoutEnginePaddingMarginTests
{
    private readonly LayoutEngine _engine = new();

    // ============================================
    // TextElement Tests
    // ============================================

    /// <summary>
    /// Verifies that padding on a TextElement increases its total size.
    /// </summary>
    [Fact]
    public void TextElement_WithPadding_IncreasesSize()
    {
        var template = new Template
        {
            Canvas = new CanvasSettings { Width = 200 },
            Elements = new List<TemplateElement>
            {
                new TextElement
                {
                    Content = "Test",
                    Size = "16",
                    Padding = "10"
                }
            }
        };

        var root = _engine.ComputeLayout(template);
        var textNode = root.Children[0];

        // Text with padding should have padding added to dimensions
        // Height should be at least fontSize * 1.4 (line height) + padding * 2 = 22.4 + 20 = 42.4
        Assert.True(textNode.Height >= 40f, $"Height {textNode.Height} should include padding (>= 40)");
    }

    /// <summary>
    /// Verifies that margin on a TextElement affects its position.
    /// </summary>
    [Fact]
    public void TextElement_WithMargin_AffectsPosition()
    {
        var template = new Template
        {
            Canvas = new CanvasSettings { Width = 200 },
            Elements = new List<TemplateElement>
            {
                new TextElement
                {
                    Content = "Test",
                    Size = "16",
                    Margin = "15"
                }
            }
        };

        var root = _engine.ComputeLayout(template);
        var textNode = root.Children[0];

        // Element should be offset by margin
        Assert.Equal(15f, textNode.X);
        Assert.Equal(15f, textNode.Y);
    }

    /// <summary>
    /// Verifies that both padding and margin work together on TextElement.
    /// </summary>
    [Fact]
    public void TextElement_WithPaddingAndMargin_BothApplied()
    {
        var template = new Template
        {
            Canvas = new CanvasSettings { Width = 200 },
            Elements = new List<TemplateElement>
            {
                new TextElement
                {
                    Content = "Test",
                    Size = "16",
                    Padding = "5",
                    Margin = "10"
                }
            }
        };

        var root = _engine.ComputeLayout(template);
        var textNode = root.Children[0];

        // Position should include margin
        Assert.Equal(10f, textNode.X);
        Assert.Equal(10f, textNode.Y);

        // Size should include padding (but not margin - margin is outside)
        // Height should be fontSize * 1.4 + padding * 2 = 22.4 + 10 = 32.4
        Assert.True(textNode.Height >= 30f, $"Height {textNode.Height} should include padding");
    }

    // ============================================
    // ImageElement Tests
    // ============================================

    /// <summary>
    /// Verifies that padding on an ImageElement increases its total size.
    /// </summary>
    [Fact]
    public void ImageElement_WithPadding_IncreasesSize()
    {
        var template = new Template
        {
            Canvas = new CanvasSettings { Width = 200 },
            Elements = new List<TemplateElement>
            {
                new ImageElement
                {
                    Src = "test.png",
                    ImageWidth = 50,
                    ImageHeight = 50,
                    Padding = "5"
                }
            }
        };

        var root = _engine.ComputeLayout(template);
        var imageNode = root.Children[0];

        // 50 + 5*2 = 60
        Assert.Equal(60f, imageNode.Width);
        Assert.Equal(60f, imageNode.Height);
    }

    /// <summary>
    /// Verifies that margin on an ImageElement affects its position.
    /// </summary>
    [Fact]
    public void ImageElement_WithMargin_AffectsPosition()
    {
        var template = new Template
        {
            Canvas = new CanvasSettings { Width = 200 },
            Elements = new List<TemplateElement>
            {
                new ImageElement
                {
                    Src = "test.png",
                    ImageWidth = 50,
                    ImageHeight = 50,
                    Margin = "8"
                }
            }
        };

        var root = _engine.ComputeLayout(template);
        var imageNode = root.Children[0];

        Assert.Equal(8f, imageNode.X);
        Assert.Equal(8f, imageNode.Y);
    }

    // ============================================
    // QrElement Tests
    // ============================================

    /// <summary>
    /// Verifies that padding on a QrElement increases its total size.
    /// </summary>
    [Fact]
    public void QrElement_WithPadding_IncreasesSize()
    {
        var template = new Template
        {
            Canvas = new CanvasSettings { Width = 200 },
            Elements = new List<TemplateElement>
            {
                new QrElement
                {
                    Data = "test",
                    Size = 80,
                    Padding = "10"
                }
            }
        };

        var root = _engine.ComputeLayout(template);
        var qrNode = root.Children[0];

        // 80 + 10*2 = 100
        Assert.Equal(100f, qrNode.Width);
        Assert.Equal(100f, qrNode.Height);
    }

    /// <summary>
    /// Verifies that margin on a QrElement affects its position.
    /// </summary>
    [Fact]
    public void QrElement_WithMargin_AffectsPosition()
    {
        var template = new Template
        {
            Canvas = new CanvasSettings { Width = 200 },
            Elements = new List<TemplateElement>
            {
                new QrElement
                {
                    Data = "test",
                    Size = 80,
                    Margin = "12"
                }
            }
        };

        var root = _engine.ComputeLayout(template);
        var qrNode = root.Children[0];

        Assert.Equal(12f, qrNode.X);
        Assert.Equal(12f, qrNode.Y);
    }

    // ============================================
    // BarcodeElement Tests
    // ============================================

    /// <summary>
    /// Verifies that padding on a BarcodeElement increases its total size.
    /// </summary>
    [Fact]
    public void BarcodeElement_WithPadding_IncreasesSize()
    {
        var template = new Template
        {
            Canvas = new CanvasSettings { Width = 300 },
            Elements = new List<TemplateElement>
            {
                new BarcodeElement
                {
                    Data = "123456",
                    BarcodeWidth = 150,
                    BarcodeHeight = 60,
                    Padding = "7"
                }
            }
        };

        var root = _engine.ComputeLayout(template);
        var barcodeNode = root.Children[0];

        // 150 + 7*2 = 164, 60 + 7*2 = 74
        Assert.Equal(164f, barcodeNode.Width);
        Assert.Equal(74f, barcodeNode.Height);
    }

    /// <summary>
    /// Verifies that margin on a BarcodeElement affects its position.
    /// </summary>
    [Fact]
    public void BarcodeElement_WithMargin_AffectsPosition()
    {
        var template = new Template
        {
            Canvas = new CanvasSettings { Width = 300 },
            Elements = new List<TemplateElement>
            {
                new BarcodeElement
                {
                    Data = "123456",
                    BarcodeWidth = 150,
                    BarcodeHeight = 60,
                    Margin = "20"
                }
            }
        };

        var root = _engine.ComputeLayout(template);
        var barcodeNode = root.Children[0];

        Assert.Equal(20f, barcodeNode.X);
        Assert.Equal(20f, barcodeNode.Y);
    }

    // ============================================
    // Percentage and Em Units Tests
    // ============================================

    /// <summary>
    /// Verifies that percentage-based padding works correctly.
    /// </summary>
    [Fact]
    public void Element_WithPercentPadding_CalculatesCorrectly()
    {
        var template = new Template
        {
            Canvas = new CanvasSettings { Width = 200 },
            Elements = new List<TemplateElement>
            {
                new QrElement
                {
                    Data = "test",
                    Size = 50,
                    Padding = "5%" // 5% of 200 = 10
                }
            }
        };

        var root = _engine.ComputeLayout(template);
        var qrNode = root.Children[0];

        // 50 + 10*2 = 70
        Assert.Equal(70f, qrNode.Width);
        Assert.Equal(70f, qrNode.Height);
    }

    /// <summary>
    /// Verifies that em-based margin works correctly.
    /// </summary>
    [Fact]
    public void Element_WithEmMargin_CalculatesCorrectly()
    {
        var template = new Template
        {
            Canvas = new CanvasSettings { Width = 200 },
            Elements = new List<TemplateElement>
            {
                new ImageElement
                {
                    Src = "test.png",
                    ImageWidth = 50,
                    ImageHeight = 50,
                    Margin = "1em" // 1em = 16px (default font size)
                }
            }
        };

        var root = _engine.ComputeLayout(template);
        var imageNode = root.Children[0];

        Assert.Equal(16f, imageNode.X);
        Assert.Equal(16f, imageNode.Y);
    }

    // ============================================
    // FlexElement Children with Margin Tests
    // ============================================

    /// <summary>
    /// Verifies that margin on child elements in a column flex affects stacking.
    /// </summary>
    [Fact]
    public void FlexColumn_ChildrenWithMargin_StacksWithMarginSpace()
    {
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
                        new TextElement { Content = "First", Height = "30", Margin = "5" },
                        new TextElement { Content = "Second", Height = "30", Margin = "5" }
                    }
                }
            }
        };

        var root = _engine.ComputeLayout(template);
        var flex = root.Children[0];

        // First element at Y = 5 (its margin)
        Assert.Equal(5f, flex.Children[0].Y);
        Assert.Equal(5f, flex.Children[0].X);

        // Second element should account for first element's total height plus its own margin
        // First: Y=5, Height=30 (content height, size includes padding)
        // So second should be at Y = 5 + 30 + 5 (first margin) + 5 (second margin) = 45
        // Or if margin is outer box: first ends at 5 + 30 = 35, second starts at 35 + 5 = 40
        Assert.True(flex.Children[1].Y > flex.Children[0].Y + flex.Children[0].Height,
            $"Second child Y {flex.Children[1].Y} should be after first child bottom {flex.Children[0].Y + flex.Children[0].Height}");
    }

    /// <summary>
    /// Verifies that margin on child elements in a row flex affects positioning.
    /// </summary>
    [Fact]
    public void FlexRow_ChildrenWithMargin_PositionsWithMarginSpace()
    {
        var template = new Template
        {
            Canvas = new CanvasSettings { Width = 300 },
            Elements = new List<TemplateElement>
            {
                new FlexElement
                {
                    Direction = FlexDirection.Row,
                    Children = new List<TemplateElement>
                    {
                        new TextElement { Content = "First", Width = "50", Margin = "10" },
                        new TextElement { Content = "Second", Width = "50", Margin = "10" }
                    }
                }
            }
        };

        var root = _engine.ComputeLayout(template);
        var flex = root.Children[0];

        // First element at X = 10 (its margin)
        Assert.Equal(10f, flex.Children[0].X);

        // Second element should account for first element plus margins
        Assert.True(flex.Children[1].X > flex.Children[0].X + flex.Children[0].Width,
            $"Second child X {flex.Children[1].X} should be after first child right edge {flex.Children[0].X + flex.Children[0].Width}");
    }

    // ============================================
    // Edge Cases
    // ============================================

    /// <summary>
    /// Verifies that zero padding has no effect.
    /// </summary>
    [Fact]
    public void Element_WithZeroPadding_NoSizeIncrease()
    {
        var template = new Template
        {
            Canvas = new CanvasSettings { Width = 200 },
            Elements = new List<TemplateElement>
            {
                new QrElement
                {
                    Data = "test",
                    Size = 80,
                    Padding = "0"
                }
            }
        };

        var root = _engine.ComputeLayout(template);
        var qrNode = root.Children[0];

        Assert.Equal(80f, qrNode.Width);
        Assert.Equal(80f, qrNode.Height);
    }

    /// <summary>
    /// Verifies that zero margin has no position offset.
    /// </summary>
    [Fact]
    public void Element_WithZeroMargin_NoPositionOffset()
    {
        var template = new Template
        {
            Canvas = new CanvasSettings { Width = 200 },
            Elements = new List<TemplateElement>
            {
                new QrElement
                {
                    Data = "test",
                    Size = 80,
                    Margin = "0"
                }
            }
        };

        var root = _engine.ComputeLayout(template);
        var qrNode = root.Children[0];

        Assert.Equal(0f, qrNode.X);
        Assert.Equal(0f, qrNode.Y);
    }

    /// <summary>
    /// Verifies that default (no padding/margin specified) elements behave as before.
    /// </summary>
    [Fact]
    public void Element_WithDefaultPaddingMargin_NoBehaviorChange()
    {
        var template = new Template
        {
            Canvas = new CanvasSettings { Width = 200 },
            Elements = new List<TemplateElement>
            {
                new QrElement
                {
                    Data = "test",
                    Size = 80
                    // Padding and Margin default to "0"
                }
            }
        };

        var root = _engine.ComputeLayout(template);
        var qrNode = root.Children[0];

        Assert.Equal(0f, qrNode.X);
        Assert.Equal(0f, qrNode.Y);
        Assert.Equal(80f, qrNode.Width);
        Assert.Equal(80f, qrNode.Height);
    }

    // ============================================
    // Non-Uniform Padding Tests
    // ============================================

    /// <summary>
    /// Verifies that two-value padding on a TextElement increases its total size
    /// using different vertical and horizontal padding.
    /// </summary>
    [Fact]
    public void TextElement_WithTwoValuePadding_IncreasesSize()
    {
        // padding: "5 15" -> top/bottom=5, left/right=15
        var template = new Template
        {
            Canvas = new CanvasSettings { Width = 200 },
            Elements = new List<TemplateElement>
            {
                new TextElement
                {
                    Content = "Test",
                    Size = "16",
                    Padding = "5 15"
                }
            }
        };

        var root = _engine.ComputeLayout(template);
        var textNode = root.Children[0];

        // Width should include horizontal padding: content + 15 + 15 = content + 30
        // Height should include vertical padding: lineHeight + 5 + 5 = lineHeight + 10
        var expectedMinHeight = 16f * 1.4f + 10f; // 22.4 + 10 = 32.4
        Assert.Equal(expectedMinHeight, textNode.Height, 1);
    }

    /// <summary>
    /// Verifies that four-value padding on a TextElement increases its total size
    /// with different top, right, bottom, and left values.
    /// </summary>
    [Fact]
    public void TextElement_WithFourValuePadding_IncreasesSize()
    {
        // padding: "10 20 30 40" -> top=10, right=20, bottom=30, left=40
        var template = new Template
        {
            Canvas = new CanvasSettings { Width = 200 },
            Elements = new List<TemplateElement>
            {
                new TextElement
                {
                    Content = "Test",
                    Size = "16",
                    Padding = "10 20 30 40"
                }
            }
        };

        var root = _engine.ComputeLayout(template);
        var textNode = root.Children[0];

        // Height: lineHeight + top(10) + bottom(30) = 22.4 + 40 = 62.4
        var expectedHeight = 16f * 1.4f + 40f;
        Assert.Equal(expectedHeight, textNode.Height, 1);
        // Width: content + left(40) + right(20) = content + 60
        Assert.True(textNode.Width >= 60f, $"Width {textNode.Width} should be >= 60 (padding alone)");
    }

    /// <summary>
    /// Verifies that two-value padding on a QrElement increases its total size
    /// using different vertical and horizontal padding.
    /// </summary>
    [Fact]
    public void QrElement_WithTwoValuePadding_IncreasesSize()
    {
        // padding: "5 20" -> top/bottom=5, left/right=20
        var template = new Template
        {
            Canvas = new CanvasSettings { Width = 200 },
            Elements = new List<TemplateElement>
            {
                new QrElement
                {
                    Data = "test",
                    Size = 80,
                    Padding = "5 20"
                }
            }
        };

        var root = _engine.ComputeLayout(template);
        var qrNode = root.Children[0];

        // Width: 80 + 20 + 20 = 120
        Assert.Equal(120f, qrNode.Width);
        // Height: 80 + 5 + 5 = 90
        Assert.Equal(90f, qrNode.Height);
    }

    /// <summary>
    /// Verifies that four-value padding on a BarcodeElement increases its total size
    /// with different top, right, bottom, and left values.
    /// </summary>
    [Fact]
    public void BarcodeElement_WithFourValuePadding_IncreasesSize()
    {
        // padding: "5 10 15 20" -> top=5, right=10, bottom=15, left=20
        var template = new Template
        {
            Canvas = new CanvasSettings { Width = 300 },
            Elements = new List<TemplateElement>
            {
                new BarcodeElement
                {
                    Data = "123456",
                    BarcodeWidth = 150,
                    BarcodeHeight = 60,
                    Padding = "5 10 15 20"
                }
            }
        };

        var root = _engine.ComputeLayout(template);
        var barcodeNode = root.Children[0];

        // Width: 150 + 20 + 10 = 180
        Assert.Equal(180f, barcodeNode.Width);
        // Height: 60 + 5 + 15 = 80
        Assert.Equal(80f, barcodeNode.Height);
    }

    /// <summary>
    /// Verifies that two-value padding on an ImageElement increases its total size
    /// using different vertical and horizontal padding.
    /// </summary>
    [Fact]
    public void ImageElement_WithTwoValuePadding_IncreasesSize()
    {
        // padding: "10 30" -> top/bottom=10, left/right=30
        var template = new Template
        {
            Canvas = new CanvasSettings { Width = 200 },
            Elements = new List<TemplateElement>
            {
                new ImageElement
                {
                    Src = "test.png",
                    ImageWidth = 50,
                    ImageHeight = 50,
                    Padding = "10 30"
                }
            }
        };

        var root = _engine.ComputeLayout(template);
        var imageNode = root.Children[0];

        // Width: 50 + 30 + 30 = 110
        Assert.Equal(110f, imageNode.Width);
        // Height: 50 + 10 + 10 = 70
        Assert.Equal(70f, imageNode.Height);
    }

    /// <summary>
    /// Verifies that single-value padding still produces the same result
    /// after the PaddingParser migration (backward compatibility).
    /// </summary>
    [Fact]
    public void UniformPadding_StillWorks_BackwardCompatible()
    {
        // Verify that single-value padding still produces the same result
        var template = new Template
        {
            Canvas = new CanvasSettings { Width = 200 },
            Elements = new List<TemplateElement>
            {
                new QrElement
                {
                    Data = "test",
                    Size = 80,
                    Padding = "10"
                }
            }
        };

        var root = _engine.ComputeLayout(template);
        var qrNode = root.Children[0];

        // 80 + 10*2 = 100 (same as before)
        Assert.Equal(100f, qrNode.Width);
        Assert.Equal(100f, qrNode.Height);
    }

    // ============================================
    // Integration: Nested Flex with Non-Uniform Padding
    // ============================================

    /// <summary>
    /// Verifies that nested flex containers with non-uniform padding
    /// correctly compose their padding offsets.
    /// </summary>
    [Fact]
    public void NestedFlex_ParentAndChildNonUniformPadding_PositionsCorrectly()
    {
        // Parent: padding "20 10" -> top/bottom=20, left/right=10
        // Child flex: padding "5 15" -> top/bottom=5, left/right=15
        var template = new Template
        {
            Canvas = new CanvasSettings { Width = 400 },
            Elements = new List<TemplateElement>
            {
                new FlexElement
                {
                    Direction = FlexDirection.Column,
                    Padding = "20 10",
                    Children = new List<TemplateElement>
                    {
                        new FlexElement
                        {
                            Direction = FlexDirection.Column,
                            Padding = "5 15",
                            Children = new List<TemplateElement>
                            {
                                new TextElement { Content = "Inner text", Height = "30" }
                            }
                        }
                    }
                }
            }
        };

        var root = _engine.ComputeLayout(template);
        var outerFlex = root.Children[0];
        var innerFlex = outerFlex.Children[0];
        var text = innerFlex.Children[0];

        // Outer flex child X = left padding(10)
        Assert.Equal(10f, innerFlex.X);
        // Outer flex child Y = top padding(20)
        Assert.Equal(20f, innerFlex.Y);

        // Inner flex child X = left padding(15)
        Assert.Equal(15f, text.X);
        // Inner flex child Y = top padding(5)
        Assert.Equal(5f, text.Y);

        // Inner flex width = 400 - 10*2 (outer horizontal padding) = 380 (stretched)
        Assert.Equal(380f, innerFlex.Width);
        // Inner flex height = 30 (child) + 5+5 (inner vertical padding) = 40
        Assert.Equal(40f, innerFlex.Height);
    }

    /// <summary>
    /// Verifies that row flex with four-value padding and a gap
    /// positions children correctly.
    /// </summary>
    [Fact]
    public void RowFlex_NonUniformPaddingWithGap_ChildrenPositionedCorrectly()
    {
        // padding: "10 20 10 20" (uniform-ish but via 4 values)
        var template = new Template
        {
            Canvas = new CanvasSettings { Width = 300 },
            Elements = new List<TemplateElement>
            {
                new FlexElement
                {
                    Direction = FlexDirection.Row,
                    Padding = "10 20 10 20",
                    Gap = "10",
                    Children = new List<TemplateElement>
                    {
                        new TextElement { Content = "A", Width = "80", Height = "40" },
                        new TextElement { Content = "B", Width = "80", Height = "40" }
                    }
                }
            }
        };

        var root = _engine.ComputeLayout(template);
        var flex = root.Children[0];

        // First child: X = left(20), Y = top(10)
        Assert.Equal(20f, flex.Children[0].X);
        Assert.Equal(10f, flex.Children[0].Y);

        // Second child: X = 20 + 80 + 10(gap) = 110
        Assert.Equal(110f, flex.Children[1].X);
        Assert.Equal(10f, flex.Children[1].Y);
    }

    /// <summary>
    /// Verifies that three-value padding on a flex column correctly applies
    /// different top and bottom values.
    /// </summary>
    [Fact]
    public void FlexColumn_ThreeValuePadding_TopAndBottomDiffer()
    {
        // padding: "10 20 30" -> top=10, left/right=20, bottom=30
        var template = new Template
        {
            Canvas = new CanvasSettings { Width = 200 },
            Elements = new List<TemplateElement>
            {
                new FlexElement
                {
                    Direction = FlexDirection.Column,
                    Padding = "10 20 30",
                    Children = new List<TemplateElement>
                    {
                        new TextElement { Content = "Hello", Height = "40" }
                    }
                }
            }
        };

        var root = _engine.ComputeLayout(template);
        var flex = root.Children[0];

        // Y = top padding = 10
        Assert.Equal(10f, flex.Children[0].Y);
        // X = left padding = 20
        Assert.Equal(20f, flex.Children[0].X);
        // Height = child(40) + top(10) + bottom(30) = 80
        Assert.Equal(80f, flex.Height);
    }

    /// <summary>
    /// Representative backward compatibility check verifying that
    /// single-value padding on a flex container still works identically.
    /// </summary>
    [Fact]
    public void AllExistingTests_SingleValuePadding_StillPass()
    {
        // This is a meta-assertion: by running the full test suite,
        // we verify that all existing tests with single-value padding still pass.
        // This individual test is a representative backward compat check.
        var template = new Template
        {
            Canvas = new CanvasSettings { Width = 300 },
            Elements = new List<TemplateElement>
            {
                new FlexElement
                {
                    Direction = FlexDirection.Column,
                    Padding = "20",
                    Children = new List<TemplateElement>
                    {
                        new TextElement { Content = "First", Height = "50" }
                    }
                }
            }
        };

        var root = _engine.ComputeLayout(template);
        var flex = root.Children[0];

        // Same as existing test: uniform padding = 20 on all sides
        Assert.Equal(20f, flex.Children[0].X);
        Assert.Equal(20f, flex.Children[0].Y);
        Assert.Equal(90f, flex.Height); // 50 + 20 + 20
    }

    // ============================================
    // 4-Side Margin Tests
    // ============================================

    /// <summary>
    /// Verifies that 4-value margin applies individual margins to each side.
    /// margin: "10 20 30 40" -> top=10, right=20, bottom=30, left=40
    /// </summary>
    [Fact]
    public void ComputeLayout_Margin4Side_AppliesIndividualMargins()
    {
        var template = new Template
        {
            Canvas = new CanvasSettings { Width = 400 },
            Elements = new List<TemplateElement>
            {
                new FlexElement
                {
                    Direction = FlexDirection.Column,
                    Children = new List<TemplateElement>
                    {
                        new TextElement { Content = "Test", Height = "30", Margin = "10 20 30 40" }
                    }
                }
            }
        };

        var root = _engine.ComputeLayout(template);
        var flex = root.Children[0];
        var child = flex.Children[0];

        // Child should be offset by left=40, top=10
        Assert.Equal(40f, child.X, 0.1f);
        Assert.Equal(10f, child.Y, 0.1f);
    }

    /// <summary>
    /// Verifies that 2-value margin applies vertical and horizontal margins.
    /// margin: "10 30" -> top/bottom=10, left/right=30
    /// </summary>
    [Fact]
    public void ComputeLayout_Margin2Value_AppliesVerticalHorizontal()
    {
        var template = new Template
        {
            Canvas = new CanvasSettings { Width = 400 },
            Elements = new List<TemplateElement>
            {
                new FlexElement
                {
                    Direction = FlexDirection.Column,
                    Children = new List<TemplateElement>
                    {
                        new TextElement { Content = "Test", Height = "30", Margin = "10 30" }
                    }
                }
            }
        };

        var root = _engine.ComputeLayout(template);
        var flex = root.Children[0];
        var child = flex.Children[0];

        // Child should be offset by left=30, top=10
        Assert.Equal(30f, child.X, 0.1f);
        Assert.Equal(10f, child.Y, 0.1f);
    }

    /// <summary>
    /// Verifies that 4-side margin in column direction affects Y position
    /// and includes top + bottom margins in stacking.
    /// </summary>
    [Fact]
    public void ComputeLayout_MarginColumn_AffectsYPositionAndXPosition()
    {
        var template = new Template
        {
            Canvas = new CanvasSettings { Width = 400 },
            Elements = new List<TemplateElement>
            {
                new FlexElement
                {
                    Direction = FlexDirection.Column,
                    Children = new List<TemplateElement>
                    {
                        new TextElement { Content = "First", Height = "30", Margin = "5 10 15 20" },
                        new TextElement { Content = "Second", Height = "30", Margin = "5 10 15 20" }
                    }
                }
            }
        };

        var root = _engine.ComputeLayout(template);
        var flex = root.Children[0];

        // First child: X=left(20), Y=top(5)
        Assert.Equal(20f, flex.Children[0].X, 0.1f);
        Assert.Equal(5f, flex.Children[0].Y, 0.1f);

        // Second child: Y = first.Y(5) + first.Height(30) + first.marginBottom(15) + second.marginTop(5) = 55
        Assert.Equal(55f, flex.Children[1].Y, 0.1f);
        Assert.Equal(20f, flex.Children[1].X, 0.1f);
    }

    /// <summary>
    /// Verifies that 4-side margin in row direction affects X position
    /// and includes left + right margins in stacking.
    /// </summary>
    [Fact]
    public void ComputeLayout_MarginRow_AffectsXPositionAndYPosition()
    {
        var template = new Template
        {
            Canvas = new CanvasSettings { Width = 400 },
            Elements = new List<TemplateElement>
            {
                new FlexElement
                {
                    Direction = FlexDirection.Row,
                    Children = new List<TemplateElement>
                    {
                        new TextElement { Content = "First", Width = "60", Height = "30", Margin = "5 10 15 20" },
                        new TextElement { Content = "Second", Width = "60", Height = "30", Margin = "5 10 15 20" }
                    }
                }
            }
        };

        var root = _engine.ComputeLayout(template);
        var flex = root.Children[0];

        // First child: X=left(20), Y=top(5)
        Assert.Equal(20f, flex.Children[0].X, 0.1f);
        Assert.Equal(5f, flex.Children[0].Y, 0.1f);

        // Second child: X = first.X(20) + first.Width(60) + first.marginRight(10) + second.marginLeft(20) = 110
        Assert.Equal(110f, flex.Children[1].X, 0.1f);
        Assert.Equal(5f, flex.Children[1].Y, 0.1f);
    }

    /// <summary>
    /// Verifies that margin and gap are both applied (they stack).
    /// </summary>
    [Fact]
    public void ComputeLayout_MarginWithGap_BothApplied()
    {
        var template = new Template
        {
            Canvas = new CanvasSettings { Width = 400 },
            Elements = new List<TemplateElement>
            {
                new FlexElement
                {
                    Direction = FlexDirection.Column,
                    Gap = "10",
                    Children = new List<TemplateElement>
                    {
                        new TextElement { Content = "First", Height = "30", Margin = "5 0 5 0" },
                        new TextElement { Content = "Second", Height = "30", Margin = "5 0 5 0" }
                    }
                }
            }
        };

        var root = _engine.ComputeLayout(template);
        var flex = root.Children[0];

        // First child: Y = marginTop(5)
        Assert.Equal(5f, flex.Children[0].Y, 0.1f);

        // Second child: Y = first.Y(5) + first.Height(30) + first.marginBottom(5) + gap(10) + second.marginTop(5) = 55
        Assert.Equal(55f, flex.Children[1].Y, 0.1f);
    }

    /// <summary>
    /// Verifies that negative margin values (if parsed) are clamped to zero.
    /// </summary>
    [Fact]
    public void ComputeLayout_MarginNegativeClamp_ClampsToZero()
    {
        var template = new Template
        {
            Canvas = new CanvasSettings { Width = 400 },
            Elements = new List<TemplateElement>
            {
                new FlexElement
                {
                    Direction = FlexDirection.Column,
                    Children = new List<TemplateElement>
                    {
                        new TextElement { Content = "Test", Height = "30", Margin = "-5" }
                    }
                }
            }
        };

        var root = _engine.ComputeLayout(template);
        var flex = root.Children[0];
        var child = flex.Children[0];

        // Negative margin should be clamped to 0
        Assert.True(child.X >= 0f, $"X={child.X} should be >= 0 (negative margin clamped)");
        Assert.True(child.Y >= 0f, $"Y={child.Y} should be >= 0 (negative margin clamped)");
    }

    // ────────────────────────────────────────────────────────────────
    // Cross-axis margin subtraction in Center/End/Stretch (M5 bug fix)
    // ────────────────────────────────────────────────────────────────

    [Fact]
    public void ComputeLayout_ColumnAlignCenter_MarginSubtractedFromCrossAxis()
    {
        // Arrange: Column 300x200, align:center, child 100px wide with margin "0 20" (left=20, right=20).
        // crossAxisSize = 300. Available for centering = 300 - 100 - 20 - 20 = 160.
        // Center offset = 160/2 = 80. child.X = 20 (mLeft) + 0 (padding) + 80 = 100.
        var template = new Template
        {
            Canvas = new CanvasSettings { Width = 300 },
            Elements = new List<TemplateElement>
            {
                new FlexElement
                {
                    Direction = FlexDirection.Column,
                    Width = "300",
                    Height = "200",
                    Align = AlignItems.Center,
                    Children = new List<TemplateElement>
                    {
                        new TextElement { Content = "Test", Width = "100", Height = "50", Margin = "0 20" }
                    }
                }
            }
        };

        var root = _engine.ComputeLayout(template);
        var child = root.Children[0].Children[0];

        // With margin subtraction: X = mLeft(20) + (300 - 100 - 20 - 20) / 2 = 20 + 80 = 100
        Assert.Equal(100f, child.X, 1);
    }

    [Fact]
    public void ComputeLayout_ColumnAlignEnd_MarginSubtractedFromCrossAxis()
    {
        // Arrange: Column 300x200, align:end, child 100px wide with margin "0 20" (left=20, right=20).
        // crossAxisSize = 300. child.X = 20 (mLeft) + (300 - 100 - 20 - 20) = 20 + 160 = 180.
        var template = new Template
        {
            Canvas = new CanvasSettings { Width = 300 },
            Elements = new List<TemplateElement>
            {
                new FlexElement
                {
                    Direction = FlexDirection.Column,
                    Width = "300",
                    Height = "200",
                    Align = AlignItems.End,
                    Children = new List<TemplateElement>
                    {
                        new TextElement { Content = "Test", Width = "100", Height = "50", Margin = "0 20" }
                    }
                }
            }
        };

        var root = _engine.ComputeLayout(template);
        var child = root.Children[0].Children[0];

        // With margin subtraction: X = mLeft(20) + 300 - 100 - 20 - 20 = 180
        Assert.Equal(180f, child.X, 1);
    }

    [Fact]
    public void ComputeLayout_ColumnAlignStretch_MarginSubtractedFromWidth()
    {
        // Arrange: Column 300x200, align:stretch (default), child with no explicit width, margin "0 20".
        // Stretch should set width = crossAxisSize - mLeft - mRight = 300 - 20 - 20 = 260.
        var template = new Template
        {
            Canvas = new CanvasSettings { Width = 300 },
            Elements = new List<TemplateElement>
            {
                new FlexElement
                {
                    Direction = FlexDirection.Column,
                    Width = "300",
                    Height = "200",
                    Children = new List<TemplateElement>
                    {
                        new TextElement { Content = "Test", Height = "50", Margin = "0 20" }
                    }
                }
            }
        };

        var root = _engine.ComputeLayout(template);
        var child = root.Children[0].Children[0];

        // Stretch width = 300 - 20 - 20 = 260
        Assert.Equal(260f, child.Width, 1);
    }

    [Fact]
    public void ComputeLayout_RowAlignCenter_MarginSubtractedFromCrossAxis()
    {
        // Arrange: Row 300x200, align:center, child 50px tall with margin "10 0" (top=10, bottom=10).
        // crossAxisSize = 200. Available for centering = 200 - 50 - 10 - 10 = 130.
        // Center offset = 130/2 = 65. child.Y = 10 (mTop) + 0 (padding) + 65 = 75.
        var template = new Template
        {
            Canvas = new CanvasSettings { Width = 300 },
            Elements = new List<TemplateElement>
            {
                new FlexElement
                {
                    Direction = FlexDirection.Row,
                    Width = "300",
                    Height = "200",
                    Align = AlignItems.Center,
                    Children = new List<TemplateElement>
                    {
                        new TextElement { Content = "Test", Width = "100", Height = "50", Margin = "10 0" }
                    }
                }
            }
        };

        var root = _engine.ComputeLayout(template);
        var child = root.Children[0].Children[0];

        // With margin subtraction: Y = mTop(10) + (200 - 50 - 10 - 10) / 2 = 10 + 65 = 75
        Assert.Equal(75f, child.Y, 1);
    }

    [Fact]
    public void ComputeLayout_RowAlignEnd_MarginSubtractedFromCrossAxis()
    {
        // Arrange: Row 300x200, align:end, child 50px tall with margin "10 0" (top=10, bottom=10).
        // crossAxisSize = 200. child.Y = 10 (mTop) + (200 - 50 - 10 - 10) = 10 + 130 = 140.
        var template = new Template
        {
            Canvas = new CanvasSettings { Width = 300 },
            Elements = new List<TemplateElement>
            {
                new FlexElement
                {
                    Direction = FlexDirection.Row,
                    Width = "300",
                    Height = "200",
                    Align = AlignItems.End,
                    Children = new List<TemplateElement>
                    {
                        new TextElement { Content = "Test", Width = "100", Height = "50", Margin = "10 0" }
                    }
                }
            }
        };

        var root = _engine.ComputeLayout(template);
        var child = root.Children[0].Children[0];

        // With margin subtraction: Y = mTop(10) + 200 - 50 - 10 - 10 = 140
        Assert.Equal(140f, child.Y, 1);
    }

    [Fact]
    public void ComputeLayout_RowAlignStretch_MarginSubtractedFromHeight()
    {
        // Arrange: Row 300x200, align:stretch (default), child with no explicit height, margin "10 0".
        // Stretch should set height = crossAxisSize - mTop - mBottom = 200 - 10 - 10 = 180.
        var template = new Template
        {
            Canvas = new CanvasSettings { Width = 300 },
            Elements = new List<TemplateElement>
            {
                new FlexElement
                {
                    Direction = FlexDirection.Row,
                    Width = "300",
                    Height = "200",
                    Children = new List<TemplateElement>
                    {
                        new TextElement { Content = "Test", Width = "100", Margin = "10 0" }
                    }
                }
            }
        };

        var root = _engine.ComputeLayout(template);
        var child = root.Children[0].Children[0];

        // Stretch height = 200 - 10 - 10 = 180
        Assert.Equal(180f, child.Height, 1);
    }
}
