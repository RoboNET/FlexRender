using FlexRender.Parsing;
using FlexRender.Xml;
using Xunit;

namespace FlexRender.Tests.Parsing.Xml;

/// <summary>
/// Tests that XML attribute validation reuses the YAML KnownProperties machinery.
/// </summary>
public class XmlValidationTests
{
    private readonly XmlTemplateParser _parser = new();

    [Fact]
    public void Parse_UnknownAttribute_ThrowsWithSuggestion()
    {
        const string xml = """
            <flexrender>
              <canvas width="300"/>
              <text content="x" colour="#000"/>
            </flexrender>
            """;

        var ex = Assert.Throws<TemplateParseException>(() => _parser.Parse(xml));
        Assert.Contains("colour", ex.Message, System.StringComparison.Ordinal);
        Assert.Contains("color", ex.Message, System.StringComparison.Ordinal);
    }

    [Fact]
    public void Parse_UnknownElementType_Throws()
    {
        const string xml = """
            <flexrender>
              <canvas width="300"/>
              <blob content="x"/>
            </flexrender>
            """;

        var ex = Assert.Throws<TemplateParseException>(() => _parser.Parse(xml));
        Assert.Contains("Unknown element type", ex.Message, System.StringComparison.Ordinal);
    }

    [Fact]
    public void Parse_CaseMismatchAttribute_HintsCaseSensitivity()
    {
        const string xml = """
            <flexrender>
              <canvas width="300"/>
              <text content="x" Color="#000"/>
            </flexrender>
            """;

        var ex = Assert.Throws<TemplateParseException>(() => _parser.Parse(xml));
        Assert.Contains("case-sensitive", ex.Message, System.StringComparison.Ordinal);
    }
}
