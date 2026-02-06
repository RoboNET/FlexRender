using FlexRender.Layout;
using FlexRender.Parsing.Ast;
using Xunit;

namespace FlexRender.Tests.Layout;

/// <summary>
/// Tests for align-content: distribution of wrapped lines along the cross axis.
/// Common setup: Row W=140 H=120 Wrap=Wrap, 5 children W=50 H=10 -> 3 lines [0-1][2-3][4].
/// crossFreeSpace = 120 - 30 = 90.
/// Also includes edge cases: margin-induced wrap, overflow fallback, WrapReverse with padding.
/// </summary>
public class LayoutEngineAlignContentTests
{
    private readonly LayoutEngine _engine = new();

    /// <summary>
    /// Creates the common test template: Row W=140 H=120 Wrap=Wrap, 5 children W=50 H=10.
    /// Lines: [0,1], [2,3], [4]. Each line crossAxisSize=10. crossFreeSpace=90.
    /// </summary>
    private static Template CreateAlignContentTemplate(AlignContent alignContent)
    {
        var flex = new FlexElement
        {
            Direction = FlexDirection.Row,
            Width = "140",
            Height = "120",
            Wrap = FlexWrap.Wrap,
            AlignContent = alignContent
        };
        for (var i = 0; i < 5; i++)
            flex.AddChild(new TextElement { Content = $"Item{i}", Width = "50", Height = "10" });

        return new Template
        {
            Canvas = new CanvasSettings { Width = 140 },
            Elements = new List<TemplateElement> { flex }
        };
    }

    [Fact]
    public void ComputeLayout_RowWrap_AlignContentStart_LinesPackedAtStart()
    {
        // AlignContent=Start: leadingCrossDim=0, betweenCrossDim=0
        // Line Y: 0, 10, 20
        var template = CreateAlignContentTemplate(AlignContent.Start);

        var root = _engine.ComputeLayout(template);
        var flexNode = root.Children[0];

        // Line 1: Y=0
        Assert.Equal(0f, flexNode.Children[0].Y, 0.1f);
        Assert.Equal(0f, flexNode.Children[1].Y, 0.1f);
        // Line 2: Y=10
        Assert.Equal(10f, flexNode.Children[2].Y, 0.1f);
        Assert.Equal(10f, flexNode.Children[3].Y, 0.1f);
        // Line 3: Y=20
        Assert.Equal(20f, flexNode.Children[4].Y, 0.1f);

        // X positions (justify=start)
        Assert.Equal(0f, flexNode.Children[0].X, 0.1f);
        Assert.Equal(50f, flexNode.Children[1].X, 0.1f);
        Assert.Equal(0f, flexNode.Children[2].X, 0.1f);
        Assert.Equal(50f, flexNode.Children[3].X, 0.1f);
        Assert.Equal(0f, flexNode.Children[4].X, 0.1f);
    }

    [Fact]
    public void ComputeLayout_RowWrap_AlignContentCenter_LinesCentered()
    {
        // AlignContent=Center: leadingCrossDim = 90/2 = 45
        // Line Y: 45, 55, 65
        var template = CreateAlignContentTemplate(AlignContent.Center);

        var root = _engine.ComputeLayout(template);
        var flexNode = root.Children[0];

        Assert.Equal(45f, flexNode.Children[0].Y, 0.1f);
        Assert.Equal(45f, flexNode.Children[1].Y, 0.1f);
        Assert.Equal(55f, flexNode.Children[2].Y, 0.1f);
        Assert.Equal(55f, flexNode.Children[3].Y, 0.1f);
        Assert.Equal(65f, flexNode.Children[4].Y, 0.1f);
    }

    [Fact]
    public void ComputeLayout_RowWrap_AlignContentEnd_LinesPackedAtEnd()
    {
        // AlignContent=End: leadingCrossDim = 90
        // Line Y: 90, 100, 110
        var template = CreateAlignContentTemplate(AlignContent.End);

        var root = _engine.ComputeLayout(template);
        var flexNode = root.Children[0];

        Assert.Equal(90f, flexNode.Children[0].Y, 0.1f);
        Assert.Equal(90f, flexNode.Children[1].Y, 0.1f);
        Assert.Equal(100f, flexNode.Children[2].Y, 0.1f);
        Assert.Equal(100f, flexNode.Children[3].Y, 0.1f);
        Assert.Equal(110f, flexNode.Children[4].Y, 0.1f);
    }

