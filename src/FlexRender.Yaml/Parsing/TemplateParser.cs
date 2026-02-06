using FlexRender.Abstractions;
using FlexRender.Configuration;
using FlexRender.Layout;
using FlexRender.Parsing.Ast;
using YamlDotNet.Core;
using YamlDotNet.RepresentationModel;
using static FlexRender.Parsing.YamlPropertyHelpers;

namespace FlexRender.Parsing;

/// <summary>
/// Parses YAML templates into AST representation.
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

    /// <summary>
    /// Registry of element type parsers. Maps type name to parser function.
    /// </summary>
    private readonly Dictionary<string, Func<YamlMappingNode, TemplateElement>> _elementParsers;

    private readonly ResourceLimits _limits;
    private readonly ElementParsers _parsers;

    /// <summary>
    /// Gets the list of supported element types.
    /// </summary>
    public IReadOnlyCollection<string> SupportedElementTypes => _elementParsers.Keys;

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
        _parsers = new ElementParsers(ParseElement);
        _elementParsers = new Dictionary<string, Func<YamlMappingNode, TemplateElement>>(StringComparer.OrdinalIgnoreCase)
        {
            ["text"] = ElementParsers.ParseTextElement,
            ["flex"] = _parsers.ParseFlexElement,
            ["qr"] = ElementParsers.ParseQrElement,
            ["barcode"] = ElementParsers.ParseBarcodeElement,
            ["image"] = ElementParsers.ParseImageElement,
            ["separator"] = ElementParsers.ParseSeparatorElement,
            ["each"] = _parsers.ParseEachElement,
            ["if"] = _parsers.ParseIfElement
        };
    }

    /// <summary>
    /// Registers a custom element parser for the specified type.
    /// </summary>
    /// <param name="typeName">The element type name (case-insensitive).</param>
    /// <param name="parser">The parser function that converts YAML to TemplateElement.</param>
    private void RegisterElementParser(string typeName, Func<YamlMappingNode, TemplateElement> parser)
    {
        ArgumentNullException.ThrowIfNull(typeName);
        ArgumentNullException.ThrowIfNull(parser);
        _elementParsers[typeName] = parser;
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

        var template = new Template();

        // Parse template metadata
        if (TryGetMapping(root, "template", out var templateNode))
        {
            template.Name = GetStringValue(templateNode, "name");
            template.Version = GetIntValue(templateNode, "version", 1);
        }

        // Parse fonts section (optional)
        if (TryGetMapping(root, "fonts", out var fontsNode))
        {
            template.Fonts = ParseFonts(fontsNode);
        }

        // Parse canvas (required)
        if (!TryGetMapping(root, "canvas", out var canvasNode))
        {
            throw new TemplateParseException("Missing required 'canvas' section");
        }

        template.Canvas = ParseCanvas(canvasNode);

        // Parse layout/elements
        if (TryGetSequence(root, "layout", out var layoutNode))
        {
            template.Elements = ParseElements(layoutNode);
        }

        return template;
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

    /// <summary>
    /// Parses the canvas section of a template.
    /// </summary>
    /// <param name="node">The YAML node containing canvas settings.</param>
    /// <returns>The parsed canvas settings.</returns>
    private static CanvasSettings ParseCanvas(YamlMappingNode node)
    {
        var canvas = new CanvasSettings();

        var fixedStr = GetStringValue(node, "fixed", "width");
        canvas.Fixed = fixedStr.ToLowerInvariant() switch
        {
            "width" => FixedDimension.Width,
            "height" => FixedDimension.Height,
            "both" => FixedDimension.Both,
            "none" => FixedDimension.None,
            _ => throw new TemplateParseException(
                $"Invalid canvas.fixed value: '{fixedStr}'. Expected 'width', 'height', 'both', or 'none'")
        };

        canvas.Width = GetIntValue(node, "width", 300);
        canvas.Height = GetIntValue(node, "height", 0);
        canvas.Background = GetStringValue(node, "background", "#ffffff");
        canvas.Rotate = GetStringValue(node, "rotate", "none");

        var dirStr = GetStringValue(node, "text-direction", "ltr");
        canvas.TextDirection = dirStr.ToLowerInvariant() switch
        {
            "rtl" => TextDirection.Rtl,
            _ => TextDirection.Ltr
        };

        var sizeValue = GetStringValue(node, "size");
        if (sizeValue != null)
        {
            throw new TemplateParseException(
                "The 'canvas.size' property has been removed. " +
                "Use 'canvas.width' and 'canvas.height' instead.");
        }

        return canvas;
    }

    /// <summary>
    /// Parses the fonts section of a template.
    /// Supports two formats:
    /// - Short format: fontName: "path/to/font.ttf"
    /// - Full format: fontName: { path: "...", fallback: "Arial" }
    /// </summary>
    /// <param name="node">The YAML node containing font definitions.</param>
    /// <returns>A dictionary mapping font names to their definitions.</returns>
    private static Dictionary<string, FontDefinition> ParseFonts(YamlMappingNode node)
    {
        var fonts = new Dictionary<string, FontDefinition>(StringComparer.OrdinalIgnoreCase);

        foreach (var entry in node.Children)
        {
            if (entry.Key is not YamlScalarNode keyNode || string.IsNullOrEmpty(keyNode.Value))
            {
                continue;
            }

            var fontName = keyNode.Value;
            FontDefinition fontDef;

            switch (entry.Value)
            {
                // Short format: fontName: "path/to/font.ttf"
                case YamlScalarNode scalarValue:
                    fontDef = new FontDefinition(scalarValue.Value ?? string.Empty);
                    break;

                // Full format: fontName: { path: "...", fallback: "Arial" }
                case YamlMappingNode mappingValue:
                    var path = GetStringValue(mappingValue, "path") ?? string.Empty;
                    var fallback = GetStringValue(mappingValue, "fallback");
                    fontDef = new FontDefinition(path, fallback);
                    break;

                default:
                    throw new TemplateParseException(
                        $"Invalid font definition for '{fontName}'. Expected a string path or object with 'path' and optional 'fallback' properties.");
            }

            fonts[fontName] = fontDef;
        }

        return fonts;
    }

    /// <summary>
    /// Parses a sequence of template elements.
    /// </summary>
    /// <param name="node">The YAML sequence node containing elements.</param>
    /// <returns>A list of parsed template elements.</returns>
    /// <exception cref="TemplateParseException">Thrown when an element cannot be parsed.</exception>
    private List<TemplateElement> ParseElements(YamlSequenceNode node)
    {
        var elements = new List<TemplateElement>(node.Children.Count);

        foreach (var child in node.Children)
        {
            if (child is YamlMappingNode elementNode)
            {
                var element = ParseElement(elementNode);
                elements.Add(element);
            }
        }

        return elements;
    }

    /// <summary>
    /// Parses a single template element.
    /// </summary>
    /// <param name="node">The YAML node containing the element definition.</param>
    /// <returns>The parsed element.</returns>
    /// <exception cref="TemplateParseException">Thrown when the element type is missing or unknown.</exception>
    private TemplateElement ParseElement(YamlMappingNode node)
    {
        var type = GetStringValue(node, "type");

        if (string.IsNullOrEmpty(type))
        {
            throw new TemplateParseException(
                "Element is missing required 'type' property. Each element must have a type (e.g., 'text', 'flex').");
        }

        if (_elementParsers.TryGetValue(type, out var parser))
        {
            return parser(node);
        }

        var supportedTypes = string.Join(", ", _elementParsers.Keys.Select(t => $"'{t}'"));
        throw new TemplateParseException(
            $"Unknown element type: '{type}'. Supported types are: {supportedTypes}.");
    }
}
