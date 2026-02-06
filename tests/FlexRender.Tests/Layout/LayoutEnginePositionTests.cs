using FlexRender.Layout;
using FlexRender.Parsing.Ast;
using Xunit;

namespace FlexRender.Tests.Layout;

/// <summary>
/// Tests for CSS positioning (absolute, relative) and aspect ratio.
/// Covers position: absolute (flow exclusion, inset placement, inset sizing, intrinsic measurement),
/// position: relative (directional offsets, priority rules, sibling independence),
/// and aspect ratio (width-defined, height-defined, neither-defined).
/// </summary>
public sealed class LayoutEnginePositionTests
{
    private readonly LayoutEngine _engine = new();

    // ────────────────────────────────────────────────────────────────
    // Position Absolute Tests
    // ────────────────────────────────────────────────────────────────

    [Fact]
    public void ComputeLayout_PositionAbsolute_ExcludedFromFlexFlow()
    {
        // Arrange: Row 300x200, 3 children 100px wide.
        // Child[1] is absolute — the two flow children should lay out as if it doesn't exist.
        // With no flex-grow the two flow children stay 100px each.
        var flex = new FlexElement
        {
            Direction = FlexDirection.Row,
            Width = "300",
            Height = "200"
        };
        flex.AddChild(new TextElement { Content = "A", Width = "100", Height = "50" });
        flex.AddChild(new TextElement { Content = "B", Width = "100", Height = "50", Position = Position.Absolute });
        flex.AddChild(new TextElement { Content = "C", Width = "100", Height = "50" });

        var template = new Template
        {
            Canvas = new CanvasSettings { Width = 300 },
            Elements = new List<TemplateElement> { flex }
        };

        // Act
        var root = _engine.ComputeLayout(template);

        // Assert
        var flexNode = root.Children[0];

        // Flow child[0] at X=0
        Assert.Equal(0f, flexNode.Children[0].X, 1);
        Assert.Equal(100f, flexNode.Children[0].Width, 1);

        // Flow child[2] should follow child[0] directly at X=100 (not X=200)
        Assert.Equal(100f, flexNode.Children[2].X, 1);
        Assert.Equal(100f, flexNode.Children[2].Width, 1);
    }

    [Fact]
    public void ComputeLayout_PositionAbsolute_PositionedByLeftTop()
    {
        // Arrange: Absolute child with Left=20, Top=30 in a 300x200 container with Padding=10.
        // Expected: child.X = 10 + 20 = 30, child.Y = 10 + 30 = 40.
        var flex = new FlexElement
        {
            Direction = FlexDirection.Row,
            Width = "300",
            Height = "200",
            Padding = "10"
        };
        flex.AddChild(new TextElement
        {
            Content = "Abs",
            Width = "50",
            Height = "50",
            Position = Position.Absolute,
            Left = "20",
            Top = "30"
        });

        var template = new Template
        {
            Canvas = new CanvasSettings { Width = 300 },
            Elements = new List<TemplateElement> { flex }
        };

        // Act
        var root = _engine.ComputeLayout(template);

        // Assert
        var absChild = root.Children[0].Children[0];
        Assert.Equal(30f, absChild.X, 1);
        Assert.Equal(40f, absChild.Y, 1);
    }

    [Fact]
    public void ComputeLayout_PositionAbsolute_PositionedByRightBottom()
    {
        // Arrange: Absolute child 50x50 with Right=20, Bottom=30 in a 300x200 container with Padding=10.
        // Expected: child.X = 300 - 10 - 50 - 20 = 220, child.Y = 200 - 10 - 50 - 30 = 110.
        var flex = new FlexElement
        {
            Direction = FlexDirection.Row,
            Width = "300",
            Height = "200",
            Padding = "10"
        };
        flex.AddChild(new TextElement
        {
            Content = "Abs",
            Width = "50",
            Height = "50",
            Position = Position.Absolute,
            Right = "20",
            Bottom = "30"
        });

        var template = new Template
        {
            Canvas = new CanvasSettings { Width = 300 },
            Elements = new List<TemplateElement> { flex }
        };

        // Act
        var root = _engine.ComputeLayout(template);

        // Assert
        var absChild = root.Children[0].Children[0];
        Assert.Equal(220f, absChild.X, 1);
        Assert.Equal(110f, absChild.Y, 1);
    }

