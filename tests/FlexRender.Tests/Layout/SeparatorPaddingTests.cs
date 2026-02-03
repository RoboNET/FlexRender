using FlexRender.Layout;
using FlexRender.Parsing.Ast;
using Xunit;

namespace FlexRender.Tests.Layout;

/// <summary>
/// Tests for non-uniform padding support on separator elements.
/// </summary>
public sealed class SeparatorPaddingTests
{
    private readonly LayoutEngine _engine = new();

    /// <summary>
    /// Verifies that a horizontal separator with non-uniform padding applies
    /// vertical padding correctly to total height. Width is governed by flex
    /// stretch in column layout, so we verify height only at top level.
    /// </summary>
    [Fact]
    public void HorizontalSeparator_WithNonUniformPadding_AppliesVerticalPadding()
    {
        var template = new Template
        {
            Canvas = new CanvasSettings { Width = 300 },
            Elements = new List<TemplateElement>
            {
                new SeparatorElement
                {
                    Orientation = SeparatorOrientation.Horizontal,
                    Thickness = 2,
                    Padding = "10 20 30 40"
                }
            }
        };

        var root = _engine.ComputeLayout(template);
        var separatorNode = root.Children[0];

        // Content height = 2 (thickness)
        // Vertical padding = top(10) + bottom(30) = 40
        Assert.Equal(2f + 40f, separatorNode.Height, 1);
    }

    /// <summary>
    /// Verifies that a separator inside a row flex container correctly applies
    /// horizontal padding to its width.
    /// </summary>
    [Fact]
    public void HorizontalSeparator_InRowFlex_WithNonUniformPadding_AppliesHorizontalPadding()
    {
        var template = new Template
        {
            Canvas = new CanvasSettings { Width = 300 },
            Elements = new List<TemplateElement>
            {
                new FlexElement
                {
                    Direction = FlexDirection.Row,
                    Width = "300",
                    Children = new List<TemplateElement>
                    {
                        new SeparatorElement
                        {
                            Orientation = SeparatorOrientation.Horizontal,
                            Thickness = 2,
                            Width = "100",
                            Padding = "10 20 30 40"
                        }
                    }
                }
            }
        };

        var root = _engine.ComputeLayout(template);
        var flexNode = root.Children[0];
        var separatorNode = flexNode.Children[0];

        // Content width = 100 (explicit), content height = 2 (thickness)
        // Horizontal padding = left(40) + right(20) = 60
        // Vertical padding = top(10) + bottom(30) = 40
        Assert.Equal(100f + 60f, separatorNode.Width, 1);
        Assert.Equal(2f + 40f, separatorNode.Height, 1);
    }

    /// <summary>
    /// Verifies that a vertical separator with non-uniform padding applies
    /// horizontal padding correctly when given explicit width.
    /// </summary>
    [Fact]
    public void VerticalSeparator_WithNonUniformPadding_AppliesCorrectly()
    {
        var template = new Template
        {
            Canvas = new CanvasSettings { Width = 300 },
            Elements = new List<TemplateElement>
            {
                new FlexElement
                {
                    Direction = FlexDirection.Row,
                    Width = "300",
                    Children = new List<TemplateElement>
                    {
                        new SeparatorElement
                        {
                            Orientation = SeparatorOrientation.Vertical,
                            Thickness = 2,
                            Padding = "5 15 25 35"
                        }
                    }
                }
            }
        };

        var root = _engine.ComputeLayout(template);
        var flexNode = root.Children[0];
        var separatorNode = flexNode.Children[0];

        // Content width = 2 (thickness), content height = 2 (thickness for vertical)
        // Horizontal padding = left(35) + right(15) = 50
        // Vertical padding = top(5) + bottom(25) = 30
        Assert.Equal(2f + 50f, separatorNode.Width, 1);
        Assert.Equal(2f + 30f, separatorNode.Height, 1);
    }

    /// <summary>
    /// Verifies that a separator with two-value padding shorthand
    /// correctly applies vertical and horizontal.
    /// </summary>
    [Fact]
    public void Separator_WithTwoValuePadding_AppliesVerticalHorizontal()
    {
        var template = new Template
        {
            Canvas = new CanvasSettings { Width = 300 },
            Elements = new List<TemplateElement>
            {
                new SeparatorElement
                {
                    Orientation = SeparatorOrientation.Horizontal,
                    Thickness = 1,
                    Padding = "10 20"
                }
            }
        };

        var root = _engine.ComputeLayout(template);
        var separatorNode = root.Children[0];

        // Content height = 1
        // Vertical padding = top(10) + bottom(10) = 20
        Assert.Equal(1f + 20f, separatorNode.Height, 1);
    }

    /// <summary>
    /// Verifies that a separator with uniform padding still works correctly
    /// after the PaddingParser migration.
    /// </summary>
    [Fact]
    public void Separator_WithUniformPadding_BackwardCompatible()
    {
        var template = new Template
        {
            Canvas = new CanvasSettings { Width = 300 },
            Elements = new List<TemplateElement>
            {
                new SeparatorElement
                {
                    Orientation = SeparatorOrientation.Horizontal,
                    Thickness = 2,
                    Padding = "15"
                }
            }
        };

        var root = _engine.ComputeLayout(template);
        var separatorNode = root.Children[0];

        // Content height = 2
        // Uniform padding 15 on all sides: Vertical = 30
        Assert.Equal(2f + 30f, separatorNode.Height, 1);
    }
}
