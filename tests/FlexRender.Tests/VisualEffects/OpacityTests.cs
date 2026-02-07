using FlexRender.Parsing;
using FlexRender.Parsing.Ast;
using Xunit;

namespace FlexRender.Tests.VisualEffects;

/// <summary>
/// Tests for opacity property parsing and validation.
/// </summary>
public sealed class OpacityTests
{
    private readonly TemplateParser _parser = new();

    private Template ParseYaml(string yaml)
    {
        using var stream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(yaml));
        return _parser.Parse(stream);
    }

    // === Default value ===

    [Fact]
    public void Opacity_DefaultValue_IsOne()
    {
        var element = new FlexElement();

        Assert.Equal(1.0f, element.Opacity);
    }

    [Fact]
    public void Opacity_DefaultValue_TextElement_IsOne()
    {
        var element = new TextElement();

        Assert.Equal(1.0f, element.Opacity);
    }

    // === Setting values ===

    [Theory]
    [InlineData(0.0f)]
    [InlineData(0.5f)]
    [InlineData(1.0f)]
    [InlineData(0.1f)]
    [InlineData(0.99f)]
    public void Opacity_ValidRange_SetsCorrectly(float value)
    {
        var element = new FlexElement { Opacity = value };

        Assert.Equal(value, element.Opacity);
    }

    // === YAML parsing ===

    [Fact]
    public void Parse_FlexWithOpacity_SetsOpacityProperty()
    {
        var yaml = """
            canvas:
              width: 300
            layout:
              - type: flex
                opacity: 0.5
                children:
                  - type: text
                    content: "50% transparent"
            """;

        var template = ParseYaml(yaml);

        var flex = Assert.IsType<FlexElement>(template.Elements[0]);
        Assert.Equal(0.5f, flex.Opacity, 0.01f);
    }

    [Fact]
    public void Parse_TextWithOpacity_SetsOpacityProperty()
    {
        var yaml = """
            canvas:
              width: 300
            layout:
              - type: text
                content: "Faded text"
                opacity: 0.3
            """;

        var template = ParseYaml(yaml);

        var text = Assert.IsType<TextElement>(template.Elements[0]);
        Assert.Equal(0.3f, text.Opacity, 0.01f);
    }

    [Fact]
    public void Parse_NoOpacity_DefaultsToOne()
    {
        var yaml = """
            canvas:
              width: 300
            layout:
              - type: flex
                children:
                  - type: text
                    content: "Normal"
            """;

        var template = ParseYaml(yaml);

        var flex = Assert.IsType<FlexElement>(template.Elements[0]);
        Assert.Equal(1.0f, flex.Opacity);
    }

    [Fact]
    public void Parse_OpacityZero_Parses()
    {
        var yaml = """
            canvas:
              width: 300
            layout:
              - type: flex
                opacity: 0
                children:
                  - type: text
                    content: "Invisible"
            """;

        var template = ParseYaml(yaml);

        var flex = Assert.IsType<FlexElement>(template.Elements[0]);
        Assert.Equal(0.0f, flex.Opacity, 0.01f);
    }

    [Fact]
    public void Parse_OpacityOne_Parses()
    {
        var yaml = """
            canvas:
              width: 300
            layout:
              - type: flex
                opacity: 1
                children:
                  - type: text
                    content: "Fully visible"
            """;

        var template = ParseYaml(yaml);

        var flex = Assert.IsType<FlexElement>(template.Elements[0]);
        Assert.Equal(1.0f, flex.Opacity);
    }

    // === Expansion preserves opacity ===

    [Fact]
    public void Expand_PreservesOpacity()
    {
        var expander = new FlexRender.TemplateEngine.TemplateExpander();
        var template = new Template();
        template.AddElement(new FlexElement
        {
            Opacity = 0.5f,
            Children = new List<TemplateElement>
            {
                new TextElement { Content = "test" }
            }
        });

        var result = expander.Expand(template, new ObjectValue());

        var flex = Assert.IsType<FlexElement>(result.Elements[0]);
        Assert.Equal(0.5f, flex.Opacity);
    }
}
