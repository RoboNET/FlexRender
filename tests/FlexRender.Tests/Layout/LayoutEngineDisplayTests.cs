using FlexRender.Layout;
using FlexRender.Parsing.Ast;
using Xunit;

namespace FlexRender.Tests.Layout;

/// <summary>
/// Tests for display:none functionality which removes elements from layout flow.
/// </summary>
public class LayoutEngineDisplayTests
{
    private readonly LayoutEngine _engine = new();

    [Fact]
    public void ComputeLayout_DisplayNone_ChildSkippedInLayout()
    {
        // Arrange: column flex with 3 children, middle one is display:none
        var template = new Template
        {
            Canvas = new CanvasSettings { Width = 300 },
            Elements = new List<TemplateElement>
            {
                new FlexElement
                {
                    Direction = FlexDirection.Column,
                    Children = new List<TemplateElement>
                    {
                        new TextElement { Content = "First", Height = "30" },
                        new TextElement { Content = "Hidden", Height = "50", Display = Display.None },
                        new TextElement { Content = "Third", Height = "30" }
                    }
                }
            }
        };

        // Act
        var root = _engine.ComputeLayout(template);
        var flex = root.Children[0];

        // Assert: third child should be right after first (hidden child skipped)
        // First at Y=0, height=30. Third should be at Y=30 (not Y=80)
        Assert.Equal(0f, flex.Children[0].Y, 0.1f);
        Assert.Equal(30f, flex.Children[2].Y, 0.1f);
    }

    [Fact]
    public void ComputeLayout_DisplayNone_OtherChildrenNotAffected()
    {
        // Arrange: row flex with 3 children, first one is display:none
        var template = new Template
        {
            Canvas = new CanvasSettings { Width = 300 },
            Elements = new List<TemplateElement>
            {
                new FlexElement
                {
                    Direction = FlexDirection.Row,
                    Children = new List<TemplateElement>
                    {
                        new TextElement { Content = "Hidden", Width = "100", Height = "30", Display = Display.None },
                        new TextElement { Content = "Second", Width = "80", Height = "30" },
                        new TextElement { Content = "Third", Width = "80", Height = "30" }
                    }
                }
            }
        };

        // Act
        var root = _engine.ComputeLayout(template);
        var flex = root.Children[0];

        // Assert: second child starts at X=0 (hidden child not occupying space)
        Assert.Equal(0f, flex.Children[1].X, 0.1f);
        // Third child follows second
        Assert.Equal(80f, flex.Children[2].X, 0.1f);
    }

    [Fact]
    public void ComputeLayout_DisplayNone_GapNotAppliedForHiddenChild()
    {
        // Arrange: column flex with gap, middle child is display:none
        var template = new Template
        {
            Canvas = new CanvasSettings { Width = 300 },
            Elements = new List<TemplateElement>
            {
                new FlexElement
                {
                    Direction = FlexDirection.Column,
                    Gap = "10",
                    Children = new List<TemplateElement>
                    {
                        new TextElement { Content = "First", Height = "30" },
                        new TextElement { Content = "Hidden", Height = "50", Display = Display.None },
                        new TextElement { Content = "Third", Height = "30" }
                    }
                }
            }
        };

        // Act
        var root = _engine.ComputeLayout(template);
        var flex = root.Children[0];

        // Assert: only one gap between First and Third (not two gaps for hidden child)
        // First at Y=0, height=30, gap=10, Third at Y=40
        Assert.Equal(0f, flex.Children[0].Y, 0.1f);
        Assert.Equal(40f, flex.Children[2].Y, 0.1f);
    }

    [Fact]
    public void MeasureIntrinsic_DisplayNone_ReturnsZero()
    {
        // Arrange: column flex with a display:none child that has explicit dimensions
        var template = new Template
        {
            Canvas = new CanvasSettings { Width = 300 },
            Elements = new List<TemplateElement>
            {
                new FlexElement
                {
                    Direction = FlexDirection.Column,
                    Children = new List<TemplateElement>
                    {
                        new TextElement { Content = "Hidden", Width = "200", Height = "100", Display = Display.None }
                    }
                }
            }
        };

        // Act
        var root = _engine.ComputeLayout(template);
        var flex = root.Children[0];

        // Assert: flex container should have zero content height
        // (display:none child contributes nothing to intrinsic size)
        Assert.Equal(0f, flex.Height, 0.1f);
    }
}
