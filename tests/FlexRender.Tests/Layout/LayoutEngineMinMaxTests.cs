using FlexRender.Layout;
using FlexRender.Parsing.Ast;
using Xunit;

namespace FlexRender.Tests.Layout;

/// <summary>
/// Tests for min/max width and height constraints with flex layout.
/// Validates iterative two-pass freeze algorithm: items clamped by min/max are frozen,
/// remaining space redistributed to unfrozen items.
/// </summary>
public class LayoutEngineMinMaxTests
{
    private readonly LayoutEngine _engine = new();

    [Fact]
    public void ComputeLayout_MinWidth_ChildDoesNotShrinkBelowMinWidth()
    {
        // Arrange: Row W=200, child[0] W=150 MinWidth=100 Shrink=1, child[1] W=150 Shrink=1
        // Bases: 150+150=300, overflow=100
        // Without min: each shrinks by 50 -> 100, 100
        // With min: child[0] clamped at 100 (min), child[1] = 100
        var flex = new FlexElement
        {
            Direction = FlexDirection.Row,
            Width = "200",
            Height = "100"
        };
        flex.AddChild(new TextElement { Content = "A", Width = "150", MinWidth = "100", Shrink = 1 });
        flex.AddChild(new TextElement { Content = "B", Width = "150", Shrink = 1 });

        var template = new Template
        {
            Canvas = new CanvasSettings { Width = 200 },
            Elements = new List<TemplateElement> { flex }
        };

        // Act
        var root = _engine.ComputeLayout(template);

        // Assert
        var flexNode = root.Children[0];
        Assert.Equal(0f, flexNode.Children[0].X, 0.1f);
        Assert.Equal(100f, flexNode.Children[0].Width, 0.1f);  // MinWidth respected
        Assert.Equal(100f, flexNode.Children[1].X, 0.1f);
        Assert.Equal(100f, flexNode.Children[1].Width, 0.1f);
    }

    [Fact]
    public void ComputeLayout_MaxWidth_ChildDoesNotGrowBeyondMaxWidth()
    {
        // Arrange: Row W=300, child[0] B=0 G=1 MaxWidth=80, child[1] B=0 G=1
        // Iter1: each hypothetical = 150, child[0] clamped at 80 -> freeze
        // Iter2: unfrozen freeSpace = 300-80 = 220, child[1] = 220
        var flex = new FlexElement
        {
            Direction = FlexDirection.Row,
            Width = "300",
            Height = "100"
        };
        flex.AddChild(new TextElement { Content = "A", Basis = "0", Grow = 1, MaxWidth = "80" });
        flex.AddChild(new TextElement { Content = "B", Basis = "0", Grow = 1 });

        var template = new Template
        {
            Canvas = new CanvasSettings { Width = 300 },
            Elements = new List<TemplateElement> { flex }
        };

        // Act
        var root = _engine.ComputeLayout(template);

        // Assert
        var flexNode = root.Children[0];
        Assert.Equal(0f, flexNode.Children[0].X, 0.1f);
        Assert.Equal(80f, flexNode.Children[0].Width, 0.1f);   // MaxWidth respected
        Assert.Equal(80f, flexNode.Children[1].X, 0.1f);
        Assert.Equal(220f, flexNode.Children[1].Width, 0.1f);
    }

    [Fact]
    public void ComputeLayout_MinHeight_ChildDoesNotShrinkBelowMinHeight()
    {
        // Arrange: Column H=100, child[0] H=80 MinHeight=60 Shrink=1, child[1] H=80 Shrink=1
        // Bases: 80+80=160, overflow=60
        // Iter1: each hypothetical shrinks by 30 -> 50, but child[0] min=60 -> freeze at 60
        // Iter2: unfrozen freeSpace = 100-60-80 = -40, child[1] = 80-40 = 40
        var flex = new FlexElement
        {
            Direction = FlexDirection.Column,
            Height = "100"
        };
        flex.AddChild(new TextElement { Content = "A", Height = "80", MinHeight = "60", Shrink = 1 });
        flex.AddChild(new TextElement { Content = "B", Height = "80", Shrink = 1 });

        var template = new Template
        {
            Canvas = new CanvasSettings { Width = 300 },
            Elements = new List<TemplateElement> { flex }
        };

        // Act
        var root = _engine.ComputeLayout(template);

        // Assert
        var flexNode = root.Children[0];
        Assert.Equal(0f, flexNode.Children[0].Y, 0.1f);
        Assert.Equal(60f, flexNode.Children[0].Height, 0.1f);  // MinHeight respected
        Assert.Equal(60f, flexNode.Children[1].Y, 0.1f);
        Assert.Equal(40f, flexNode.Children[1].Height, 0.1f);
    }

