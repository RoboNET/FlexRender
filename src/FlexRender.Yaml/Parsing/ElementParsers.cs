using System.Globalization;
using FlexRender.Layout;
using FlexRender.Parsing.Ast;
using YamlDotNet.RepresentationModel;
using static FlexRender.Parsing.YamlPropertyHelpers;

namespace FlexRender.Parsing;

/// <summary>
/// Contains parsers for individual template element types (text, flex, qr, barcode, image, separator)
/// and control flow elements (each, if).
/// </summary>
internal sealed class ElementParsers
{
    private readonly Func<YamlMappingNode, TemplateElement> _parseElement;

    /// <summary>
    /// Initializes a new instance of the <see cref="ElementParsers"/> class.
    /// </summary>
    /// <param name="parseElement">
    /// Callback to the parent parser's <c>ParseElement</c> method, used for recursive child parsing
    /// (e.g., flex children, each/if branches).
    /// </param>
    internal ElementParsers(Func<YamlMappingNode, TemplateElement> parseElement)
    {
        ArgumentNullException.ThrowIfNull(parseElement);
        _parseElement = parseElement;
    }

    /// <summary>
    /// Applies common flex-item properties (Width, Height, Grow, Shrink, Basis, Order, AlignSelf)
    /// to a parsed element. These properties are now on the base <see cref="TemplateElement"/> class.
    /// </summary>
    /// <param name="node">The YAML mapping node containing the property values.</param>
    /// <param name="element">The element to apply flex-item properties to.</param>
    internal static void ApplyFlexItemProperties(YamlMappingNode node, TemplateElement element)
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
        // (BarcodeWidth/BarcodeHeight, ImageWidth/ImageHeight) in their element-specific parsers.
        // We also propagate those values to the base flex Width/Height so the layout engine
        // can properly center elements via margin auto or align properties.
        switch (element)
        {
            case BarcodeElement bc:
                // Set flex Width/Height from barcode-specific dimensions for proper layout centering
                if (bc.BarcodeWidth.HasValue)
                    element.Width = bc.BarcodeWidth.Value.ToString(CultureInfo.InvariantCulture);
                if (bc.BarcodeHeight.HasValue)
                    element.Height = bc.BarcodeHeight.Value.ToString(CultureInfo.InvariantCulture);
                break;
            case QrElement qr:
                // QR is square: Size maps to both Width and Height for proper layout centering
                if (qr.Size.HasValue)
                {
                    var sizeStr = qr.Size.Value.ToString(CultureInfo.InvariantCulture);
                    element.Width = sizeStr;
                    element.Height = sizeStr;
                }
                break;
            case ImageElement img:
                // Set flex Width/Height from image-specific dimensions for proper layout centering
                if (img.ImageWidth.HasValue)
                    element.Width = img.ImageWidth.Value.ToString(CultureInfo.InvariantCulture);
                if (img.ImageHeight.HasValue)
                    element.Height = img.ImageHeight.Value.ToString(CultureInfo.InvariantCulture);
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

        // Border properties
        element.Border = GetStringValue(node, "border");
        element.BorderWidth = GetStringValue(node, "border-width") ?? GetStringValue(node, "borderWidth");
        element.BorderColor = GetStringValue(node, "border-color") ?? GetStringValue(node, "borderColor");
        element.BorderStyle = GetStringValue(node, "border-style") ?? GetStringValue(node, "borderStyle");
        element.BorderTop = GetStringValue(node, "border-top") ?? GetStringValue(node, "borderTop");
        element.BorderRight = GetStringValue(node, "border-right") ?? GetStringValue(node, "borderRight");
        element.BorderBottom = GetStringValue(node, "border-bottom") ?? GetStringValue(node, "borderBottom");
        element.BorderLeft = GetStringValue(node, "border-left") ?? GetStringValue(node, "borderLeft");
        element.BorderRadius = GetStringValue(node, "border-radius") ?? GetStringValue(node, "borderRadius");

        var dirStr = GetStringValue(node, "text-direction");
        if (dirStr != null)
        {
            element.TextDirection = dirStr.ToLowerInvariant() switch
            {
                "rtl" => TextDirection.Rtl,
                "ltr" => TextDirection.Ltr,
                _ => null
            };
        }

        // Visual effect properties
        element.Opacity = Math.Clamp(GetFloatValue(node, "opacity", 1.0f), 0.0f, 1.0f);
        element.BoxShadow = GetStringValue(node, "box-shadow") ?? GetStringValue(node, "boxShadow");
    }

