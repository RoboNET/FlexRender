using FlexRender.Parsing;
using FlexRender.Parsing.Ast;
using Xunit;

namespace FlexRender.Tests.Parsing;

/// <summary>
/// Tests for TemplateParser text element parsing.
/// </summary>
public class TemplateParserTextTests
{
    private readonly TemplateParser _parser = new();

    /// <summary>
    /// Verifies that a simple text element is parsed correctly.
    /// </summary>
    [Fact]
    public void Parse_SimpleTextElement_ParsesCorrectly()
    {
        const string yaml = """
            canvas:
              width: 300
            layout:
              - type: text
                content: "Hello World"
            """;

        var template = _parser.Parse(yaml);

        Assert.Single(template.Elements);
        var text = Assert.IsType<TextElement>(template.Elements[0]);
        Assert.Equal("Hello World", text.Content);
    }

    /// <summary>
    /// Verifies that a text element with all properties is parsed correctly.
    /// </summary>
    [Fact]
    public void Parse_TextElementWithAllProperties_ParsesCorrectly()
    {
        const string yaml = """
            canvas:
              width: 300
            layout:
              - type: text
                content: "Total: {{total}} rub"
                font: bold
                size: 1.5em
                color: "#ff0000"
                align: center
                wrap: false
                overflow: clip
                maxLines: 2
                rotate: right
            """;

        var template = _parser.Parse(yaml);

        Assert.Single(template.Elements);
        var text = Assert.IsType<TextElement>(template.Elements[0]);

        Assert.Equal("Total: {{total}} rub", text.Content);
        Assert.Equal("bold", text.Font);
        Assert.Equal("1.5em", text.Size);
        Assert.Equal("#ff0000", text.Color);
        Assert.Equal(TextAlign.Center, text.Align);
        Assert.False(text.Wrap);
        Assert.Equal(TextOverflow.Clip, text.Overflow);
        Assert.Equal(2, text.MaxLines);
        Assert.Equal("right", text.Rotate);
    }

    /// <summary>
    /// Verifies that multiple text elements are all parsed.
    /// </summary>
    [Fact]
    public void Parse_MultipleTextElements_ParsesAll()
    {
        const string yaml = """
            canvas:
              width: 300
            layout:
              - type: text
                content: "Line 1"
              - type: text
                content: "Line 2"
              - type: text
                content: "Line 3"
            """;

        var template = _parser.Parse(yaml);

        Assert.Equal(3, template.Elements.Count);
        Assert.All(template.Elements, e => Assert.IsType<TextElement>(e));

        Assert.Equal("Line 1", ((TextElement)template.Elements[0]).Content);
        Assert.Equal("Line 2", ((TextElement)template.Elements[1]).Content);
        Assert.Equal("Line 3", ((TextElement)template.Elements[2]).Content);
    }

    /// <summary>
    /// Verifies that a text element with minimal properties uses default values.
    /// </summary>
    [Fact]
    public void Parse_TextWithDefaultValues_UsesDefaults()
    {
        const string yaml = """
            canvas:
              width: 300
            layout:
              - type: text
                content: "Test"
            """;

        var template = _parser.Parse(yaml);
        var text = Assert.IsType<TextElement>(template.Elements[0]);

        Assert.Equal("main", text.Font);
        Assert.Equal("1em", text.Size);
        Assert.Equal("#000000", text.Color);
        Assert.Equal(TextAlign.Left, text.Align);
        Assert.True(text.Wrap);
        Assert.Equal(TextOverflow.Ellipsis, text.Overflow);
        Assert.Null(text.MaxLines);
        Assert.Equal("none", text.Rotate);
    }

    /// <summary>
    /// Verifies that all text alignment values are parsed correctly.
    /// </summary>
    [Fact]
    public void Parse_TextAlignments_AllValuesWork()
    {
        const string yaml = """
            canvas:
              width: 300
            layout:
              - type: text
                content: "Left"
                align: left
              - type: text
                content: "Center"
                align: center
              - type: text
                content: "Right"
                align: right
            """;

        var template = _parser.Parse(yaml);

        Assert.Equal(TextAlign.Left, ((TextElement)template.Elements[0]).Align);
        Assert.Equal(TextAlign.Center, ((TextElement)template.Elements[1]).Align);
        Assert.Equal(TextAlign.Right, ((TextElement)template.Elements[2]).Align);
    }

    /// <summary>
    /// Verifies that all text overflow values are parsed correctly.
    /// </summary>
    [Fact]
    public void Parse_TextOverflows_AllValuesWork()
    {
        const string yaml = """
            canvas:
              width: 300
            layout:
              - type: text
                content: "Ellipsis"
                overflow: ellipsis
              - type: text
                content: "Clip"
                overflow: clip
              - type: text
                content: "Visible"
                overflow: visible
            """;

        var template = _parser.Parse(yaml);

        Assert.Equal(TextOverflow.Ellipsis, ((TextElement)template.Elements[0]).Overflow);
        Assert.Equal(TextOverflow.Clip, ((TextElement)template.Elements[1]).Overflow);
        Assert.Equal(TextOverflow.Visible, ((TextElement)template.Elements[2]).Overflow);
    }

