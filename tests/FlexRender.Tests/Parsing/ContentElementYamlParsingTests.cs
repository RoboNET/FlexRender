using FlexRender.Parsing;
using FlexRender.Parsing.Ast;
using Xunit;

namespace FlexRender.Tests.Parsing;

/// <summary>
/// Tests for parsing the options block on ContentElement from YAML.
/// </summary>
public sealed class ContentElementYamlParsingTests
{
    private readonly TemplateParser _parser = new();

    /// <summary>
    /// Verifies that a nested options block is parsed into a dictionary hierarchy.
    /// </summary>
    [Fact]
    public void ParseContentElement_WithOptions_ParsesOptionsBlock()
    {
        const string yaml = """
            template:
              name: test
              version: 1
            canvas:
              width: 400
              height: 600
            layout:
              - type: content
                source: "test"
                format: ndc
                options:
                  input_encoding: cp866
                  charsets:
                    I:
                      bold: true
                      font_size: 14
                      encoding: qwerty-jcuken
                    "1":
                      bold: false
                      font_size: 12
            """;

        var template = _parser.Parse(yaml);

        var content = Assert.IsType<ContentElement>(template.Elements[0]);

        Assert.NotNull(content.Options);
        Assert.Equal("cp866", content.Options["input_encoding"]);

        var charsets = content.Options["charsets"] as IReadOnlyDictionary<string, object>;
        Assert.NotNull(charsets);

        var charsetI = charsets!["I"] as IReadOnlyDictionary<string, object>;
        Assert.NotNull(charsetI);
        Assert.Equal("true", charsetI!["bold"]?.ToString());
        Assert.Equal("14", charsetI["font_size"]?.ToString());
        Assert.Equal("qwerty-jcuken", charsetI["encoding"]?.ToString());

        var charset1 = charsets["1"] as IReadOnlyDictionary<string, object>;
        Assert.NotNull(charset1);
        Assert.Equal("false", charset1!["bold"]?.ToString());
        Assert.Equal("12", charset1["font_size"]?.ToString());
    }

    /// <summary>
    /// Verifies that Options is null when no options block is present.
    /// </summary>
    [Fact]
    public void ParseContentElement_WithoutOptions_OptionsIsNull()
    {
        const string yaml = """
            template:
              name: test
              version: 1
            canvas:
              width: 400
              height: 600
            layout:
              - type: content
                source: "test"
                format: markdown
            """;

        var template = _parser.Parse(yaml);

        var content = Assert.IsType<ContentElement>(template.Elements[0]);

        Assert.Null(content.Options);
    }

    /// <summary>
    /// Verifies that options dictionary keys are case-insensitive.
    /// </summary>
    [Fact]
    public void ParseContentElement_OptionKeys_AreCaseInsensitive()
    {
        const string yaml = """
            template:
              name: test
              version: 1
            canvas:
              width: 400
            layout:
              - type: content
                source: "test"
                format: ndc
                options:
                  MyKey: hello
            """;

        var template = _parser.Parse(yaml);

        var content = Assert.IsType<ContentElement>(template.Elements[0]);

        Assert.NotNull(content.Options);
        Assert.Equal("hello", content.Options["mykey"]);
        Assert.Equal("hello", content.Options["MYKEY"]);
    }

    /// <summary>
    /// Verifies that sequence values inside options are parsed as lists.
    /// </summary>
    [Fact]
    public void ParseContentElement_WithSequenceOption_ParsesAsList()
    {
        const string yaml = """
            template:
              name: test
              version: 1
            canvas:
              width: 400
            layout:
              - type: content
                source: "test"
                format: ndc
                options:
                  items:
                    - alpha
                    - bravo
                    - charlie
            """;

        var template = _parser.Parse(yaml);

        var content = Assert.IsType<ContentElement>(template.Elements[0]);

        Assert.NotNull(content.Options);
        var items = content.Options["items"] as IReadOnlyList<object>;
        Assert.NotNull(items);
        Assert.Equal(3, items!.Count);
        Assert.Equal("alpha", items[0]);
        Assert.Equal("bravo", items[1]);
        Assert.Equal("charlie", items[2]);
    }
}
