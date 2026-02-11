using FlexRender.Parsing;
using FlexRender.Parsing.Ast;
using Xunit;

namespace FlexRender.Tests.VisualEffects;

/// <summary>
/// Tests for YAML parsing of the box-shadow property.
/// </summary>
public sealed class BoxShadowParsingTests
{
    private readonly TemplateParser _parser = new();

    private Template ParseYaml(string yaml)
    {
        using var stream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(yaml));
        return _parser.Parse(stream);
    }

    [Fact]
    public void Parse_FlexWithBoxShadow_SetsBoxShadowProperty()
    {
        var yaml = """
            canvas:
              width: 300
            layout:
              - type: flex
                box-shadow: "4 4 8 rgba(0,0,0,0.3)"
                background: "#ffffff"
                children:
                  - type: text
                    content: "Shadowed"
            """;

        var template = ParseYaml(yaml);

        var flex = Assert.IsType<FlexElement>(template.Elements[0]);
        Assert.Equal("4 4 8 rgba(0,0,0,0.3)", flex.BoxShadow);
    }

    [Fact]
    public void Parse_FlexWithBoxShadowCamelCase_SetsBoxShadowProperty()
    {
        var yaml = """
            canvas:
              width: 300
            layout:
              - type: flex
                boxShadow: "4 4 8 rgba(0,0,0,0.3)"
                background: "#ffffff"
                children:
                  - type: text
                    content: "Shadowed"
            """;

        var template = ParseYaml(yaml);

        var flex = Assert.IsType<FlexElement>(template.Elements[0]);
        Assert.Equal("4 4 8 rgba(0,0,0,0.3)", flex.BoxShadow);
    }

    [Fact]
    public void Parse_NoBoxShadow_PropertyIsNull()
    {
        var yaml = """
            canvas:
              width: 300
            layout:
              - type: flex
                background: "#ffffff"
                children:
                  - type: text
                    content: "No shadow"
            """;

        var template = ParseYaml(yaml);

        var flex = Assert.IsType<FlexElement>(template.Elements[0]);
        Assert.Null(flex.BoxShadow.Value);
    }

    [Fact]
    public void Parse_TextWithBoxShadow_SetsProperty()
    {
        var yaml = """
            canvas:
              width: 300
            layout:
              - type: text
                content: "Shadowed text"
                box-shadow: "2 2 4 #000000"
            """;

        var template = ParseYaml(yaml);

        var text = Assert.IsType<TextElement>(template.Elements[0]);
        Assert.Equal("2 2 4 #000000", text.BoxShadow);
    }

    // === Expansion preserves box-shadow ===

    [Fact]
    public void Expand_PreservesBoxShadow()
    {
        var expander = new FlexRender.TemplateEngine.TemplateExpander();
        var template = new Template();
        template.AddElement(new FlexElement
        {
            BoxShadow = "4 4 8 #000000",
            Background = "#ffffff",
            Children = new List<TemplateElement>
            {
                new TextElement { Content = "test" }
            }
        });

        var result = expander.Expand(template, new ObjectValue());

        var flex = Assert.IsType<FlexElement>(result.Elements[0]);
        Assert.Equal("4 4 8 #000000", flex.BoxShadow);
    }
}