    /// <summary>
    /// Verifies that a template with no layout section returns empty elements.
    /// </summary>
    [Fact]
    public void Parse_NoLayout_ReturnsEmptyElements()
    {
        const string yaml = """
            canvas:
              width: 300
            """;

        var template = _parser.Parse(yaml);

        Assert.Empty(template.Elements);
    }

    /// <summary>
    /// Verifies that template expressions in content are preserved.
    /// </summary>
    [Fact]
    public void Parse_TemplateExpressionInContent_PreservesExpression()
    {
        const string yaml = """
            canvas:
              width: 300
            layout:
              - type: text
                content: "{{#if discount}}Discount: {{discount}}%{{/if}}"
            """;

        var template = _parser.Parse(yaml);
        var text = Assert.IsType<TextElement>(template.Elements[0]);

        Assert.Equal("{{#if discount}}Discount: {{discount}}%{{/if}}", text.Content);
    }

    /// <summary>
    /// Verifies that an unknown element type throws TemplateParseException.
    /// </summary>
    [Fact]
    public void Parse_UnknownElementType_ThrowsTemplateParseException()
    {
        const string yaml = """
            canvas:
              width: 300
            layout:
              - type: unknown_element
                content: "Test"
            """;

        var ex = Assert.Throws<TemplateParseException>(() => _parser.Parse(yaml));
        Assert.Contains("unknown_element", ex.Message.ToLowerInvariant());
    }

    /// <summary>
    /// Verifies that an element with missing type throws TemplateParseException.
    /// </summary>
    [Fact]
    public void Parse_MissingElementType_ThrowsTemplateParseException()
    {
        const string yaml = """
            canvas:
              width: 300
            layout:
              - content: "Test"
            """;

        var ex = Assert.Throws<TemplateParseException>(() => _parser.Parse(yaml));
        Assert.Contains("type", ex.Message.ToLowerInvariant());
    }

    /// <summary>
    /// Verifies that a text element with background property is parsed correctly.
    /// </summary>
    [Fact]
    public void Parse_TextWithBackground_ParsesBackground()
    {
        const string yaml = """
            canvas:
              width: 100
            layout:
              - type: text
                content: "Hello"
                background: "#ff0000"
            """;

        var template = _parser.Parse(yaml);
        var text = template.Elements[0] as TextElement;

        Assert.NotNull(text);
        Assert.Equal("#ff0000", text.Background);
    }

    /// <summary>
    /// Verifies that a text element with padding property is parsed correctly.
    /// </summary>
    [Fact]
    public void Parse_TextWithPadding_ParsesPadding()
    {
        const string yaml = """
            canvas:
              width: 100
            layout:
              - type: text
                content: "Hello"
                padding: "8px"
            """;

        var template = _parser.Parse(yaml);
        var text = template.Elements[0] as TextElement;

        Assert.NotNull(text);
        Assert.Equal("8px", text.Padding);
    }

    /// <summary>
    /// Verifies that a text element with margin property is parsed correctly.
    /// </summary>
    [Fact]
    public void Parse_TextWithMargin_ParsesMargin()
    {
        const string yaml = """
            canvas:
              width: 100
            layout:
              - type: text
                content: "Hello"
                margin: "4"
            """;

        var template = _parser.Parse(yaml);
        var text = template.Elements[0] as TextElement;

        Assert.NotNull(text);
        Assert.Equal("4", text.Margin);
    }

    /// <summary>
    /// Verifies that a text element with lineHeight property is parsed correctly.
    /// </summary>
    [Fact]
    public void Parse_TextWithLineHeight_ParsesLineHeight()
    {
        const string yaml = """
            canvas:
              fixed: width
              width: 300
            layout:
              - type: text
                content: "Hello"
                lineHeight: "1.8"
            """;

        var template = _parser.Parse(yaml);
        var text = Assert.IsType<TextElement>(template.Elements[0]);
        Assert.Equal("1.8", text.LineHeight);
    }

    /// <summary>
    /// Verifies that a text element without lineHeight defaults to empty string.
    /// </summary>
    [Fact]
    public void Parse_TextWithoutLineHeight_DefaultsToEmpty()
    {
        const string yaml = """
            canvas:
              fixed: width
              width: 300
            layout:
              - type: text
                content: "Hello"
            """;

        var template = _parser.Parse(yaml);
        var text = Assert.IsType<TextElement>(template.Elements[0]);
        Assert.Equal("", text.LineHeight);
    }

    /// <summary>
    /// Verifies that text align "start" is parsed as TextAlign.Start.
    /// </summary>
    [Fact]
    public void Parse_TextAlignStart_ParsesCorrectly()
    {
        const string yaml = """
            canvas:
              width: 400
            layout:
              - type: text
                content: "Hello"
                align: start
            """;

        var template = _parser.Parse(yaml);

        var text = Assert.IsType<TextElement>(template.Elements[0]);
        Assert.Equal(TextAlign.Start, text.Align);
    }

    /// <summary>
    /// Verifies that text align "end" is parsed as TextAlign.End.
    /// </summary>
    [Fact]
    public void Parse_TextAlignEnd_ParsesCorrectly()
    {
        const string yaml = """
            canvas:
              width: 400
            layout:
              - type: text
                content: "Hello"
                align: end
            """;

        var template = _parser.Parse(yaml);

        var text = Assert.IsType<TextElement>(template.Elements[0]);
        Assert.Equal(TextAlign.End, text.Align);
    }
}