    [Fact]
    public void ComputeLayout_PositionAbsolute_DefaultsToTopLeftPadding()
    {
        // Arrange: Absolute child with no insets in a 300x200 container with Padding=15.
        // Expected: positioned at (15, 15) — the padding start.
        var flex = new FlexElement
        {
            Direction = FlexDirection.Row,
            Width = "300",
            Height = "200",
            Padding = "15"
        };
        flex.AddChild(new TextElement
        {
            Content = "Abs",
            Width = "50",
            Height = "50",
            Position = Position.Absolute
        });

        var template = new Template
        {
            Canvas = new CanvasSettings { Width = 300 },
            Elements = new List<TemplateElement> { flex }
        };

        // Act
        var root = _engine.ComputeLayout(template);

        // Assert
        var absChild = root.Children[0].Children[0];
        Assert.Equal(15f, absChild.X, 1);
        Assert.Equal(15f, absChild.Y, 1);
    }

    [Fact]
    public void ComputeLayout_PositionAbsolute_InsetSizing_LeftRight()
    {
        // Arrange: Absolute child with Left=20, Right=20, no explicit Width,
        // in a 300x200 container with Padding=10.
        // Expected: child.Width = 300 - 10 - 10 - 20 - 20 = 240.
        var flex = new FlexElement
        {
            Direction = FlexDirection.Row,
            Width = "300",
            Height = "200",
            Padding = "10"
        };
        flex.AddChild(new TextElement
        {
            Content = "Abs",
            Height = "50",
            Position = Position.Absolute,
            Left = "20",
            Right = "20"
        });

        var template = new Template
        {
            Canvas = new CanvasSettings { Width = 300 },
            Elements = new List<TemplateElement> { flex }
        };

        // Act
        var root = _engine.ComputeLayout(template);

        // Assert
        var absChild = root.Children[0].Children[0];
        Assert.Equal(240f, absChild.Width, 1);
        // X should be padding.Left + left inset = 10 + 20 = 30
        Assert.Equal(30f, absChild.X, 1);
    }

    [Fact]
    public void ComputeLayout_PositionAbsolute_InsetSizing_TopBottom()
    {
        // Arrange: Absolute child with Top=10, Bottom=10, no explicit Height,
        // in a 300x200 container with Padding=5.
        // Expected: child.Height = 200 - 5 - 5 - 10 - 10 = 170.
        var flex = new FlexElement
        {
            Direction = FlexDirection.Row,
            Width = "300",
            Height = "200",
            Padding = "5"
        };
        flex.AddChild(new TextElement
        {
            Content = "Abs",
            Width = "50",
            Position = Position.Absolute,
            Top = "10",
            Bottom = "10"
        });

        var template = new Template
        {
            Canvas = new CanvasSettings { Width = 300 },
            Elements = new List<TemplateElement> { flex }
        };

        // Act
        var root = _engine.ComputeLayout(template);

        // Assert
        var absChild = root.Children[0].Children[0];
        Assert.Equal(170f, absChild.Height, 1);
        // Y should be padding.Top + top inset = 5 + 10 = 15
        Assert.Equal(15f, absChild.Y, 1);
    }

    [Fact]
    public void ComputeLayout_PositionAbsolute_ExcludedFromIntrinsicMeasurement()
    {
        // Arrange: Container with auto height, one absolute child (100px tall)
        // and one flow child (50px tall). Container height should be based on
        // the flow child only (50px + padding), not the absolute child.
        var flex = new FlexElement
        {
            Direction = FlexDirection.Column,
            Width = "300",
            Padding = "10"
        };
        flex.AddChild(new TextElement
        {
            Content = "Flow",
            Width = "100",
            Height = "50"
        });
        flex.AddChild(new TextElement
        {
            Content = "Abs",
            Width = "100",
            Height = "100",
            Position = Position.Absolute
        });

        var template = new Template
        {
            Canvas = new CanvasSettings { Width = 300 },
            Elements = new List<TemplateElement> { flex }
        };

        // Act
        var root = _engine.ComputeLayout(template);

        // Assert: container height = 50 (flow child) + 10 + 10 (padding) = 70
        var flexNode = root.Children[0];
        Assert.Equal(70f, flexNode.Height, 1);
    }

