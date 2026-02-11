using FlexRender.Parsing;
using FlexRender.Parsing.Ast;
using Xunit;

namespace FlexRender.Tests.Parsing;

/// <summary>
/// Tests for TemplateParser font section parsing.
/// </summary>
public class TemplateParserFontTests
{
    private readonly TemplateParser _parser = new();

    /// <summary>
    /// Verifies that the short format font definition (path only) is parsed correctly.
    /// </summary>
    [Fact]
    public void Parse_ShortFormatFont_ParsesPathCorrectly()
    {
        const string yaml = """
            fonts:
              main: "assets/fonts/Roboto-Regular.ttf"
              bold: "assets/fonts/Roboto-Bold.ttf"
            canvas:
              width: 300
            """;

        var template = _parser.Parse(yaml);

        Assert.Equal(2, template.Fonts.Count);
        Assert.True(template.Fonts.ContainsKey("main"));
        Assert.True(template.Fonts.ContainsKey("bold"));
        Assert.Equal("assets/fonts/Roboto-Regular.ttf", template.Fonts["main"].Path);
        Assert.Equal("assets/fonts/Roboto-Bold.ttf", template.Fonts["bold"].Path);
        Assert.Null(template.Fonts["main"].Fallback);
        Assert.Null(template.Fonts["bold"].Fallback);
    }

    /// <summary>
    /// Verifies that the full format font definition (with path and fallback) is parsed correctly.
    /// </summary>
    [Fact]
    public void Parse_FullFormatFontWithFallback_ParsesCorrectly()
    {
        const string yaml = """
            fonts:
              heading:
                path: "assets/fonts/OpenSans-Bold.ttf"
                fallback: "Arial"
            canvas:
              width: 300
            """;

        var template = _parser.Parse(yaml);

        Assert.Single(template.Fonts);
        Assert.True(template.Fonts.ContainsKey("heading"));
        Assert.Equal("assets/fonts/OpenSans-Bold.ttf", template.Fonts["heading"].Path);
        Assert.Equal("Arial", template.Fonts["heading"].Fallback);
    }

    /// <summary>
    /// Verifies that the full format font definition without fallback is parsed correctly.
    /// </summary>
    [Fact]
    public void Parse_FullFormatFontWithoutFallback_ParsesCorrectly()
    {
        const string yaml = """
            fonts:
              custom:
                path: "assets/fonts/Custom.otf"
            canvas:
              width: 300
            """;

        var template = _parser.Parse(yaml);

        Assert.Single(template.Fonts);
        Assert.True(template.Fonts.ContainsKey("custom"));
        Assert.Equal("assets/fonts/Custom.otf", template.Fonts["custom"].Path);
        Assert.Null(template.Fonts["custom"].Fallback);
    }

    /// <summary>
    /// Verifies that mixed short and full format font definitions are parsed correctly.
    /// </summary>
    [Fact]
    public void Parse_MixedFormatFonts_ParsesAllCorrectly()
    {
        const string yaml = """
            fonts:
              main: "assets/fonts/Roboto-Regular.ttf"
              heading:
                path: "assets/fonts/OpenSans-Bold.ttf"
                fallback: "Helvetica"
              mono: "assets/fonts/JetBrainsMono.ttf"
            canvas:
              width: 300
            """;

        var template = _parser.Parse(yaml);

        Assert.Equal(3, template.Fonts.Count);

        Assert.Equal("assets/fonts/Roboto-Regular.ttf", template.Fonts["main"].Path);
        Assert.Null(template.Fonts["main"].Fallback);

        Assert.Equal("assets/fonts/OpenSans-Bold.ttf", template.Fonts["heading"].Path);
        Assert.Equal("Helvetica", template.Fonts["heading"].Fallback);

        Assert.Equal("assets/fonts/JetBrainsMono.ttf", template.Fonts["mono"].Path);
        Assert.Null(template.Fonts["mono"].Fallback);
    }

    /// <summary>
    /// Verifies that a template without fonts section has empty fonts dictionary.
    /// </summary>
    [Fact]
    public void Parse_NoFontsSection_ReturnsEmptyFontsDictionary()
    {
        const string yaml = """
            canvas:
              width: 300
            layout:
              - type: text
                content: "Hello"
            """;

        var template = _parser.Parse(yaml);

        Assert.NotNull(template.Fonts);
        Assert.Empty(template.Fonts);
    }

    /// <summary>
    /// Verifies that font names are case-insensitive.
    /// </summary>
    [Fact]
    public void Parse_FontNames_AreCaseInsensitive()
    {
        const string yaml = """
            fonts:
              Main: "assets/fonts/main.ttf"
              BOLD: "assets/fonts/bold.ttf"
            canvas:
              width: 300
            """;

        var template = _parser.Parse(yaml);

        Assert.True(template.Fonts.ContainsKey("main"));
        Assert.True(template.Fonts.ContainsKey("Main"));
        Assert.True(template.Fonts.ContainsKey("MAIN"));
        Assert.True(template.Fonts.ContainsKey("bold"));
        Assert.True(template.Fonts.ContainsKey("Bold"));
        Assert.True(template.Fonts.ContainsKey("BOLD"));
    }

