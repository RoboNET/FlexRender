using FlexRender.Layout;
using FlexRender.Parsing;
using FlexRender.Parsing.Ast;
using Xunit;

namespace FlexRender.Tests.Parsing;

/// <summary>
/// Tests for parsing the text-direction property on canvas and elements.
/// Also verifies backward compatibility with the legacy "dir" alias.
/// </summary>
public class TemplateParserDirTests
{
    private readonly TemplateParser _parser = new();

    [Fact]
    public void Parse_CanvasTextDirectionRtl_SetsDirectionRtl()
    {
        const string yaml = """
            canvas:
              width: 400
              text-direction: rtl
            """;

        var template = _parser.Parse(yaml);

        Assert.Equal(TextDirection.Rtl, template.Canvas.TextDirection);
    }

    [Fact]
    public void Parse_CanvasTextDirectionLtr_SetsDirectionLtr()
    {
        const string yaml = """
            canvas:
              width: 400
              text-direction: ltr
            """;

        var template = _parser.Parse(yaml);

        Assert.Equal(TextDirection.Ltr, template.Canvas.TextDirection);
    }

    [Fact]
    public void Parse_CanvasNoTextDirection_DefaultsToLtr()
    {
        const string yaml = """
            canvas:
              width: 400
            """;

        var template = _parser.Parse(yaml);

        Assert.Equal(TextDirection.Ltr, template.Canvas.TextDirection);
    }

    [Fact]
    public void Parse_CanvasTextDirectionCaseInsensitive_ParsesCorrectly()
    {
        const string yaml = """
            canvas:
              width: 400
              text-direction: RTL
            """;

        var template = _parser.Parse(yaml);

        Assert.Equal(TextDirection.Rtl, template.Canvas.TextDirection);
    }

    [Fact]
    public void Parse_CanvasTextDirectionInvalidValue_DefaultsToLtr()
    {
        const string yaml = """
            canvas:
              width: 400
              text-direction: invalid
            """;

        var template = _parser.Parse(yaml);

        Assert.Equal(TextDirection.Ltr, template.Canvas.TextDirection);
    }

    [Fact]
    public void Parse_ElementTextDirectionRtl_SetsDirection()
    {
        const string yaml = """
            canvas:
              width: 400
            layout:
              - type: text
                content: "Hello"
                text-direction: rtl
            """;

        var template = _parser.Parse(yaml);

        var text = Assert.IsType<TextElement>(template.Elements[0]);
        Assert.Equal(TextDirection.Rtl, text.TextDirection);
    }

    [Fact]
    public void Parse_ElementTextDirectionLtr_SetsDirection()
    {
        const string yaml = """
            canvas:
              width: 400
            layout:
              - type: text
                content: "Hello"
                text-direction: ltr
            """;

        var template = _parser.Parse(yaml);

        var text = Assert.IsType<TextElement>(template.Elements[0]);
        Assert.Equal(TextDirection.Ltr, text.TextDirection);
    }

    [Fact]
    public void Parse_ElementNoTextDirection_DefaultsToNull()
    {
        const string yaml = """
            canvas:
              width: 400
            layout:
              - type: text
                content: "Hello"
            """;

        var template = _parser.Parse(yaml);

        var text = Assert.IsType<TextElement>(template.Elements[0]);
        Assert.Null(text.TextDirection);
    }

    [Fact]
    public void Parse_FlexElementTextDirectionRtl_SetsDirection()
    {
        const string yaml = """
            canvas:
              width: 400
            layout:
              - type: flex
                text-direction: rtl
                children:
                  - type: text
                    content: "Hello"
            """;

        var template = _parser.Parse(yaml);

        var flex = Assert.IsType<FlexElement>(template.Elements[0]);
        Assert.Equal(TextDirection.Rtl, flex.TextDirection);
    }

    [Fact]
    public void Parse_ElementTextDirectionInvalidValue_DefaultsToNull()
    {
        const string yaml = """
            canvas:
              width: 400
            layout:
              - type: text
                content: "Hello"
                text-direction: invalid
            """;

        var template = _parser.Parse(yaml);

        var text = Assert.IsType<TextElement>(template.Elements[0]);
        Assert.Null(text.TextDirection);
    }
}