    // ────────────────────────────────────────────────────────────────
    // Position Relative Tests
    // ────────────────────────────────────────────────────────────────

    [Fact]
    public void ComputeLayout_PositionRelative_LeftOffset_ShiftsRight()
    {
        // Arrange: Single child with Position=Relative, Left=15 in a Row container.
        // Normal flow position X=0, Y=0. After relative offset: X=15, Y=0.
        var flex = new FlexElement
        {
            Direction = FlexDirection.Row,
            Width = "300",
            Height = "200"
        };
        flex.AddChild(new TextElement
        {
            Content = "Rel",
            Width = "80",
            Height = "40",
            Position = Position.Relative,
            Left = "15"
        });

        var template = new Template
        {
            Canvas = new CanvasSettings { Width = 300 },
            Elements = new List<TemplateElement> { flex }
        };

        // Act
        var root = _engine.ComputeLayout(template);

        // Assert
        var child = root.Children[0].Children[0];
        Assert.Equal(15f, child.X, 1);
        Assert.Equal(0f, child.Y, 1);
    }

    [Fact]
    public void ComputeLayout_PositionRelative_TopOffset_ShiftsDown()
    {
        // Arrange: Single child with Position=Relative, Top=20 in a Row container.
        // Normal flow X=0, Y=0. After relative offset: X=0, Y=20.
        var flex = new FlexElement
        {
            Direction = FlexDirection.Row,
            Width = "300",
            Height = "200"
        };
        flex.AddChild(new TextElement
        {
            Content = "Rel",
            Width = "80",
            Height = "40",
            Position = Position.Relative,
            Top = "20"
        });

        var template = new Template
        {
            Canvas = new CanvasSettings { Width = 300 },
            Elements = new List<TemplateElement> { flex }
        };

        // Act
        var root = _engine.ComputeLayout(template);

        // Assert
        var child = root.Children[0].Children[0];
        Assert.Equal(0f, child.X, 1);
        Assert.Equal(20f, child.Y, 1);
    }

    [Fact]
    public void ComputeLayout_PositionRelative_RightOffset_ShiftsLeft()
    {
        // Arrange: Single child with Position=Relative, Right=15 (no Left defined).
        // Normal flow X=0. After relative offset: X = 0 - 15 = -15.
        // Right means shift left per CSS spec.
        var flex = new FlexElement
        {
            Direction = FlexDirection.Row,
            Width = "300",
            Height = "200"
        };
        flex.AddChild(new TextElement
        {
            Content = "Rel",
            Width = "80",
            Height = "40",
            Position = Position.Relative,
            Right = "15"
        });

        var template = new Template
        {
            Canvas = new CanvasSettings { Width = 300 },
            Elements = new List<TemplateElement> { flex }
        };

        // Act
        var root = _engine.ComputeLayout(template);

        // Assert
        var child = root.Children[0].Children[0];
        Assert.Equal(-15f, child.X, 1);
        Assert.Equal(0f, child.Y, 1);
    }

    [Fact]
    public void ComputeLayout_PositionRelative_BottomOffset_ShiftsUp()
    {
        // Arrange: Single child with Position=Relative, Bottom=20 (no Top defined).
        // Normal flow Y=0. After relative offset: Y = 0 - 20 = -20.
        // Bottom means shift up per CSS spec.
        var flex = new FlexElement
        {
            Direction = FlexDirection.Row,
            Width = "300",
            Height = "200"
        };
        flex.AddChild(new TextElement
        {
            Content = "Rel",
            Width = "80",
            Height = "40",
            Position = Position.Relative,
            Bottom = "20"
        });

        var template = new Template
        {
            Canvas = new CanvasSettings { Width = 300 },
            Elements = new List<TemplateElement> { flex }
        };

        // Act
        var root = _engine.ComputeLayout(template);

        // Assert
        var child = root.Children[0].Children[0];
        Assert.Equal(0f, child.X, 1);
        Assert.Equal(-20f, child.Y, 1);
    }