    /// <summary>
    /// Verifies that fonts can be referenced in text elements.
    /// </summary>
    [Fact]
    public void Parse_FontsWithTextElements_ReferencesWork()
    {
        const string yaml = """
            fonts:
              main: "assets/fonts/Roboto-Regular.ttf"
              bold: "assets/fonts/Roboto-Bold.ttf"
            canvas:
              width: 300
            layout:
              - type: text
                content: "Hello"
                font: main
              - type: text
                content: "Bold text"
                font: bold
            """;

        var template = _parser.Parse(yaml);

        Assert.Equal(2, template.Fonts.Count);
        Assert.Equal(2, template.Elements.Count);

        var text1 = Assert.IsType<TextElement>(template.Elements[0]);
        var text2 = Assert.IsType<TextElement>(template.Elements[1]);

        Assert.Equal("main", text1.Font);
        Assert.Equal("bold", text2.Font);
    }

    /// <summary>
    /// Verifies that template metadata, fonts, canvas, and layout all work together.
    /// </summary>
    [Fact]
    public void Parse_FullTemplateWithFonts_ParsesAllSections()
    {
        const string yaml = """
            template:
              name: "my-template"
              version: 2
            fonts:
              main: "assets/fonts/Roboto-Regular.ttf"
              heading:
                path: "assets/fonts/OpenSans-Bold.ttf"
                fallback: "Arial"
            canvas:
              width: 400
              background: "#f0f0f0"
            layout:
              - type: text
                content: "Title"
                font: heading
              - type: text
                content: "Body text"
                font: main
            """;

        var template = _parser.Parse(yaml);

        Assert.Equal("my-template", template.Name);
        Assert.Equal(2, template.Version);

        Assert.Equal(2, template.Fonts.Count);
        Assert.Equal("assets/fonts/Roboto-Regular.ttf", template.Fonts["main"].Path);
        Assert.Equal("assets/fonts/OpenSans-Bold.ttf", template.Fonts["heading"].Path);
        Assert.Equal("Arial", template.Fonts["heading"].Fallback);

        Assert.Equal(400, template.Canvas.Width);
        Assert.Equal("#f0f0f0", template.Canvas.Background.Value);

        Assert.Equal(2, template.Elements.Count);
    }

    /// <summary>
    /// Verifies that FontDefinition default constructor creates empty values.
    /// </summary>
    [Fact]
    public void FontDefinition_DefaultConstructor_HasEmptyValues()
    {
        var fontDef = new FontDefinition();

        Assert.Equal(string.Empty, fontDef.Path);
        Assert.Null(fontDef.Fallback);
    }

    /// <summary>
    /// Verifies that FontDefinition constructor with path sets path correctly.
    /// </summary>
    [Fact]
    public void FontDefinition_PathConstructor_SetsPath()
    {
        var fontDef = new FontDefinition("test/path.ttf");

        Assert.Equal("test/path.ttf", fontDef.Path);
        Assert.Null(fontDef.Fallback);
    }

    /// <summary>
    /// Verifies that FontDefinition constructor with path and fallback sets both values.
    /// </summary>
    [Fact]
    public void FontDefinition_FullConstructor_SetsBothValues()
    {
        var fontDef = new FontDefinition("test/path.ttf", "Arial");

        Assert.Equal("test/path.ttf", fontDef.Path);
        Assert.Equal("Arial", fontDef.Fallback);
    }

    /// <summary>
    /// Verifies that the special "default" font name is parsed correctly.
    /// </summary>
    [Fact]
    public void Parse_DefaultFontName_ParsesCorrectly()
    {
        const string yaml = """
            fonts:
              default: "assets/fonts/Roboto-Regular.ttf"
              bold: "assets/fonts/Roboto-Bold.ttf"
            canvas:
              width: 300
            """;

        var template = _parser.Parse(yaml);

        Assert.Equal(2, template.Fonts.Count);
        Assert.True(template.Fonts.ContainsKey("default"));
        Assert.Equal("assets/fonts/Roboto-Regular.ttf", template.Fonts["default"].Path);
    }

    /// <summary>
    /// Verifies that text elements without explicit font use the default "main" font.
    /// </summary>
    [Fact]
    public void Parse_TextWithoutFont_UsesMainAsDefault()
    {
        const string yaml = """
            fonts:
              default: "assets/fonts/Roboto-Regular.ttf"
            canvas:
              width: 300
            layout:
              - type: text
                content: "Text without explicit font"
            """;

        var template = _parser.Parse(yaml);

        var textElement = Assert.IsType<TextElement>(template.Elements[0]);
        Assert.Equal("main", textElement.Font);
    }
}
