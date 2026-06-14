using FlexRender.Configuration;
using FlexRender.Layout;
using FlexRender.Parsing.Ast;
using FlexRender.Parsing.Nodes;
using static FlexRender.Parsing.NodePropertyHelpers;

namespace FlexRender.Parsing;

/// <summary>
/// Format-neutral template parsing engine. Consumes a neutral <see cref="TemplateMapping"/>
/// document root (produced by the YAML or XML node converters) and builds a <see cref="Template"/> AST.
/// Shared by <c>FlexRender.Yaml</c> and <c>FlexRender.Xml</c>; depends only on Core types.
/// </summary>
public sealed class TemplateEngine
{
    private readonly Dictionary<string, Func<TemplateMapping, TemplateElement>> _elementParsers;
    private readonly ResourceLimits _limits;
    private readonly ElementParsers _parsers;

    /// <summary>
    /// Gets the list of supported element types.
    /// </summary>
    public IReadOnlyCollection<string> SupportedElementTypes => _elementParsers.Keys;

    /// <summary>
    /// Initializes a new instance of the <see cref="TemplateEngine"/> class with the given resource limits.
    /// </summary>
    /// <param name="limits">The resource limits to apply.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="limits"/> is null.</exception>
    public TemplateEngine(ResourceLimits limits)
    {
        ArgumentNullException.ThrowIfNull(limits);
        _limits = limits;
        _parsers = new ElementParsers(ParseElement);
        _elementParsers = new Dictionary<string, Func<TemplateMapping, TemplateElement>>(StringComparer.OrdinalIgnoreCase)
        {
            ["text"] = ElementParsers.ParseTextElement,
            ["flex"] = _parsers.ParseFlexElement,
            ["qr"] = ElementParsers.ParseQrElement,
            ["barcode"] = ElementParsers.ParseBarcodeElement,
            ["image"] = ElementParsers.ParseImageElement,
            ["separator"] = ElementParsers.ParseSeparatorElement,
            ["each"] = _parsers.ParseEachElement,
            ["if"] = _parsers.ParseIfElement,
            ["table"] = ElementParsers.ParseTableElement,
            ["svg"] = ElementParsers.ParseSvgElement,
            ["content"] = ElementParsers.ParseContentElement,
            ["rect"] = ShapeParsers.ParseRectElement,
            ["circle"] = ShapeParsers.ParseCircleElement,
            ["ellipse"] = ShapeParsers.ParseEllipseElement,
            ["draw"] = node => ShapeParsers.ParseDrawElement(node, _limits.MaxShapesPerDraw),
            ["chart"] = node => ChartParsers.ParseChartElement(node, _limits.MaxSeriesPerChart, _limits.MaxDataPointsPerSeries)
        };
    }

