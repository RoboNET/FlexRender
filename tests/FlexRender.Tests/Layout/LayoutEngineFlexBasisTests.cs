using FlexRender.Layout;
using FlexRender.Parsing.Ast;
using Xunit;

namespace FlexRender.Tests.Layout;

/// <summary>
/// Tests for flex-basis property: initial main-axis size before grow/shrink distribution.
/// Covers px, %, auto values, interaction with grow/shrink, and edge cases
/// (basis overrides dimension, fractional grow, shrink=0).
/// </summary>
public class LayoutEngineFlexBasisTests
{
    private readonly LayoutEngine _engine = new();

    [Fact]
    public void ComputeLayout_Column_FlexBasisPixels_SetsInitialSize()
    {
        // Arrange: Column container H=200, child with Basis="50", no grow/shrink
        var flex = new FlexElement
        {
            Direction = FlexDirection.Column,
            Height = "200"
        };
        flex.AddChild(new TextElement { Content = "test", Basis = "50", Grow = 0, Shrink = 0 });

        var template = new Template
        {
            Canvas = new CanvasSettings { Width = 300 },
            Elements = new List<TemplateElement> { flex }
        };

        // Act
        var root = _engine.ComputeLayout(template);

        // Assert: child height = basis = 50, Y = 0, Width = 300 (stretched)
        var flexNode = root.Children[0];
        var child = flexNode.Children[0];
        Assert.Equal(50f, child.Height, 0.1f);
        Assert.Equal(0f, child.Y, 0.1f);
        Assert.Equal(300f, child.Width, 0.1f);
    }

    [Fact]
    public void ComputeLayout_Column_FlexBasisPercent_CalculatesFromParent()
    {
        // Arrange: Column container H=200, child with Basis="25%" -> 25% of 200 = 50
        var flex = new FlexElement
        {
            Direction = FlexDirection.Column,
            Height = "200"
        };
        flex.AddChild(new TextElement { Content = "test", Basis = "25%", Grow = 0, Shrink = 0 });

        var template = new Template
        {
            Canvas = new CanvasSettings { Width = 300 },
            Elements = new List<TemplateElement> { flex }
        };

        // Act
        var root = _engine.ComputeLayout(template);

        // Assert: child height = 25% of 200 = 50
        var flexNode = root.Children[0];
        var child = flexNode.Children[0];
        Assert.Equal(50f, child.Height, 0.1f);
        Assert.Equal(0f, child.Y, 0.1f);
    }

    [Fact]
    public void ComputeLayout_Column_FlexBasisAuto_UsesContentSize()
    {
        // Arrange: Column container H=200, child with Basis="auto" and Height="40"
        // Basis="auto" falls back to main-axis dimension (Height=40)
        var flex = new FlexElement
        {
            Direction = FlexDirection.Column,
            Height = "200"
        };
        flex.AddChild(new TextElement { Content = "test", Height = "40", Basis = "auto" });

        var template = new Template
        {
            Canvas = new CanvasSettings { Width = 300 },
            Elements = new List<TemplateElement> { flex }
        };

        // Act
        var root = _engine.ComputeLayout(template);

        // Assert: child height = 40 (from Height, since basis="auto")
        var flexNode = root.Children[0];
        var child = flexNode.Children[0];
        Assert.Equal(40f, child.Height, 0.1f);
        Assert.Equal(0f, child.Y, 0.1f);
    }

    [Fact]
    public void ComputeLayout_Column_FlexBasisWithGrow_GrowsFromBasis()
    {
        // Arrange: Column H=200, 2 children each Basis="50" Grow=1
        // Total bases = 100, freeSpace = 100, each grows by 50 -> each H=100
        var flex = new FlexElement
        {
            Direction = FlexDirection.Column,
            Height = "200"
        };
        flex.AddChild(new TextElement { Content = "A", Basis = "50", Grow = 1 });
        flex.AddChild(new TextElement { Content = "B", Basis = "50", Grow = 1 });

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
        Assert.Equal(100f, flexNode.Children[0].Height, 0.1f);
        Assert.Equal(100f, flexNode.Children[1].Y, 0.1f);
        Assert.Equal(100f, flexNode.Children[1].Height, 0.1f);
    }

