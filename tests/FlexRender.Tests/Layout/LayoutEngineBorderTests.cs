using FlexRender.Layout;
using FlexRender.Parsing.Ast;
using Xunit;

namespace FlexRender.Tests.Layout;

/// <summary>
/// Tests that border width affects layout sizing following the CSS border-box model.
/// Border width reduces content area just like padding -- the element's total box size
/// includes border width inside its bounds.
/// </summary>
public sealed class LayoutEngineBorderTests
{
    private readonly LayoutEngine _engine = new();

    // ============================================
    // Element Sizing: Border adds to total size
    // ============================================

    [Fact]
    public void ComputeLayout_TextWithBorder_HeightIncludesBorderWidth()
    {
        // Arrange: text with height=30, border="2 solid #000"
        // Expected: total height = 30 (content) + 2 (top border) + 2 (bottom border) = 34
        var template = new Template
        {
            Canvas = new CanvasSettings { Width = 300 },
            Elements = new List<TemplateElement>
            {
                new TextElement
                {
                    Content = "Hello",
                    Height = "30",
                    Border = "2 solid #000"
                }
            }
        };

        var root = _engine.ComputeLayout(template);
        var textNode = root.Children[0];

        // Height should include border: 30 + 2 + 2 = 34
        Assert.Equal(34f, textNode.Height, 0.1f);
    }

    [Fact]
    public void ComputeLayout_TextWithBorder_WidthIncludesBorderWidth()
    {
        // Arrange: text with explicit width=100, border="3 solid #000"
        // Expected: total width = 100 + 3 + 3 = 106
        var template = new Template
        {
            Canvas = new CanvasSettings { Width = 300 },
            Elements = new List<TemplateElement>
            {
                new TextElement
                {
                    Content = "Hello",
                    Width = "100",
                    Height = "30",
                    Border = "3 solid #000"
                }
            }
        };

        var root = _engine.ComputeLayout(template);
        var textNode = root.Children[0];

        Assert.Equal(106f, textNode.Width, 0.1f);
        Assert.Equal(36f, textNode.Height, 0.1f);
    }

    [Fact]
    public void ComputeLayout_TextWithBorderAndPadding_BothAffectSize()
    {
        // Arrange: text with height=20, padding=5, border="2 solid #000"
        // Expected: total height = 20 + 5*2 (padding) + 2*2 (border) = 34
        var template = new Template
        {
            Canvas = new CanvasSettings { Width = 300 },
            Elements = new List<TemplateElement>
            {
                new TextElement
                {
                    Content = "Hello",
                    Height = "20",
                    Padding = "5",
                    Border = "2 solid #000"
                }
            }
        };

        var root = _engine.ComputeLayout(template);
        var textNode = root.Children[0];

        // Total height = content(20) + padding(5+5) + border(2+2) = 34
        Assert.Equal(34f, textNode.Height, 0.1f);
    }

    // ============================================
    // Flex Container: Border reduces content area
    // ============================================

    [Fact]
    public void ComputeLayout_FlexWithBorder_ChildPositionedInsideBorder()
    {
        // Arrange: flex container with border="4 solid #000" and a child
        // Children should be positioned inside the border (like padding)
        var template = new Template
        {
            Canvas = new CanvasSettings { Width = 300 },
            Elements = new List<TemplateElement>
            {
                new FlexElement
                {
                    Direction = FlexDirection.Column,
                    Border = "4 solid #000",
                    Children = new List<TemplateElement>
                    {
                        new TextElement { Content = "Inside border", Height = "30" }
                    }
                }
            }
        };

        var root = _engine.ComputeLayout(template);
        var flex = root.Children[0];
        var child = flex.Children[0];

        // Child should be offset by border width (same as padding behavior)
        Assert.Equal(4f, child.X, 0.1f);
        Assert.Equal(4f, child.Y, 0.1f);
    }

