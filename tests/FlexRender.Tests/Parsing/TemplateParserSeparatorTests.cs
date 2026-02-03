using FlexRender.Layout;
using FlexRender.Parsing;
using FlexRender.Parsing.Ast;
using Xunit;

namespace FlexRender.Tests.Parsing;

/// <summary>
/// Tests for TemplateParser separator element parsing.
/// </summary>
public sealed class TemplateParserSeparatorTests
{
    private readonly TemplateParser _parser = new();

    /// <summary>
    /// Verifies separator element parsing with minimal configuration (just type).
    /// </summary>
    [Fact]
    public void Parse_SeparatorElement_MinimalConfig()
    {
        var yaml = """
            canvas:
              width: 300
            layout:
              - type: separator
            """;

        var template = _parser.Parse(yaml);

        Assert.Single(template.Elements);
        var separator = Assert.IsType<SeparatorElement>(template.Elements[0]);
        Assert.Equal(SeparatorOrientation.Horizontal, separator.Orientation);
        Assert.Equal(SeparatorStyle.Dotted, separator.Style);
        Assert.Equal("#000000", separator.Color);
        Assert.Equal(1f, separator.Thickness);
    }

    /// <summary>
    /// Verifies separator element parsing with all properties.
    /// </summary>
    [Fact]
    public void Parse_SeparatorElement_AllProperties()
    {
        var yaml = """
            canvas:
              width: 300
            layout:
              - type: separator
                orientation: vertical
                style: solid
                color: "#cccccc"
                thickness: 3
                padding: "5"
                margin: "10"
                background: "#ffffff"
                rotate: right
            """;

        var template = _parser.Parse(yaml);

        var separator = Assert.IsType<SeparatorElement>(template.Elements[0]);
        Assert.Equal(SeparatorOrientation.Vertical, separator.Orientation);
        Assert.Equal(SeparatorStyle.Solid, separator.Style);
        Assert.Equal("#cccccc", separator.Color);
        Assert.Equal(3f, separator.Thickness);
        Assert.Equal("5", separator.Padding);
        Assert.Equal("10", separator.Margin);
        Assert.Equal("#ffffff", separator.Background);
        Assert.Equal("right", separator.Rotate);
    }

    /// <summary>
    /// Verifies all orientation values can be parsed (case-insensitive).
    /// </summary>
    [Theory]
    [InlineData("horizontal", SeparatorOrientation.Horizontal)]
    [InlineData("vertical", SeparatorOrientation.Vertical)]
    [InlineData("HORIZONTAL", SeparatorOrientation.Horizontal)]
    [InlineData("Vertical", SeparatorOrientation.Vertical)]
    public void Parse_SeparatorElement_AllOrientations(string yamlValue, SeparatorOrientation expected)
    {
        var yaml = $"""
            canvas:
              width: 300
            layout:
              - type: separator
                orientation: {yamlValue}
            """;

        var template = _parser.Parse(yaml);

        var separator = Assert.IsType<SeparatorElement>(template.Elements[0]);
        Assert.Equal(expected, separator.Orientation);
    }

    /// <summary>
    /// Verifies all style values can be parsed (case-insensitive).
    /// </summary>
    [Theory]
    [InlineData("dotted", SeparatorStyle.Dotted)]
    [InlineData("dashed", SeparatorStyle.Dashed)]
    [InlineData("solid", SeparatorStyle.Solid)]
    [InlineData("DASHED", SeparatorStyle.Dashed)]
    [InlineData("Solid", SeparatorStyle.Solid)]
    public void Parse_SeparatorElement_AllStyles(string yamlValue, SeparatorStyle expected)
    {
        var yaml = $"""
            canvas:
              width: 300
            layout:
              - type: separator
                style: {yamlValue}
            """;

        var template = _parser.Parse(yaml);

        var separator = Assert.IsType<SeparatorElement>(template.Elements[0]);
        Assert.Equal(expected, separator.Style);
    }

    /// <summary>
    /// Verifies separator element parsing with flex-item properties.
    /// </summary>
    [Fact]
    public void Parse_SeparatorElement_FlexItemProperties()
    {
        var yaml = """
            canvas:
              width: 300
            layout:
              - type: separator
                grow: 1
                shrink: 0
                basis: "50px"
                order: 2
                alignSelf: center
                width: "200"
                height: "10"
            """;

        var template = _parser.Parse(yaml);

        var separator = Assert.IsType<SeparatorElement>(template.Elements[0]);
        Assert.Equal(1f, separator.Grow);
        Assert.Equal(0f, separator.Shrink);
        Assert.Equal("50px", separator.Basis);
        Assert.Equal(2, separator.Order);
        Assert.Equal(AlignSelf.Center, separator.AlignSelf);
        Assert.Equal("200", separator.Width);
        Assert.Equal("10", separator.Height);
    }

    /// <summary>
    /// Verifies separator appears in supported element types.
    /// </summary>
    [Fact]
    public void SupportedElementTypes_IncludesSeparator()
    {
        var supported = _parser.SupportedElementTypes;

        Assert.Contains("separator", supported, StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Verifies that zero thickness is rejected at parse time.
    /// </summary>
    [Fact]
    public void Parse_SeparatorElement_ZeroThickness_Throws()
    {
        var yaml = """
            canvas:
              width: 300
            layout:
              - type: separator
                thickness: 0
            """;

        var ex = Assert.Throws<TemplateParseException>(() => _parser.Parse(yaml));
        Assert.Contains("thickness", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Verifies that negative thickness is rejected at parse time.
    /// </summary>
    [Fact]
    public void Parse_SeparatorElement_NegativeThickness_Throws()
    {
        var yaml = """
            canvas:
              width: 300
            layout:
              - type: separator
                thickness: -2
            """;

        var ex = Assert.Throws<TemplateParseException>(() => _parser.Parse(yaml));
        Assert.Contains("thickness", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Verifies separator can be used inside a flex container.
    /// </summary>
    [Fact]
    public void Parse_SeparatorInsideFlex_ParsesCorrectly()
    {
        var yaml = """
            canvas:
              width: 300
            layout:
              - type: flex
                direction: column
                children:
                  - type: text
                    content: "Above"
                  - type: separator
                    style: dashed
                    color: "#999999"
                  - type: text
                    content: "Below"
            """;

        var template = _parser.Parse(yaml);

        var flex = Assert.IsType<FlexElement>(template.Elements[0]);
        Assert.Equal(3, flex.Children.Count);
        Assert.IsType<TextElement>(flex.Children[0]);
        var separator = Assert.IsType<SeparatorElement>(flex.Children[1]);
        Assert.Equal(SeparatorStyle.Dashed, separator.Style);
        Assert.Equal("#999999", separator.Color);
        Assert.IsType<TextElement>(flex.Children[2]);
    }
}