    [Fact]
    public void ComputeLayout_PositionRelative_LeftTakesPriorityOverRight()
    {
        // Arrange: Child with Position=Relative, Left=10 and Right=20.
        // Per CSS spec, Left wins when both are specified. X shifted by +10 only.
        var flex = new FlexElement
        {
            Direction = FlexDirection.Row,
            Width = "300",
            Height = "200"
        };
        flex.AddChild(new TextElement
        {
            Content = "Rel",
            Width = "80",
            Height = "40",
            Position = Position.Relative,
            Left = "10",
            Right = "20"
        });

        var template = new Template
        {
            Canvas = new CanvasSettings { Width = 300 },
            Elements = new List<TemplateElement> { flex }
        };

        // Act
        var root = _engine.ComputeLayout(template);

        // Assert: Left wins, so X = 0 + 10 = 10
        var child = root.Children[0].Children[0];
        Assert.Equal(10f, child.X, 1);
    }

    [Fact]
    public void ComputeLayout_PositionRelative_DoesNotAffectSiblings()
    {
        // Arrange: Two children in a Row. First is Relative with Left=100.
        // Second child should be at X=80 (width of first child), as if
        // first child were Static — relative offset doesn't affect siblings.
        var flex = new FlexElement
        {
            Direction = FlexDirection.Row,
            Width = "300",
            Height = "200"
        };
        flex.AddChild(new TextElement
        {
            Content = "Rel",
            Width = "80",
            Height = "40",
            Position = Position.Relative,
            Left = "100"
        });
        flex.AddChild(new TextElement
        {
            Content = "Static",
            Width = "80",
            Height = "40"
        });

        var template = new Template
        {
            Canvas = new CanvasSettings { Width = 300 },
            Elements = new List<TemplateElement> { flex }
        };

        // Act
        var root = _engine.ComputeLayout(template);

        // Assert: second child at X=80 (not affected by first child's offset)
        var flexNode = root.Children[0];
        Assert.Equal(80f, flexNode.Children[1].X, 1);
        Assert.Equal(0f, flexNode.Children[1].Y, 1);
    }

    // ────────────────────────────────────────────────────────────────
    // Aspect Ratio Tests
    // ────────────────────────────────────────────────────────────────

    [Fact]
    public void ComputeLayout_AspectRatio_WidthDefined_CalculatesHeight()
    {
        // Arrange: Child with Width=200 and AspectRatio=2.0 (width/height).
        // Expected: Height = 200 / 2 = 100.
        var flex = new FlexElement
        {
            Direction = FlexDirection.Column,
            Width = "400",
            Height = "400"
        };
        flex.AddChild(new TextElement
        {
            Content = "AR",
            Width = "200",
            AspectRatio = 2.0f
        });

        var template = new Template
        {
            Canvas = new CanvasSettings { Width = 400 },
            Elements = new List<TemplateElement> { flex }
        };

        // Act
        var root = _engine.ComputeLayout(template);

        // Assert
        var child = root.Children[0].Children[0];
        Assert.Equal(200f, child.Width, 1);
        Assert.Equal(100f, child.Height, 1);
    }