    [Fact]
    public void ComputeLayout_MaxHeight_ChildDoesNotGrowBeyondMaxHeight()
    {
        // Arrange: Column H=200, child[0] B=0 G=1 MaxHeight=60, child[1] B=0 G=1
        // Iter1: each hypothetical = 100, child[0] clamped at 60 -> freeze
        // Iter2: unfrozen freeSpace = 200-60 = 140, child[1] = 140
        var flex = new FlexElement
        {
            Direction = FlexDirection.Column,
            Height = "200"
        };
        flex.AddChild(new TextElement { Content = "A", Basis = "0", Grow = 1, MaxHeight = "60" });
        flex.AddChild(new TextElement { Content = "B", Basis = "0", Grow = 1 });

        var template = new Template
        {
            Canvas = new CanvasSettings { Width = 300 },
            Elements = new List<TemplateElement> { flex }
        };

        // Act
        var root = _engine.ComputeLayout(template);

        // Assert
        var flexNode = root.Children[0];
        Assert.Equal(0f, flexNode.Children[0].Y, 0.1f);
        Assert.Equal(60f, flexNode.Children[0].Height, 0.1f);  // MaxHeight respected
        Assert.Equal(60f, flexNode.Children[1].Y, 0.1f);
        Assert.Equal(140f, flexNode.Children[1].Height, 0.1f);
    }

    [Fact]
    public void ComputeLayout_MinWidth_WithFlexShrink_RespectsMinWidth()
    {
        // Arrange: Row W=100, child[0] W=80 MinWidth=60 Shrink=1, child[1] W=80 Shrink=1
        // Bases: 160, overflow=60
        // Iter1: each hypothetical shrinks by 30 -> 50, child[0] min=60 -> freeze at 60
        // Iter2: unfrozen freeSpace = 100-60-80 = -40, child[1] = 80-40 = 40
        var flex = new FlexElement
        {
            Direction = FlexDirection.Row,
            Width = "100",
            Height = "100"
        };
        flex.AddChild(new TextElement { Content = "A", Width = "80", MinWidth = "60", Shrink = 1 });
        flex.AddChild(new TextElement { Content = "B", Width = "80", Shrink = 1 });

        var template = new Template
        {
            Canvas = new CanvasSettings { Width = 100 },
            Elements = new List<TemplateElement> { flex }
        };

        // Act
        var root = _engine.ComputeLayout(template);

        // Assert
        var flexNode = root.Children[0];
        Assert.Equal(0f, flexNode.Children[0].X, 0.1f);
        Assert.Equal(60f, flexNode.Children[0].Width, 0.1f);   // MinWidth holds even under shrink
        Assert.Equal(60f, flexNode.Children[1].X, 0.1f);
        Assert.Equal(40f, flexNode.Children[1].Width, 0.1f);
    }

    [Fact]
    public void ComputeLayout_MaxWidth_WithFlexGrow_RespectsMaxWidth()
    {
        // Arrange: Row W=300, child[0] W=50 G=1 MaxWidth=100, child[1] W=50 G=1
        // Bases: 100, freeSpace=200
        // Iter1: each hypothetical = 50+100=150, child[0] clamped at 100 -> freeze
        // Iter2: unfrozen freeSpace = 300-100-50 = 150, child[1] = 50+150 = 200
        var flex = new FlexElement
        {
            Direction = FlexDirection.Row,
            Width = "300",
            Height = "100"
        };
        flex.AddChild(new TextElement { Content = "A", Width = "50", Grow = 1, MaxWidth = "100" });
        flex.AddChild(new TextElement { Content = "B", Width = "50", Grow = 1 });

        var template = new Template
        {
            Canvas = new CanvasSettings { Width = 300 },
            Elements = new List<TemplateElement> { flex }
        };

        // Act
        var root = _engine.ComputeLayout(template);

        // Assert
        var flexNode = root.Children[0];
        Assert.Equal(0f, flexNode.Children[0].X, 0.1f);
        Assert.Equal(100f, flexNode.Children[0].Width, 0.1f);  // MaxWidth limits growth
        Assert.Equal(100f, flexNode.Children[1].X, 0.1f);
        Assert.Equal(200f, flexNode.Children[1].Width, 0.1f);
    }

    [Fact]
    public void ComputeLayout_MinMaxWidth_ClampsToRange()
    {
        // Arrange: Row W=300, child[0] B=0 G=1 MinWidth=80 MaxWidth=120, child[1] B=0 G=1
        // Iter1: each hypothetical = 150, child[0] clamped at 120 (max) -> freeze
        // Iter2: unfrozen freeSpace = 300-120 = 180, child[1] = 180
        var flex = new FlexElement
        {
            Direction = FlexDirection.Row,
            Width = "300",
            Height = "100"
        };
        flex.AddChild(new TextElement { Content = "A", Basis = "0", Grow = 1, MinWidth = "80", MaxWidth = "120" });
        flex.AddChild(new TextElement { Content = "B", Basis = "0", Grow = 1 });

        var template = new Template
        {
            Canvas = new CanvasSettings { Width = 300 },
            Elements = new List<TemplateElement> { flex }
        };

        // Act
        var root = _engine.ComputeLayout(template);

        // Assert: child[0] clamped to max within [80, 120] range
        var flexNode = root.Children[0];
        Assert.Equal(0f, flexNode.Children[0].X, 0.1f);
        Assert.Equal(120f, flexNode.Children[0].Width, 0.1f);
        Assert.Equal(120f, flexNode.Children[1].X, 0.1f);
        Assert.Equal(180f, flexNode.Children[1].Width, 0.1f);
    }

