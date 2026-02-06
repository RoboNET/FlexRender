using FlexRender.Layout;
using FlexRender.Parsing.Ast;
using Xunit;

namespace FlexRender.Tests.Layout;

/// <summary>
/// Tests for flex-wrap: line breaking when children exceed container main-axis size.
/// Covers row wrap, column wrap, wrap-reverse, gap interaction, different child heights,
/// and edge cases (single child, empty children).
/// </summary>
public class LayoutEngineWrapTests
{
    private readonly LayoutEngine _engine = new();

    [Fact]
    public void ComputeLayout_RowWrap_ChildrenExceedWidth_WrapsToNewLine()
    {
        // Arrange: Row W=300, 5 children W=80 H=40, Wrap=Wrap
        // Line 1: items 0-2 (80+80+80=240 <= 300, 240+80=320 > 300)
        // Line 2: items 3-4 (80+80=160)
        // Container auto-height = 40 + 40 = 80
        var flex = new FlexElement
        {
            Direction = FlexDirection.Row,
            Width = "300",
            Wrap = FlexWrap.Wrap
        };
        for (var i = 0; i < 5; i++)
            flex.AddChild(new TextElement { Content = $"Item{i}", Width = "80", Height = "40" });

        var template = new Template
        {
            Canvas = new CanvasSettings { Width = 300 },
            Elements = new List<TemplateElement> { flex }
        };

        // Act
        var root = _engine.ComputeLayout(template);

        // Assert
        var flexNode = root.Children[0];

        // Line 1: Y=0
        Assert.Equal(0f, flexNode.Children[0].X, 0.1f);
        Assert.Equal(0f, flexNode.Children[0].Y, 0.1f);
        Assert.Equal(80f, flexNode.Children[1].X, 0.1f);
        Assert.Equal(0f, flexNode.Children[1].Y, 0.1f);
        Assert.Equal(160f, flexNode.Children[2].X, 0.1f);
        Assert.Equal(0f, flexNode.Children[2].Y, 0.1f);

        // Line 2: Y=40
        Assert.Equal(0f, flexNode.Children[3].X, 0.1f);
        Assert.Equal(40f, flexNode.Children[3].Y, 0.1f);
        Assert.Equal(80f, flexNode.Children[4].X, 0.1f);
        Assert.Equal(40f, flexNode.Children[4].Y, 0.1f);

        // Container height = 80
        Assert.Equal(80f, flexNode.Height, 0.1f);
    }

    [Fact]
    public void ComputeLayout_RowWrap_AllChildrenFit_SingleLine()
    {
        // Arrange: Row W=300 H=100, 3 children W=80 H=40 (80+80+80=240 <= 300)
        var flex = new FlexElement
        {
            Direction = FlexDirection.Row,
            Width = "300",
            Height = "100",
            Wrap = FlexWrap.Wrap
        };
        for (var i = 0; i < 3; i++)
            flex.AddChild(new TextElement { Content = $"Item{i}", Width = "80", Height = "40" });

        var template = new Template
        {
            Canvas = new CanvasSettings { Width = 300 },
            Elements = new List<TemplateElement> { flex }
        };

        // Act
        var root = _engine.ComputeLayout(template);

        // Assert: all on line 1, Y=0
        var flexNode = root.Children[0];
        Assert.Equal(0f, flexNode.Children[0].X, 0.1f);
        Assert.Equal(0f, flexNode.Children[0].Y, 0.1f);
        Assert.Equal(80f, flexNode.Children[1].X, 0.1f);
        Assert.Equal(0f, flexNode.Children[1].Y, 0.1f);
        Assert.Equal(160f, flexNode.Children[2].X, 0.1f);
        Assert.Equal(0f, flexNode.Children[2].Y, 0.1f);
    }

