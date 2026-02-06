using FlexRender.Layout;
using FlexRender.Parsing.Ast;
using Xunit;

namespace FlexRender.Tests.Layout;

/// <summary>
/// Tests for RTL direction propagation through layout engine.
/// </summary>
public class LayoutEngineRtlTests
{
    private readonly LayoutEngine _engine = new();

    [Fact]
    public void ComputeLayout_CanvasDirRtl_BuildsSuccessfully()
    {
        var template = new Template
        {
            Canvas = new CanvasSettings { Width = 400, TextDirection = TextDirection.Rtl },
            Elements = new List<TemplateElement>
            {
                new TextElement { Content = "Hello", Width = "100", Height = "30" }
            }
        };

        var root = _engine.ComputeLayout(template);

        Assert.NotNull(root);
        Assert.Single(root.Children);
    }

    [Fact]
    public void ComputeLayout_CanvasDirLtr_BuildsSuccessfully()
    {
        var template = new Template
        {
            Canvas = new CanvasSettings { Width = 400, TextDirection = TextDirection.Ltr },
            Elements = new List<TemplateElement>
            {
                new TextElement { Content = "Hello", Width = "100", Height = "30" }
            }
        };

        var root = _engine.ComputeLayout(template);

        Assert.NotNull(root);
    }

