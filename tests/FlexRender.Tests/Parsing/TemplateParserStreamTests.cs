using System.Text;
using FlexRender.Abstractions;
using FlexRender.Parsing;
using Xunit;

namespace FlexRender.Tests.Parsing;

/// <summary>
/// Tests for <see cref="ITemplateParser"/> interface methods including Stream-based parsing.
/// </summary>
public sealed class TemplateParserStreamTests
{
    [Fact]
    public void Parse_Stream_ParsesValidYaml()
    {
        var yaml = """
            canvas:
              width: 300
              background: "#ffffff"
            layout:
              - type: text
                content: "Hello"
            """;

        ITemplateParser parser = new TemplateParser();
        using var stream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(yaml));

        var template = parser.Parse(stream);

        Assert.Equal(300, template.Canvas.Width);
        Assert.Single(template.Elements);
    }

    [Fact]
    public void Parse_Stream_ThrowsOnNull()
    {
        ITemplateParser parser = new TemplateParser();

        Assert.Throws<ArgumentNullException>(() => parser.Parse((Stream)null!));
    }

    [Fact]
    public void Parse_String_ViaInterface()
    {
        var yaml = """
            canvas:
              width: 200
              background: "#000000"
            layout:
              - type: text
                content: "Test"
            """;

        ITemplateParser parser = new TemplateParser();
        var template = parser.Parse(yaml);

        Assert.Equal(200, template.Canvas.Width);
    }
}
