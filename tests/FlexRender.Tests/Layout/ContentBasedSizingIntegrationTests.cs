using FlexRender.Layout;
using FlexRender.Parsing;
using Xunit;

namespace FlexRender.Tests.Layout;

/// <summary>
/// Integration tests for content-based sizing end-to-end:
/// YAML parsing -> Intrinsic measurement -> Layout
/// </summary>
public class ContentBasedSizingIntegrationTests
{
    private readonly TemplateParser _parser = new();
    private readonly LayoutEngine _engine = new();

    [Fact]
    public void FixedWidth_HeightFromContent_ParseAndLayout()
    {
        // QR size=100, text size=16px => line height 16*1.4=22.4
        // Note: TemplateParser does not map 'height' for text elements from YAML,
        // so text height is derived from font size, not the YAML height value.
        // Expected total height: 100 + 22.4 = 122.4
        const string yaml = """
            canvas:
              fixed: width
              width: 300
            layout:
              - type: qr
                data: "test"
                size: 100
              - type: text
                content: "Hello"
                size: 16px
            """;

        var template = _parser.Parse(yaml);
        var root = _engine.ComputeLayout(template);

        Assert.Equal(300f, root.Width);
        Assert.True(root.Height >= 122f, $"Root height {root.Height} should be >= 122 (QR 100 + text ~22.4)");
    }

    [Fact]
    public void FixedBoth_ParseAndLayout()
    {
        const string yaml = """
            canvas:
              fixed: both
              width: 400
              height: 300
            layout:
              - type: text
                content: "Hello"
                size: 16px
            """;

        var template = _parser.Parse(yaml);
        var root = _engine.ComputeLayout(template);

        Assert.Equal(400f, root.Width);
        Assert.Equal(300f, root.Height);
    }

    [Fact]
    public void FixedNone_ParseAndLayout_SizesFromContent()
    {
        const string yaml = """
            canvas:
              fixed: none
            layout:
              - type: qr
                data: "test"
                size: 120
            """;

        var template = _parser.Parse(yaml);
        var root = _engine.ComputeLayout(template);

        Assert.True(root.Width > 0, "Width should be positive");
        Assert.True(root.Height >= 120f, $"Height {root.Height} should be >= 120 for QR");
    }

    [Fact]
    public void FixedHeight_ParseAndLayout_WidthFromContent()
    {
        const string yaml = """
            canvas:
              fixed: height
              height: 400
            layout:
              - type: flex
                direction: row
                children:
                  - type: qr
                    data: "a"
                    size: 80
                  - type: qr
                    data: "b"
                    size: 80
            """;

        var template = _parser.Parse(yaml);
        var root = _engine.ComputeLayout(template);

        Assert.Equal(400f, root.Height);
        Assert.True(root.Width > 0, "Width should be positive from QR content");
    }

    [Fact]
    public void NestedFlexLayout_WithIntrinsicSizing()
    {
        const string yaml = """
            canvas:
              fixed: width
              width: 630
            layout:
              - type: flex
                direction: row
                gap: 8
                children:
                  - type: flex
                    direction: column
                    width: 200
                    gap: 4
                    children:
                      - type: text
                        content: "Title"
                        size: 14px
                        height: 20
                      - type: text
                        content: "5000"
                        size: 44px
                        height: 60
                  - type: qr
                    data: "test"
                    size: 80
            """;

        var template = _parser.Parse(yaml);
        var root = _engine.ComputeLayout(template);

        Assert.Equal(630f, root.Width);
        var row = root.Children[0];
        Assert.Equal(2, row.Children.Count);
        Assert.Equal(200f, row.Children[0].Width);
        Assert.Equal(80f, row.Children[1].Width);
        Assert.Equal(80f, row.Children[1].Height);
    }
}
