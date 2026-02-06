using FlexRender.Layout;
using FlexRender.Parsing.Ast;
using Xunit;

namespace FlexRender.Tests.Layout;

/// <summary>
/// Tests for RowReverse and ColumnReverse flex directions.
/// </summary>
public class LayoutEngineDirectionTests
{
    private readonly LayoutEngine _engine = new();

    [Fact]
    public void ComputeLayout_RowReverse_ChildrenOrderedRightToLeft()
    {
        // Arrange: row-reverse with 3 children of known widths
        var template = new Template
        {
            Canvas = new CanvasSettings { Width = 300 },
            Elements = new List<TemplateElement>
            {
                new FlexElement
                {
                    Direction = FlexDirection.RowReverse,
                    Children = new List<TemplateElement>
                    {
                        new TextElement { Content = "A", Width = "60", Height = "30" },
                        new TextElement { Content = "B", Width = "60", Height = "30" },
                        new TextElement { Content = "C", Width = "60", Height = "30" }
                    }
                }
            }
        };

        // Act
        var root = _engine.ComputeLayout(template);
        var flex = root.Children[0];

        // Assert: first child (A) should be at right edge, last child (C) at left
        // In RowReverse, first document-order child is placed rightmost
        Assert.True(flex.Children[0].X > flex.Children[1].X,
            $"First child X={flex.Children[0].X} should be > second child X={flex.Children[1].X} in row-reverse");
        Assert.True(flex.Children[1].X > flex.Children[2].X,
            $"Second child X={flex.Children[1].X} should be > third child X={flex.Children[2].X} in row-reverse");
    }

    [Fact]
    public void ComputeLayout_RowReverse_FirstChildAtRightEdge()
    {
        // Arrange: row-reverse with explicit container width
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
                        new TextElement { Content = "A", Width = "80", Height = "30" }
                    }
                }
            }
        };

        // Act
        var root = _engine.ComputeLayout(template);
        var flex = root.Children[0];

        // Assert: child's right edge should be at container width (300)
        var childRightEdge = flex.Children[0].X + flex.Children[0].Width;
        Assert.Equal(300f, childRightEdge, 0.1f);
    }

    [Fact]
    public void ComputeLayout_RowReverse_WithGap_GapBetweenReversedItems()
    {
        // Arrange: row-reverse with gap
        var template = new Template
        {
            Canvas = new CanvasSettings { Width = 300 },
            Elements = new List<TemplateElement>
            {
                new FlexElement
                {
                    Direction = FlexDirection.RowReverse,
                    Width = "300",
                    Gap = "10",
                    Children = new List<TemplateElement>
                    {
                        new TextElement { Content = "A", Width = "60", Height = "30" },
                        new TextElement { Content = "B", Width = "60", Height = "30" }
                    }
                }
            }
        };

        // Act
        var root = _engine.ComputeLayout(template);
        var flex = root.Children[0];

        // Assert: gap should exist between reversed items
        // A is rightmost, B is to its left. Gap = space between them.
        var gapBetween = flex.Children[0].X - (flex.Children[1].X + flex.Children[1].Width);
        Assert.Equal(10f, gapBetween, 0.1f);
    }

    [Fact]
    public void ComputeLayout_ColumnReverse_ChildrenOrderedBottomToTop()
    {
        // Arrange: column-reverse with 3 children
        var template = new Template
        {
            Canvas = new CanvasSettings { Width = 300 },
            Elements = new List<TemplateElement>
            {
                new FlexElement
                {
                    Direction = FlexDirection.ColumnReverse,
                    Height = "200",
                    Children = new List<TemplateElement>
                    {
                        new TextElement { Content = "A", Height = "30" },
                        new TextElement { Content = "B", Height = "30" },
                        new TextElement { Content = "C", Height = "30" }
                    }
                }
            }
        };

        // Act
        var root = _engine.ComputeLayout(template);
        var flex = root.Children[0];

        // Assert: first child (A) should be at bottom, last child (C) at top
        Assert.True(flex.Children[0].Y > flex.Children[1].Y,
            $"First child Y={flex.Children[0].Y} should be > second child Y={flex.Children[1].Y} in column-reverse");
        Assert.True(flex.Children[1].Y > flex.Children[2].Y,
            $"Second child Y={flex.Children[1].Y} should be > third child Y={flex.Children[2].Y} in column-reverse");
    }

    [Fact]
    public void ComputeLayout_ColumnReverse_FirstChildAtBottom()
    {
        // Arrange: column-reverse with explicit container height
        var template = new Template
        {
            Canvas = new CanvasSettings { Width = 300 },
            Elements = new List<TemplateElement>
            {
                new FlexElement
                {
                    Direction = FlexDirection.ColumnReverse,
                    Height = "200",
                    Children = new List<TemplateElement>
                    {
                        new TextElement { Content = "A", Height = "40" }
                    }
                }
            }
        };

        // Act
        var root = _engine.ComputeLayout(template);
        var flex = root.Children[0];

        // Assert: child's bottom edge should be at container height (200)
        var childBottomEdge = flex.Children[0].Y + flex.Children[0].Height;
        Assert.Equal(200f, childBottomEdge, 0.1f);
    }

    [Fact]
    public void ComputeLayout_ColumnReverse_WithGap_GapBetweenReversedItems()
    {
        // Arrange: column-reverse with gap
        var template = new Template
        {
            Canvas = new CanvasSettings { Width = 300 },
            Elements = new List<TemplateElement>
            {
                new FlexElement
                {
                    Direction = FlexDirection.ColumnReverse,
                    Height = "200",
                    Gap = "10",
                    Children = new List<TemplateElement>
                    {
                        new TextElement { Content = "A", Height = "40" },
                        new TextElement { Content = "B", Height = "40" }
                    }
                }
            }
        };

        // Act
        var root = _engine.ComputeLayout(template);
        var flex = root.Children[0];

        // Assert: gap should exist between reversed items
        // A is bottommost, B is above. Gap = space between them.
        var gapBetween = flex.Children[0].Y - (flex.Children[1].Y + flex.Children[1].Height);
        Assert.Equal(10f, gapBetween, 0.1f);
    }
}
