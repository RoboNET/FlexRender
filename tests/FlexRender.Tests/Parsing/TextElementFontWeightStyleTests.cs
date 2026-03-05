using FlexRender.Parsing;
using FlexRender.Parsing.Ast;
using Xunit;

namespace FlexRender.Tests.Parsing;

public sealed class TextElementFontWeightStyleTests
{
    private readonly TemplateParser _parser = new();

    [Fact]
    public void FontWeight_DefaultsToNormal()
    {
        var text = new TextElement();
        Assert.Equal(FontWeight.Normal, text.FontWeight.Value);
    }

    [Fact]
    public void FontStyle_DefaultsToNormal()
    {
        var text = new TextElement();
        Assert.Equal(FontStyle.Normal, text.FontStyle.Value);
    }

    [Theory]
    [InlineData("bold", FontWeight.Bold)]
    [InlineData("700", FontWeight.Bold)]
    [InlineData("light", FontWeight.Light)]
    [InlineData("300", FontWeight.Light)]
    [InlineData("normal", FontWeight.Normal)]
    [InlineData("400", FontWeight.Normal)]
    [InlineData("thin", FontWeight.Thin)]
    [InlineData("100", FontWeight.Thin)]
    [InlineData("black", FontWeight.Black)]
    [InlineData("900", FontWeight.Black)]
    [InlineData("semi-bold", FontWeight.SemiBold)]
    [InlineData("600", FontWeight.SemiBold)]
    public void Parse_FontWeight_ParsesCorrectly(string yamlValue, FontWeight expected)
    {
        var yaml = $"""
            canvas:
              width: 200
            layout:
              - type: text
                content: "test"
                fontWeight: "{yamlValue}"
            """;

        var template = _parser.Parse(yaml);
        var text = Assert.IsType<TextElement>(template.Elements[0]);
        Assert.Equal(expected, text.FontWeight.Value);
    }

    [Theory]
    [InlineData("normal", FontStyle.Normal)]
    [InlineData("italic", FontStyle.Italic)]
    [InlineData("oblique", FontStyle.Oblique)]
    public void Parse_FontStyle_ParsesCorrectly(string yamlValue, FontStyle expected)
    {
        var yaml = $"""
            canvas:
              width: 200
            layout:
              - type: text
                content: "test"
                fontStyle: "{yamlValue}"
            """;

        var template = _parser.Parse(yaml);
        var text = Assert.IsType<TextElement>(template.Elements[0]);
        Assert.Equal(expected, text.FontStyle.Value);
    }

    [Fact]
    public void Parse_FontWeight_WithExpression()
    {
        var yaml = """
            canvas:
              width: 200
            layout:
              - type: text
                content: "test"
                fontWeight: "{{weight}}"
            """;

        var template = _parser.Parse(yaml);
        var text = Assert.IsType<TextElement>(template.Elements[0]);
        Assert.True(text.FontWeight.IsExpression);
    }

    [Fact]
    public void Parse_BothFontWeightAndStyle()
    {
        var yaml = """
            canvas:
              width: 200
            layout:
              - type: text
                content: "Bold italic text"
                fontWeight: bold
                fontStyle: italic
            """;

        var template = _parser.Parse(yaml);
        var text = Assert.IsType<TextElement>(template.Elements[0]);
        Assert.Equal(FontWeight.Bold, text.FontWeight.Value);
        Assert.Equal(FontStyle.Italic, text.FontStyle.Value);
    }
}