    [Fact]
    public void ComputeLayout_AspectRatio_HeightDefined_CalculatesWidth()
    {
        // Arrange: Child with Height=100 and AspectRatio=2.0 (width/height).
        // Expected: Width = 100 * 2 = 200.
        var flex = new FlexElement
        {
            Direction = FlexDirection.Column,
            Width = "400",
            Height = "400"
        };
        flex.AddChild(new TextElement
        {
            Content = "AR",
            Height = "100",
            AspectRatio = 2.0f
        });

        var template = new Template
        {
            Canvas = new CanvasSettings { Width = 400 },
            Elements = new List<TemplateElement> { flex }
        };

        // Act
        var root = _engine.ComputeLayout(template);

        // Assert
        var child = root.Children[0].Children[0];
        Assert.Equal(200f, child.Width, 1);
        Assert.Equal(100f, child.Height, 1);
    }

    [Fact]
    public void ComputeLayout_AspectRatio_NeitherDefined_NoEffect()
    {
        // Arrange: Child with AspectRatio=2.0 but no explicit Width or Height.
        // AspectRatio should not apply — can't infer dimensions from nothing.
        // The child should get default sizing from the layout engine (e.g., stretch to container width).
        var flex = new FlexElement
        {
            Direction = FlexDirection.Column,
            Width = "400",
            Height = "400"
        };
        flex.AddChild(new TextElement
        {
            Content = "AR",
            AspectRatio = 2.0f
        });

        var template = new Template
        {
            Canvas = new CanvasSettings { Width = 400 },
            Elements = new List<TemplateElement> { flex }
        };

        // Act
        var root = _engine.ComputeLayout(template);

        // Assert: child should NOT have height computed as width/2
        // (since neither dimension was explicitly provided, aspect ratio has no anchor)
        var child = root.Children[0].Children[0];
        // The child's width and height should be whatever the layout engine defaults to,
        // NOT width=400 height=200 from aspect ratio. The exact values depend on the
        // engine's default behavior, but height should NOT be width/aspectRatio.
        // For a column flex with stretch, width would be 400, but height should remain
        // the default text height (not 200).
        Assert.NotEqual(200f, child.Height);
    }

    // ────────────────────────────────────────────────────────────────
    // Absolute Positioning: Justify/Align Fallback Tests
    // ────────────────────────────────────────────────────────────────

    [Fact]
    public void ComputeLayout_PositionAbsolute_NoInsets_JustifyCenter_CentersOnMainAxis()
    {
        // Arrange: Row 300x200, justify:center, absolute child 100x50 without insets.
        // Expected: X = (300-100)/2 = 100 (centered on main axis).
        var flex = new FlexElement
        {
            Direction = FlexDirection.Row,
            Width = "300",
            Height = "200",
            Justify = JustifyContent.Center
        };
        flex.AddChild(new TextElement
        {
            Content = "Abs",
            Width = "100",
            Height = "50",
            Position = Position.Absolute
        });

        var template = new Template
        {
            Canvas = new CanvasSettings { Width = 300 },
            Elements = new List<TemplateElement> { flex }
        };

        // Act
        var root = _engine.ComputeLayout(template);

        // Assert
        var absChild = root.Children[0].Children[0];
        Assert.Equal(100f, absChild.X, 1);
    }

    [Fact]
    public void ComputeLayout_PositionAbsolute_NoInsets_AlignEnd_AlignsCrossAxis()
    {
        // Arrange: Row 300x200, align:end, absolute child 100x50 without insets.
        // Expected: Y = 200-50 = 150 (aligned to end on cross axis).
        var flex = new FlexElement
        {
            Direction = FlexDirection.Row,
            Width = "300",
            Height = "200",
            Align = AlignItems.End
        };
        flex.AddChild(new TextElement
        {
            Content = "Abs",
            Width = "100",
            Height = "50",
            Position = Position.Absolute
        });

        var template = new Template
        {
            Canvas = new CanvasSettings { Width = 300 },
            Elements = new List<TemplateElement> { flex }
        };

        // Act
        var root = _engine.ComputeLayout(template);

        // Assert
        var absChild = root.Children[0].Children[0];
        Assert.Equal(150f, absChild.Y, 1);
    }

    // ────────────────────────────────────────────────────────────────
    // Aspect Ratio: Post Flex Resolution Tests
    // ────────────────────────────────────────────────────────────────

