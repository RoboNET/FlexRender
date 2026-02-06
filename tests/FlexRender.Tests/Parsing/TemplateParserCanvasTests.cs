using FlexRender.Layout;
using FlexRender.Parsing;
using FlexRender.Parsing.Ast;
using Xunit;

namespace FlexRender.Tests.Parsing;

/// <summary>
/// Tests for TemplateParser canvas settings parsing.
/// </summary>
public class TemplateParserCanvasTests
{
    private readonly TemplateParser _parser = new();

    /// <summary>
    /// Verifies that a minimal template with just canvas width returns default values.
    /// </summary>
    [Fact]
    public void Parse_MinimalTemplate_ReturnsDefaultCanvas()
    {
        const string yaml = """
            canvas:
              width: 300
            """;

        var template = _parser.Parse(yaml);

        Assert.NotNull(template);
        Assert.Equal(FixedDimension.Width, template.Canvas.Fixed);
        Assert.Equal(300, template.Canvas.Width);
        Assert.Equal("#ffffff", template.Canvas.Background);
    }

    /// <summary>
    /// Verifies that all canvas settings are parsed correctly.
    /// </summary>
    [Fact]
    public void Parse_CanvasWithAllSettings_ParsesCorrectly()
    {
        const string yaml = """
            canvas:
              fixed: height
              height: 500
              background: "#f0f0f0"
            """;

        var template = _parser.Parse(yaml);

        Assert.Equal(FixedDimension.Height, template.Canvas.Fixed);
        Assert.Equal(500, template.Canvas.Height);
        Assert.Equal("#f0f0f0", template.Canvas.Background);
    }

    /// <summary>
    /// Verifies that canvas with fixed width is parsed correctly.
    /// </summary>
    [Fact]
    public void Parse_CanvasWithWidthFixed_ParsesCorrectly()
    {
        const string yaml = """
            canvas:
              fixed: width
              width: 400
            """;

        var template = _parser.Parse(yaml);

        Assert.Equal(FixedDimension.Width, template.Canvas.Fixed);
        Assert.Equal(400, template.Canvas.Width);
    }

    /// <summary>
    /// Verifies that template metadata is parsed correctly.
    /// </summary>
    [Fact]
    public void Parse_TemplateMetadata_ParsesCorrectly()
    {
        const string yaml = """
            template:
              name: "receipt-standard"
              version: 2
            canvas:
              width: 300
            """;

        var template = _parser.Parse(yaml);

        Assert.Equal("receipt-standard", template.Name);
        Assert.Equal(2, template.Version);
    }

    /// <summary>
    /// Verifies that empty YAML throws an appropriate exception.
    /// </summary>
    [Fact]
    public void Parse_EmptyYaml_ThrowsException()
    {
        const string yaml = "";

        var ex = Assert.Throws<TemplateParseException>(() => _parser.Parse(yaml));
        Assert.Contains("empty", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Verifies that invalid YAML syntax throws an appropriate exception.
    /// </summary>
    [Fact]
    public void Parse_InvalidYaml_ThrowsException()
    {
        const string yaml = """
            canvas:
              width: [invalid
            """;

        Assert.Throws<TemplateParseException>(() => _parser.Parse(yaml));
    }

    /// <summary>
    /// Verifies that missing canvas section throws an appropriate exception.
    /// </summary>
    [Fact]
    public void Parse_MissingCanvas_ThrowsException()
    {
        const string yaml = """
            template:
              name: "test"
            """;

        var ex = Assert.Throws<TemplateParseException>(() => _parser.Parse(yaml));
        Assert.Contains("canvas", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Verifies that canvas rotate defaults to "none" when not specified.
    /// </summary>
    [Fact]
    public void Parse_CanvasWithoutRotate_DefaultsToNone()
    {
        const string yaml = """
            canvas:
              width: 300
            """;

        var template = _parser.Parse(yaml);

        Assert.Equal("none", template.Canvas.Rotate);
    }

    /// <summary>
    /// Verifies that canvas rotate is parsed correctly.
    /// </summary>
    [Theory]
    [InlineData("none")]
    [InlineData("left")]
    [InlineData("right")]
    [InlineData("flip")]
    [InlineData("90")]
    [InlineData("-45")]
    public void Parse_CanvasWithRotate_ParsesCorrectly(string rotate)
    {
        var yaml = $"""
            canvas:
              width: 300
              rotate: {rotate}
            """;

        var template = _parser.Parse(yaml);

        Assert.Equal(rotate, template.Canvas.Rotate);
    }

    /// <summary>
    /// Verifies that canvas with fixed both parses correctly.
    /// </summary>
    [Fact]
    public void Parse_CanvasWithFixedBoth_ParsesCorrectly()
    {
        const string yaml = """
            canvas:
              fixed: both
              width: 630
              height: 200
            """;
        var template = _parser.Parse(yaml);
        Assert.Equal(FixedDimension.Both, template.Canvas.Fixed);
        Assert.Equal(630, template.Canvas.Width);
        Assert.Equal(200, template.Canvas.Height);
    }

    /// <summary>
    /// Verifies that canvas with fixed none parses correctly.
    /// </summary>
    [Fact]
    public void Parse_CanvasWithFixedNone_ParsesCorrectly()
    {
        const string yaml = """
            canvas:
              fixed: none
            """;
        var template = _parser.Parse(yaml);
        Assert.Equal(FixedDimension.None, template.Canvas.Fixed);
    }

    /// <summary>
    /// Verifies that canvas with width only parses correctly.
    /// </summary>
    [Fact]
    public void Parse_CanvasWithWidthOnly_ParsesCorrectly()
    {
        const string yaml = """
            canvas:
              fixed: width
              width: 400
            """;
        var template = _parser.Parse(yaml);
        Assert.Equal(FixedDimension.Width, template.Canvas.Fixed);
        Assert.Equal(400, template.Canvas.Width);
    }

    /// <summary>
    /// Verifies that canvas with height only parses correctly.
    /// </summary>
    [Fact]
    public void Parse_CanvasWithHeightOnly_ParsesCorrectly()
    {
        const string yaml = """
            canvas:
              fixed: height
              height: 300
            """;
        var template = _parser.Parse(yaml);
        Assert.Equal(FixedDimension.Height, template.Canvas.Fixed);
        Assert.Equal(300, template.Canvas.Height);
    }

    /// <summary>
    /// Verifies that the deprecated 'size' canvas property throws an informative error.
    /// </summary>
    [Fact]
    public void CanvasSettings_Dir_DefaultsToLtr()
    {
        var canvas = new CanvasSettings();
        Assert.Equal(TextDirection.Ltr, canvas.TextDirection);
    }

    [Fact]
    public void Parse_CanvasWithDeprecatedSize_ThrowsError()
    {
        const string yaml = """
            canvas:
              fixed: width
              size: 300
            """;

        var ex = Assert.Throws<TemplateParseException>(() => _parser.Parse(yaml));
        Assert.Contains("canvas.size", ex.Message);
        Assert.Contains("removed", ex.Message);
    }
}
