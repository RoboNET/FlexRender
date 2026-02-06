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

    // ────────────────────────────────────────────────────────────────
    // Visual Docs Diagnostic Tests — modeled after position/*.yaml
    // ────────────────────────────────────────────────────────────────

    [Fact]
    public void ComputeLayout_PositionAbsolute_Column_TopLeft_PositionsCorrectly()
    {
        // Models absolute-top-left.yaml: Column container 360x180, padding=20, gap=12.
        // Two flow children followed by one absolute child with top=10, left=10.
        // Absolute child should be placed at padding + inset, NOT after flow children.
        var flex = new FlexElement
        {
            Direction = FlexDirection.Column,
            Width = "360",
            Height = "180",
            Padding = "20",
            Gap = "12",
            Background = "#f5f5f5"
        };
        flex.AddChild(new FlexElement { Width = "200", Height = "40" });
        flex.AddChild(new FlexElement { Width = "160", Height = "40" });
        flex.AddChild(new FlexElement
        {
            Width = "80",
            Height = "40",
            Position = Position.Absolute,
            Top = "10",
            Left = "10"
        });

        var template = new Template
        {
            Canvas = new CanvasSettings { Width = 360 },
            Elements = new List<TemplateElement> { flex }
        };

        // Act
        var root = _engine.ComputeLayout(template);

        // Assert: absolute child at padding + inset = (20+10, 20+10) = (30, 30)
        var container = root.Children[0];
        var absChild = container.Children[2];
        Assert.Equal(30f, absChild.X, 1);
        Assert.Equal(30f, absChild.Y, 1);
    }

    [Fact]
    public void ComputeLayout_PositionAbsolute_Column_BottomRight_PositionsCorrectly()
    {
        // Models absolute-bottom-right.yaml: Column container 360x180, padding=20, gap=12.
        // Same container but absolute child has bottom=10, right=10.
        // X = 360 - 20 - 80 - 10 = 250, Y = 180 - 20 - 40 - 10 = 110.
        var flex = new FlexElement
        {
            Direction = FlexDirection.Column,
            Width = "360",
            Height = "180",
            Padding = "20",
            Gap = "12",
            Background = "#f5f5f5"
        };
        flex.AddChild(new FlexElement { Width = "200", Height = "40" });
        flex.AddChild(new FlexElement { Width = "160", Height = "40" });
        flex.AddChild(new FlexElement
        {
            Width = "80",
            Height = "40",
            Position = Position.Absolute,
            Bottom = "10",
            Right = "10"
        });

        var template = new Template
        {
            Canvas = new CanvasSettings { Width = 360 },
            Elements = new List<TemplateElement> { flex }
        };

        // Act
        var root = _engine.ComputeLayout(template);

        // Assert: X = container(360) - padding(20) - width(80) - right(10) = 250
        //         Y = container(180) - padding(20) - height(40) - bottom(10) = 110
        var container = root.Children[0];
        var absChild = container.Children[2];
        Assert.Equal(250f, absChild.X, 1);
        Assert.Equal(110f, absChild.Y, 1);
    }

    [Fact]
    public void ComputeLayout_PositionAbsolute_Column_FlowExclusion_WithGap()
    {
        // Models flow-exclusion.yaml: Column container 360x260, padding=20, gap=8.
        // Child A (static, h=50), Child B (absolute, top=10, right=10),
        // Child C (static, h=50), Child D (static, h=50).
        // The absolute child B should be excluded from flow, so C follows A with gap.
        var flex = new FlexElement
        {
            Direction = FlexDirection.Column,
            Width = "360",
            Height = "260",
            Padding = "20",
            Gap = "8"
        };
        // Child A — static flow
        flex.AddChild(new FlexElement { Height = "50" });
        // Child B — absolute, excluded from flow
        flex.AddChild(new FlexElement
        {
            Padding = "8 16",
            Position = Position.Absolute,
            Top = "10",
            Right = "10"
        });
        // Child C — static flow
        flex.AddChild(new FlexElement { Height = "50" });
        // Child D — static flow
        flex.AddChild(new FlexElement { Height = "50" });

        var template = new Template
        {
            Canvas = new CanvasSettings { Width = 360 },
            Elements = new List<TemplateElement> { flex }
        };

        // Act
        var root = _engine.ComputeLayout(template);

        // Assert
        var container = root.Children[0];
        var childA = container.Children[0];
        var childC = container.Children[2];
        var childD = container.Children[3];

        // Child A at padding top = 20
        Assert.Equal(20f, childA.Y, 1);
        // Child C follows A: 20 + 50 + 8 (gap) = 78
        Assert.Equal(78f, childC.Y, 1);
        // Child D follows C: 78 + 50 + 8 (gap) = 136
        Assert.Equal(136f, childD.Y, 1);
    }

    [Fact]
    public void ComputeLayout_PositionRelative_Row_WithGap_VisibleShift()
    {
        // Models relative-offset.yaml: Row container 360x120, padding=20, gap=16, align=start.
        // Box1 (80x60), Box2 (80x60, relative, top=15, left=20), Box3 (80x60).
        // Box2 shifts visually but does not affect Box3's position.
        var flex = new FlexElement
        {
            Direction = FlexDirection.Row,
            Width = "360",
            Height = "120",
            Padding = "20",
            Gap = "16",
            Align = AlignItems.Start
        };
        flex.AddChild(new FlexElement { Width = "80", Height = "60" });
        flex.AddChild(new FlexElement
        {
            Width = "80",
            Height = "60",
            Position = Position.Relative,
            Top = "15",
            Left = "20"
        });
        flex.AddChild(new FlexElement { Width = "80", Height = "60" });

        var template = new Template
        {
            Canvas = new CanvasSettings { Width = 360 },
            Elements = new List<TemplateElement> { flex }
        };

        // Act
        var root = _engine.ComputeLayout(template);

        // Assert
        var container = root.Children[0];
        var box1 = container.Children[0];
        var box2 = container.Children[1];
        var box3 = container.Children[2];

        // Box1 at padding left = 20
        Assert.Equal(20f, box1.X, 1);
        // Box2 normal flow X = 20 + 80 + 16 = 116, then + left offset 20 = 136
        Assert.Equal(136f, box2.X, 1);
        // Box2 normal flow Y = 20, then + top offset 15 = 35
        Assert.Equal(35f, box2.Y, 1);
        // Box3 unaffected by Box2's offset: 20 + 80 + 16 + 80 + 16 = 212
        Assert.Equal(212f, box3.X, 1);
    }

    [Fact]
    public void ComputeLayout_PositionAbsolute_Column_CenteredByInsets()
    {
        // Models absolute-center.yaml: Column container 360x200, padding=20, gap=12.
        // One flow child plus one absolute child with top=40, bottom=40, left=100, right=100.
        // Insets are relative to the padding box (content area).
        // Content area: 360-20-20=320 wide, 200-20-20=160 tall.
        // Absolute child: width = 320 - 100 - 100 = 120, height = 160 - 40 - 40 = 80.
        // Position: X = 20 + 100 = 120, Y = 20 + 40 = 60.
        var flex = new FlexElement
        {
            Direction = FlexDirection.Column,
            Width = "360",
            Height = "200",
            Padding = "20",
            Gap = "12"
        };
        flex.AddChild(new TextElement { Content = "Flow content" });
        flex.AddChild(new FlexElement
        {
            Position = Position.Absolute,
            Top = "40",
            Bottom = "40",
            Left = "100",
            Right = "100"
        });

        var template = new Template
        {
            Canvas = new CanvasSettings { Width = 360 },
            Elements = new List<TemplateElement> { flex }
        };

        // Act
        var root = _engine.ComputeLayout(template);

        // Assert
        var container = root.Children[0];
        var absChild = container.Children[1];

        Assert.Equal(120f, absChild.Width, 1);
        Assert.Equal(80f, absChild.Height, 1);
        Assert.Equal(120f, absChild.X, 1);
        Assert.Equal(60f, absChild.Y, 1);
    }

    [Fact]
    public void ComputeLayout_PositionAbsolute_InsetSizing_AllFourInsets()
    {
        // Models inset-sizing.yaml: Column container 360x300, no padding.
        // Absolute child with top=20, bottom=20, left=20, right=20, no explicit width/height.
        // Expected: width = 360 - 20 - 20 = 320, height = 300 - 20 - 20 = 260.
        // Position: X = 20, Y = 20.
        var flex = new FlexElement
        {
            Direction = FlexDirection.Column,
            Width = "360",
            Height = "300",
            Padding = "0"
        };
        flex.AddChild(new FlexElement
        {
            Position = Position.Absolute,
            Top = "20",
            Bottom = "20",
            Left = "20",
            Right = "20"
        });

        var template = new Template
        {
            Canvas = new CanvasSettings { Width = 360 },
            Elements = new List<TemplateElement> { flex }
        };

        // Act
        var root = _engine.ComputeLayout(template);

        // Assert
        var container = root.Children[0];
        var absChild = container.Children[0];

        Assert.Equal(320f, absChild.Width, 1);
        Assert.Equal(260f, absChild.Height, 1);
        Assert.Equal(20f, absChild.X, 1);
        Assert.Equal(20f, absChild.Y, 1);
    }
}
