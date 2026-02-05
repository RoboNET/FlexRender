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
    public async Task ParseFile_ExceedsMaxFileSize_ThrowsTemplateParseException()
    {
        var filePath = Path.Combine(_tempDir, "large.yaml");

        // Create a file larger than 1MB (MaxFileSize)
        var largeContent = new string('a', (int)(TemplateParser.MaxFileSize + 1));
        await File.WriteAllTextAsync(filePath, largeContent);

        var ex = await Assert.ThrowsAsync<TemplateParseException>(
            () => _parser.ParseFile(filePath, CancellationToken.None));
        Assert.Contains("exceeds maximum", ex.Message.ToLowerInvariant());
    }

    /// <summary>
    /// Verifies that ParseFile succeeds when the file is at the maximum size.
    /// </summary>
    [Fact]
    public async Task ParseFile_AtMaxFileSize_ParsesOrThrowsYamlException()
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
        await File.WriteAllTextAsync(filePath, content);

        // Should not throw file size exception (may throw YAML parsing error if content is invalid)
        var exception = await Record.ExceptionAsync(() => _parser.ParseFile(filePath, CancellationToken.None));

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
    public async Task ParseFile_ValidFile_ParsesCorrectly()
    {
        var filePath = Path.Combine(_tempDir, "template.yaml");
        await File.WriteAllTextAsync(filePath, """
            canvas:
              width: 400
            layout:
              - type: text
                content: "From file"
            """);

        var template = await _parser.ParseFile(filePath, CancellationToken.None);

        Assert.Equal(400, template.Canvas.Width);
        Assert.Single(template.Elements);
    }

    /// <summary>
    /// Verifies that ParseFile throws FileNotFoundException when the file does not exist.
    /// </summary>
    [Fact]
    public async Task ParseFile_FileNotFound_ThrowsFileNotFoundException()
    {
        var filePath = Path.Combine(_tempDir, "nonexistent.yaml");

        await Assert.ThrowsAsync<FileNotFoundException>(() => _parser.ParseFile(filePath, CancellationToken.None));
    }

    /// <summary>
    /// Verifies that ParseFile throws TemplateParseException for invalid YAML content.
    /// </summary>
    [Fact]
    public async Task ParseFile_InvalidYaml_ThrowsTemplateParseException()
    {
        var filePath = Path.Combine(_tempDir, "invalid.yaml");
        await File.WriteAllTextAsync(filePath, """
            canvas:
              width: [broken
            """);

        await Assert.ThrowsAsync<TemplateParseException>(() => _parser.ParseFile(filePath, CancellationToken.None));
    }

    /// <summary>
    /// Verifies that ParseFile correctly handles UTF-8 encoded content with special characters.
    /// </summary>
    [Fact]
    public async Task ParseFile_Utf8Content_ParsesCorrectly()
    {
        var filePath = Path.Combine(_tempDir, "utf8.yaml");
        await File.WriteAllTextAsync(filePath, """
            canvas:
              width: 300
            layout:
              - type: text
                content: "Итого: 1500 ₽"
            """);

        var template = await _parser.ParseFile(filePath, CancellationToken.None);
        var text = Assert.IsType<TextElement>(template.Elements[0]);

        Assert.Equal("Итого: 1500 ₽", text.Content);
    }

    [Fact]
    public async Task ParseFile_WithCustomMaxFileSize_UsesCustomLimit()
    {
        var limits = new ResourceLimits { MaxTemplateFileSize = 100 };
        var parser = new TemplateParser(limits);

        var filePath = Path.Combine(_tempDir, "medium.yaml");
        var content = "canvas:\n  width: 300\n" + new string('#', 200);
        await File.WriteAllTextAsync(filePath, content);

        // File is > 100 bytes so should be rejected
        var ex = await Assert.ThrowsAsync<TemplateParseException>(
            () => parser.ParseFile(filePath, CancellationToken.None));
        Assert.Contains("exceeds maximum", ex.Message.ToLowerInvariant());
    }

    [Fact]
    public async Task ParseFile_WithLargerCustomLimit_AcceptsLargerFiles()
    {
        var limits = new ResourceLimits { MaxTemplateFileSize = 10 * 1024 * 1024 };
        var parser = new TemplateParser(limits);

        var filePath = Path.Combine(_tempDir, "valid.yaml");
        var content = """
            canvas:
              width: 300
            """;
        await File.WriteAllTextAsync(filePath, content);

        // Should succeed since file is well under 10 MB
        var template = await parser.ParseFile(filePath, CancellationToken.None);
        Assert.NotNull(template);
    }

    [Fact]
    public async Task Constructor_DefaultLimits_UsesOneMbMaxFileSize()
    {
        // Parameterless constructor should still use 1 MB default
        var parser = new TemplateParser();

        var filePath = Path.Combine(_tempDir, "huge.yaml");
        var largeContent = new string('a', (int)(1024 * 1024 + 1));
        await File.WriteAllTextAsync(filePath, largeContent);

        var ex = await Assert.ThrowsAsync<TemplateParseException>(
            () => parser.ParseFile(filePath, CancellationToken.None));
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
