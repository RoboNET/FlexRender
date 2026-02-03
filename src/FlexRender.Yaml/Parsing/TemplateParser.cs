using FlexRender.Abstractions;
using FlexRender.Configuration;
using FlexRender.Layout;
using FlexRender.Parsing.Ast;
using YamlDotNet.Core;
using YamlDotNet.RepresentationModel;

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
        _elementParsers = new Dictionary<string, Func<YamlMappingNode, TemplateElement>>(StringComparer.OrdinalIgnoreCase)
        {
            ["text"] = ParseTextElement,
            ["flex"] = ParseFlexElement,
            ["qr"] = ParseQrElement,
            ["barcode"] = ParseBarcodeElement,
            ["image"] = ParseImageElement,
            ["separator"] = ParseSeparatorElement
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
    /// Parses a YAML string into a Template AST with data preprocessing.
    /// This method expands {{#each}} blocks using the provided data before parsing.
    /// </summary>
    /// <param name="yaml">The YAML string to parse.</param>
    /// <param name="data">The data to use for preprocessing (expanding {{#each}} blocks).</param>
    /// <returns>The parsed template.</returns>
    /// <exception cref="TemplateParseException">Thrown when parsing fails.</exception>
    public Template Parse(string yaml, TemplateValue? data)
    {
        var preprocessedYaml = YamlPreprocessor.Preprocess(yaml, data, _limits);
        return Parse(preprocessedYaml);
    }

    /// <summary>
    /// Parses a YAML string into a Template AST.
    /// </summary>
    /// <param name="yaml">The YAML string to parse.</param>
    /// <returns>The parsed template.</returns>
    /// <exception cref="TemplateParseException">Thrown when parsing fails.</exception>
    public Template Parse(string yaml)
    {
        if (string.IsNullOrWhiteSpace(yaml))
        {
            throw new TemplateParseException("Template YAML is empty or whitespace");
        }

        YamlMappingNode root;
        try
        {
            var yamlStream = new YamlStream();
            using var reader = new StringReader(yaml);
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
    /// Parses a YAML file into a Template AST.
    /// </summary>
    /// <param name="path">The path to the YAML file.</param>
    /// <returns>The parsed template.</returns>
    /// <exception cref="FileNotFoundException">Thrown when the file does not exist.</exception>
    /// <exception cref="TemplateParseException">Thrown when parsing fails or file exceeds maximum size.</exception>
    public Template ParseFile(string path)
    {
        if (!File.Exists(path))
        {
            throw new FileNotFoundException($"Template file not found: {path}", path);
        }

        // Security: Validate file size before reading to prevent resource exhaustion
        var fileInfo = new FileInfo(path);
        if (fileInfo.Length > _limits.MaxTemplateFileSize)
        {
            throw new TemplateParseException(
                $"Template file size ({fileInfo.Length} bytes) exceeds maximum allowed size ({_limits.MaxTemplateFileSize} bytes)");
        }

        var yaml = File.ReadAllText(path);
        return Parse(yaml);
    }

    /// <summary>
    /// Parses the canvas section of a template.
    /// </summary>
    /// <param name="node">The YAML node containing canvas settings.</param>
    /// <returns>The parsed canvas settings.</returns>
    private CanvasSettings ParseCanvas(YamlMappingNode node)
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
        var elements = new List<TemplateElement>();

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

    /// <summary>
    /// Applies common flex-item properties (Width, Height, Grow, Shrink, Basis, Order, AlignSelf)
    /// to a parsed element. Because these properties are declared separately on each element class
    /// rather than on the base <see cref="TemplateElement"/>, a pattern match dispatches to the
    /// correct concrete type.
    /// </summary>
    /// <param name="node">The YAML mapping node containing the property values.</param>
    /// <param name="element">The element to apply flex-item properties to.</param>
    private void ApplyFlexItemProperties(YamlMappingNode node, TemplateElement element)
    {
        var width = GetStringValue(node, "width");
        var height = GetStringValue(node, "height");
        var grow = GetFloatValue(node, "grow", 0f);
        var shrink = GetFloatValue(node, "shrink", 1f);
        var basis = GetStringValue(node, "basis", "auto");
        var order = GetIntValue(node, "order", 0);

        var alignSelfStr = GetStringValue(node, "alignSelf", "auto");
        var alignSelf = alignSelfStr.ToLowerInvariant() switch
        {
            "auto" => AlignSelf.Auto,
            "start" => AlignSelf.Start,
            "center" => AlignSelf.Center,
            "end" => AlignSelf.End,
            "stretch" => AlignSelf.Stretch,
            "baseline" => AlignSelf.Baseline,
            _ => AlignSelf.Auto
        };

        switch (element)
        {
            case FlexElement flex:
                flex.Width = width;
                flex.Height = height;
                flex.Grow = grow;
                flex.Shrink = shrink;
                flex.Basis = basis;
                flex.Order = order;
                flex.AlignSelf = alignSelf;
                break;
            case TextElement text:
                text.Width = width;
                text.Height = height;
                text.Grow = grow;
                text.Shrink = shrink;
                text.Basis = basis;
                text.Order = order;
                text.AlignSelf = alignSelf;
                break;
            case QrElement qr:
                qr.Width = width;
                qr.Height = height;
                qr.Grow = grow;
                qr.Shrink = shrink;
                qr.Basis = basis;
                qr.Order = order;
                qr.AlignSelf = alignSelf;
                break;
            case BarcodeElement barcode:
                barcode.Grow = grow;
                barcode.Shrink = shrink;
                barcode.Basis = basis;
                barcode.Order = order;
                barcode.AlignSelf = alignSelf;
                // NOTE: Width/Height not set because YAML width/height map to BarcodeWidth/BarcodeHeight
                break;
            case ImageElement image:
                image.Grow = grow;
                image.Shrink = shrink;
                image.Basis = basis;
                image.Order = order;
                image.AlignSelf = alignSelf;
                // NOTE: Width/Height not set because YAML width/height map to ImageWidth/ImageHeight
                break;
            case SeparatorElement separator:
                separator.Width = width;
                separator.Height = height;
                separator.Grow = grow;
                separator.Shrink = shrink;
                separator.Basis = basis;
                separator.Order = order;
                separator.AlignSelf = alignSelf;
                break;
        }
    }

    /// <summary>
    /// Parses a text element from YAML.
    /// </summary>
    /// <param name="node">The YAML node containing the text element definition.</param>
    /// <returns>The parsed text element.</returns>
    private TextElement ParseTextElement(YamlMappingNode node)
    {
        var text = new TextElement
        {
            Content = GetStringValue(node, "content", ""),
            Font = GetStringValue(node, "font", "main"),
            Size = GetStringValue(node, "size", "1em"),
            Color = GetStringValue(node, "color", "#000000"),
            Wrap = GetBoolValue(node, "wrap", true),
            MaxLines = GetNullableIntValue(node, "maxLines"),
            Rotate = GetStringValue(node, "rotate", "none"),
            Background = GetStringValue(node, "background"),
            Padding = GetStringValue(node, "padding", "0"),
            Margin = GetStringValue(node, "margin", "0"),
            LineHeight = GetStringValue(node, "lineHeight", "")
        };

        var alignStr = GetStringValue(node, "align", "left");
        text.Align = alignStr.ToLowerInvariant() switch
        {
            "left" => TextAlign.Left,
            "center" => TextAlign.Center,
            "right" => TextAlign.Right,
            _ => TextAlign.Left
        };

        var overflowStr = GetStringValue(node, "overflow", "ellipsis");
        text.Overflow = overflowStr.ToLowerInvariant() switch
        {
            "ellipsis" => TextOverflow.Ellipsis,
            "clip" => TextOverflow.Clip,
            "visible" => TextOverflow.Visible,
            _ => TextOverflow.Ellipsis
        };

        ApplyFlexItemProperties(node, text);
        return text;
    }

    /// <summary>
    /// Parses a flex container element from YAML.
    /// </summary>
    /// <param name="node">The YAML node containing the flex element definition.</param>
    /// <returns>The parsed flex element.</returns>
    private FlexElement ParseFlexElement(YamlMappingNode node)
    {
        var flex = new FlexElement
        {
            Gap = GetStringValue(node, "gap", "0"),
            Padding = GetStringValue(node, "padding", "0"),
            Margin = GetStringValue(node, "margin", "0"),
            Background = GetStringValue(node, "background"),
            Rotate = GetStringValue(node, "rotate", "none")
        };

        var directionStr = GetStringValue(node, "direction", "column");
        flex.Direction = directionStr.ToLowerInvariant() switch
        {
            "row" => FlexDirection.Row,
            "column" => FlexDirection.Column,
            _ => FlexDirection.Column
        };

        var wrapStr = GetStringValue(node, "wrap", "nowrap");
        flex.Wrap = wrapStr.ToLowerInvariant() switch
        {
            "nowrap" => FlexWrap.NoWrap,
            "wrap" => FlexWrap.Wrap,
            "wrap-reverse" => FlexWrap.WrapReverse,
            _ => FlexWrap.NoWrap
        };

        var justifyStr = GetStringValue(node, "justify", "start");
        flex.Justify = justifyStr.ToLowerInvariant() switch
        {
            "start" => JustifyContent.Start,
            "center" => JustifyContent.Center,
            "end" => JustifyContent.End,
            "space-between" => JustifyContent.SpaceBetween,
            "space-around" => JustifyContent.SpaceAround,
            "space-evenly" => JustifyContent.SpaceEvenly,
            _ => JustifyContent.Start
        };

        var alignStr = GetStringValue(node, "align", "stretch");
        flex.Align = alignStr.ToLowerInvariant() switch
        {
            "start" => AlignItems.Start,
            "center" => AlignItems.Center,
            "end" => AlignItems.End,
            "stretch" => AlignItems.Stretch,
            "baseline" => AlignItems.Baseline,
            _ => AlignItems.Stretch
        };

        // Parse children
        if (TryGetSequence(node, "children", out var childrenNode))
        {
            foreach (var child in childrenNode.Children)
            {
                if (child is YamlMappingNode childMapping)
                {
                    var childElement = ParseElement(childMapping);
                    flex.AddChild(childElement);
                }
            }
        }

        ApplyFlexItemProperties(node, flex);
        return flex;
    }

    /// <summary>
    /// Parses a QR code element from YAML.
    /// </summary>
    /// <param name="node">The YAML node containing the QR element definition.</param>
    /// <returns>The parsed QR element.</returns>
    private QrElement ParseQrElement(YamlMappingNode node)
    {
        var qr = new QrElement
        {
            Data = GetStringValue(node, "data", ""),
            Size = GetIntValue(node, "size", 100),
            Foreground = GetStringValue(node, "foreground", "#000000"),
            Background = GetStringValue(node, "background", "#ffffff"),
            Rotate = GetStringValue(node, "rotate", "none"),
            Padding = GetStringValue(node, "padding", "0"),
            Margin = GetStringValue(node, "margin", "0")
        };

        var ecStr = GetStringValue(node, "errorCorrection", "M");
        qr.ErrorCorrection = ecStr.ToUpperInvariant() switch
        {
            "L" => ErrorCorrectionLevel.L,
            "M" => ErrorCorrectionLevel.M,
            "Q" => ErrorCorrectionLevel.Q,
            "H" => ErrorCorrectionLevel.H,
            _ => ErrorCorrectionLevel.M
        };

        ApplyFlexItemProperties(node, qr);
        return qr;
    }

    /// <summary>
    /// Parses a barcode element from YAML.
    /// </summary>
    /// <param name="node">The YAML node containing the barcode element definition.</param>
    /// <returns>The parsed barcode element.</returns>
    private BarcodeElement ParseBarcodeElement(YamlMappingNode node)
    {
        var barcode = new BarcodeElement
        {
            Data = GetStringValue(node, "data", ""),
            BarcodeWidth = GetIntValue(node, "width", 200),
            BarcodeHeight = GetIntValue(node, "height", 80),
            ShowText = GetBoolValue(node, "showText", true),
            Foreground = GetStringValue(node, "foreground", "#000000"),
            Background = GetStringValue(node, "background", "#ffffff"),
            Rotate = GetStringValue(node, "rotate", "none"),
            Padding = GetStringValue(node, "padding", "0"),
            Margin = GetStringValue(node, "margin", "0")
        };

        var formatStr = GetStringValue(node, "format", "code128");
        barcode.Format = formatStr.ToLowerInvariant() switch
        {
            "code128" => BarcodeFormat.Code128,
            "code39" => BarcodeFormat.Code39,
            "ean13" => BarcodeFormat.Ean13,
            "ean8" => BarcodeFormat.Ean8,
            "upc" => BarcodeFormat.Upc,
            _ => BarcodeFormat.Code128
        };

        ApplyFlexItemProperties(node, barcode);
        return barcode;
    }

    /// <summary>
    /// Parses an image element from YAML.
    /// </summary>
    /// <param name="node">The YAML node containing the image element definition.</param>
    /// <returns>The parsed image element.</returns>
    private ImageElement ParseImageElement(YamlMappingNode node)
    {
        var image = new ImageElement
        {
            Src = GetStringValue(node, "src", ""),
            ImageWidth = GetNullableIntValue(node, "width"),
            ImageHeight = GetNullableIntValue(node, "height"),
            Rotate = GetStringValue(node, "rotate", "none"),
            Background = GetStringValue(node, "background"),
            Padding = GetStringValue(node, "padding", "0"),
            Margin = GetStringValue(node, "margin", "0")
        };

        var fitStr = GetStringValue(node, "fit", "contain");
        image.Fit = fitStr.ToLowerInvariant() switch
        {
            "fill" => ImageFit.Fill,
            "contain" => ImageFit.Contain,
            "cover" => ImageFit.Cover,
            "none" => ImageFit.None,
            _ => ImageFit.Contain
        };

        ApplyFlexItemProperties(node, image);
        return image;
    }

    /// <summary>
    /// Parses a separator element from YAML.
    /// </summary>
    /// <param name="node">The YAML node containing the separator element definition.</param>
    /// <returns>The parsed separator element.</returns>
    private SeparatorElement ParseSeparatorElement(YamlMappingNode node)
    {
        var thickness = GetFloatValue(node, "thickness", 1f);
        if (thickness <= 0)
        {
            throw new TemplateParseException(
                $"Separator thickness must be greater than 0. Got {thickness}.");
        }

        var separator = new SeparatorElement
        {
            // NOTE: Color is not validated at parse time. This is consistent with the
            // existing pattern for other elements (e.g., TextElement.Color). Invalid
            // colors fall back to black at render time via ColorParser.Parse.
            Color = GetStringValue(node, "color", "#000000"),
            Thickness = thickness,
            Rotate = GetStringValue(node, "rotate", "none"),
            Background = GetStringValue(node, "background"),
            Padding = GetStringValue(node, "padding", "0"),
            Margin = GetStringValue(node, "margin", "0")
        };

        var orientationStr = GetStringValue(node, "orientation", "horizontal");
        separator.Orientation = orientationStr.ToLowerInvariant() switch
        {
            "horizontal" => SeparatorOrientation.Horizontal,
            "vertical" => SeparatorOrientation.Vertical,
            _ => SeparatorOrientation.Horizontal
        };

        var styleStr = GetStringValue(node, "style", "dotted");
        separator.Style = styleStr.ToLowerInvariant() switch
        {
            "dotted" => SeparatorStyle.Dotted,
            "dashed" => SeparatorStyle.Dashed,
            "solid" => SeparatorStyle.Solid,
            _ => SeparatorStyle.Dotted
        };

        ApplyFlexItemProperties(node, separator);
        return separator;
    }

    #region YAML Helper Methods

    /// <summary>
    /// Tries to get a mapping node from a parent node by key.
    /// </summary>
    /// <param name="parent">The parent mapping node.</param>
    /// <param name="key">The key to look up.</param>
    /// <param name="result">The resulting mapping node if found.</param>
    /// <returns>True if the key exists and is a mapping node; otherwise, false.</returns>
    private static bool TryGetMapping(YamlMappingNode parent, string key, out YamlMappingNode result)
    {
        result = null!;
        var scalarKey = new YamlScalarNode(key);
        if (parent.Children.TryGetValue(scalarKey, out var node) && node is YamlMappingNode mapping)
        {
            result = mapping;
            return true;
        }
        return false;
    }

    /// <summary>
    /// Tries to get a sequence node from a parent node by key.
    /// </summary>
    /// <param name="parent">The parent mapping node.</param>
    /// <param name="key">The key to look up.</param>
    /// <param name="result">The resulting sequence node if found.</param>
    /// <returns>True if the key exists and is a sequence node; otherwise, false.</returns>
    private static bool TryGetSequence(YamlMappingNode parent, string key, out YamlSequenceNode result)
    {
        result = null!;
        var scalarKey = new YamlScalarNode(key);
        if (parent.Children.TryGetValue(scalarKey, out var node) && node is YamlSequenceNode sequence)
        {
            result = sequence;
            return true;
        }
        return false;
    }

    /// <summary>
    /// Gets a string value from a mapping node by key.
    /// </summary>
    /// <param name="node">The mapping node to search.</param>
    /// <param name="key">The key to look up.</param>
    /// <returns>The string value if found; otherwise, null.</returns>
    private static string? GetStringValue(YamlMappingNode node, string key)
    {
        var scalarKey = new YamlScalarNode(key);
        if (node.Children.TryGetValue(scalarKey, out var value) && value is YamlScalarNode scalar)
        {
            return scalar.Value;
        }
        return null;
    }

    /// <summary>
    /// Gets a string value from a mapping node by key with a default value.
    /// </summary>
    /// <param name="node">The mapping node to search.</param>
    /// <param name="key">The key to look up.</param>
    /// <param name="defaultValue">The default value if the key is not found.</param>
    /// <returns>The string value if found; otherwise, the default value.</returns>
    private static string GetStringValue(YamlMappingNode node, string key, string defaultValue)
    {
        return GetStringValue(node, key) ?? defaultValue;
    }

    /// <summary>
    /// Gets an integer value from a mapping node by key with a default value.
    /// </summary>
    /// <param name="node">The mapping node to search.</param>
    /// <param name="key">The key to look up.</param>
    /// <param name="defaultValue">The default value if the key is not found or cannot be parsed.</param>
    /// <returns>The integer value if found and valid; otherwise, the default value.</returns>
    private static int GetIntValue(YamlMappingNode node, string key, int defaultValue)
    {
        var strValue = GetStringValue(node, key);
        if (strValue != null && int.TryParse(strValue, out var intValue))
        {
            return intValue;
        }
        return defaultValue;
    }

    /// <summary>
    /// Gets a float value from a mapping node by key with a default value.
    /// </summary>
    private static float GetFloatValue(YamlMappingNode node, string key, float defaultValue)
    {
        var strValue = GetStringValue(node, key);
        if (strValue != null && float.TryParse(strValue, System.Globalization.NumberStyles.Float,
            System.Globalization.CultureInfo.InvariantCulture, out var floatValue))
        {
            return floatValue;
        }
        return defaultValue;
    }

    /// <summary>
    /// Gets a nullable integer value from a mapping node by key.
    /// </summary>
    /// <param name="node">The mapping node to search.</param>
    /// <param name="key">The key to look up.</param>
    /// <returns>The integer value if found and valid; otherwise, null.</returns>
    private static int? GetNullableIntValue(YamlMappingNode node, string key)
    {
        var strValue = GetStringValue(node, key);
        if (strValue != null && int.TryParse(strValue, out var intValue))
        {
            return intValue;
        }
        return null;
    }

    /// <summary>
    /// Gets a boolean value from a mapping node by key with a default value.
    /// </summary>
    /// <param name="node">The mapping node to search.</param>
    /// <param name="key">The key to look up.</param>
    /// <param name="defaultValue">The default value if the key is not found or cannot be parsed.</param>
    /// <returns>The boolean value if found and valid; otherwise, the default value.</returns>
    private static bool GetBoolValue(YamlMappingNode node, string key, bool defaultValue)
    {
        var strValue = GetStringValue(node, key);
        if (strValue != null && bool.TryParse(strValue, out var boolValue))
        {
            return boolValue;
        }
        return defaultValue;
    }

    #endregion
}