    [Fact]
    public void ComputeLayout_FlexWithBorderAndPadding_ChildPositionedInsideBoth()
    {
        // Arrange: flex with border=2 and padding=10
        // Child should be offset by border + padding = 12
        var template = new Template
        {
            Canvas = new CanvasSettings { Width = 300 },
            Elements = new List<TemplateElement>
            {
                new FlexElement
                {
                    Direction = FlexDirection.Column,
                    Border = "2 solid #000",
                    Padding = "10",
                    Children = new List<TemplateElement>
                    {
                        new TextElement { Content = "Inside", Height = "30" }
                    }
                }
            }
        };

        var root = _engine.ComputeLayout(template);
        var flex = root.Children[0];
        var child = flex.Children[0];

        // Child offset = border(2) + padding(10) = 12
        Assert.Equal(12f, child.X, 0.1f);
        Assert.Equal(12f, child.Y, 0.1f);
    }

    [Fact]
    public void ComputeLayout_FlexWithBorder_ChildWidthReducedByBorder()
    {
        // Arrange: flex container 300px wide, border=5, child with stretch (default)
        // Child width should be 300 - 5 - 5 = 290
        var template = new Template
        {
            Canvas = new CanvasSettings { Width = 300 },
            Elements = new List<TemplateElement>
            {
                new FlexElement
                {
                    Direction = FlexDirection.Column,
                    Border = "5 solid #000",
                    Children = new List<TemplateElement>
                    {
                        new TextElement { Content = "Stretched", Height = "30" }
                    }
                }
            }
        };

        var root = _engine.ComputeLayout(template);
        var flex = root.Children[0];
        var child = flex.Children[0];

        // Stretched child width = container(300) - border(5+5) = 290
        Assert.Equal(290f, child.Width, 0.1f);
    }

    [Fact]
    public void ComputeLayout_FlexWithBorder_HeightIncludesBorder()
    {
        // Arrange: flex with auto height, border=3, child height=40
        // Flex height = child(40) + border_top(3) + border_bottom(3) = 46
        var template = new Template
        {
            Canvas = new CanvasSettings { Width = 300 },
            Elements = new List<TemplateElement>
            {
                new FlexElement
                {
                    Direction = FlexDirection.Column,
                    Border = "3 solid #000",
                    Children = new List<TemplateElement>
                    {
                        new TextElement { Content = "Content", Height = "40" }
                    }
                }
            }
        };

        var root = _engine.ComputeLayout(template);
        var flex = root.Children[0];

        // Auto height = border_top(3) + child(40) + border_bottom(3) = 46
        Assert.Equal(46f, flex.Height, 0.1f);
    }

    // ============================================
    // Per-Side Borders
    // ============================================

    [Fact]
    public void ComputeLayout_FlexWithPerSideBorder_AsymmetricInsets()
    {
        // Arrange: flex with borderTop=5 and borderBottom=0
        // Child Y = 5 (top border only)
        var template = new Template
        {
            Canvas = new CanvasSettings { Width = 300 },
            Elements = new List<TemplateElement>
            {
                new FlexElement
                {
                    Direction = FlexDirection.Column,
                    BorderTop = "5 solid #000",
                    Children = new List<TemplateElement>
                    {
                        new TextElement { Content = "Content", Height = "30" }
                    }
                }
            }
        };

        var root = _engine.ComputeLayout(template);
        var flex = root.Children[0];
        var child = flex.Children[0];

        // Child Y offset by top border only
        Assert.Equal(5f, child.Y, 0.1f);
        // Child X = 0 (no left border)
        Assert.Equal(0f, child.X, 0.1f);
        // Flex height = top_border(5) + child(30) + bottom_border(0) = 35
        Assert.Equal(35f, flex.Height, 0.1f);
    }

    [Fact]
    public void ComputeLayout_FlexWithLeftAndRightBorder_ReducesContentWidth()
    {
        // Arrange: flex with borderLeft=10, borderRight=10
        // Content width = 300 - 10 - 10 = 280
        var template = new Template
        {
            Canvas = new CanvasSettings { Width = 300 },
            Elements = new List<TemplateElement>
            {
                new FlexElement
                {
                    Direction = FlexDirection.Column,
                    BorderLeft = "10 solid #000",
                    BorderRight = "10 solid #000",
                    Children = new List<TemplateElement>
                    {
                        new TextElement { Content = "Content", Height = "30" }
                    }
                }
            }
        };

        var root = _engine.ComputeLayout(template);
        var flex = root.Children[0];
        var child = flex.Children[0];

        Assert.Equal(10f, child.X, 0.1f);
        Assert.Equal(280f, child.Width, 0.1f);
    }