    [Fact]
    public void ComputeLayout_ColumnRtl_NoLayoutChange()
    {
        // Column direction is unaffected by RTL (per CSS spec)
        var template = new Template
        {
            Canvas = new CanvasSettings { Width = 300, TextDirection = TextDirection.Rtl },
            Elements = new List<TemplateElement>
            {
                new FlexElement
                {
                    Direction = FlexDirection.Column,
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

        // Column: A should be above B regardless of RTL
        Assert.True(flex.Children[0].Y < flex.Children[1].Y,
            "Column layout should be top-to-bottom regardless of RTL");
    }

    [Fact]
    public void ComputeLayout_CanvasRtl_ChildNodeInheritsDirection()
    {
        var template = new Template
        {
            Canvas = new CanvasSettings { Width = 400, TextDirection = TextDirection.Rtl },
            Elements = new List<TemplateElement>
            {
                new TextElement { Content = "Hello", Width = "100", Height = "30" }
            }
        };

        var root = _engine.ComputeLayout(template);
        var child = root.Children[0];

        Assert.Equal(TextDirection.Rtl, child.Direction);
    }

    [Fact]
    public void ComputeLayout_ElementOverridesCanvasDir_NodeHasOverride()
    {
        var template = new Template
        {
            Canvas = new CanvasSettings { Width = 400, TextDirection = TextDirection.Rtl },
            Elements = new List<TemplateElement>
            {
                new FlexElement
                {
                    TextDirection = TextDirection.Ltr,
                    Children = new List<TemplateElement>
                    {
                        new TextElement { Content = "Hello", Width = "100", Height = "30" }
                    }
                }
            }
        };

        var root = _engine.ComputeLayout(template);
        var flex = root.Children[0];
        var child = flex.Children[0];

        Assert.Equal(TextDirection.Ltr, flex.Direction);
        Assert.Equal(TextDirection.Ltr, child.Direction);
    }

    [Fact]
    public void ComputeLayout_NestedDirOverride_PropagatesCorrectly()
    {
        var template = new Template
        {
            Canvas = new CanvasSettings { Width = 400, TextDirection = TextDirection.Rtl },
            Elements = new List<TemplateElement>
            {
                new FlexElement
                {
                    Children = new List<TemplateElement>
                    {
                        new FlexElement
                        {
                            TextDirection = TextDirection.Ltr,
                            Children = new List<TemplateElement>
                            {
                                new TextElement { Content = "LTR text", Width = "100", Height = "30" }
                            }
                        },
                        new TextElement { Content = "RTL text", Width = "100", Height = "30" }
                    }
                }
            }
        };

        var root = _engine.ComputeLayout(template);
        var outerFlex = root.Children[0];
        var innerFlex = outerFlex.Children[0];
        var ltrText = innerFlex.Children[0];
        var rtlText = outerFlex.Children[1];

        // Inner flex has explicit LTR override
        Assert.Equal(TextDirection.Ltr, innerFlex.Direction);
        // Text inside LTR flex inherits LTR
        Assert.Equal(TextDirection.Ltr, ltrText.Direction);
        // Text without override inherits RTL from canvas
        Assert.Equal(TextDirection.Rtl, rtlText.Direction);
    }

    [Fact]
    public void ComputeLayout_RowRtl_PositionsRightToLeft()
    {
        var template = new Template
        {
            Canvas = new CanvasSettings { Width = 300, TextDirection = TextDirection.Rtl },
            Elements = new List<TemplateElement>
            {
                new FlexElement
                {
                    Direction = FlexDirection.Row,
                    Width = "300",
                    Children = new List<TemplateElement>
                    {
                        new TextElement { Content = "A", Width = "60", Height = "30" },
                        new TextElement { Content = "B", Width = "60", Height = "30" },
                        new TextElement { Content = "C", Width = "60", Height = "30" }
                    }
                }
            }
        };

        var root = _engine.ComputeLayout(template);
        var flex = root.Children[0];

        // In RTL, first child (A) should be rightmost, C should be leftmost
        Assert.True(flex.Children[0].X > flex.Children[1].X,
            $"A (X={flex.Children[0].X}) should be right of B (X={flex.Children[1].X}) in RTL");
        Assert.True(flex.Children[1].X > flex.Children[2].X,
            $"B (X={flex.Children[1].X}) should be right of C (X={flex.Children[2].X}) in RTL");

        // First child should be at right edge
        Assert.Equal(240f, flex.Children[0].X, 1f); // 300 - 0 - 60 = 240
        Assert.Equal(180f, flex.Children[1].X, 1f); // 300 - 60 - 60 = 180
        Assert.Equal(120f, flex.Children[2].X, 1f); // 300 - 120 - 60 = 120
    }

    [Fact]
    public void ComputeLayout_RowReverseRtl_PositionsLeftToRight()
    {
        // RowReverse + RTL cancel each other out (XOR)
        var template = new Template
        {
            Canvas = new CanvasSettings { Width = 300, TextDirection = TextDirection.Rtl },
            Elements = new List<TemplateElement>
            {
                new FlexElement
                {
                    Direction = FlexDirection.RowReverse,
                    Width = "300",
                    Children = new List<TemplateElement>
                    {
                        new TextElement { Content = "A", Width = "60", Height = "30" },
                        new TextElement { Content = "B", Width = "60", Height = "30" },
                        new TextElement { Content = "C", Width = "60", Height = "30" }
                    }
                }
            }
        };

        var root = _engine.ComputeLayout(template);
        var flex = root.Children[0];

        // RowReverse + RTL = LTR (they cancel out)
        Assert.True(flex.Children[0].X < flex.Children[1].X,
            $"A (X={flex.Children[0].X}) should be left of B (X={flex.Children[1].X}) in RowReverse+RTL");
        Assert.True(flex.Children[1].X < flex.Children[2].X,
            $"B (X={flex.Children[1].X}) should be left of C (X={flex.Children[2].X}) in RowReverse+RTL");
    }

    [Fact]
    public void ComputeLayout_RowRtl_JustifyCenter_CentersCorrectly()
    {
        var template = new Template
        {
            Canvas = new CanvasSettings { Width = 300, TextDirection = TextDirection.Rtl },
            Elements = new List<TemplateElement>
            {
                new FlexElement
                {
                    Direction = FlexDirection.Row,
                    Width = "300",
                    Justify = JustifyContent.Center,
                    Children = new List<TemplateElement>
                    {
                        new TextElement { Content = "A", Width = "60", Height = "30" },
                        new TextElement { Content = "B", Width = "60", Height = "30" }
                    }
                }
            }
        };

        var root = _engine.ComputeLayout(template);
        var flex = root.Children[0];

        // 300 - 120 = 180 free space, center offset = 90
        // In RTL with center: positions are mirrored
        // A should still be rightmost in RTL
        Assert.True(flex.Children[0].X > flex.Children[1].X,
            "A should be right of B in RTL center");
    }

    [Fact]
    public void ComputeLayout_RowRtl_FlexGrow_DistributesCorrectly()
    {
        var template = new Template
        {
            Canvas = new CanvasSettings { Width = 300, TextDirection = TextDirection.Rtl },
            Elements = new List<TemplateElement>
            {
                new FlexElement
                {
                    Direction = FlexDirection.Row,
                    Width = "300",
                    Children = new List<TemplateElement>
                    {
                        new TextElement { Content = "A", Width = "60", Height = "30", Grow = 1 },
                        new TextElement { Content = "B", Width = "60", Height = "30" }
                    }
                }
            }
        };

        var root = _engine.ComputeLayout(template);
        var flex = root.Children[0];

        // A grows to 240 (300-60), B stays at 60
        Assert.Equal(240f, flex.Children[0].Width, 1f); // A grew
        Assert.Equal(60f, flex.Children[1].Width, 1f);  // B unchanged

        // RTL mirroring: A starts at right, B at left
        Assert.True(flex.Children[0].X > flex.Children[1].X,
            "Grown child A should still be right of B in RTL");
    }

    [Fact]
    public void ComputeLayout_RowRtl_SpaceBetween_DistributesCorrectly()
    {
        var template = new Template
        {
            Canvas = new CanvasSettings { Width = 300, TextDirection = TextDirection.Rtl },
            Elements = new List<TemplateElement>
            {
                new FlexElement
                {
                    Direction = FlexDirection.Row,
                    Width = "300",
                    Justify = JustifyContent.SpaceBetween,
                    Children = new List<TemplateElement>
                    {
                        new TextElement { Content = "A", Width = "60", Height = "30" },
                        new TextElement { Content = "B", Width = "60", Height = "30" }
                    }
                }
            }
        };

        var root = _engine.ComputeLayout(template);
        var flex = root.Children[0];

        // SpaceBetween: A at 0, B at 240 in LTR
        // RTL mirror: A at 300-0-60=240, B at 300-240-60=0
        Assert.Equal(240f, flex.Children[0].X, 1f); // A at right
        Assert.Equal(0f, flex.Children[1].X, 1f);   // B at left
    }

    [Fact]
    public void ComputeLayout_RowLtr_NoMirroring()
    {
        // Ensure LTR row still works correctly
        var template = new Template
        {
            Canvas = new CanvasSettings { Width = 300, TextDirection = TextDirection.Ltr },
            Elements = new List<TemplateElement>
            {
                new FlexElement
                {
                    Direction = FlexDirection.Row,
                    Width = "300",
                    Children = new List<TemplateElement>
                    {
                        new TextElement { Content = "A", Width = "60", Height = "30" },
                        new TextElement { Content = "B", Width = "60", Height = "30" }
                    }
                }
            }
        };

        var root = _engine.ComputeLayout(template);
        var flex = root.Children[0];

        Assert.Equal(0f, flex.Children[0].X, 1f);  // A at left
        Assert.Equal(60f, flex.Children[1].X, 1f); // B after A
    }

    [Fact]
    public void ComputeLayout_NestedRowInColumnWithCanvasRtl_RowChildrenMirrored()
    {
        // Bug repro: when canvas has dir: rtl, a row flex nested inside a column flex
        // should position its children right-to-left (first child rightmost).
        var template = new Template
        {
            Canvas = new CanvasSettings { Width = 400, TextDirection = TextDirection.Rtl },
            Elements = new List<TemplateElement>
            {
                new FlexElement
                {
                    Direction = FlexDirection.Column,
                    Padding = "20",
                    Gap = "12",
                    Children = new List<TemplateElement>
                    {
                        new TextElement { Content = "header", Width = "100", Height = "20" },
                        new FlexElement
                        {
                            Direction = FlexDirection.Row,
                            Padding = "16",
                            Gap = "10",
                            Children = new List<TemplateElement>
                            {
                                new FlexElement { Width = "90", Height = "60" }, // First  -> rightmost in RTL
                                new FlexElement { Width = "90", Height = "60" }, // Second -> middle
                                new FlexElement { Width = "90", Height = "60" }  // Third  -> leftmost in RTL
                            }
                        }
                    }
                }
            }
        };

        var root = _engine.ComputeLayout(template);

        // root -> outer column flex -> [0]=header text, [1]=inner row flex
        var outerColumn = root.Children[0];
        Assert.Equal(2, outerColumn.Children.Count);

        var innerRow = outerColumn.Children[1];
        Assert.Equal(3, innerRow.Children.Count);

        var first = innerRow.Children[0];
        var second = innerRow.Children[1];
        var third = innerRow.Children[2];

        // In RTL, the first child should be rightmost (largest X),
        // and the third child should be leftmost (smallest X).
        Assert.True(first.X > second.X,
            $"First child (X={first.X}) should be right of Second (X={second.X}) in nested RTL row");
        Assert.True(second.X > third.X,
            $"Second child (X={second.X}) should be right of Third (X={third.X}) in nested RTL row");
    }

    [Fact]
    public void ComputeLayout_RowRtl_Wrap_LinesPositionedRtl()
    {
        var template = new Template
        {
            Canvas = new CanvasSettings { Width = 200, TextDirection = TextDirection.Rtl },
            Elements = new List<TemplateElement>
            {
                new FlexElement
                {
                    Direction = FlexDirection.Row,
                    Wrap = FlexWrap.Wrap,
                    Width = "200",
                    Children = new List<TemplateElement>
                    {
                        new TextElement { Content = "A", Width = "80", Height = "30" },
                        new TextElement { Content = "B", Width = "80", Height = "30" },
                        new TextElement { Content = "C", Width = "80", Height = "30" }
                    }
                }
            }
        };

        var root = _engine.ComputeLayout(template);
        var flex = root.Children[0];

        // In a 200px container with wrap: A(80) + B(80) = 160 fits, C wraps
        // In RTL: A should be rightmost on line 1, B to its left
        // C should be rightmost on line 2
        Assert.True(flex.Children[0].X > flex.Children[1].X,
            $"A (X={flex.Children[0].X}) should be right of B (X={flex.Children[1].X}) in RTL wrap");

        // C should be on a different line (different Y)
        Assert.True(flex.Children[2].Y > flex.Children[0].Y,
            "C should be on a line below A");
    }
}
