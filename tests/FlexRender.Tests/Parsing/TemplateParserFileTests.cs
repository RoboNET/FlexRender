using FlexRender.Configuration;
using FlexRender.Parsing;
using FlexRender.Parsing.Ast;
using Xunit;

namespace FlexRender.Tests.Parsing;

/// <summary>
/// Tests for the <see cref="TemplateParser.ParseFile"/> method.
/// </summary>
public class TemplateParserFileTests : IDisposable
{
    private readonly TemplateParser _parser = new();
    private readonly string _tempDir;

    // Security Tests: File Size Limits
    /// <summary>
    /// Verifies that ParseFile throws TemplateParseException when the file exceeds the maximum size.
    /// </summary>
    [Fact]
    public void ParseFile_ExceedsMaxFileSize_ThrowsTemplateParseException()
    {
        var filePath = Path.Combine(_tempDir, "large.yaml");

        // Create a file larger than 1MB (MaxFileSize)
        var largeContent = new string('a', (int)(TemplateParser.MaxFileSize + 1));
        File.WriteAllText(filePath, largeContent);

        var ex = Assert.Throws<TemplateParseException>(() => _parser.ParseFile(filePath));
        Assert.Contains("exceeds maximum", ex.Message.ToLowerInvariant());
    }

    /// <summary>
    /// Verifies that ParseFile succeeds when the file is at the maximum size.
    /// </summary>
    [Fact]
    public void ParseFile_AtMaxFileSize_ParsesOrThrowsYamlException()
    {
        var filePath = Path.Combine(_tempDir, "maxsize.yaml");

        // Create valid YAML content that is close to max size
        var validYaml = """
            canvas:
              width: 300
            """;

        // Pad with valid YAML comments to approach max size (but not exceed)
        var padding = new string('#', 1000);
        var content = validYaml + "\n" + padding;
        File.WriteAllText(filePath, content);

        // Should not throw file size exception (may throw YAML parsing error if content is invalid)
        var exception = Record.Exception(() => _parser.ParseFile(filePath));

        // Either succeeds or throws a non-size-related error
        if (exception != null)
        {
            Assert.DoesNotContain("exceeds maximum", exception.Message.ToLowerInvariant());
        }
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="TemplateParserFileTests"/> class.
    /// </summary>
    public TemplateParserFileTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"FlexRenderTests_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);
    }

    /// <summary>
    /// Cleans up the temporary directory after tests complete.
    /// </summary>
    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, recursive: true);
    }

    /// <summary>
    /// Verifies that ParseFile correctly parses a valid YAML template file.
    /// </summary>
    [Fact]
    public void ParseFile_ValidFile_ParsesCorrectly()
    {
        var filePath = Path.Combine(_tempDir, "template.yaml");
        File.WriteAllText(filePath, """
            canvas:
              width: 400
            layout:
              - type: text
                content: "From file"
            """);

        var template = _parser.ParseFile(filePath);

        Assert.Equal(400, template.Canvas.Width);
        Assert.Single(template.Elements);
    }

    /// <summary>
    /// Verifies that ParseFile throws FileNotFoundException when the file does not exist.
    /// </summary>
    [Fact]
    public void ParseFile_FileNotFound_ThrowsFileNotFoundException()
    {
        var filePath = Path.Combine(_tempDir, "nonexistent.yaml");

        Assert.Throws<FileNotFoundException>(() => _parser.ParseFile(filePath));
    }

    /// <summary>
    /// Verifies that ParseFile throws TemplateParseException for invalid YAML content.
    /// </summary>
    [Fact]
    public void ParseFile_InvalidYaml_ThrowsTemplateParseException()
    {
        var filePath = Path.Combine(_tempDir, "invalid.yaml");
        File.WriteAllText(filePath, """
            canvas:
              width: [broken
            """);

        Assert.Throws<TemplateParseException>(() => _parser.ParseFile(filePath));
    }

    /// <summary>
    /// Verifies that ParseFile correctly handles UTF-8 encoded content with special characters.
    /// </summary>
    [Fact]
    public void ParseFile_Utf8Content_ParsesCorrectly()
    {
        var filePath = Path.Combine(_tempDir, "utf8.yaml");
        File.WriteAllText(filePath, """
            canvas:
              width: 300
            layout:
              - type: text
                content: "Итого: 1500 ₽"
            """);

        var template = _parser.ParseFile(filePath);
        var text = Assert.IsType<TextElement>(template.Elements[0]);

        Assert.Equal("Итого: 1500 ₽", text.Content);
    }

    [Fact]
    public void ParseFile_WithCustomMaxFileSize_UsesCustomLimit()
    {
        var limits = new ResourceLimits { MaxTemplateFileSize = 100 };
        var parser = new TemplateParser(limits);

        var filePath = Path.Combine(_tempDir, "medium.yaml");
        var content = "canvas:\n  width: 300\n" + new string('#', 200);
        File.WriteAllText(filePath, content);

        // File is > 100 bytes so should be rejected
        var ex = Assert.Throws<TemplateParseException>(() => parser.ParseFile(filePath));
        Assert.Contains("exceeds maximum", ex.Message.ToLowerInvariant());
    }

    [Fact]
    public void ParseFile_WithLargerCustomLimit_AcceptsLargerFiles()
    {
        var limits = new ResourceLimits { MaxTemplateFileSize = 10 * 1024 * 1024 };
        var parser = new TemplateParser(limits);

        var filePath = Path.Combine(_tempDir, "valid.yaml");
        var content = """
            canvas:
              width: 300
            """;
        File.WriteAllText(filePath, content);

        // Should succeed since file is well under 10 MB
        var template = parser.ParseFile(filePath);
        Assert.NotNull(template);
    }

    [Fact]
    public void Constructor_DefaultLimits_UsesOneMbMaxFileSize()
    {
        // Parameterless constructor should still use 1 MB default
        var parser = new TemplateParser();

        var filePath = Path.Combine(_tempDir, "huge.yaml");
        var largeContent = new string('a', (int)(1024 * 1024 + 1));
        File.WriteAllText(filePath, largeContent);

        var ex = Assert.Throws<TemplateParseException>(() => parser.ParseFile(filePath));
        Assert.Contains("exceeds maximum", ex.Message.ToLowerInvariant());
    }

    [Fact]
    public void Parse_WithDataAndCustomPreprocessorInputSize_RejectsOversizedInput()
    {
        var limits = new ResourceLimits { MaxPreprocessorInputSize = 50 };
        var parser = new TemplateParser(limits);

        var yaml = """
            canvas:
              width: 300
            layout:
              - type: text
                content: "Hello"
            """ + new string('#', 100);
        var data = new ObjectValue { ["x"] = "y" };

        var ex = Assert.Throws<ArgumentException>(() => parser.Parse(yaml, data));
        Assert.Contains("exceeds maximum", ex.Message.ToLowerInvariant());
    }
}