    // ============================================
    // No Border (Zero/None)
    // ============================================

    [Fact]
    public void ComputeLayout_NoBorder_NoLayoutImpact()
    {
        // Arrange: element without any border properties
        // Layout should be identical to current behavior (no border overhead)
        var template = new Template
        {
            Canvas = new CanvasSettings { Width = 300 },
            Elements = new List<TemplateElement>
            {
                new FlexElement
                {
                    Direction = FlexDirection.Column,
                    Padding = "10",
                    Children = new List<TemplateElement>
                    {
                        new TextElement { Content = "No border", Height = "30" }
                    }
                }
            }
        };

        var root = _engine.ComputeLayout(template);
        var flex = root.Children[0];
        var child = flex.Children[0];

        // Without border: child at padding offset
        Assert.Equal(10f, child.X, 0.1f);
        Assert.Equal(10f, child.Y, 0.1f);
        Assert.Equal(280f, child.Width, 0.1f);
    }

    [Fact]
    public void ComputeLayout_BorderStyleNone_NoLayoutImpact()
    {
        // Arrange: border with style=none should not affect layout
        var template = new Template
        {
            Canvas = new CanvasSettings { Width = 300 },
            Elements = new List<TemplateElement>
            {
                new FlexElement
                {
                    Direction = FlexDirection.Column,
                    Border = "5 none #000",
                    Children = new List<TemplateElement>
                    {
                        new TextElement { Content = "No visible border", Height = "30" }
                    }
                }
            }
        };

        var root = _engine.ComputeLayout(template);
        var flex = root.Children[0];
        var child = flex.Children[0];

        // border-style:none means no border drawn AND no space consumed
        Assert.Equal(0f, child.X, 0.1f);
        Assert.Equal(0f, child.Y, 0.1f);
        Assert.Equal(300f, child.Width, 0.1f);
    }

    // ============================================
    // Row Direction with Borders
    // ============================================

    [Fact]
    public void ComputeLayout_RowFlexWithBorder_ChildrenPositionedInsideBorder()
    {
        // Arrange: row flex with border=4, two children
        var template = new Template
        {
            Canvas = new CanvasSettings { Width = 300 },
            Elements = new List<TemplateElement>
            {
                new FlexElement
                {
                    Direction = FlexDirection.Row,
                    Height = "60",
                    Border = "4 solid #000",
                    Children = new List<TemplateElement>
                    {
                        new TextElement { Content = "A", Width = "100", Height = "30" },
                        new TextElement { Content = "B", Width = "100", Height = "30" }
                    }
                }
            }
        };

        var root = _engine.ComputeLayout(template);
        var flex = root.Children[0];

        // First child at X=4 (border), Y=4 (border)
        Assert.Equal(4f, flex.Children[0].X, 0.1f);
        // Second child at X=4+100=104
        Assert.Equal(104f, flex.Children[1].X, 0.1f);
    }

    // ============================================
    // Image/QR/Barcode Elements with Borders
    // ============================================

    [Fact]
    public void ComputeLayout_ImageWithBorder_SizeIncludesBorder()
    {
        // Arrange: image with explicit size and border
        var template = new Template
        {
            Canvas = new CanvasSettings { Width = 300 },
            Elements = new List<TemplateElement>
            {
                new ImageElement
                {
                    Width = "100",
                    Height = "100",
                    Border = "2 solid #000"
                }
            }
        };

        var root = _engine.ComputeLayout(template);
        var imageNode = root.Children[0];

        // Total = content + border
        Assert.Equal(104f, imageNode.Width, 0.1f);
        Assert.Equal(104f, imageNode.Height, 0.1f);
    }

    [Fact]
    public void ComputeLayout_QrWithBorder_SizeIncludesBorder()
    {
        var template = new Template
        {
            Canvas = new CanvasSettings { Width = 300 },
            Elements = new List<TemplateElement>
            {
                new QrElement
                {
                    Data = "test",
                    Size = 80,
                    Border = "3 solid #000"
                }
            }
        };

        var root = _engine.ComputeLayout(template);
        var qrNode = root.Children[0];

        // Total = 80 + 3 + 3 = 86
        Assert.Equal(86f, qrNode.Width, 0.1f);
        Assert.Equal(86f, qrNode.Height, 0.1f);
    }
}