    [Fact]
    public void ComputeLayout_RowWrap_ThreeLines_CalculatesHeightCorrectly()
    {
        // Arrange: Row W=200, 6 children W=80 H=30, Wrap=Wrap
        // Line 1: items 0-1 (80+80=160 <= 200, 160+80=240 > 200)
        // Line 2: items 2-3
        // Line 3: items 4-5
        // Container auto-height = 30 + 30 + 30 = 90
        var flex = new FlexElement
        {
            Direction = FlexDirection.Row,
            Width = "200",
            Wrap = FlexWrap.Wrap
        };
        for (var i = 0; i < 6; i++)
            flex.AddChild(new TextElement { Content = $"Item{i}", Width = "80", Height = "30" });

        var template = new Template
        {
            Canvas = new CanvasSettings { Width = 200 },
            Elements = new List<TemplateElement> { flex }
        };

        // Act
        var root = _engine.ComputeLayout(template);

        // Assert
        var flexNode = root.Children[0];

        // Line 1: Y=0
        Assert.Equal(0f, flexNode.Children[0].Y, 0.1f);
        Assert.Equal(0f, flexNode.Children[1].Y, 0.1f);
        Assert.Equal(80f, flexNode.Children[1].X, 0.1f);

        // Line 2: Y=30
        Assert.Equal(30f, flexNode.Children[2].Y, 0.1f);
        Assert.Equal(0f, flexNode.Children[2].X, 0.1f);
        Assert.Equal(30f, flexNode.Children[3].Y, 0.1f);

        // Line 3: Y=60
        Assert.Equal(60f, flexNode.Children[4].Y, 0.1f);
        Assert.Equal(60f, flexNode.Children[5].Y, 0.1f);

        // Container height = 90
        Assert.Equal(90f, flexNode.Height, 0.1f);
    }

    [Fact]
    public void ComputeLayout_RowWrap_WithGap_GapAppliedBetweenLinesAndItems()
    {
        // Arrange: Row W=200, Gap="10", 4 children W=80 H=40, Wrap=Wrap
        // mainGap=10, crossGap=10
        // Line 1: 80+10+80=170 <= 200, 170+10+80=260 > 200 -> items 0-1
        // Line 2: items 2-3
        // Container auto-height = 40 + 10 (crossGap) + 40 = 90
        var flex = new FlexElement
        {
            Direction = FlexDirection.Row,
            Width = "200",
            Wrap = FlexWrap.Wrap,
            Gap = "10"
        };
        for (var i = 0; i < 4; i++)
            flex.AddChild(new TextElement { Content = $"Item{i}", Width = "80", Height = "40" });

        var template = new Template
        {
            Canvas = new CanvasSettings { Width = 200 },
            Elements = new List<TemplateElement> { flex }
        };

        // Act
        var root = _engine.ComputeLayout(template);

        // Assert
        var flexNode = root.Children[0];

        // Line 1: child[0] X=0, child[1] X=90 (80+10)
        Assert.Equal(0f, flexNode.Children[0].X, 0.1f);
        Assert.Equal(0f, flexNode.Children[0].Y, 0.1f);
        Assert.Equal(90f, flexNode.Children[1].X, 0.1f);
        Assert.Equal(0f, flexNode.Children[1].Y, 0.1f);

        // Line 2: Y=50 (40+10 crossGap), child[2] X=0, child[3] X=90
        Assert.Equal(0f, flexNode.Children[2].X, 0.1f);
        Assert.Equal(50f, flexNode.Children[2].Y, 0.1f);
        Assert.Equal(90f, flexNode.Children[3].X, 0.1f);
        Assert.Equal(50f, flexNode.Children[3].Y, 0.1f);

        // Container height = 90
        Assert.Equal(90f, flexNode.Height, 0.1f);
    }

    [Fact]
    public void ComputeLayout_RowWrapReverse_WrapsInReverse()
    {
        // Arrange: Row W=200 H=120, 4 children W=80 H=40, Wrap=WrapReverse
        // Line 1: items 0-1, Line 2: items 2-3
        // WrapReverse: newY = containerH - oldY - childH
        // child[0]: 120 - 0 - 40 = 80
        // child[2]: 120 - 40 - 40 = 40
        var flex = new FlexElement
        {
            Direction = FlexDirection.Row,
            Width = "200",
            Height = "120",
            Wrap = FlexWrap.WrapReverse
        };
        for (var i = 0; i < 4; i++)
            flex.AddChild(new TextElement { Content = $"Item{i}", Width = "80", Height = "40" });

        var template = new Template
        {
            Canvas = new CanvasSettings { Width = 200 },
            Elements = new List<TemplateElement> { flex }
        };

        // Act
        var root = _engine.ComputeLayout(template);

        // Assert: lines flipped on cross axis
        var flexNode = root.Children[0];

        // Line 1 (originally Y=0) -> Y=80
        Assert.Equal(0f, flexNode.Children[0].X, 0.1f);
        Assert.Equal(80f, flexNode.Children[0].Y, 0.1f);
        Assert.Equal(80f, flexNode.Children[1].X, 0.1f);
        Assert.Equal(80f, flexNode.Children[1].Y, 0.1f);

        // Line 2 (originally Y=40) -> Y=40
        Assert.Equal(0f, flexNode.Children[2].X, 0.1f);
        Assert.Equal(40f, flexNode.Children[2].Y, 0.1f);
        Assert.Equal(80f, flexNode.Children[3].X, 0.1f);
        Assert.Equal(40f, flexNode.Children[3].Y, 0.1f);
    }

