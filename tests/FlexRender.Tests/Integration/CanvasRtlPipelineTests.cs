using FlexRender.Parsing;
using FlexRender.Rendering;
using Xunit;

namespace FlexRender.Tests.Integration;

/// <summary>
/// Integration tests verifying that canvas-level <c>text-direction: rtl</c> survives the full
/// rendering pipeline (YAML parse -> TemplatePreprocessor -> LayoutEngine).
/// These tests exist to catch regressions where the preprocessor silently drops
/// <see cref="FlexRender.Parsing.Ast.CanvasSettings.TextDirection"/>, causing RTL templates
/// to render as LTR.
/// </summary>
public sealed class CanvasRtlPipelineTests : IDisposable
{
    private readonly SkiaRenderer _renderer = new();
    private readonly TemplateParser _parser = new();

    /// <inheritdoc />
    public void Dispose()
    {
        _renderer.Dispose();
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// Verifies that when the canvas specifies <c>text-direction: rtl</c>, a row flex container
    /// positions its children right-to-left through the full rendering pipeline
    /// (YAML parse, template expansion, preprocessing, and layout computation).
    /// This catches the bug where <see cref="TemplatePreprocessor"/> fails to copy
    /// <c>Canvas.TextDirection</c> to the processed canvas, causing RTL mirroring to be lost.
    /// </summary>
    [Fact]
    public void Parse_CanvasRtl_RowChildrenPositionedRightToLeft()
    {
        const string yaml = """
            canvas:
              fixed: width
              width: 400
              text-direction: rtl

            layout:
              - type: flex
                direction: row
                gap: 10
                children:
                  - type: flex
                    width: "100"
                    height: "50"
                  - type: flex
                    width: "100"
                    height: "50"
                  - type: flex
                    width: "100"
                    height: "50"
            """;

        var template = _parser.Parse(yaml);
        var data = new ObjectValue();

        // Full pipeline: expand -> preprocess -> layout
        var root = _renderer.ComputeLayout(template, data);

        // root has one child: the row flex container
        Assert.Single(root.Children);
        var rowFlex = root.Children[0];
        Assert.Equal(3, rowFlex.Children.Count);

        var first = rowFlex.Children[0];
        var second = rowFlex.Children[1];
        var third = rowFlex.Children[2];

        // In RTL, the first child should be rightmost (largest X)
        // and the third child should be leftmost (smallest X).
        Assert.True(first.X > third.X,
            $"First child (X={first.X}) should have a larger X than third child (X={third.X}) in RTL row layout. " +
            "If this fails, canvas text-direction: rtl is being dropped during preprocessing.");

        Assert.True(first.X > second.X,
            $"First child (X={first.X}) should be right of second child (X={second.X}) in RTL");
        Assert.True(second.X > third.X,
            $"Second child (X={second.X}) should be right of third child (X={third.X}) in RTL");
    }
}