    /// <summary>
    /// Builds a <see cref="Template"/> from a neutral document-root mapping.
    /// Shared by the YAML and XML node converters, which translate their respective
    /// source formats into the equivalent neutral <see cref="TemplateMapping"/> tree.
    /// </summary>
    /// <param name="root">The document root mapping node (from a format converter).</param>
    /// <returns>The parsed template.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="root"/> is null.</exception>
    /// <exception cref="TemplateParseException">Thrown when required sections are missing or invalid.</exception>
    public Template ParseDocumentRoot(TemplateMapping root)
    {
        ArgumentNullException.ThrowIfNull(root);

        var template = new Template();

        // Parse template metadata
        if (TryGetMapping(root, "template", out var templateNode))
        {
            template.Name = GetStringValue(templateNode, "name");
            template.Version = GetIntValue(templateNode, "version", 1);
            template.Culture = GetStringValue(templateNode, "culture");
        }

        // Parse fonts section (optional) — supports both mapping and list formats
        if (root.TryGet("fonts", out var fontsNode))
        {
            template.Fonts = fontsNode switch
            {
                TemplateMapping fontsMapping => ParseFonts(fontsMapping),
                TemplateSequence fontsSequence => ParseFontsList(fontsSequence),
                _ => throw new TemplateParseException(
                    "Invalid 'fonts' section. Expected a mapping (name: path) or a list of font entries.")
            };
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
    /// Parses the canvas section of a template.
    /// </summary>
    /// <param name="node">The node containing canvas settings.</param>
    /// <returns>The parsed canvas settings.</returns>
    private static CanvasSettings ParseCanvas(TemplateMapping node)
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
    /// <param name="node">The node containing font definitions.</param>
    /// <returns>A dictionary mapping font names to their definitions.</returns>
    private static Dictionary<string, FontDefinition> ParseFonts(TemplateMapping node)
    {
        var fonts = new Dictionary<string, FontDefinition>(StringComparer.OrdinalIgnoreCase);

        foreach (var fontName in node.Keys)
        {
            if (string.IsNullOrEmpty(fontName))
            {
                continue;
            }

            node.TryGet(fontName, out var value);
            FontDefinition fontDef;

            switch (value)
            {
                // Short format: fontName: "path/to/font.ttf"
                case TemplateScalar scalarValue:
                    fontDef = new FontDefinition(scalarValue.Value ?? string.Empty);
                    break;

                // Full format: fontName: { path: "...", fallback: "Arial" }
                case TemplateMapping mappingValue:
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
    /// Parses the fonts section when provided as a sequence (list format).
    /// Supports two item formats:
    /// - Simple string: <c>- "path/to/font.ttf"</c>
    /// - Object: <c>- { path: "...", name: "heading", fallback: "Arial" }</c>
    /// The first font in the list automatically becomes "default" if no font is explicitly named "default".
    /// </summary>
    /// <param name="node">The sequence node containing font entries.</param>
    /// <returns>A dictionary mapping font names to their definitions.</returns>
    private static Dictionary<string, FontDefinition> ParseFontsList(TemplateSequence node)
    {
        var fonts = new Dictionary<string, FontDefinition>(StringComparer.OrdinalIgnoreCase);
        var index = 0;
        var hasDefault = false;

        foreach (var item in node.Items)
        {
            string? name = null;
            string path;
            string? fallback = null;

            switch (item)
            {
                // Simple string: - "path/to/font.ttf"
                case TemplateScalar scalar:
                    path = scalar.Value ?? string.Empty;
                    break;

                // Object: - { path: "...", name: "heading", fallback: "Arial" }
                case TemplateMapping mapping:
                    path = GetStringValue(mapping, "path") ?? string.Empty;
                    name = GetStringValue(mapping, "name");
                    fallback = GetStringValue(mapping, "fallback");
                    break;

                default:
                    continue;
            }

            if (string.IsNullOrEmpty(path))
                continue;

            // Auto-assign name if not provided
            if (string.IsNullOrEmpty(name))
            {
                // First unnamed font becomes "default"
                if (!hasDefault && !fonts.ContainsKey("default"))
                {
                    name = "default";
                }
                else
                {
                    name = $"__font_{index}";
                }
            }

            if (string.Equals(name, "default", StringComparison.OrdinalIgnoreCase))
                hasDefault = true;

            fonts[name] = new FontDefinition(path, fallback);
            index++;
        }

        return fonts;
    }

    /// <summary>
    /// Parses a sequence of template elements.
    /// </summary>
    /// <param name="node">The sequence node containing elements.</param>
    /// <returns>A list of parsed template elements.</returns>
    /// <exception cref="TemplateParseException">Thrown when an element cannot be parsed.</exception>
    private List<TemplateElement> ParseElements(TemplateSequence node)
    {
        var elements = new List<TemplateElement>(node.Items.Count);

        foreach (var child in node.Items)
        {
            if (child is TemplateMapping elementNode)
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
    /// <param name="node">The node containing the element definition.</param>
    /// <returns>The parsed element.</returns>
    /// <exception cref="TemplateParseException">Thrown when the element type is missing or unknown.</exception>
    private TemplateElement ParseElement(TemplateMapping node)
    {
        var type = GetStringValue(node, "type");

        if (string.IsNullOrEmpty(type))
        {
            throw new TemplateParseException(
                "Element is missing required 'type' property. Each element must have a type (e.g., 'text', 'flex').");
        }

        if (_elementParsers.TryGetValue(type, out var parser))
        {
            KnownProperties.Validate(node, type);
            return parser(node);
        }

        var supportedTypes = string.Join(", ", _elementParsers.Keys.Select(t => $"'{t}'"));
        throw new TemplateParseException(
            $"Unknown element type: '{type}'. Supported types are: {supportedTypes}.");
    }
}
