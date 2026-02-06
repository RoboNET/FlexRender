using FlexRender.Layout;
using FlexRender.Parsing;
using FlexRender.Parsing.Ast;
using Xunit;

namespace FlexRender.Tests.Layout;

/// <summary>
/// Tests for overflow:hidden functionality.
/// Covers YAML parsing of the overflow property and verifies that overflow
/// is a rendering-only concern that does not alter layout positions.
/// </summary>
public sealed class LayoutEngineOverflowTests
{
    private readonly LayoutEngine _engine = new();
    private readonly TemplateParser _parser = new();

    // ────────────────────────────────────────────────────────────────
    // Parser Tests
    // ────────────────────────────────────────────────────────────────

    /// <summary>
    /// Verifies that parsing a flex element with overflow: hidden sets the property correctly.
    /// </summary>
    [Fact]
    public void ParseFlexElement_Overflow_Hidden_ParsesCorrectly()
    {
        // Arrange
        const string yaml = """
            canvas:
              width: 300
            layout:
              - type: flex
                overflow: hidden
                children: []
            """;

        // Act
        var template = _parser.Parse(yaml);
        var flex = template.Elements[0] as FlexElement;

        // Assert
        Assert.NotNull(flex);
        Assert.Equal(Overflow.Hidden, flex.Overflow);
    }

    /// <summary>
    /// Verifies that a flex element without an overflow property defaults to Visible.
    /// </summary>
    [Fact]
    public void ParseFlexElement_Overflow_Default_IsVisible()
    {
        // Arrange
        const string yaml = """
            canvas:
              width: 300
            layout:
              - type: flex
                children: []
            """;

        // Act
        var template = _parser.Parse(yaml);
        var flex = template.Elements[0] as FlexElement;

        // Assert
        Assert.NotNull(flex);
        Assert.Equal(Overflow.Visible, flex.Overflow);
    }

    /// <summary>
    /// Verifies that an unrecognized overflow value falls back to Visible.
    /// </summary>
    [Fact]
    public void ParseFlexElement_Overflow_UnknownValue_DefaultsToVisible()
    {
        // Arrange
        const string yaml = """
            canvas:
              width: 300
            layout:
              - type: flex
                overflow: scroll
                children: []
            """;

        // Act
        var template = _parser.Parse(yaml);
        var flex = template.Elements[0] as FlexElement;

        // Assert
        Assert.NotNull(flex);
        Assert.Equal(Overflow.Visible, flex.Overflow);
    }

    // ────────────────────────────────────────────────────────────────
    // Layout Tests
    // ────────────────────────────────────────────────────────────────

    /// <summary>
    /// Verifies that overflow:hidden does not change child layout positions.
    /// Overflow is purely a rendering concern -- children that exceed bounds are
    /// still laid out at the same coordinates regardless of the overflow setting.
    /// </summary>
    [Fact]
    public void ComputeLayout_OverflowHidden_DoesNotAffectLayout()
    {
        // Arrange: two identical flex containers, one with overflow:hidden, one without.
        // Children exceed the container height (100px container, 3x50px children = 150px content).
        var flexVisible = new FlexElement
        {
            Direction = FlexDirection.Column,
            Width = "200",
            Height = "100",
            Overflow = Overflow.Visible
        };
        flexVisible.AddChild(new TextElement { Content = "A", Height = "50" });
        flexVisible.AddChild(new TextElement { Content = "B", Height = "50" });
        flexVisible.AddChild(new TextElement { Content = "C", Height = "50" });

        var flexHidden = new FlexElement
        {
            Direction = FlexDirection.Column,
            Width = "200",
            Height = "100",
            Overflow = Overflow.Hidden
        };
        flexHidden.AddChild(new TextElement { Content = "A", Height = "50" });
        flexHidden.AddChild(new TextElement { Content = "B", Height = "50" });
        flexHidden.AddChild(new TextElement { Content = "C", Height = "50" });

        var templateVisible = new Template
        {
            Canvas = new CanvasSettings { Width = 200 },
            Elements = new List<TemplateElement> { flexVisible }
        };

        var templateHidden = new Template
        {
            Canvas = new CanvasSettings { Width = 200 },
            Elements = new List<TemplateElement> { flexHidden }
        };

        // Act
        var rootVisible = _engine.ComputeLayout(templateVisible);
        var rootHidden = _engine.ComputeLayout(templateHidden);

        var nodesVisible = rootVisible.Children[0];
        var nodesHidden = rootHidden.Children[0];

        // Assert: all child positions and sizes match exactly
        Assert.Equal(nodesVisible.Children.Count, nodesHidden.Children.Count);
        for (var i = 0; i < nodesVisible.Children.Count; i++)
        {
            Assert.Equal(nodesVisible.Children[i].X, nodesHidden.Children[i].X, 0.1f);
            Assert.Equal(nodesVisible.Children[i].Y, nodesHidden.Children[i].Y, 0.1f);
            Assert.Equal(nodesVisible.Children[i].Width, nodesHidden.Children[i].Width, 0.1f);
            Assert.Equal(nodesVisible.Children[i].Height, nodesHidden.Children[i].Height, 0.1f);
        }
    }

    /// <summary>
    /// Verifies that overflow:hidden on a row container does not change child positions.
    /// </summary>
    [Fact]
    public void ComputeLayout_OverflowHidden_Row_DoesNotAffectLayout()
    {
        // Arrange: row container 100px wide with children totaling 150px wide
        var flexHidden = new FlexElement
        {
            Direction = FlexDirection.Row,
            Width = "100",
            Height = "50",
            Overflow = Overflow.Hidden
        };
        flexHidden.AddChild(new TextElement { Content = "A", Width = "50", Height = "50" });
        flexHidden.AddChild(new TextElement { Content = "B", Width = "50", Height = "50" });
        flexHidden.AddChild(new TextElement { Content = "C", Width = "50", Height = "50" });

        var template = new Template
        {
            Canvas = new CanvasSettings { Width = 300 },
            Elements = new List<TemplateElement> { flexHidden }
        };

        // Act
        var root = _engine.ComputeLayout(template);
        var flex = root.Children[0];

        // Assert: children are still laid out sequentially, shrink distributes space
        // but all 3 children exist and have non-zero widths
        Assert.Equal(3, flex.Children.Count);
        Assert.Equal(0f, flex.Children[0].X, 0.1f);
        Assert.True(flex.Children[0].Width > 0);
        Assert.True(flex.Children[1].Width > 0);
        Assert.True(flex.Children[2].Width > 0);
    }
}