    [Fact]
    public void ComputeLayout_AspectRatio_WithFlexGrow_RecalcsCrossAxis()
    {
        // Arrange: Row 400px, child width=100 aspect-ratio=2 grow=1.
        // After grow, width fills to 400. Height should be recalculated as 400/2 = 200.
        var flex = new FlexElement
        {
            Direction = FlexDirection.Row,
            Width = "400",
            Height = "400"
        };
        flex.AddChild(new TextElement
        {
            Content = "AR",
            Width = "100",
            Height = "50",
            AspectRatio = 2.0f,
            Grow = 1
        });

        var template = new Template
        {
            Canvas = new CanvasSettings { Width = 400 },
            Elements = new List<TemplateElement> { flex }
        };

        // Act
        var root = _engine.ComputeLayout(template);

        // Assert: width should grow to 400 (full container), height = 400/2 = 200
        var child = root.Children[0].Children[0];
        Assert.Equal(400f, child.Width, 1);
        Assert.Equal(200f, child.Height, 1);
    }

    // ────────────────────────────────────────────────────────────────
    // Column-direction: Absolute Justify/Align + AspectRatio Tests
    // ────────────────────────────────────────────────────────────────

    [Fact]
    public void ComputeLayout_PositionAbsolute_Column_JustifyCenter_CentersOnMainAxis()
    {
        // Arrange: Column 200x400, justify:center, absolute child 50x50 without insets.
        // Column main axis = Y. Expected: Y = (400-50)/2 = 175.
        var flex = new FlexElement
        {
            Direction = FlexDirection.Column,
            Width = "200",
            Height = "400",
            Justify = JustifyContent.Center
        };
        flex.AddChild(new TextElement
        {
            Content = "Abs",
            Width = "50",
            Height = "50",
            Position = Position.Absolute
        });

        var template = new Template
        {
            Canvas = new CanvasSettings { Width = 200 },
            Elements = new List<TemplateElement> { flex }
        };

        // Act
        var root = _engine.ComputeLayout(template);

        // Assert
        var absChild = root.Children[0].Children[0];
        Assert.Equal(175f, absChild.Y, 1);
    }

    [Fact]
    public void ComputeLayout_PositionAbsolute_Column_AlignEnd_AlignsCrossAxis()
    {
        // Arrange: Column 200x400, align:end, absolute child 50x50 without insets.
        // Column cross axis = X. Expected: X = 200-50 = 150.
        var flex = new FlexElement
        {
            Direction = FlexDirection.Column,
            Width = "200",
            Height = "400",
            Align = AlignItems.End
        };
        flex.AddChild(new TextElement
        {
            Content = "Abs",
            Width = "50",
            Height = "50",
            Position = Position.Absolute
        });

        var template = new Template
        {
            Canvas = new CanvasSettings { Width = 200 },
            Elements = new List<TemplateElement> { flex }
        };

        // Act
        var root = _engine.ComputeLayout(template);

        // Assert
        var absChild = root.Children[0].Children[0];
        Assert.Equal(150f, absChild.X, 1);
    }

    [Fact]
    public void ComputeLayout_AspectRatio_Column_WithFlexGrow_RecalcsCrossAxis()
    {
        // Arrange: Column 200x400, child height=100 aspect-ratio=2 grow=1.
        // After grow, height fills to 400. Width should be recalculated as 400*2 = 800.
        var flex = new FlexElement
        {
            Direction = FlexDirection.Column,
            Width = "200",
            Height = "400"
        };
        flex.AddChild(new TextElement
        {
            Content = "AR",
            Width = "50",
            Height = "100",
            AspectRatio = 2.0f,
            Grow = 1
        });

        var template = new Template
        {
            Canvas = new CanvasSettings { Width = 200 },
            Elements = new List<TemplateElement> { flex }
        };

        // Act
        var root = _engine.ComputeLayout(template);

        // Assert: height should grow to 400 (full container), width = 400*2 = 800
        var child = root.Children[0].Children[0];
        Assert.Equal(400f, child.Height, 1);
        Assert.Equal(800f, child.Width, 1);
    }
}