    [Fact]
    public void ComputeLayout_MinMaxHeight_ClampsToRange()
    {
        // Arrange: Column H=300, child[0] B=0 G=1 MinHeight=50 MaxHeight=80, child[1] B=0 G=1
        // Iter1: each hypothetical = 150, child[0] clamped at 80 -> freeze
        // Iter2: unfrozen freeSpace = 300-80 = 220, child[1] = 220
        var flex = new FlexElement
        {
            Direction = FlexDirection.Column,
            Height = "300"
        };
        flex.AddChild(new TextElement { Content = "A", Basis = "0", Grow = 1, MinHeight = "50", MaxHeight = "80" });
        flex.AddChild(new TextElement { Content = "B", Basis = "0", Grow = 1 });

        var template = new Template
        {
            Canvas = new CanvasSettings { Width = 300 },
            Elements = new List<TemplateElement> { flex }
        };

        // Act
        var root = _engine.ComputeLayout(template);

        // Assert: child[0] clamped to max within [50, 80] range
        var flexNode = root.Children[0];
        Assert.Equal(0f, flexNode.Children[0].Y, 0.1f);
        Assert.Equal(80f, flexNode.Children[0].Height, 0.1f);
        Assert.Equal(80f, flexNode.Children[1].Y, 0.1f);
        Assert.Equal(220f, flexNode.Children[1].Height, 0.1f);
    }

    [Fact]
    public void ComputeLayout_MinWidth_PercentValue_CalculatesFromParent()
    {
        // Arrange: Row W=200, child[0] B=0 G=1 MinWidth=25%, child[1] B=0 G=1
        // MinWidth = 25% of 200 = 50
        // Each hypothetical = 100 (not constrained by min=50), so equal distribution
        var flex = new FlexElement
        {
            Direction = FlexDirection.Row,
            Width = "200",
            Height = "100"
        };
        flex.AddChild(new TextElement { Content = "A", Basis = "0", Grow = 1, MinWidth = "25%" });
        flex.AddChild(new TextElement { Content = "B", Basis = "0", Grow = 1 });

        var template = new Template
        {
            Canvas = new CanvasSettings { Width = 200 },
            Elements = new List<TemplateElement> { flex }
        };

        // Act
        var root = _engine.ComputeLayout(template);

        // Assert: min doesn't constrain here (100 >= 50), equal distribution
        var flexNode = root.Children[0];
        Assert.Equal(0f, flexNode.Children[0].X, 0.1f);
        Assert.Equal(100f, flexNode.Children[0].Width, 0.1f);
        Assert.True(flexNode.Children[0].Width >= 50f);  // MinWidth=25%=50 respected
        Assert.Equal(100f, flexNode.Children[1].X, 0.1f);
        Assert.Equal(100f, flexNode.Children[1].Width, 0.1f);
    }

    [Fact]
    public void ComputeLayout_TwoPassResolution_MultipleConstrainedItems_CorrectDistribution()
    {
        // Arrange: Row W=300, 3 children B=0 G=1, child[0] MaxWidth=50, child[1] MaxWidth=80, child[2] unconstrained
        // Iter1: each hypothetical = 100
        //   child[0] clamped at 50 -> freeze
        //   child[1] clamped at 80 -> freeze
        //   child[2] = 100, not clamped
        // Iter2: unfrozen freeSpace = 300-50-80 = 170, child[2] = 170
        var flex = new FlexElement
        {
            Direction = FlexDirection.Row,
            Width = "300",
            Height = "100"
        };
        flex.AddChild(new TextElement { Content = "A", Basis = "0", Grow = 1, MaxWidth = "50" });
        flex.AddChild(new TextElement { Content = "B", Basis = "0", Grow = 1, MaxWidth = "80" });
        flex.AddChild(new TextElement { Content = "C", Basis = "0", Grow = 1 });

        var template = new Template
        {
            Canvas = new CanvasSettings { Width = 300 },
            Elements = new List<TemplateElement> { flex }
        };

        // Act
        var root = _engine.ComputeLayout(template);

        // Assert: iterative two-pass freeze resolves correctly
        var flexNode = root.Children[0];
        Assert.Equal(0f, flexNode.Children[0].X, 0.1f);
        Assert.Equal(50f, flexNode.Children[0].Width, 0.1f);

        Assert.Equal(50f, flexNode.Children[1].X, 0.1f);
        Assert.Equal(80f, flexNode.Children[1].Width, 0.1f);

        Assert.Equal(130f, flexNode.Children[2].X, 0.1f);
        Assert.Equal(170f, flexNode.Children[2].Width, 0.1f);
    }
}