    [Fact]
    public void ComputeLayout_ColumnWrap_ChildrenExceedHeight_WrapsToNewColumn()
    {
        // Arrange: Column H=200, 4 children W=60 H=80, Wrap=Wrap
        // Line 1: items 0-1 (80+80=160 <= 200, 160+80=240 > 200)
        // Line 2: items 2-3
        // Container auto-width = 60 + 60 = 120
        var flex = new FlexElement
        {
            Direction = FlexDirection.Column,
            Height = "200",
            Wrap = FlexWrap.Wrap
        };
        for (var i = 0; i < 4; i++)
            flex.AddChild(new TextElement { Content = $"Item{i}", Width = "60", Height = "80" });

        var template = new Template
        {
            Canvas = new CanvasSettings { Width = 300 },
            Elements = new List<TemplateElement> { flex }
        };

        // Act
        var root = _engine.ComputeLayout(template);

        // Assert
        var flexNode = root.Children[0];

        // Line 1: X=0
        Assert.Equal(0f, flexNode.Children[0].X, 0.1f);
        Assert.Equal(0f, flexNode.Children[0].Y, 0.1f);
        Assert.Equal(0f, flexNode.Children[1].X, 0.1f);
        Assert.Equal(80f, flexNode.Children[1].Y, 0.1f);

        // Line 2: X=60
        Assert.Equal(60f, flexNode.Children[2].X, 0.1f);
        Assert.Equal(0f, flexNode.Children[2].Y, 0.1f);
        Assert.Equal(60f, flexNode.Children[3].X, 0.1f);
        Assert.Equal(80f, flexNode.Children[3].Y, 0.1f);

        // Container width = 120
        Assert.Equal(120f, flexNode.Width, 0.1f);
    }

    [Fact]
    public void ComputeLayout_ColumnWrapReverse_WrapsInReverse()
    {
        // Arrange: Column W=200 H=200, 4 children W=60 H=80, Wrap=WrapReverse
        // Line 1: items 0-1 at X=0, Line 2: items 2-3 at X=60
        // WrapReverse: newX = containerW - oldX - childW
        // child[0]: 200 - 0 - 60 = 140
        // child[2]: 200 - 60 - 60 = 80
        var flex = new FlexElement
        {
            Direction = FlexDirection.Column,
            Width = "200",
            Height = "200",
            Wrap = FlexWrap.WrapReverse
        };
        for (var i = 0; i < 4; i++)
            flex.AddChild(new TextElement { Content = $"Item{i}", Width = "60", Height = "80" });

        var template = new Template
        {
            Canvas = new CanvasSettings { Width = 200 },
            Elements = new List<TemplateElement> { flex }
        };

        // Act
        var root = _engine.ComputeLayout(template);

        // Assert: lines flipped on cross axis (X)
        var flexNode = root.Children[0];

        // Line 1 (originally X=0) -> X=140
        Assert.Equal(140f, flexNode.Children[0].X, 0.1f);
        Assert.Equal(0f, flexNode.Children[0].Y, 0.1f);
        Assert.Equal(140f, flexNode.Children[1].X, 0.1f);
        Assert.Equal(80f, flexNode.Children[1].Y, 0.1f);

        // Line 2 (originally X=60) -> X=80
        Assert.Equal(80f, flexNode.Children[2].X, 0.1f);
        Assert.Equal(0f, flexNode.Children[2].Y, 0.1f);
        Assert.Equal(80f, flexNode.Children[3].X, 0.1f);
        Assert.Equal(80f, flexNode.Children[3].Y, 0.1f);
    }

