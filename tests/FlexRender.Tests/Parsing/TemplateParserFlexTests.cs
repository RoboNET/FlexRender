using FlexRender.Parsing;
using FlexRender.Parsing.Ast;
using Xunit;

namespace FlexRender.Tests.Parsing;

/// <summary>
/// Tests for TemplateParser flex element parsing.
/// </summary>
public class TemplateParserFlexTests
{
    private readonly TemplateParser _parser = new();

    /// <summary>
    /// Verifies that a flex element with background is parsed correctly.
    /// </summary>
    [Fact]
    public void Parse_FlexWithBackground_ParsesBackground()
    {
        const string yaml = """
            canvas:
              width: 100
            layout:
              - type: flex
                background: "#000000"
                children: []
            """;

        var template = _parser.Parse(yaml);
        var flex = template.Elements[0] as FlexElement;

        Assert.NotNull(flex);
        Assert.Equal("#000000", flex.Background);
    }

    /// <summary>
    /// Verifies that a flex element with margin is parsed correctly.
    /// </summary>
    [Fact]
    public void Parse_FlexWithMargin_ParsesMargin()
    {
        const string yaml = """
            canvas:
              width: 100
            layout:
              - type: flex
                margin: "10px"
                children: []
            """;

        var template = _parser.Parse(yaml);
        var flex = template.Elements[0] as FlexElement;

        Assert.NotNull(flex);
        Assert.Equal("10px", flex.Margin);
    }

    /// <summary>
    /// Verifies that a flex element with padding is parsed correctly.
    /// </summary>
    [Fact]
    public void Parse_FlexWithPadding_ParsesPadding()
    {
        const string yaml = """
            canvas:
              width: 100
            layout:
              - type: flex
                padding: "12"
                children: []
            """;

        var template = _parser.Parse(yaml);
        var flex = template.Elements[0] as FlexElement;

        Assert.NotNull(flex);
        Assert.Equal("12", flex.Padding);
    }

    /// <summary>
    /// Verifies that a flex element with all base properties is parsed correctly.
    /// </summary>
    [Fact]
    public void Parse_FlexWithAllBaseProperties_ParsesAllCorrectly()
    {
        const string yaml = """
            canvas:
              width: 100
            layout:
              - type: flex
                background: "#ff5500"
                padding: "8px"
                margin: "4px 8px"
                children: []
            """;

        var template = _parser.Parse(yaml);
        var flex = template.Elements[0] as FlexElement;

        Assert.NotNull(flex);
        Assert.Equal("#ff5500", flex.Background);
        Assert.Equal("8px", flex.Padding);
        Assert.Equal("4px 8px", flex.Margin);
    }

    /// <summary>
    /// Verifies that a flex element without background has null background.
    /// </summary>
    [Fact]
    public void Parse_FlexWithoutBackground_HasNullBackground()
    {
        const string yaml = """
            canvas:
              width: 100
            layout:
              - type: flex
                children: []
            """;

        var template = _parser.Parse(yaml);
        var flex = template.Elements[0] as FlexElement;

        Assert.NotNull(flex);
        Assert.Null(flex.Background.Value);
    }

    /// <summary>
    /// Verifies that a flex element without margin has default margin of "0".
    /// </summary>
    [Fact]
    public void Parse_FlexWithoutMargin_HasDefaultMargin()
    {
        const string yaml = """
            canvas:
              width: 100
            layout:
              - type: flex
                children: []
            """;

        var template = _parser.Parse(yaml);
        var flex = template.Elements[0] as FlexElement;

        Assert.NotNull(flex);
        Assert.Equal("0", flex.Margin);
    }
}
