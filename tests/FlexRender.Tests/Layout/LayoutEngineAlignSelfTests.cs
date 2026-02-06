using FlexRender.Layout;
using FlexRender.Parsing.Ast;
using Xunit;

namespace FlexRender.Tests.Layout;

/// <summary>
/// Tests for align-self property which overrides the parent's align-items for individual children.
/// </summary>
public class LayoutEngineAlignSelfTests
{
    private readonly LayoutEngine _engine = new();

    [Fact]
    public void ComputeLayout_RowAlignItemsStretch_ChildAlignSelfStart_ChildNotStretched()
    {
        // Arrange: row flex with height=100, align=stretch, child with alignSelf=start, height=30
        var flex = new FlexElement
        {
            Direction = FlexDirection.Row,
            Height = "100",
            Align = AlignItems.Stretch
        };
        flex.AddChild(new TextElement { Content = "test", Height = "30", AlignSelf = AlignSelf.Start });

        var template = new Template
        {
            Canvas = new CanvasSettings { Width = 400 },
            Elements = new List<TemplateElement> { flex }
        };

        // Act
        var root = _engine.ComputeLayout(template);

        // Assert: child should NOT be stretched, should keep height=30, Y=0
        var flexNode = root.Children[0];
        var childNode = flexNode.Children[0];
        Assert.Equal(30f, childNode.Height, 0.1f);
        Assert.Equal(0f, childNode.Y, 0.1f);
    }

    [Fact]
    public void ComputeLayout_RowAlignItemsStretch_ChildAlignSelfCenter_ChildCentered()
    {
        // Arrange: row flex with height=100, align=stretch, child with alignSelf=center, height=40
        var flex = new FlexElement
        {
            Direction = FlexDirection.Row,
            Height = "100",
            Align = AlignItems.Stretch
        };
        flex.AddChild(new TextElement { Content = "test", Height = "40", AlignSelf = AlignSelf.Center });

        var template = new Template
        {
            Canvas = new CanvasSettings { Width = 400 },
            Elements = new List<TemplateElement> { flex }
        };

        // Act
        var root = _engine.ComputeLayout(template);

        // Assert: child should be centered vertically: (100-40)/2 = 30
        var flexNode = root.Children[0];
        var childNode = flexNode.Children[0];
        Assert.Equal(40f, childNode.Height, 0.1f);
        Assert.Equal(30f, childNode.Y, 0.1f);
    }

    [Fact]
    public void ComputeLayout_RowAlignItemsStretch_ChildAlignSelfEnd_ChildAtEnd()
    {
        // Arrange: row flex with height=100, align=stretch, child with alignSelf=end, height=40
        var flex = new FlexElement
        {
            Direction = FlexDirection.Row,
            Height = "100",
            Align = AlignItems.Stretch
        };
        flex.AddChild(new TextElement { Content = "test", Height = "40", AlignSelf = AlignSelf.End });

        var template = new Template
        {
            Canvas = new CanvasSettings { Width = 400 },
            Elements = new List<TemplateElement> { flex }
        };

        // Act
        var root = _engine.ComputeLayout(template);

        // Assert: child at bottom: 100-40 = 60
        var flexNode = root.Children[0];
        var childNode = flexNode.Children[0];
        Assert.Equal(40f, childNode.Height, 0.1f);
        Assert.Equal(60f, childNode.Y, 0.1f);
    }

    [Fact]
    public void ComputeLayout_RowAlignItemsStretch_ChildAlignSelfStretch_ChildStretched()
    {
        // Arrange: row flex with height=100, align=stretch, child with alignSelf=stretch (no explicit height)
        var flex = new FlexElement
        {
            Direction = FlexDirection.Row,
            Height = "100",
            Align = AlignItems.Stretch
        };
        flex.AddChild(new TextElement { Content = "test", Width = "100", AlignSelf = AlignSelf.Stretch });

        var template = new Template
        {
            Canvas = new CanvasSettings { Width = 400 },
            Elements = new List<TemplateElement> { flex }
        };

        // Act
        var root = _engine.ComputeLayout(template);

        // Assert: child should be stretched to container cross-axis size
        var flexNode = root.Children[0];
        var childNode = flexNode.Children[0];
        Assert.Equal(100f, childNode.Height, 0.1f);
    }

