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
            ["separator"] = ParseSeparatorElement,
            ["each"] = ParseEachElement,
            ["if"] = ParseIfElement
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

    /// <summary>
    /// Applies common flex-item properties (Width, Height, Grow, Shrink, Basis, Order, AlignSelf)
    /// to a parsed element. These properties are now on the base <see cref="TemplateElement"/> class.
    /// </summary>
    /// <param name="node">The YAML mapping node containing the property values.</param>
    /// <param name="element">The element to apply flex-item properties to.</param>
    private static void ApplyFlexItemProperties(YamlMappingNode node, TemplateElement element)
    {
        element.Grow = GetFloatValue(node, "grow", 0f);
        element.Shrink = GetFloatValue(node, "shrink", 1f);
        element.Basis = GetStringValue(node, "basis", "auto");
        element.Order = GetIntValue(node, "order", 0);

        var displayStr = GetStringValue(node, "display", "flex");
        element.Display = displayStr.ToLowerInvariant() switch
        {
            "flex" => Display.Flex,
            "none" => Display.None,
            _ => Display.Flex
        };

        var alignSelfStr = GetStringValue(node, "alignSelf", "auto");
        element.AlignSelf = alignSelfStr.ToLowerInvariant() switch
        {
            "auto" => AlignSelf.Auto,
            "start" => AlignSelf.Start,
            "center" => AlignSelf.Center,
            "end" => AlignSelf.End,
            "stretch" => AlignSelf.Stretch,
            "baseline" => AlignSelf.Baseline,
            _ => AlignSelf.Auto
        };

        // Width/Height: Barcode and Image YAML width/height map to content-specific properties
        // (BarcodeWidth/BarcodeHeight, ImageWidth/ImageHeight), NOT to the base flex Width/Height.
        // This preserves backward compatibility.
        switch (element)
        {
            case BarcodeElement:
            case ImageElement:
                // width/height already parsed in element-specific parsers
                break;
            default:
                element.Width = GetStringValue(node, "width");
                element.Height = GetStringValue(node, "height");
                break;
        }

        // Min/Max constraints: support both camelCase and kebab-case
        element.MinWidth = GetStringValue(node, "min-width") ?? GetStringValue(node, "minWidth");
        element.MaxWidth = GetStringValue(node, "max-width") ?? GetStringValue(node, "maxWidth");
        element.MinHeight = GetStringValue(node, "min-height") ?? GetStringValue(node, "minHeight");
        element.MaxHeight = GetStringValue(node, "max-height") ?? GetStringValue(node, "maxHeight");

        // Position properties
        var positionStr = GetStringValue(node, "position", "static");
        element.Position = positionStr.ToLowerInvariant() switch
        {
            "static" => Position.Static,
            "relative" => Position.Relative,
            "absolute" => Position.Absolute,
            _ => Position.Static
        };

        element.Top = GetStringValue(node, "top");
        element.Right = GetStringValue(node, "right");
        element.Bottom = GetStringValue(node, "bottom");
        element.Left = GetStringValue(node, "left");

        // Aspect ratio
        element.AspectRatio = GetNullableFloatValue(node, "aspectRatio")
                              ?? GetNullableFloatValue(node, "aspect-ratio");
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
            "row-reverse" => FlexDirection.RowReverse,
            "column-reverse" => FlexDirection.ColumnReverse,
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

        var alignContentStr = GetStringValue(node, "align-content") ?? GetStringValue(node, "alignContent");
        if (alignContentStr != null)
        {
            flex.AlignContent = alignContentStr.ToLowerInvariant() switch
            {
                "start" => AlignContent.Start,
                "center" => AlignContent.Center,
                "end" => AlignContent.End,
                "stretch" => AlignContent.Stretch,
                "space-between" => AlignContent.SpaceBetween,
                "space-around" => AlignContent.SpaceAround,
                "space-evenly" => AlignContent.SpaceEvenly,
                _ => AlignContent.Stretch
            };
        }

        flex.RowGap = GetStringValue(node, "row-gap") ?? GetStringValue(node, "rowGap");
        flex.ColumnGap = GetStringValue(node, "column-gap") ?? GetStringValue(node, "columnGap");

        var overflowStr = GetStringValue(node, "overflow", "visible");
        flex.Overflow = overflowStr.ToLowerInvariant() switch
        {
            "visible" => Overflow.Visible,
            "hidden" => Overflow.Hidden,
            _ => Overflow.Visible
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

    /// <summary>
    /// Parses an each (loop) element from YAML.
    /// </summary>
    /// <param name="node">The YAML node containing the each element definition.</param>
    /// <returns>The parsed each element.</returns>
    /// <exception cref="TemplateParseException">Thrown when the required 'array' property is missing.</exception>
    private EachElement ParseEachElement(YamlMappingNode node)
    {
        var arrayPath = GetStringValue(node, "array");
        if (string.IsNullOrEmpty(arrayPath))
        {
            throw new TemplateParseException("Each element requires 'array' property");
        }

        var itemVariable = GetStringValue(node, "as");
        var children = ParseChildren(node, "children");

        return new EachElement(children)
        {
            ArrayPath = arrayPath,
            ItemVariable = itemVariable
        };
    }

    /// <summary>
    /// Parses a conditional (if) element from YAML.
    /// </summary>
    /// <param name="node">The YAML node containing the if element definition.</param>
    /// <returns>The parsed if element.</returns>
    /// <exception cref="TemplateParseException">Thrown when the required 'condition' property is missing.</exception>
    private IfElement ParseIfElement(YamlMappingNode node)
    {
        var conditionPath = GetStringValue(node, "condition");
        if (string.IsNullOrEmpty(conditionPath))
        {
            throw new TemplateParseException("If element requires 'condition' property");
        }

        var (op, compareValue) = ParseConditionOperator(node);

        var thenBranch = ParseChildren(node, "then");
        var elseBranch = ParseChildren(node, "else");

        IfElement? elseIf = null;
        if (node.Children.TryGetValue(new YamlScalarNode("elseIf"), out var elseIfNode) && elseIfNode is YamlMappingNode elseIfMapping)
        {
            elseIf = ParseIfElement(elseIfMapping);
        }

        return new IfElement(thenBranch, elseBranch)
        {
            ConditionPath = conditionPath,
            Operator = op,
            CompareValue = compareValue,
            ElseIf = elseIf
        };
    }

    /// <summary>
    /// Parses the condition operator and compare value from a YAML node.
    /// Only one operator key is allowed per condition.
    /// </summary>
    /// <param name="node">The YAML node containing the condition.</param>
    /// <returns>A tuple of the operator (null for truthy check) and the compare value.</returns>
    private static (ConditionOperator? Operator, object? CompareValue) ParseConditionOperator(YamlMappingNode node)
    {
        // Check for equals/notEquals (string comparison)
        var equalsValue = GetStringValue(node, "equals");
        if (equalsValue != null)
        {
            return (ConditionOperator.Equals, equalsValue);
        }

        var notEqualsValue = GetStringValue(node, "notEquals");
        if (notEqualsValue != null)
        {
            return (ConditionOperator.NotEquals, notEqualsValue);
        }

        // Check for in/notIn (array of strings)
        if (TryGetSequence(node, "in", out var inSequence))
        {
            var inValues = ParseStringArray(inSequence);
            return (ConditionOperator.In, inValues);
        }

        if (TryGetSequence(node, "notIn", out var notInSequence))
        {
            var notInValues = ParseStringArray(notInSequence);
            return (ConditionOperator.NotIn, notInValues);
        }

        // Check for contains (string)
        var containsValue = GetStringValue(node, "contains");
        if (containsValue != null)
        {
            return (ConditionOperator.Contains, containsValue);
        }

        // Check for numeric comparisons
        var greaterThanValue = GetDoubleValue(node, "greaterThan");
        if (greaterThanValue.HasValue)
        {
            return (ConditionOperator.GreaterThan, greaterThanValue.Value);
        }

        var greaterThanOrEqualValue = GetDoubleValue(node, "greaterThanOrEqual");
        if (greaterThanOrEqualValue.HasValue)
        {
            return (ConditionOperator.GreaterThanOrEqual, greaterThanOrEqualValue.Value);
        }

        var lessThanValue = GetDoubleValue(node, "lessThan");
        if (lessThanValue.HasValue)
        {
            return (ConditionOperator.LessThan, lessThanValue.Value);
        }

        var lessThanOrEqualValue = GetDoubleValue(node, "lessThanOrEqual");
        if (lessThanOrEqualValue.HasValue)
        {
            return (ConditionOperator.LessThanOrEqual, lessThanOrEqualValue.Value);
        }

        // Check for hasItems (bool)
        var hasItemsValue = GetNullableBoolValue(node, "hasItems");
        if (hasItemsValue.HasValue)
        {
            return (ConditionOperator.HasItems, hasItemsValue.Value);
        }

        // Check for count comparisons
        var countEqualsValue = GetNullableIntValue(node, "countEquals");
        if (countEqualsValue.HasValue)
        {
            return (ConditionOperator.CountEquals, countEqualsValue.Value);
        }

        var countGreaterThanValue = GetNullableIntValue(node, "countGreaterThan");
        if (countGreaterThanValue.HasValue)
        {
            return (ConditionOperator.CountGreaterThan, countGreaterThanValue.Value);
        }

        // No operator specified - truthy check
        return (null, null);
    }

    /// <summary>
    /// Parses a YAML sequence node into an array of strings.
    /// </summary>
    /// <param name="sequence">The YAML sequence node.</param>
    /// <returns>A list of strings.</returns>
    private static List<string> ParseStringArray(YamlSequenceNode sequence)
    {
        var result = new List<string>(sequence.Children.Count);
        foreach (var child in sequence.Children)
        {
            if (child is YamlScalarNode scalar && scalar.Value != null)
            {
                result.Add(scalar.Value);
            }
        }
        return result;
    }

    /// <summary>
    /// Parses a sequence of child elements from a named key in a YAML mapping node.
    /// </summary>
    /// <param name="node">The parent YAML mapping node.</param>
    /// <param name="key">The key containing the child sequence.</param>
    /// <returns>A list of parsed child elements, or an empty list if the key doesn't exist.</returns>
    private IReadOnlyList<TemplateElement> ParseChildren(YamlMappingNode node, string key)
    {
        if (!node.Children.TryGetValue(new YamlScalarNode(key), out var childrenNode))
        {
            return Array.Empty<TemplateElement>();
        }

        if (childrenNode is not YamlSequenceNode sequence)
        {
            return Array.Empty<TemplateElement>();
        }

        return ParseElements(sequence);
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

    /// <summary>
    /// Gets a nullable boolean value from a mapping node by key.
    /// </summary>
    /// <param name="node">The mapping node to search.</param>
    /// <param name="key">The key to look up.</param>
    /// <returns>The boolean value if found and valid; otherwise, null.</returns>
    private static bool? GetNullableBoolValue(YamlMappingNode node, string key)
    {
        var strValue = GetStringValue(node, key);
        if (strValue != null && bool.TryParse(strValue, out var boolValue))
        {
            return boolValue;
        }
        return null;
    }

    /// <summary>
    /// Gets a nullable float value from a mapping node by key.
    /// </summary>
    /// <param name="node">The mapping node to search.</param>
    /// <param name="key">The key to look up.</param>
    /// <returns>The float value if found and valid; otherwise, null.</returns>
    private static float? GetNullableFloatValue(YamlMappingNode node, string key)
    {
        var strValue = GetStringValue(node, key);
        if (strValue != null && float.TryParse(strValue, System.Globalization.NumberStyles.Float,
            System.Globalization.CultureInfo.InvariantCulture, out var floatValue))
        {
            return floatValue;
        }
        return null;
    }

    /// <summary>
    /// Gets a nullable double value from a mapping node by key.
    /// </summary>
    /// <param name="node">The mapping node to search.</param>
    /// <param name="key">The key to look up.</param>
    /// <returns>The double value if found and valid; otherwise, null.</returns>
    private static double? GetDoubleValue(YamlMappingNode node, string key)
    {
        var strValue = GetStringValue(node, key);
        if (strValue != null && double.TryParse(strValue, System.Globalization.NumberStyles.Float,
            System.Globalization.CultureInfo.InvariantCulture, out var doubleValue))
        {
            return doubleValue;
        }
        return null;
    }

    #endregion
}
