using FlexRender.Layout;
using FlexRender.Parsing;
using FlexRender.Parsing.Ast;
using Xunit;

namespace FlexRender.Tests.Integration;

/// <summary>
/// Integration tests verifying that CSS position properties (position, top, right, bottom, left)
/// are correctly parsed from YAML templates and preserved on the resulting AST elements.
/// These tests exist to catch regressions where the YAML parser silently drops position data,
/// causing absolute/relative positioned elements to render as static.
/// </summary>
public sealed class YamlPositionParsingTests
{
    private readonly TemplateParser _parser = new();

    /// <summary>
    /// Verifies that position: absolute and inset properties (top, left) are parsed
    /// correctly on a flex child element.
    /// </summary>
    [Fact]
    public void Parse_PositionAbsolute_OnFlexElement_SetsPositionProperty()
    {
        const string yaml = """
            canvas:
              width: 400
              background: "#ffffff"
            layout:
              - type: flex
                width: "400"
                height: "200"
                children:
                  - type: flex
                    position: absolute
                    top: "10"
                    left: "20"
                    width: "80"
                    height: "40"
            """;

        var template = _parser.Parse(yaml);

        var container = (FlexElement)template.Elements[0];
        var absChild = container.Children[0];

        Assert.Equal(Position.Absolute, absChild.Position);
        Assert.Equal("10", absChild.Top);
        Assert.Equal("20", absChild.Left);
        Assert.Equal("80", absChild.Width);
        Assert.Equal("40", absChild.Height);
    }

    /// <summary>
    /// Verifies that position: relative and inset properties are parsed correctly
    /// on a flex child element.
    /// </summary>
    [Fact]
    public void Parse_PositionRelative_OnFlexElement_SetsProperties()
    {
        const string yaml = """
            canvas:
              width: 400
              background: "#ffffff"
            layout:
              - type: flex
                direction: row
                children:
                  - type: flex
                    position: relative
                    top: "15"
                    left: "20"
                    width: "80"
                    height: "60"
            """;

        var template = _parser.Parse(yaml);

        var container = (FlexElement)template.Elements[0];
        var relChild = container.Children[0];

        Assert.Equal(Position.Relative, relChild.Position);
        Assert.Equal("15", relChild.Top);
        Assert.Equal("20", relChild.Left);
    }

    /// <summary>
    /// Verifies that position: absolute is parsed correctly on a text element,
    /// including top and right inset properties.
    /// </summary>
    [Fact]
    public void Parse_PositionAbsolute_OnTextElement_SetsPositionProperty()
    {
        const string yaml = """
            canvas:
              width: 400
              background: "#ffffff"
            layout:
              - type: flex
                width: "300"
                height: "200"
                children:
                  - type: text
                    content: "Badge"
                    position: absolute
                    top: "5"
                    right: "5"
            """;

        var template = _parser.Parse(yaml);

        var container = (FlexElement)template.Elements[0];
        var absText = container.Children[0];

        Assert.Equal(Position.Absolute, absText.Position);
        Assert.Equal("5", absText.Top);
        Assert.Equal("5", absText.Right);
    }

    /// <summary>
    /// Verifies that position: absolute is parsed correctly on an image element,
    /// including top and right inset properties. Note: image width/height in YAML
    /// map to ImageWidth/ImageHeight, not the base Width/Height properties.
    /// </summary>
    [Fact]
    public void Parse_PositionAbsolute_OnImageElement_SetsPositionProperty()
    {
        const string yaml = """
            canvas:
              width: 400
              background: "#ffffff"
            layout:
              - type: flex
                width: "300"
                height: "200"
                children:
                  - type: image
                    src: "test.png"
                    position: absolute
                    top: "8"
                    right: "8"
                    width: "24"
                    height: "24"
            """;

        var template = _parser.Parse(yaml);

        var container = (FlexElement)template.Elements[0];
        var absImage = container.Children[0];

        Assert.Equal(Position.Absolute, absImage.Position);
        Assert.Equal("8", absImage.Top);
        Assert.Equal("8", absImage.Right);
        // Image width/height in YAML maps to ImageWidth/ImageHeight, not base Width/Height
        var imageElement = Assert.IsType<ImageElement>(absImage);
        Assert.Equal(24, imageElement.ImageWidth);
        Assert.Equal(24, imageElement.ImageHeight);
    }

    /// <summary>
    /// Verifies that when no position property is specified in YAML, the default
    /// is Static and all inset properties are null.
    /// </summary>
    [Fact]
    public void Parse_DefaultPosition_IsStatic()
    {
        const string yaml = """
            canvas:
              width: 400
              background: "#ffffff"
            layout:
              - type: flex
                width: "300"
                height: "200"
                children:
                  - type: text
                    content: "Hello"
            """;

        var template = _parser.Parse(yaml);

        var container = (FlexElement)template.Elements[0];
        var child = container.Children[0];

        Assert.Equal(Position.Static, child.Position);
        Assert.Null(child.Top);
        Assert.Null(child.Right);
        Assert.Null(child.Bottom);
        Assert.Null(child.Left);
    }

    /// <summary>
    /// Verifies that all four inset properties (top, right, bottom, left) are parsed
    /// when used together with position: absolute.
    /// </summary>
    [Fact]
    public void Parse_PositionAbsolute_AllInsets_ParsedCorrectly()
    {
        const string yaml = """
            canvas:
              width: 400
              background: "#ffffff"
            layout:
              - type: flex
                width: "400"
                height: "300"
                children:
                  - type: flex
                    position: absolute
                    top: "10"
                    right: "20"
                    bottom: "30"
                    left: "40"
            """;

        var template = _parser.Parse(yaml);

        var container = (FlexElement)template.Elements[0];
        var absChild = container.Children[0];

        Assert.Equal(Position.Absolute, absChild.Position);
        Assert.Equal("10", absChild.Top);
        Assert.Equal("20", absChild.Right);
        Assert.Equal("30", absChild.Bottom);
        Assert.Equal("40", absChild.Left);
    }

    /// <summary>
    /// Verifies that position: absolute works on deeply nested elements
    /// (child of a child flex container).
    /// </summary>
    [Fact]
    public void Parse_PositionAbsolute_NestedFlexChild_PreservesPosition()
    {
        const string yaml = """
            canvas:
              width: 400
              background: "#ffffff"
            layout:
              - type: flex
                width: "400"
                height: "300"
                children:
                  - type: flex
                    width: "200"
                    height: "200"
                    children:
                      - type: text
                        content: "Nested badge"
                        position: absolute
                        top: "5"
                        right: "5"
            """;

        var template = _parser.Parse(yaml);

        var outer = (FlexElement)template.Elements[0];
        var inner = (FlexElement)outer.Children[0];
        var nestedText = inner.Children[0];

        Assert.Equal(Position.Absolute, nestedText.Position);
        Assert.Equal("5", nestedText.Top);
        Assert.Equal("5", nestedText.Right);
    }
}
