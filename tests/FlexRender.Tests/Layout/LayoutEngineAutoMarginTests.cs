using FlexRender.Layout;
using FlexRender.Parsing.Ast;
using Xunit;

namespace FlexRender.Tests.Layout;

/// <summary>
/// Tests for auto margin behavior in flexbox layout.
/// Auto margins consume free space before justify-content on the main axis,
/// and override align-items/align-self on the cross axis.
/// </summary>
public sealed class LayoutEngineAutoMarginTests
{
    private readonly LayoutEngine _engine = new();

    // ────────────────────────────────────────────────────────────────
    // Main Axis Auto Margins (Row)
    // ────────────────────────────────────────────────────────────────

    [Fact]
    public void ComputeLayout_AutoMarginLeft_PushesChildToRight()
    {
        // Arrange: Row container 300px, child 100px with margin-left: auto.
        // Free space = 300 - 100 = 200. All goes to left auto margin.
        // Expected: child X = 200.
        var flex = new FlexElement
        {
            Direction = FlexDirection.Row,
            Width = "300",
            Height = "200"
        };
        flex.AddChild(new TextElement
        {
            Content = "A",
            Width = "100",
            Height = "50",
            Margin = "0 0 0 auto"
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
        Assert.Equal(200f, child.X, 1);
    }

    [Fact]
    public void ComputeLayout_AutoMarginBothSides_CentersChild()
    {
        // Arrange: Row container 300px, child 100px with margin: 0 auto.
        // Free space = 200, split between left and right auto margins.
        // Expected: child X = 100 (centered).
        var flex = new FlexElement
        {
            Direction = FlexDirection.Row,
            Width = "300",
            Height = "200"
        };
        flex.AddChild(new TextElement
        {
            Content = "A",
            Width = "100",
            Height = "50",
            Margin = "0 auto"
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
        Assert.Equal(100f, child.X, 1);
    }

    [Fact]
    public void ComputeLayout_AutoMargins_OverridesJustifyContent()
    {
        // Arrange: Row container 300px with JustifyContent.End, child 100px
        // with margin: "0 auto 0 0" (right=auto). The auto right margin absorbs
        // the 200px of free space, keeping child at X=0 despite justify-content: end.
        var flex = new FlexElement
        {
            Direction = FlexDirection.Row,
            Width = "300",
            Height = "200",
            Justify = JustifyContent.End
        };
        flex.AddChild(new TextElement
        {
            Content = "A",
            Width = "100",
            Height = "50",
            Margin = "0 auto 0 0"
        });

        var template = new Template
        {
            Canvas = new CanvasSettings { Width = 300 },
            Elements = new List<TemplateElement> { flex }
        };

        // Act
        var root = _engine.ComputeLayout(template);

        // Assert: child stays at X=0 because auto right margin absorbs free space
        var child = root.Children[0].Children[0];
        Assert.Equal(0f, child.X, 1);
    }

    [Fact]
    public void ComputeLayout_AutoMargins_NegativeFreeSpace_MarginIsZero()
    {
        // Arrange: Row container 300px, child 400px wide with margin: "0 auto".
        // Negative free space means auto margins resolve to 0.
        var flex = new FlexElement
        {
            Direction = FlexDirection.Row,
            Width = "300",
            Height = "200"
        };
        flex.AddChild(new TextElement
        {
            Content = "A",
            Width = "400",
            Height = "50",
            Shrink = 0,
            Margin = "0 auto"
        });

        var template = new Template
        {
            Canvas = new CanvasSettings { Width = 300 },
            Elements = new List<TemplateElement> { flex }
        };

        // Act
        var root = _engine.ComputeLayout(template);

        // Assert: child positioned normally at X=0 (auto margins have no effect)
        var child = root.Children[0].Children[0];
        Assert.Equal(0f, child.X, 1);
    }

    // ────────────────────────────────────────────────────────────────
    // Cross Axis Auto Margins (Row)
    // ────────────────────────────────────────────────────────────────

    [Fact]
    public void ComputeLayout_AutoMarginCrossAxis_CentersVertically()
    {
        // Arrange: Row container 300x200, child 100x50 with margin: "auto 0".
        // Cross axis free space = 200 - 50 = 150. Both top and bottom auto.
        // Expected: child Y = 150/2 = 75 (centered).
        var flex = new FlexElement
        {
            Direction = FlexDirection.Row,
            Width = "300",
            Height = "200"
        };
        flex.AddChild(new TextElement
        {
            Content = "A",
            Width = "100",
            Height = "50",
            Margin = "auto 0"
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
        Assert.Equal(75f, child.Y, 1);
    }

    [Fact]
    public void ComputeLayout_AutoMarginCrossAxis_TopAutoOnly_PushesDown()
    {
        // Arrange: Row container 300x200, child 100x50 with margin: "auto 0 0 0".
        // Cross axis free space = 200 - 50 = 150. Only top is auto.
        // Expected: child Y = 150 (pushed to bottom).
        var flex = new FlexElement
        {
            Direction = FlexDirection.Row,
            Width = "300",
            Height = "200"
        };
        flex.AddChild(new TextElement
        {
            Content = "A",
            Width = "100",
            Height = "50",
            Margin = "auto 0 0 0"
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
        Assert.Equal(150f, child.Y, 1);
    }

    // ────────────────────────────────────────────────────────────────
    // Main Axis Auto Margins (Column)
    // ────────────────────────────────────────────────────────────────

    [Fact]
    public void ComputeLayout_Column_AutoMarginTop_PushesChildDown()
    {
        // Arrange: Column container 300x400, child 100 tall with margin: "auto 0 0 0".
        // Main axis is vertical. Free space = 400 - 100 = 300. All goes to top auto margin.
        // Expected: child Y = 300.
        var flex = new FlexElement
        {
            Direction = FlexDirection.Column,
            Width = "300",
            Height = "400"
        };
        flex.AddChild(new TextElement
        {
            Content = "A",
            Width = "100",
            Height = "100",
            Margin = "auto 0 0 0"
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
        Assert.Equal(300f, child.Y, 1);
    }

    // ────────────────────────────────────────────────────────────────
    // Multiple Children with Auto Margins
    // ────────────────────────────────────────────────────────────────

    [Fact]
    public void ComputeLayout_MultipleChildren_AutoMarginDistributesFreeSpace()
    {
        // Arrange: Row container 400px. Two children 100px each.
        // Child[0] has margin-right: auto, child[1] is normal.
        // Free space = 400 - 200 = 200. All goes to child[0]'s right auto margin.
        // Expected: child[0] X=0, child[1] X=300 (100 + 200 auto margin).
        var flex = new FlexElement
        {
            Direction = FlexDirection.Row,
            Width = "400",
            Height = "100"
        };
        flex.AddChild(new TextElement
        {
            Content = "A",
            Width = "100",
            Height = "50",
            Margin = "0 auto 0 0"
        });
        flex.AddChild(new TextElement
        {
            Content = "B",
            Width = "100",
            Height = "50"
        });

        var template = new Template
        {
            Canvas = new CanvasSettings { Width = 400 },
            Elements = new List<TemplateElement> { flex }
        };

        // Act
        var root = _engine.ComputeLayout(template);

        // Assert
        var flexNode = root.Children[0];
        Assert.Equal(0f, flexNode.Children[0].X, 1);
        Assert.Equal(300f, flexNode.Children[1].X, 1);
    }
}