    [Fact]
    public void ComputeLayout_RowWrap_AlignContentStretch_LinesStretchToFill()
    {
        // AlignContent=Stretch: extraPerLine = 90/3 = 30, new line height = 10+30 = 40
        // Line Y: 0, 40, 80
        var template = CreateAlignContentTemplate(AlignContent.Stretch);

        var root = _engine.ComputeLayout(template);
        var flexNode = root.Children[0];

        Assert.Equal(0f, flexNode.Children[0].Y, 0.1f);
        Assert.Equal(0f, flexNode.Children[1].Y, 0.1f);
        Assert.Equal(40f, flexNode.Children[2].Y, 0.1f);
        Assert.Equal(40f, flexNode.Children[3].Y, 0.1f);
        Assert.Equal(80f, flexNode.Children[4].Y, 0.1f);
    }

    [Fact]
    public void ComputeLayout_RowWrap_AlignContentSpaceBetween_SpaceDistributed()
    {
        // AlignContent=SpaceBetween: betweenCrossDim = 90/(3-1) = 45
        // Line Y: 0, 0+10+45=55, 55+10+45=110
        var template = CreateAlignContentTemplate(AlignContent.SpaceBetween);

        var root = _engine.ComputeLayout(template);
        var flexNode = root.Children[0];

        Assert.Equal(0f, flexNode.Children[0].Y, 0.1f);
        Assert.Equal(0f, flexNode.Children[1].Y, 0.1f);
        Assert.Equal(55f, flexNode.Children[2].Y, 0.1f);
        Assert.Equal(55f, flexNode.Children[3].Y, 0.1f);
        Assert.Equal(110f, flexNode.Children[4].Y, 0.1f);
    }

    [Fact]
    public void ComputeLayout_RowWrap_AlignContentSpaceAround_SpaceDistributed()
    {
        // AlignContent=SpaceAround: leadingCrossDim = 90/(2*3) = 15, leadPerLine = 90/3 = 30
        // Line Y: 15, 15+10+30=55, 55+10+30=95
        var template = CreateAlignContentTemplate(AlignContent.SpaceAround);

        var root = _engine.ComputeLayout(template);
        var flexNode = root.Children[0];

        Assert.Equal(15f, flexNode.Children[0].Y, 0.1f);
        Assert.Equal(15f, flexNode.Children[1].Y, 0.1f);
        Assert.Equal(55f, flexNode.Children[2].Y, 0.1f);
        Assert.Equal(55f, flexNode.Children[3].Y, 0.1f);
        Assert.Equal(95f, flexNode.Children[4].Y, 0.1f);
    }

    [Fact]
    public void ComputeLayout_RowWrap_AlignContentSpaceEvenly_EvenDistribution()
    {
        // AlignContent=SpaceEvenly: slot = 90/(3+1) = 22.5
        // Line Y: 22.5, 22.5+10+22.5=55, 55+10+22.5=87.5
        var template = CreateAlignContentTemplate(AlignContent.SpaceEvenly);

        var root = _engine.ComputeLayout(template);
        var flexNode = root.Children[0];

        Assert.Equal(22.5f, flexNode.Children[0].Y, 0.5f);
        Assert.Equal(22.5f, flexNode.Children[1].Y, 0.5f);
        Assert.Equal(55f, flexNode.Children[2].Y, 0.5f);
        Assert.Equal(55f, flexNode.Children[3].Y, 0.5f);
        Assert.Equal(87.5f, flexNode.Children[4].Y, 0.5f);
    }

