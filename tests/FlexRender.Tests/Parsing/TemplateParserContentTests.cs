using FlexRender.Parsing;
using FlexRender.Parsing.Ast;
using Xunit;

namespace FlexRender.Tests.Parsing;

/// <summary>
/// Tests for TemplateParser ContentElement parsing.
/// </summary>
public sealed class TemplateParserContentTests
{
    private readonly TemplateParser _parser = new();

    /// <summary>
    /// Verifies that source and format are parsed correctly from a content element.
    /// </summary>
    [Fact]
    public void Parse_ContentElement_ParsesSourceAndFormat()
    {
        const string yaml = """
            template:
              name: test
              version: 1
            canvas:
              width: 200
            layout:
              - type: content
                source: "hello world"
                format: markdown
            """;

        var template = _parser.Parse(yaml);

        Assert.Single(template.Elements);
        var content = Assert.IsType<ContentElement>(template.Elements[0]);
        Assert.Equal("hello world", content.Source.Value);
        Assert.Equal("markdown", content.Format.Value);
    }

    /// <summary>
    /// Verifies that expression-based source and format are detected as expressions.
    /// </summary>
    [Fact]
    public void Parse_ContentElement_WithExpressions()
    {
        const string yaml = """
            template:
              name: test
              version: 1
            canvas:
              width: 200
            layout:
              - type: content
                source: "{{body}}"
                format: "{{fmt}}"
            """;

        var template = _parser.Parse(yaml);

        var content = Assert.IsType<ContentElement>(template.Elements[0]);
        Assert.True(content.Source.IsExpression);
        Assert.True(content.Format.IsExpression);
    }

    /// <summary>
    /// Verifies that format defaults to an empty string when not specified.
    /// </summary>
    [Fact]
    public void Parse_ContentElement_DefaultFormat()
    {
        const string yaml = """
            template:
              name: test
              version: 1
            canvas:
              width: 200
            layout:
              - type: content
                source: "some text"
            """;

        var template = _parser.Parse(yaml);

        var content = Assert.IsType<ContentElement>(template.Elements[0]);
        Assert.Equal("some text", content.Source.Value);
        Assert.Equal("", content.Format.Value);
    }

    /// <summary>
    /// Verifies that flex-item properties (padding, margin, grow) are inherited correctly.
    /// </summary>
    [Fact]
    public void Parse_ContentElement_InheritsFlexItemProperties()
    {
        const string yaml = """
            template:
              name: test
              version: 1
            canvas:
              width: 200
            layout:
              - type: content
                source: "text"
                format: markdown
                padding: "10"
                margin: "5"
                grow: 1
            """;

        var template = _parser.Parse(yaml);

        var content = Assert.IsType<ContentElement>(template.Elements[0]);
        Assert.Equal("10", content.Padding.Value);
        Assert.Equal("5", content.Margin.Value);
        Assert.Equal(1, content.Grow.Value);
    }
}