    /// <summary>
    /// Parses a text element from YAML.
    /// </summary>
    /// <param name="node">The YAML node containing the text element definition.</param>
    /// <returns>The parsed text element.</returns>
    internal static TemplateElement ParseTextElement(YamlMappingNode node)
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
            "start" => TextAlign.Start,
            "end" => TextAlign.End,
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
    internal TemplateElement ParseFlexElement(YamlMappingNode node)
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
                    var childElement = _parseElement(childMapping);
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
    internal static TemplateElement ParseQrElement(YamlMappingNode node)
    {
        var qr = new QrElement
        {
            Data = GetStringValue(node, "data", ""),
            Size = GetIntValue(node, "size", 100),
            Foreground = GetStringValue(node, "foreground", "#000000"),
            Background = GetStringValue(node, "background"),
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
    internal static TemplateElement ParseBarcodeElement(YamlMappingNode node)
    {
        var barcode = new BarcodeElement
        {
            Data = GetStringValue(node, "data", ""),
            BarcodeWidth = GetIntValue(node, "width", 200),
            BarcodeHeight = GetIntValue(node, "height", 80),
            ShowText = GetBoolValue(node, "showText", true),
            Foreground = GetStringValue(node, "foreground", "#000000"),
            Background = GetStringValue(node, "background"),
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
    internal static TemplateElement ParseImageElement(YamlMappingNode node)
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
    internal static TemplateElement ParseSeparatorElement(YamlMappingNode node)
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
    internal TemplateElement ParseEachElement(YamlMappingNode node)
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
    internal TemplateElement ParseIfElement(YamlMappingNode node)
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
            elseIf = (IfElement)ParseIfElement(elseIfMapping);
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
    internal static (ConditionOperator? Operator, object? CompareValue) ParseConditionOperator(YamlMappingNode node)
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
    /// Parses an SVG element from YAML.
    /// Supports both external SVG files (via src) and inline SVG markup (via content).
    /// </summary>
    /// <param name="node">The YAML node containing the SVG element definition.</param>
    /// <returns>The parsed SVG element.</returns>
    /// <exception cref="TemplateParseException">
    /// Thrown when neither src nor content is specified, or both are specified.
    /// </exception>
    internal static TemplateElement ParseSvgElement(YamlMappingNode node)
    {
        var src = GetStringValue(node, "src");
        var content = GetStringValue(node, "content");

        var hasSrc = !string.IsNullOrEmpty(src);
        var hasContent = !string.IsNullOrEmpty(content);

        if (!hasSrc && !hasContent)
        {
            throw new TemplateParseException(
                "SVG element requires either 'src' or 'content' property.");
        }

        if (hasSrc && hasContent)
        {
            throw new TemplateParseException(
                "SVG element cannot have both 'src' and 'content'. Use one or the other.");
        }

        var svg = new SvgElement
        {
            Src = src,
            Content = content,
            SvgWidth = GetNullableIntValue(node, "width"),
            SvgHeight = GetNullableIntValue(node, "height"),
            Rotate = GetStringValue(node, "rotate", "none"),
            Background = GetStringValue(node, "background"),
            Padding = GetStringValue(node, "padding", "0"),
            Margin = GetStringValue(node, "margin", "0")
        };

        var fitStr = GetStringValue(node, "fit", "contain");
        svg.Fit = fitStr.ToLowerInvariant() switch
        {
            "fill" => ImageFit.Fill,
            "contain" => ImageFit.Contain,
            "cover" => ImageFit.Cover,
            "none" => ImageFit.None,
            _ => ImageFit.Contain
        };

        ApplyFlexItemProperties(node, svg);
        return svg;
    }

    /// <summary>
    /// Parses a table element from YAML.
    /// Supports both dynamic (data-bound) and static (hardcoded rows) tables.
    /// </summary>
    /// <param name="node">The YAML node containing the table element definition.</param>
    /// <returns>The parsed table element.</returns>
    /// <exception cref="TemplateParseException">
    /// Thrown when columns are missing, or both array and rows are specified.
    /// </exception>
    internal static TemplateElement ParseTableElement(YamlMappingNode node)
    {
        // Parse columns (required)
        if (!TryGetSequence(node, "columns", out var columnsNode))
        {
            throw new TemplateParseException("Table element requires 'columns' property");
        }

        var columns = ParseTableColumns(columnsNode);
        if (columns.Count == 0)
        {
            throw new TemplateParseException("Table element must have at least one column");
        }

        // Parse data source: array (dynamic) or rows (static) -- mutually exclusive
        var arrayPath = GetStringValue(node, "array");
        var hasRows = TryGetSequence(node, "rows", out var rowsNode);

        if (!string.IsNullOrEmpty(arrayPath) && hasRows)
        {
            throw new TemplateParseException("Table element cannot have both 'array' and 'rows' properties");
        }

        IReadOnlyList<TableRow> rows = hasRows ? ParseTableRows(rowsNode, columns) : Array.Empty<TableRow>();

        var table = new TableElement(columns, rows)
        {
            ArrayPath = arrayPath,
            ItemVariable = GetStringValue(node, "as"),
            Font = GetStringValue(node, "font", "main"),
            Size = GetStringValue(node, "size", "1em"),
            Color = GetStringValue(node, "color", "#000000"),
            RowGap = GetStringValue(node, "rowGap") ?? GetStringValue(node, "row-gap"),
            ColumnGap = GetStringValue(node, "columnGap") ?? GetStringValue(node, "column-gap"),
            HeaderFont = GetStringValue(node, "headerFont") ?? GetStringValue(node, "header-font"),
            HeaderColor = GetStringValue(node, "headerColor") ?? GetStringValue(node, "header-color"),
            HeaderSize = GetStringValue(node, "headerSize") ?? GetStringValue(node, "header-size"),
            HeaderBorderBottom = GetStringValue(node, "headerBorderBottom") ?? GetStringValue(node, "header-border-bottom"),
            Rotate = GetStringValue(node, "rotate", "none"),
            Background = GetStringValue(node, "background"),
            Padding = GetStringValue(node, "padding", "0"),
            Margin = GetStringValue(node, "margin", "0")
        };

        ApplyFlexItemProperties(node, table);
        return table;
    }

    /// <summary>
    /// Parses a YAML sequence of column definitions into <see cref="TableColumn"/> objects.
    /// </summary>
    /// <param name="sequence">The YAML sequence node containing column mappings.</param>
    /// <returns>A list of parsed table columns.</returns>
    private static List<TableColumn> ParseTableColumns(YamlSequenceNode sequence)
    {
        var columns = new List<TableColumn>(sequence.Children.Count);

        foreach (var child in sequence.Children)
        {
            if (child is not YamlMappingNode colNode)
            {
                continue;
            }

            var column = new TableColumn
            {
                Key = GetStringValue(colNode, "key", ""),
                Label = GetStringValue(colNode, "label"),
                Width = GetStringValue(colNode, "width"),
                Grow = GetFloatValue(colNode, "grow", 0f),
                Font = GetStringValue(colNode, "font"),
                Color = GetStringValue(colNode, "color"),
                Size = GetStringValue(colNode, "size"),
                Format = GetStringValue(colNode, "format")
            };

            var alignStr = GetStringValue(colNode, "align", "left");
            column.Align = alignStr.ToLowerInvariant() switch
            {
                "left" => TextAlign.Left,
                "center" => TextAlign.Center,
                "right" => TextAlign.Right,
                "start" => TextAlign.Start,
                "end" => TextAlign.End,
                _ => TextAlign.Left
            };

            columns.Add(column);
        }

        return columns;
    }

    /// <summary>
    /// Parses a YAML sequence of static row definitions into <see cref="TableRow"/> objects.
    /// Each row is a YAML mapping where keys match column keys.
    /// </summary>
    /// <param name="sequence">The YAML sequence node containing row mappings.</param>
    /// <param name="columns">The column definitions for key reference.</param>
    /// <returns>A list of parsed table rows.</returns>
    private static List<TableRow> ParseTableRows(YamlSequenceNode sequence, IReadOnlyList<TableColumn> columns)
    {
        var rows = new List<TableRow>(sequence.Children.Count);

        foreach (var child in sequence.Children)
        {
            if (child is not YamlMappingNode rowNode)
            {
                continue;
            }

            var row = new TableRow
            {
                Font = GetStringValue(rowNode, "font"),
                Color = GetStringValue(rowNode, "color"),
                Size = GetStringValue(rowNode, "size")
            };

            // Extract cell values matching column keys
            foreach (var column in columns)
            {
                var value = GetStringValue(rowNode, column.Key);
                if (value != null)
                {
                    row.Values[column.Key] = value;
                }
            }

            rows.Add(row);
        }

        return rows;
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

        var elements = new List<TemplateElement>(sequence.Children.Count);
        foreach (var child in sequence.Children)
        {
            if (child is YamlMappingNode elementNode)
            {
                var element = _parseElement(elementNode);
                elements.Add(element);
            }
        }
        return elements;
    }
}