    [Fact]
    public void ComputeLayout_RowWrap_DifferentHeightChildren_LineHeightIsMaxChild()
    {
        // Arrange: Row W=200, children with different heights, Wrap=Wrap
        // Line 1: items 0-1 (80+80=160 <= 200), crossSize=Max(30,60)=60
        // Line 2: items 2-3 (80+80=160 <= 200), crossSize=Max(20,50)=50
        // Container auto-height = 60 + 50 = 110
        var flex = new FlexElement
        {
            Direction = FlexDirection.Row,
            Width = "200",
            Wrap = FlexWrap.Wrap
        };
        flex.AddChild(new TextElement { Content = "A", Width = "80", Height = "30" });
        flex.AddChild(new TextElement { Content = "B", Width = "80", Height = "60" });
        flex.AddChild(new TextElement { Content = "C", Width = "80", Height = "20" });
        flex.AddChild(new TextElement { Content = "D", Width = "80", Height = "50" });

        var template = new Template
        {
            Canvas = new CanvasSettings { Width = 200 },
            Elements = new List<TemplateElement> { flex }
        };

        // Act
        var root = _engine.ComputeLayout(template);

        // Assert
        var flexNode = root.Children[0];

        // Line 1: Y=0, line height = 60
        Assert.Equal(0f, flexNode.Children[0].Y, 0.1f);
        Assert.Equal(30f, flexNode.Children[0].Height, 0.1f);
        Assert.Equal(0f, flexNode.Children[1].Y, 0.1f);
        Assert.Equal(60f, flexNode.Children[1].Height, 0.1f);

        // Line 2: Y=60 (after line 1 with height 60), line height = 50
        Assert.Equal(60f, flexNode.Children[2].Y, 0.1f);
        Assert.Equal(20f, flexNode.Children[2].Height, 0.1f);
        Assert.Equal(60f, flexNode.Children[3].Y, 0.1f);
        Assert.Equal(50f, flexNode.Children[3].Height, 0.1f);

        // Container height = 110
        Assert.Equal(110f, flexNode.Height, 0.1f);
    }

    [Fact]
    public void ComputeLayout_RowWrap_SingleChild_NoWrap()
    {
        // Arrange: Row W=200 H=100, 1 child W=80 H=40, Wrap=Wrap -> single line
        var flex = new FlexElement
        {
            Direction = FlexDirection.Row,
            Width = "200",
            Height = "100",
            Wrap = FlexWrap.Wrap
        };
        flex.AddChild(new TextElement { Content = "Only", Width = "80", Height = "40" });

        var template = new Template
        {
            Canvas = new CanvasSettings { Width = 200 },
            Elements = new List<TemplateElement> { flex }
        };

        // Act
        var root = _engine.ComputeLayout(template);

        // Assert: single child on line 1
        var flexNode = root.Children[0];
        Assert.Equal(0f, flexNode.Children[0].X, 0.1f);
        Assert.Equal(0f, flexNode.Children[0].Y, 0.1f);
        Assert.Equal(80f, flexNode.Children[0].Width, 0.1f);
        Assert.Equal(40f, flexNode.Children[0].Height, 0.1f);
    }

    [Fact]
    public void ComputeLayout_RowWrap_EmptyChildren_NoWrap()
    {
        // Arrange: Row W=200 H=100, no children, Wrap=Wrap
        var flex = new FlexElement
        {
            Direction = FlexDirection.Row,
            Width = "200",
            Height = "100",
            Wrap = FlexWrap.Wrap
        };

        var template = new Template
        {
            Canvas = new CanvasSettings { Width = 200 },
            Elements = new List<TemplateElement> { flex }
        };

        // Act
        var root = _engine.ComputeLayout(template);

        // Assert: container exists with correct dimensions, no children
        var flexNode = root.Children[0];
        Assert.Equal(200f, flexNode.Width, 0.1f);
        Assert.Equal(100f, flexNode.Height, 0.1f);
        Assert.Empty(flexNode.Children);
    }
}
