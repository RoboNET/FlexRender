using FlexRender.Layout;
using FlexRender.Parsing.Ast;
using Xunit;

namespace FlexRender.Tests.Snapshots;

/// <summary>
/// Visual snapshot tests for separator element rendering.
/// Tests all three separator styles (dotted, dashed, solid) in both orientations.
/// </summary>
/// <remarks>
/// Run with <c>UPDATE_SNAPSHOTS=true</c> to regenerate golden images.
/// </remarks>
public sealed class SeparatorSnapshotTests : SnapshotTestBase
{
    /// <summary>
    /// Tests horizontal separators with all three styles: dotted, dashed, and solid.
    /// </summary>
    [Fact]
    public void SeparatorHorizontal_AllStyles()
    {
        var template = CreateTemplate(300);

        var flex = new FlexElement
        {
            Direction = FlexDirection.Column,
            Gap = "15",
            Padding = "10"
        };

        flex.AddChild(new TextElement { Content = "Dotted", Size = "12" });
        flex.AddChild(new SeparatorElement
        {
            Orientation = SeparatorOrientation.Horizontal,
            Style = SeparatorStyle.Dotted,
            Thickness = 2f,
            Color = "#000000"
        });

        flex.AddChild(new TextElement { Content = "Dashed", Size = "12" });
        flex.AddChild(new SeparatorElement
        {
            Orientation = SeparatorOrientation.Horizontal,
            Style = SeparatorStyle.Dashed,
            Thickness = 2f,
            Color = "#333333"
        });

        flex.AddChild(new TextElement { Content = "Solid", Size = "12" });
        flex.AddChild(new SeparatorElement
        {
            Orientation = SeparatorOrientation.Horizontal,
            Style = SeparatorStyle.Solid,
            Thickness = 2f,
            Color = "#666666"
        });

        template.AddElement(flex);

        AssertSnapshot("separator_horizontal_all_styles", template, new ObjectValue());
    }

    /// <summary>
    /// Tests vertical separators with all three styles: dotted, dashed, and solid.
    /// </summary>
    [Fact]
    public void SeparatorVertical_AllStyles()
    {
        var template = CreateTemplate(300);

        var flex = new FlexElement
        {
            Direction = FlexDirection.Row,
            Gap = "10",
            Padding = "10",
            Height = "80",
            Align = AlignItems.Stretch
        };

        flex.AddChild(new TextElement { Content = "A", Size = "12", Width = "50" });
        flex.AddChild(new SeparatorElement
        {
            Orientation = SeparatorOrientation.Vertical,
            Style = SeparatorStyle.Dotted,
            Thickness = 2f,
            Color = "#000000"
        });

        flex.AddChild(new TextElement { Content = "B", Size = "12", Width = "50" });
        flex.AddChild(new SeparatorElement
        {
            Orientation = SeparatorOrientation.Vertical,
            Style = SeparatorStyle.Dashed,
            Thickness = 2f,
            Color = "#333333"
        });

        flex.AddChild(new TextElement { Content = "C", Size = "12", Width = "50" });
        flex.AddChild(new SeparatorElement
        {
            Orientation = SeparatorOrientation.Vertical,
            Style = SeparatorStyle.Solid,
            Thickness = 2f,
            Color = "#666666"
        });

        flex.AddChild(new TextElement { Content = "D", Size = "12", Width = "50" });

        template.AddElement(flex);

        AssertSnapshot("separator_vertical_all_styles", template, new ObjectValue());
    }

    /// <summary>
    /// Creates a template with the specified canvas width and white background.
    /// </summary>
    /// <param name="width">The canvas width in pixels.</param>
    /// <returns>A new template configured with the specified width.</returns>
    private static Template CreateTemplate(int width)
    {
        return new Template
        {
            Canvas = new CanvasSettings
            {
                Fixed = FixedDimension.Width,
                Width = width,
                Background = "#ffffff"
            }
        };
    }
}