    [Fact]
    public void ComputeLayout_RowAlignItemsCenter_ChildAlignSelfEnd_OverridesParent()
    {
        // Arrange: row flex with height=100, align=center, child with alignSelf=end, height=30
        var flex = new FlexElement
        {
            Direction = FlexDirection.Row,
            Height = "100",
            Align = AlignItems.Center
        };
        flex.AddChild(new TextElement { Content = "test", Height = "30", AlignSelf = AlignSelf.End });

        var template = new Template
        {
            Canvas = new CanvasSettings { Width = 400 },
            Elements = new List<TemplateElement> { flex }
        };

        // Act
        var root = _engine.ComputeLayout(template);

        // Assert: child should be at end, not centered. Y = 100 - 30 = 70
        var flexNode = root.Children[0];
        var childNode = flexNode.Children[0];
        Assert.Equal(70f, childNode.Y, 0.1f);
    }

    [Fact]
    public void ComputeLayout_ColumnAlignItemsStretch_ChildAlignSelfCenter_ChildCentered()
    {
        // Arrange: column flex (width=300 from canvas), align=stretch, child with alignSelf=center, width=100
        var flex = new FlexElement
        {
            Direction = FlexDirection.Column,
            Align = AlignItems.Stretch
        };
        flex.AddChild(new TextElement { Content = "test", Width = "100", Height = "30", AlignSelf = AlignSelf.Center });

        var template = new Template
        {
            Canvas = new CanvasSettings { Width = 300 },
            Elements = new List<TemplateElement> { flex }
        };

        // Act
        var root = _engine.ComputeLayout(template);

        // Assert: child centered on cross axis (X). (300-100)/2 = 100
        var flexNode = root.Children[0];
        var childNode = flexNode.Children[0];
        Assert.Equal(100f, childNode.X, 0.1f);
        Assert.Equal(100f, childNode.Width, 0.1f);
    }

    [Fact]
    public void ComputeLayout_AlignSelfAuto_UsesParentAlignItems()
    {
        // Arrange: row flex with height=100, align=end, child with alignSelf=auto (default), height=40
        var flex = new FlexElement
        {
            Direction = FlexDirection.Row,
            Height = "100",
            Align = AlignItems.End
        };
        flex.AddChild(new TextElement { Content = "test", Height = "40", AlignSelf = AlignSelf.Auto });

        var template = new Template
        {
            Canvas = new CanvasSettings { Width = 400 },
            Elements = new List<TemplateElement> { flex }
        };

        // Act
        var root = _engine.ComputeLayout(template);

        // Assert: child at end (same as parent align-items=end). Y = 100 - 40 = 60
        var flexNode = root.Children[0];
        var childNode = flexNode.Children[0];
        Assert.Equal(60f, childNode.Y, 0.1f);
    }

    [Fact]
    public void ComputeLayout_MultipleChildren_DifferentAlignSelf_EachPositionedCorrectly()
    {
        // Arrange: row flex with height=100, align=stretch
        // Child A: alignSelf=start, height=30
        // Child B: alignSelf=center, height=40
        // Child C: alignSelf=end, height=20
        var flex = new FlexElement
        {
            Direction = FlexDirection.Row,
            Height = "100",
            Align = AlignItems.Stretch
        };
        flex.AddChild(new TextElement { Content = "A", Width = "80", Height = "30", AlignSelf = AlignSelf.Start });
        flex.AddChild(new TextElement { Content = "B", Width = "80", Height = "40", AlignSelf = AlignSelf.Center });
        flex.AddChild(new TextElement { Content = "C", Width = "80", Height = "20", AlignSelf = AlignSelf.End });

        var template = new Template
        {
            Canvas = new CanvasSettings { Width = 400 },
            Elements = new List<TemplateElement> { flex }
        };

        // Act
        var root = _engine.ComputeLayout(template);

        // Assert
        var flexNode = root.Children[0];

        // Child A: alignSelf=start -> Y=0, height=30
        Assert.Equal(0f, flexNode.Children[0].Y, 0.1f);
        Assert.Equal(30f, flexNode.Children[0].Height, 0.1f);

        // Child B: alignSelf=center -> Y=(100-40)/2=30, height=40
        Assert.Equal(30f, flexNode.Children[1].Y, 0.1f);
        Assert.Equal(40f, flexNode.Children[1].Height, 0.1f);

        // Child C: alignSelf=end -> Y=100-20=80, height=20
        Assert.Equal(80f, flexNode.Children[2].Y, 0.1f);
        Assert.Equal(20f, flexNode.Children[2].Height, 0.1f);
    }
}