    [Fact]
    public void ComputeLayout_Column_FlexBasisWithShrink_ShrinksFromBasis()
    {
        // Arrange: Column H=150, 2 children each Basis="100" Shrink=1
        // Total bases = 200, overflow = 50
        // CSS-spec shrink (scaled by basis): scaledFactor = 1*100 = 100 each, total = 200
        // Each shrinks by: 50 * 100/200 = 25 -> each H = 100 - 25 = 75
        var flex = new FlexElement
        {
            Direction = FlexDirection.Column,
            Height = "150"
        };
        flex.AddChild(new TextElement { Content = "A", Basis = "100", Shrink = 1 });
        flex.AddChild(new TextElement { Content = "B", Basis = "100", Shrink = 1 });

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
        Assert.Equal(75f, flexNode.Children[0].Height, 0.1f);
        Assert.Equal(75f, flexNode.Children[1].Y, 0.1f);
        Assert.Equal(75f, flexNode.Children[1].Height, 0.1f);
    }

    [Fact]
    public void ComputeLayout_Row_FlexBasisPixels_SetsInitialWidth()
    {
        // Arrange: Row W=300 H=100, child Basis="80", no grow/shrink
        var flex = new FlexElement
        {
            Direction = FlexDirection.Row,
            Width = "300",
            Height = "100"
        };
        flex.AddChild(new TextElement { Content = "test", Basis = "80", Grow = 0, Shrink = 0 });

        var template = new Template
        {
            Canvas = new CanvasSettings { Width = 300 },
            Elements = new List<TemplateElement> { flex }
        };

        // Act
        var root = _engine.ComputeLayout(template);

        // Assert: child width = basis = 80, stretched height = 100
        var flexNode = root.Children[0];
        var child = flexNode.Children[0];
        Assert.Equal(80f, child.Width, 0.1f);
        Assert.Equal(0f, child.X, 0.1f);
        Assert.Equal(100f, child.Height, 0.1f);
    }

    [Fact]
    public void ComputeLayout_Row_FlexBasis0_WithGrow_EqualDistribution()
    {
        // Arrange: Row W=300 H=100, 3 children each Basis="0" Grow=1
        // Total bases = 0, freeSpace = 300, each gets 300/3 = 100
        var flex = new FlexElement
        {
            Direction = FlexDirection.Row,
            Width = "300",
            Height = "100"
        };
        flex.AddChild(new TextElement { Content = "A", Basis = "0", Grow = 1 });
        flex.AddChild(new TextElement { Content = "B", Basis = "0", Grow = 1 });
        flex.AddChild(new TextElement { Content = "C", Basis = "0", Grow = 1 });

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
        Assert.Equal(100f, flexNode.Children[0].Width, 0.1f);
        Assert.Equal(100f, flexNode.Children[0].Height, 0.1f);

        Assert.Equal(100f, flexNode.Children[1].X, 0.1f);
        Assert.Equal(100f, flexNode.Children[1].Width, 0.1f);

        Assert.Equal(200f, flexNode.Children[2].X, 0.1f);
        Assert.Equal(100f, flexNode.Children[2].Width, 0.1f);
    }

    [Fact]
    public void ComputeLayout_FlexBasis_OverridesMainAxisDimension()
    {
        // Arrange: Column H=200, child[0] H="20" Basis="50" Grow=1, child[1] H="10" Grow=1, child[2] H="10" Grow=1
        // flex-basis takes priority over explicit Height (Yoga: computeFlexBasisForChild priority 1 > 3)
        // child[0] basis=50, child[1] basis=10 (from Height), child[2] basis=10
        // totalBases = 70, freeSpace = 130, totalGrow = 3, growPer = 130/3 = 43.33
        // child[0]: 50 + 43.33 = 93.33, child[1]: 10 + 43.33 = 53.33, child[2]: 10 + 43.33 = 53.33
        var flex = new FlexElement
        {
            Direction = FlexDirection.Column,
            Height = "200"
        };
        flex.AddChild(new TextElement { Content = "A", Height = "20", Basis = "50", Grow = 1 });
        flex.AddChild(new TextElement { Content = "B", Height = "10", Grow = 1 });
        flex.AddChild(new TextElement { Content = "C", Height = "10", Grow = 1 });

        var template = new Template
        {
            Canvas = new CanvasSettings { Width = 300 },
            Elements = new List<TemplateElement> { flex }
        };

        // Act
        var root = _engine.ComputeLayout(template);

        // Assert: child[0] gets more space because basis=50 > height fallback
        var flexNode = root.Children[0];
        Assert.Equal(0f, flexNode.Children[0].Y, 0.1f);
        Assert.Equal(93.33f, flexNode.Children[0].Height, 1f);

        Assert.Equal(93.33f, flexNode.Children[1].Y, 1f);
        Assert.Equal(53.33f, flexNode.Children[1].Height, 1f);

        Assert.Equal(146.67f, flexNode.Children[2].Y, 1f);
        Assert.Equal(53.33f, flexNode.Children[2].Height, 1f);

        // Key assertion: child[0] height > child[1] height (basis=50 vs basis=10)
        Assert.True(flexNode.Children[0].Height > flexNode.Children[1].Height);
    }

