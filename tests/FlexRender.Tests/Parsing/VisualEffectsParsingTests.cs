using FlexRender.Parsing;
using FlexRender.Parsing.Ast;
using Xunit;

namespace FlexRender.Tests.Parsing;

/// <summary>
/// Tests for parsing visual effects properties (opacity, box-shadow) from YAML templates.
/// </summary>
public sealed class VisualEffectsParsingTests
{
    private readonly TemplateParser _parser = new();

    // === Opacity parsing ===

    [Fact]
    public void Parse_OpacityOnFlex_SetsOpacityValue()
    {
        const string yaml = """
            canvas:
              width: 300
            layout:
              - type: flex
                opacity: 0.5
                children:
                  - type: text
                    content: "hello"
            """;

        var template = _parser.Parse(yaml);
        var flex = Assert.IsType<FlexElement>(template.Elements[0]);
        Assert.Equal(0.5f, flex.Opacity);
    }

    [Fact]
    public void Parse_OpacityDefault_IsOne()
    {
        const string yaml = """
            canvas:
              width: 300
            layout:
              - type: flex
                children:
                  - type: text
                    content: "hello"
            """;

        var template = _parser.Parse(yaml);
        var flex = Assert.IsType<FlexElement>(template.Elements[0]);
        Assert.Equal(1.0f, flex.Opacity);
    }

    [Fact]
    public void Parse_OpacityZero_SetsOpacityToZero()
    {
        const string yaml = """
            canvas:
              width: 300
            layout:
              - type: flex
                opacity: 0
                children:
                  - type: text
                    content: "hello"
            """;

        var template = _parser.Parse(yaml);
        var flex = Assert.IsType<FlexElement>(template.Elements[0]);
        Assert.Equal(0f, flex.Opacity);
    }

    [Fact]
    public void Parse_OpacityOnText_SetsOpacityValue()
    {
        const string yaml = """
            canvas:
              width: 300
            layout:
              - type: text
                content: "hello"
                opacity: 0.75
            """;

        var template = _parser.Parse(yaml);
        var text = Assert.IsType<TextElement>(template.Elements[0]);
        Assert.Equal(0.75f, text.Opacity);
    }

    // === Box-shadow parsing ===

    [Fact]
    public void Parse_BoxShadowOnFlex_SetsBoxShadowString()
    {
        const string yaml = """
            canvas:
              width: 300
            layout:
              - type: flex
                box-shadow: "4 4 8 #333333"
                children:
                  - type: text
                    content: "hello"
            """;

        var template = _parser.Parse(yaml);
        var flex = Assert.IsType<FlexElement>(template.Elements[0]);
        Assert.Equal("4 4 8 #333333", flex.BoxShadow);
    }

    [Fact]
    public void Parse_BoxShadowDefault_IsNull()
    {
        const string yaml = """
            canvas:
              width: 300
            layout:
              - type: flex
                children:
                  - type: text
                    content: "hello"
            """;

        var template = _parser.Parse(yaml);
        var flex = Assert.IsType<FlexElement>(template.Elements[0]);
        Assert.Null(flex.BoxShadow.Value);
    }

    [Fact]
    public void Parse_BoxShadowCamelCase_SetsBoxShadowString()
    {
        const string yaml = """
            canvas:
              width: 300
            layout:
              - type: flex
                boxShadow: "2 2 4 #000000"
                children:
                  - type: text
                    content: "hello"
            """;

        var template = _parser.Parse(yaml);
        var flex = Assert.IsType<FlexElement>(template.Elements[0]);
        Assert.Equal("2 2 4 #000000", flex.BoxShadow);
    }

    [Fact]
    public void Parse_BoxShadowOnText_SetsBoxShadowString()
    {
        const string yaml = """
            canvas:
              width: 300
            layout:
              - type: text
                content: "hello"
                box-shadow: "1 1 3 #555555"
            """;

        var template = _parser.Parse(yaml);
        var text = Assert.IsType<TextElement>(template.Elements[0]);
        Assert.Equal("1 1 3 #555555", text.BoxShadow);
    }

    // === Background gradient parsing ===

    [Fact]
    public void Parse_GradientBackground_SetsBackgroundString()
    {
        const string yaml = """
            canvas:
              width: 300
            layout:
              - type: flex
                background: "linear-gradient(to right, #ff0000, #0000ff)"
                children:
                  - type: text
                    content: "hello"
            """;

        var template = _parser.Parse(yaml);
        var flex = Assert.IsType<FlexElement>(template.Elements[0]);
        Assert.Equal("linear-gradient(to right, #ff0000, #0000ff)", flex.Background);
    }

    [Fact]
    public void Parse_RadialGradientBackground_SetsBackgroundString()
    {
        const string yaml = """
            canvas:
              width: 300
            layout:
              - type: flex
                background: "radial-gradient(#ffffff, #000000)"
                children:
                  - type: text
                    content: "hello"
            """;

        var template = _parser.Parse(yaml);
        var flex = Assert.IsType<FlexElement>(template.Elements[0]);
        Assert.Equal("radial-gradient(#ffffff, #000000)", flex.Background);
    }

    // === Combined visual effects ===

    [Fact]
    public void Parse_AllVisualEffectsCombined_AllParsed()
    {
        const string yaml = """
            canvas:
              width: 300
            layout:
              - type: flex
                opacity: 0.8
                box-shadow: "2 2 4 #333333"
                background: "linear-gradient(to bottom, #ffffff, #eeeeee)"
                children:
                  - type: text
                    content: "hello"
            """;

        var template = _parser.Parse(yaml);
        var flex = Assert.IsType<FlexElement>(template.Elements[0]);
        Assert.Equal(0.8f, flex.Opacity);
        Assert.Equal("2 2 4 #333333", flex.BoxShadow);
        Assert.Equal("linear-gradient(to bottom, #ffffff, #eeeeee)", flex.Background);
    }
}
