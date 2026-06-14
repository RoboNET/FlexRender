using FlexRender.Abstractions;
using FlexRender.Configuration;
using FlexRender.Parsing.Ast;
using YamlDotNet.Core;
using YamlDotNet.RepresentationModel;

namespace FlexRender.Parsing;

/// <summary>
/// Parses YAML templates into the <see cref="Template"/> AST. Loads YAML via YamlDotNet,
/// converts it to the format-neutral node model, then delegates to the shared
/// <see cref="TemplateEngine"/> for all element parsing and validation.
/// </summary>
public sealed class TemplateParser : ITemplateParser
{
    /// <summary>
    /// Maximum allowed file size in bytes (1 MB) to prevent resource exhaustion.
    /// </summary>
    /// <remarks>
    /// This constant is preserved for backward compatibility. The actual limit used
    /// at runtime comes from <see cref="ResourceLimits.MaxTemplateFileSize"/>.
    /// </remarks>
    public const long MaxFileSize = 1024 * 1024; // 1 MB

    private readonly ResourceLimits _limits;
    private readonly TemplateEngine _engine;

    /// <summary>
    /// Gets the list of supported element types.
    /// </summary>
    public IReadOnlyCollection<string> SupportedElementTypes => _engine.SupportedElementTypes;

    /// <summary>
    /// Initializes a new instance of the <see cref="TemplateParser"/> class with default resource limits.
    /// </summary>
    public TemplateParser() : this(new ResourceLimits())
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="TemplateParser"/> class with custom resource limits.
    /// </summary>
    /// <param name="limits">The resource limits to apply.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="limits"/> is null.</exception>
    public TemplateParser(ResourceLimits limits)
    {
        ArgumentNullException.ThrowIfNull(limits);
        _limits = limits;
        _engine = new TemplateEngine(limits);
    }

    /// <summary>
    /// Parses a YAML string into a Template AST.
    /// </summary>
    /// <param name="content">The YAML string to parse.</param>
    /// <returns>The parsed template.</returns>
    /// <exception cref="TemplateParseException">Thrown when parsing fails.</exception>
    public Template Parse(string content)
    {
        if (string.IsNullOrWhiteSpace(content))
        {
            throw new TemplateParseException("Template YAML is empty or whitespace");
        }

        YamlMappingNode root;
        try
        {
            var yamlStream = new YamlStream();
            using var reader = new StringReader(content);
            yamlStream.Load(reader);

            if (yamlStream.Documents.Count == 0)
            {
                throw new TemplateParseException("Template YAML is empty");
            }

            root = (YamlMappingNode)yamlStream.Documents[0].RootNode;
        }
        catch (YamlException ex)
        {
            throw new TemplateParseException($"Invalid YAML: {ex.Message}", ex);
        }

        return _engine.ParseDocumentRoot(YamlNodeConverter.Convert(root));
    }

    /// <summary>
    /// Parses a YAML template from a stream.
    /// </summary>
    /// <param name="stream">The stream containing YAML content.</param>
    /// <returns>The parsed template.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="stream"/> is null.</exception>
    /// <exception cref="TemplateParseException">Thrown when parsing fails.</exception>
    public Template Parse(Stream stream)
    {
        ArgumentNullException.ThrowIfNull(stream);
        using var reader = new StreamReader(stream);
        var yaml = reader.ReadToEnd();
        return Parse(yaml);
    }

    /// <summary>
    /// Asynchronously parses a YAML file into a Template AST.
    /// </summary>
    /// <param name="path">The path to the YAML file.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The parsed template.</returns>
    /// <exception cref="FileNotFoundException">Thrown when the file does not exist.</exception>
    /// <exception cref="TemplateParseException">Thrown when parsing fails or file exceeds maximum size.</exception>
    public Task<Template> ParseFileAsync(string path, CancellationToken cancellationToken = default)
        => ParseFile(path, cancellationToken);

    /// <summary>
    /// Asynchronously parses a YAML file into a Template AST.
    /// </summary>
    /// <param name="path">The path to the YAML file.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The parsed template.</returns>
    /// <exception cref="FileNotFoundException">Thrown when the file does not exist.</exception>
    /// <exception cref="TemplateParseException">Thrown when parsing fails or file exceeds maximum size.</exception>
    /// <remarks>
    /// This method is equivalent to <see cref="ParseFileAsync(string, CancellationToken)"/>.
    /// The non-suffixed name follows the project's async naming convention.
    /// </remarks>
    public async Task<Template> ParseFile(string path, CancellationToken cancellationToken)
    {
        // Let ReadAllTextAsync throw FileNotFoundException naturally to avoid TOCTOU issues
        var fileInfo = new FileInfo(path);
        if (fileInfo.Exists && fileInfo.Length > _limits.MaxTemplateFileSize)
        {
            throw new TemplateParseException(
                $"Template file size ({fileInfo.Length} bytes) exceeds maximum allowed size ({_limits.MaxTemplateFileSize} bytes)");
        }

        var yaml = await File.ReadAllTextAsync(path, cancellationToken).ConfigureAwait(false);
        return Parse(yaml);
    }
}
