using Xunit;

namespace FlexRender.Tests.Snapshots;

/// <summary>
/// Visual snapshot tests for shape elements (rect, circle, ellipse) and the draw element.
/// Covers basic box shapes, gradient fills (linear and radial), and painter's-order
/// compositing of draw primitives (rect, line, polyline, circle, path).
/// </summary>
/// <remarks>
/// Run with <c>UPDATE_SNAPSHOTS=true</c> to regenerate golden images.
/// </remarks>
public sealed class ShapeSnapshotTests : SnapshotTestBase
{
    /// <summary>
    /// Tests basic box shapes: a rounded stroked rectangle, a filled circle, and a stroked ellipse.
    /// </summary>
    [Fact]
    public async Task Shapes_BoxBasic_RectCircleEllipse()
    {
        const string yaml = """
            canvas:
              width: 260
              height: 90
              fixed: both
              background: "#ffffff"
            layout:
              - type: flex
                direction: row
                gap: "10"
                padding: "10"
                align: center
                children:
                  - type: rect
                    width: 70
                    height: 50
                    fill: "#4A90D9"
                    stroke: "#1f3a5f"
                    stroke-width: 2
                    radius: 6
                  - type: circle
                    size: 50
                    fill: "#e74c3c"
                  - type: ellipse
                    width: 80
                    height: 50
                    fill: "#2ecc71"
                    stroke: "#145a32"
                    stroke-width: 2
            """;

        var template = Parser.Parse(yaml);

        await AssertSnapshot("shapes_box_basic", template, new ObjectValue());
    }

    /// <summary>
    /// Tests gradient fills on box shapes: a linear gradient rectangle and a radial gradient circle.
    /// </summary>
    [Fact]
    public async Task Shapes_Gradient_LinearAndRadial()
    {
        const string yaml = """
            canvas:
              width: 220
              height: 110
              fixed: both
              background: "#ffffff"
            layout:
              - type: flex
                direction: row
                gap: "10"
                padding: "10"
                children:
                  - type: rect
                    width: 90
                    height: 90
                    fill:
                      gradient: linear
                      colors: ["#ff0000", "#0000ff"]
                      angle: 45
                  - type: circle
                    size: 90
                    fill:
                      gradient: radial
                      colors: ["#ffffff", "#222222"]
            """;

        var template = Parser.Parse(yaml);

        await AssertSnapshot("shapes_gradient", template, new ObjectValue());
    }

    /// <summary>
    /// Tests painter's-order compositing of draw primitives: a rounded rect, a line,
    /// a polyline, a circle, and a closed path, each drawn over the previous.
    /// </summary>
    [Fact]
    public async Task Draw_Overlap_PaintersOrder()
    {
        const string yaml = """
            canvas:
              width: 200
              height: 160
              fixed: both
              background: "#ffffff"
            layout:
              - type: draw
                width: 200
                height: 160
                shapes:
                  - rect: {x: 20, y: 20, width: 120, height: 80, fill: "#cccccc", radius: 8}
                  - line: {x1: 0, y1: 80, x2: 200, y2: 40, stroke: "#333333", stroke-width: 3}
                  - polyline: {points: [[10, 140], [60, 110], [110, 130], [160, 100]], stroke: "#4A90D9", stroke-width: 2}
                  - circle: {cx: 130, cy: 70, r: 35, fill: "#e74c3c"}
                  - path: {d: "M 20 150 L 80 110 Q 120 90 160 120 Z", fill: "#2ecc71"}
            """;

        var template = Parser.Parse(yaml);

        await AssertSnapshot("draw_overlap", template, new ObjectValue());
    }
}