    [Fact]
    public void ComputeLayout_RowWrap_ChildMarginCausesWrap()
    {
        // Arrange: Row W=85, child[0] W=40 H=40, child[1] W=40 H=40 Margin="5"
        // child[1] consumes 40+5+5=50 on main axis. Total: 40+50=90 > 85 -> wraps
        // Without margin: 40+40=80 <= 85, both fit on one line (incorrect)
        var flex = new FlexElement
        {
            Direction = FlexDirection.Row,
            Width = "85",
            Wrap = FlexWrap.Wrap
        };
        flex.AddChild(new TextElement { Content = "A", Width = "40", Height = "40" });
        flex.AddChild(new TextElement { Content = "B", Width = "40", Height = "40", Margin = "5" });

        var template = new Template
        {
            Canvas = new CanvasSettings { Width = 85 },
            Elements = new List<TemplateElement> { flex }
        };

        // Act
        var root = _engine.ComputeLayout(template);

        // Assert: child[1] wraps to new line
        var flexNode = root.Children[0];

        // Line 1: child[0] at (0, 0)
        Assert.Equal(0f, flexNode.Children[0].X, 0.1f);
        Assert.Equal(0f, flexNode.Children[0].Y, 0.1f);

        // Line 2: child[1] at (marginLeft=5, line1.crossSize + marginTop=5)
        Assert.Equal(5f, flexNode.Children[1].X, 0.1f);
        Assert.Equal(45f, flexNode.Children[1].Y, 0.1f);
    }

    [Fact]
    public void ComputeLayout_RowWrap_AlignContentOverflowFallback_SpaceBetweenToStart()
    {
        // Arrange: Row W=100 H=40, 2 children W=60 H=30, Wrap=Wrap, AlignContent=SpaceBetween
        // Line 1: child[0]=60 > 100-60=40 (child[1] wraps)
        // totalLinesSize = 30+30 = 60, availableCross = 40, crossFreeSpace = -20
        // SpaceBetween -> fallback to Start (negative free space)
        var flex = new FlexElement
        {
            Direction = FlexDirection.Row,
            Width = "100",
            Height = "40",
            Wrap = FlexWrap.Wrap,
            AlignContent = AlignContent.SpaceBetween
        };
        flex.AddChild(new TextElement { Content = "A", Width = "60", Height = "30" });
        flex.AddChild(new TextElement { Content = "B", Width = "60", Height = "30" });

        var template = new Template
        {
            Canvas = new CanvasSettings { Width = 100 },
            Elements = new List<TemplateElement> { flex }
        };

        // Act
        var root = _engine.ComputeLayout(template);

        // Assert: fallback to Start, lines packed at Y=0
        var flexNode = root.Children[0];
        Assert.Equal(0f, flexNode.Children[0].X, 0.1f);
        Assert.Equal(0f, flexNode.Children[0].Y, 0.1f);
        Assert.Equal(0f, flexNode.Children[1].X, 0.1f);
        Assert.Equal(30f, flexNode.Children[1].Y, 0.1f);
    }

    [Fact]
    public void ComputeLayout_RowWrapReverse_UsesFullContainerDimension()
    {
        // Arrange: Row W=200 H=100 Padding="10", WrapReverse, 3 children W=80 H=30
        // Available main = 200-10-10 = 180. 80+80=160 <= 180, 160+80=240 > 180 -> break
        // Line 1: items 0-1 at Y=10 (padding-top), Line 2: item 2 at Y=40
        // WrapReverse: newY = containerH - oldY - childH (FULL container = 100)
        // child[0]: 100-10-30 = 60, child[2]: 100-40-30 = 30
        var flex = new FlexElement
        {
            Direction = FlexDirection.Row,
            Width = "200",
            Height = "100",
            Wrap = FlexWrap.WrapReverse,
            Padding = "10"
        };
        flex.AddChild(new TextElement { Content = "A", Width = "80", Height = "30" });
        flex.AddChild(new TextElement { Content = "B", Width = "80", Height = "30" });
        flex.AddChild(new TextElement { Content = "C", Width = "80", Height = "30" });

        var template = new Template
        {
            Canvas = new CanvasSettings { Width = 200 },
            Elements = new List<TemplateElement> { flex }
        };

        // Act
        var root = _engine.ComputeLayout(template);

        // Assert: WrapReverse uses FULL container height (100), not inner (80)
        var flexNode = root.Children[0];

        // Line 1 items (originally Y=10) -> Y=60
        Assert.Equal(10f, flexNode.Children[0].X, 0.1f);
        Assert.Equal(60f, flexNode.Children[0].Y, 0.1f);
        Assert.Equal(90f, flexNode.Children[1].X, 0.1f);
        Assert.Equal(60f, flexNode.Children[1].Y, 0.1f);

        // Line 2 item (originally Y=40) -> Y=30
        Assert.Equal(10f, flexNode.Children[2].X, 0.1f);
        Assert.Equal(30f, flexNode.Children[2].Y, 0.1f);
    }
}