    [Fact]
    public void ComputeLayout_FractionalGrow_TotalLessThanOne_DistributesPartialSpace()
    {
        // Arrange: Column H=500, children grow=0.2/0.2/0.4 basis=40/0/0, all shrink=0
        // totalGrow = 0.2+0.2+0.4 = 0.8, floored to 1 (Yoga factor flooring)
        // freeSpace = 500 - 40 = 460
        // child[0]: 40 + 460*0.2/1 = 40 + 92 = 132
        // child[1]: 0 + 460*0.2/1 = 92
        // child[2]: 0 + 460*0.4/1 = 184
        // Total = 408 (underfilled, correct CSS behavior for totalGrow < 1)
        var flex = new FlexElement
        {
            Direction = FlexDirection.Column,
            Height = "500"
        };
        flex.AddChild(new TextElement { Content = "A", Basis = "40", Grow = 0.2f, Shrink = 0 });
        flex.AddChild(new TextElement { Content = "B", Basis = "0", Grow = 0.2f, Shrink = 0 });
        flex.AddChild(new TextElement { Content = "C", Basis = "0", Grow = 0.4f, Shrink = 0 });

        var template = new Template
        {
            Canvas = new CanvasSettings { Width = 300 },
            Elements = new List<TemplateElement> { flex }
        };

        // Act
        var root = _engine.ComputeLayout(template);

        // Assert
        var flexNode = root.Children[0];
        Assert.Equal(132f, flexNode.Children[0].Height, 0.1f);
        Assert.Equal(92f, flexNode.Children[1].Height, 0.1f);
        Assert.Equal(184f, flexNode.Children[2].Height, 0.1f);

        // Total children height < container height (undistributed space remains)
        var totalChildHeight = flexNode.Children[0].Height + flexNode.Children[1].Height + flexNode.Children[2].Height;
        Assert.True(totalChildHeight < 500f);

        // Grow ratio: child[2] = 2 * child[1] (0.4:0.2 = 2:1)
        Assert.Equal(flexNode.Children[2].Height, 2 * flexNode.Children[1].Height, 0.1f);
    }

    [Fact]
    public void ComputeLayout_FlexShrinkZero_ItemDoesNotShrink()
    {
        // Arrange: Column H=125, 3 children H=50 each, shrink=0/1/0
        // Only child[1] shrinks. Overflow = 150-125 = 25.
        // scaledShrink[1] = 1*50 = 50, total=50
        // child[1]: 50 + (-25)*50/50 = 50 - 25 = 25
        var flex = new FlexElement
        {
            Direction = FlexDirection.Column,
            Height = "125"
        };
        flex.AddChild(new TextElement { Content = "A", Width = "50", Height = "50", Shrink = 0 });
        flex.AddChild(new TextElement { Content = "B", Width = "50", Height = "50", Shrink = 1 });
        flex.AddChild(new TextElement { Content = "C", Width = "50", Height = "50", Shrink = 0 });

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
        Assert.Equal(50f, flexNode.Children[0].Height, 0.1f);  // shrink=0, unchanged

        Assert.Equal(50f, flexNode.Children[1].Y, 0.1f);
        Assert.Equal(25f, flexNode.Children[1].Height, 0.1f);  // only shrinkable item

        Assert.Equal(75f, flexNode.Children[2].Y, 0.1f);
        Assert.Equal(50f, flexNode.Children[2].Height, 0.1f);  // shrink=0, unchanged
    }
}
