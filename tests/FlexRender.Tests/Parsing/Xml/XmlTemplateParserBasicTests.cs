using FlexRender.Parsing;
using FlexRender.Parsing.Ast;
using FlexRender.Xml;
using Xunit;

namespace FlexRender.Tests.Parsing.Xml;

/// <summary>
/// Tests for basic XML template parsing (canvas, text, separator).
/// </summary>
public class XmlTemplateParserBasicTests
{
    private readonly XmlTemplateParser _parser = new();

    [Fact]
    public void Parse_CanvasAndText_ContentAttribute()
    {
        const string xml = """
            <flexrender>
              <canvas width="300" background="#ffffff"/>
              <text content="Hello World" size="1.5em" color="#ff0000"/>
            </flexrender>
            """;

        var template = _parser.Parse(xml);

        Assert.Equal(300, template.Canvas.Width);
        var text = Assert.IsType<TextElement>(Assert.Single(template.Elements));
        Assert.Equal("Hello World", text.Content);
        Assert.Equal("1.5em", text.Size.Value);
        Assert.Equal("#ff0000", text.Color.Value);
    }

    [Fact]
    public void Parse_TextInnerTextUsedAsContent()
    {
        const string xml = """
            <flexrender>
              <canvas width="300"/>
              <text size="1em">Inline body</text>
            </flexrender>
            """;

        var text = Assert.IsType<TextElement>(Assert.Single(_parser.Parse(xml).Elements));
        Assert.Equal("Inline body", text.Content);
    }

    [Fact]
    public void Parse_Separator()
    {
        const string xml = """
            <flexrender>
              <canvas width="300"/>
              <separator orientation="horizontal" style="dashed" thickness="2" color="#333333"/>
            </flexrender>
            """;

        var sep = Assert.IsType<SeparatorElement>(Assert.Single(_parser.Parse(xml).Elements));
        Assert.Equal(SeparatorOrientation.Horizontal, sep.Orientation);
        Assert.Equal(SeparatorStyle.Dashed, sep.Style);
        Assert.Equal(2f, sep.Thickness);
    }

    [Fact]
    public void Parse_NullContent_Throws()
    {
        Assert.Throws<System.ArgumentNullException>(() => _parser.Parse((string)null!));
    }

    [Fact]
    public void Parse_MissingCanvas_Throws()
    {
        const string xml = "<flexrender><text content=\"x\"/></flexrender>";
        var ex = Assert.Throws<TemplateParseException>(() => _parser.Parse(xml));
        Assert.Contains("canvas", ex.Message, System.StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Parse_MalformedXml_ThrowsTemplateParseException()
    {
        const string xml = "<flexrender><canvas width=\"300\"></flexrender>";
        Assert.Throws<TemplateParseException>(() => _parser.Parse(xml));
    }
}
