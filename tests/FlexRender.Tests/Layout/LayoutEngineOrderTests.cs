using FlexRender.Layout;
using FlexRender.Parsing.Ast;
using Xunit;

namespace FlexRender.Tests.Layout;

/// <summary>
/// Tests for the CSS <c>order</c> property in flex layout.
/// The <c>order</c> property controls the visual ordering of flex items
/// without changing the source (DOM) order. Items are sorted by ascending
/// <c>order</c> value; equal values preserve source order (stable sort).
/// </summary>
public sealed class LayoutEngineOrderTests
{
    private readonly LayoutEngine _engine = new();

    // ============================================
    // Row Direction Tests
    // ============================================

    [Fact]
    public void ComputeLayout_Row_OrderProperty_SortsItemsByOrder()
    {
        // Arrange: three items in document order A(order:3), B(order:1), C(order:2)
        // Expected visual order: B(order:1), C(order:2), A(order:3)
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
                        new TextElement { Content = "A", Width = "80", Height = "30", Order = 3 },
                        new TextElement { Content = "B", Width = "80", Height = "30", Order = 1 },
                        new TextElement { Content = "C", Width = "80", Height = "30", Order = 2 }
                    }
                }
            }
        };

        // Act
        var root = _engine.ComputeLayout(template);
        var flex = root.Children[0];

        // Assert: B should be leftmost (X=0), then C, then A
        // After sorting by order: B(1), C(2), A(3)
        // B is at index 1 in source, C at index 2, A at index 0
        // We check positions: the item with order:1 should be at X=0
        var itemA = FindChildByContent(flex, "A");
        var itemB = FindChildByContent(flex, "B");
        var itemC = FindChildByContent(flex, "C");

        Assert.True(itemB.X < itemC.X,
            $"B (order:1) X={itemB.X} should be before C (order:2) X={itemC.X}");
        Assert.True(itemC.X < itemA.X,
            $"C (order:2) X={itemC.X} should be before A (order:3) X={itemA.X}");

        // Verify exact positions: each item is 80px wide, no gap
        Assert.Equal(0f, itemB.X, 0.1f);
        Assert.Equal(80f, itemC.X, 0.1f);
        Assert.Equal(160f, itemA.X, 0.1f);
    }

    [Fact]
    public void ComputeLayout_Row_EqualOrder_PreservesSourceOrder()
    {
        // Arrange: three items all with order:1 -- should preserve source order A, B, C
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
                        new TextElement { Content = "A", Width = "80", Height = "30", Order = 1 },
                        new TextElement { Content = "B", Width = "80", Height = "30", Order = 1 },
                        new TextElement { Content = "C", Width = "80", Height = "30", Order = 1 }
                    }
                }
            }
        };

        // Act
        var root = _engine.ComputeLayout(template);
        var flex = root.Children[0];

        // Assert: stable sort preserves source order A, B, C
        var itemA = FindChildByContent(flex, "A");
        var itemB = FindChildByContent(flex, "B");
        var itemC = FindChildByContent(flex, "C");

        Assert.Equal(0f, itemA.X, 0.1f);
        Assert.Equal(80f, itemB.X, 0.1f);
        Assert.Equal(160f, itemC.X, 0.1f);
    }

    [Fact]
    public void ComputeLayout_Row_DefaultOrder_AllZero_PreservesSourceOrder()
    {
        // Arrange: items without explicit order all have order=0, preserving source order
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
                        new TextElement { Content = "A", Width = "80", Height = "30" },
                        new TextElement { Content = "B", Width = "80", Height = "30" },
                        new TextElement { Content = "C", Width = "80", Height = "30" }
                    }
                }
            }
        };

        // Act
        var root = _engine.ComputeLayout(template);
        var flex = root.Children[0];

        // Assert: A, B, C positioned in source order
        var itemA = FindChildByContent(flex, "A");
        var itemB = FindChildByContent(flex, "B");
        var itemC = FindChildByContent(flex, "C");

        Assert.Equal(0f, itemA.X, 0.1f);
        Assert.Equal(80f, itemB.X, 0.1f);
        Assert.Equal(160f, itemC.X, 0.1f);
    }

    [Fact]
    public void ComputeLayout_Row_NegativeOrder_PlacedFirst()
    {
        // Arrange: B has order:-1, should appear before A(order:0) and C(order:0)
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
                        new TextElement { Content = "A", Width = "80", Height = "30", Order = 0 },
                        new TextElement { Content = "B", Width = "80", Height = "30", Order = -1 },
                        new TextElement { Content = "C", Width = "80", Height = "30", Order = 0 }
                    }
                }
            }
        };

        // Act
        var root = _engine.ComputeLayout(template);
        var flex = root.Children[0];

        // Assert: visual order should be B(-1), A(0), C(0)
        var itemA = FindChildByContent(flex, "A");
        var itemB = FindChildByContent(flex, "B");
        var itemC = FindChildByContent(flex, "C");

        Assert.Equal(0f, itemB.X, 0.1f);
        Assert.Equal(80f, itemA.X, 0.1f);
        Assert.Equal(160f, itemC.X, 0.1f);
    }

    [Fact]
    public void ComputeLayout_Row_MixedOrder_SortsCorrectly()
    {
        // Arrange: A(order:2), B(order:-1), C(order:0), D(order:1)
        // Expected visual order: B(-1), C(0), D(1), A(2)
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
                        new TextElement { Content = "A", Width = "80", Height = "30", Order = 2 },
                        new TextElement { Content = "B", Width = "80", Height = "30", Order = -1 },
                        new TextElement { Content = "C", Width = "80", Height = "30", Order = 0 },
                        new TextElement { Content = "D", Width = "80", Height = "30", Order = 1 }
                    }
                }
            }
        };

        // Act
        var root = _engine.ComputeLayout(template);
        var flex = root.Children[0];

        // Assert: B(-1), C(0), D(1), A(2)
        var itemA = FindChildByContent(flex, "A");
        var itemB = FindChildByContent(flex, "B");
        var itemC = FindChildByContent(flex, "C");
        var itemD = FindChildByContent(flex, "D");

        Assert.Equal(0f, itemB.X, 0.1f);
        Assert.Equal(80f, itemC.X, 0.1f);
        Assert.Equal(160f, itemD.X, 0.1f);
        Assert.Equal(240f, itemA.X, 0.1f);
    }

    // ============================================
    // Column Direction Tests
    // ============================================

    [Fact]
    public void ComputeLayout_Column_OrderProperty_SortsItemsByOrder()
    {
        // Arrange: column direction with items A(order:3), B(order:1), C(order:2)
        // Expected visual order: B(1), C(2), A(3)
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
                        new TextElement { Content = "A", Height = "40", Order = 3 },
                        new TextElement { Content = "B", Height = "40", Order = 1 },
                        new TextElement { Content = "C", Height = "40", Order = 2 }
                    }
                }
            }
        };

        // Act
        var root = _engine.ComputeLayout(template);
        var flex = root.Children[0];

        // Assert: B should be topmost (Y=0), then C, then A
        var itemA = FindChildByContent(flex, "A");
        var itemB = FindChildByContent(flex, "B");
        var itemC = FindChildByContent(flex, "C");

        Assert.True(itemB.Y < itemC.Y,
            $"B (order:1) Y={itemB.Y} should be before C (order:2) Y={itemC.Y}");
        Assert.True(itemC.Y < itemA.Y,
            $"C (order:2) Y={itemC.Y} should be before A (order:3) Y={itemA.Y}");

        Assert.Equal(0f, itemB.Y, 0.1f);
        Assert.Equal(40f, itemC.Y, 0.1f);
        Assert.Equal(80f, itemA.Y, 0.1f);
    }

    // ============================================
    // Interaction with Reverse Directions
    // ============================================

    [Fact]
    public void ComputeLayout_RowReverse_OrderAppliedThenReversed()
    {
        // Arrange: row-reverse with A(order:2), B(order:1)
        // Sorted by order: B(1), A(2)
        // Then RowReverse flips: A appears at right, B at left? No --
        // RowReverse reverses the start position. Sorted order is B,A.
        // With RowReverse, B goes rightmost, A goes left of B.
        var template = new Template
        {
            Canvas = new CanvasSettings { Width = 300 },
            Elements = new List<TemplateElement>
            {
                new FlexElement
                {
                    Direction = FlexDirection.RowReverse,
                    Width = "300",
                    Children = new List<TemplateElement>
                    {
                        new TextElement { Content = "A", Width = "80", Height = "30", Order = 2 },
                        new TextElement { Content = "B", Width = "80", Height = "30", Order = 1 }
                    }
                }
            }
        };

        // Act
        var root = _engine.ComputeLayout(template);
        var flex = root.Children[0];

        var itemA = FindChildByContent(flex, "A");
        var itemB = FindChildByContent(flex, "B");

        // After order sort: B(1), A(2)
        // After RowReverse: first sorted item (B) goes to rightmost position
        Assert.True(itemB.X > itemA.X,
            $"In row-reverse, B (order:1, first in sorted order) X={itemB.X} should be > A X={itemA.X}");
    }

    // ============================================
    // Interaction with Flex Properties
    // ============================================

    [Fact]
    public void ComputeLayout_Row_OrderWithFlexGrow_DistributesSpaceByVisualOrder()
    {
        // Arrange: A(order:2,grow:1), B(order:1,grow:2) in row container
        // Sorted: B(1,grow:2), A(2,grow:1)
        // B should get 2/3 of free space, A gets 1/3
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
                        new TextElement { Content = "A", Height = "30", Order = 2, Grow = 1, Basis = "0" },
                        new TextElement { Content = "B", Height = "30", Order = 1, Grow = 2, Basis = "0" }
                    }
                }
            }
        };

        // Act
        var root = _engine.ComputeLayout(template);
        var flex = root.Children[0];

        var itemA = FindChildByContent(flex, "A");
        var itemB = FindChildByContent(flex, "B");

        // B should be first (leftmost) due to order:1 < order:2
        Assert.True(itemB.X < itemA.X,
            $"B (order:1) X={itemB.X} should be before A (order:2) X={itemA.X}");

        // B with grow:2 should be wider than A with grow:1
        Assert.True(itemB.Width > itemA.Width,
            $"B (grow:2) width={itemB.Width} should be > A (grow:1) width={itemA.Width}");

        // Total should fill container
        var totalWidth = itemA.Width + itemB.Width;
        Assert.Equal(300f, totalWidth, 1f);
    }

    [Fact]
    public void ComputeLayout_Row_OrderWithGap_GapBetweenSortedItems()
    {
        // Arrange: row with gap=10, A(order:2), B(order:1)
        // Sorted: B, A. Gap should be between B and A.
        var template = new Template
        {
            Canvas = new CanvasSettings { Width = 300 },
            Elements = new List<TemplateElement>
            {
                new FlexElement
                {
                    Direction = FlexDirection.Row,
                    Gap = "10",
                    Children = new List<TemplateElement>
                    {
                        new TextElement { Content = "A", Width = "80", Height = "30", Order = 2 },
                        new TextElement { Content = "B", Width = "80", Height = "30", Order = 1 }
                    }
                }
            }
        };

        // Act
        var root = _engine.ComputeLayout(template);
        var flex = root.Children[0];

        var itemA = FindChildByContent(flex, "A");
        var itemB = FindChildByContent(flex, "B");

        // B first, then gap, then A
        Assert.Equal(0f, itemB.X, 0.1f);
        Assert.Equal(90f, itemA.X, 0.1f); // 80 + 10 gap
    }

    // ============================================
    // Edge Cases
    // ============================================

    [Fact]
    public void ComputeLayout_Order_DisplayNoneItemsPreserved()
    {
        // Arrange: B is display:none, A(order:2), B(order:1,display:none), C(order:0)
        // Only A and C are visible. Sorted visible: C(0), A(2)
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
                        new TextElement { Content = "A", Width = "80", Height = "30", Order = 2 },
                        new TextElement { Content = "B", Width = "80", Height = "30", Order = 1, Display = Display.None },
                        new TextElement { Content = "C", Width = "80", Height = "30", Order = 0 }
                    }
                }
            }
        };

        // Act
        var root = _engine.ComputeLayout(template);
        var flex = root.Children[0];

        var itemA = FindChildByContent(flex, "A");
        var itemC = FindChildByContent(flex, "C");

        // C(order:0) should be before A(order:2)
        Assert.True(itemC.X < itemA.X,
            $"C (order:0) X={itemC.X} should be before A (order:2) X={itemA.X}");
        Assert.Equal(0f, itemC.X, 0.1f);
        Assert.Equal(80f, itemA.X, 0.1f);
    }

    [Fact]
    public void ComputeLayout_Order_PositiveOrder_MovesToEnd()
    {
        // Arrange: A(order:1), B(order:0), C(order:0)
        // Sorted: B(0), C(0), A(1) -- B and C preserve source order among themselves
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
                        new TextElement { Content = "A", Width = "80", Height = "30", Order = 1 },
                        new TextElement { Content = "B", Width = "80", Height = "30", Order = 0 },
                        new TextElement { Content = "C", Width = "80", Height = "30", Order = 0 }
                    }
                }
            }
        };

        // Act
        var root = _engine.ComputeLayout(template);
        var flex = root.Children[0];

        var itemA = FindChildByContent(flex, "A");
        var itemB = FindChildByContent(flex, "B");
        var itemC = FindChildByContent(flex, "C");

        // Visual order: B, C, A
        Assert.Equal(0f, itemB.X, 0.1f);
        Assert.Equal(80f, itemC.X, 0.1f);
        Assert.Equal(160f, itemA.X, 0.1f);
    }

    // ============================================
    // Helper
    // ============================================

    /// <summary>
    /// Finds a child LayoutNode by the Content property of its TextElement.
    /// </summary>
    private static LayoutNode FindChildByContent(LayoutNode parent, string content)
    {
        foreach (var child in parent.Children)
        {
            if (child.Element is TextElement text && text.Content.Value == content)
                return child;
        }

        throw new InvalidOperationException($"No child found with content '{content}'");
    }
}
