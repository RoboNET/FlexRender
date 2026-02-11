using System.Globalization;
using FlexRender.Configuration;
using FlexRender.Parsing.Ast;

namespace FlexRender.TemplateEngine;

/// <summary>
/// Expands control flow elements (Each, If) into concrete template elements.
/// Enables template caching by separating parsing from data binding.
/// </summary>
public sealed class TemplateExpander
{
    private readonly ResourceLimits _limits;
    private readonly InlineExpressionEvaluator? _expressionEvaluator;

    /// <summary>
    /// Creates a new TemplateExpander with default resource limits.
    /// </summary>
    public TemplateExpander() : this(new ResourceLimits())
    {
    }

    /// <summary>
    /// Creates a new TemplateExpander with the specified resource limits.
    /// </summary>
    /// <param name="limits">Resource limits for expansion depth protection.</param>
    /// <exception cref="ArgumentNullException">Thrown when limits is null.</exception>
    public TemplateExpander(ResourceLimits limits)
    {
        ArgumentNullException.ThrowIfNull(limits);
        _limits = limits;
        _expressionEvaluator = new InlineExpressionEvaluator();
    }

    /// <summary>
    /// Creates a new TemplateExpander with the specified resource limits and filter registry.
    /// </summary>
    /// <param name="limits">Resource limits for expansion depth protection.</param>
    /// <param name="filterRegistry">The filter registry for expression evaluation.</param>
    /// <exception cref="ArgumentNullException">Thrown when limits or filterRegistry is null.</exception>
    public TemplateExpander(ResourceLimits limits, FilterRegistry filterRegistry)
        : this(limits, filterRegistry, CultureInfo.InvariantCulture)
    {
    }

    /// <summary>
    /// Creates a new TemplateExpander with the specified resource limits, filter registry, and culture.
    /// </summary>
    /// <param name="limits">Resource limits for expansion depth protection.</param>
    /// <param name="filterRegistry">The filter registry for expression evaluation.</param>
    /// <param name="culture">The culture to use for culture-aware filter formatting.</param>
    /// <exception cref="ArgumentNullException">Thrown when any parameter is null.</exception>
    public TemplateExpander(ResourceLimits limits, FilterRegistry filterRegistry, CultureInfo culture)
    {
        ArgumentNullException.ThrowIfNull(limits);
        ArgumentNullException.ThrowIfNull(filterRegistry);
        ArgumentNullException.ThrowIfNull(culture);
        _limits = limits;
        _expressionEvaluator = new InlineExpressionEvaluator(filterRegistry, culture);
    }

    /// <summary>
    /// Expands EachElement and IfElement instances into concrete elements based on data.
    /// Returns a new Template with all control flow elements resolved.
    /// </summary>
    /// <param name="template">The template containing control flow elements.</param>
    /// <param name="data">The data for evaluating conditions and iterating arrays.</param>
    /// <returns>A new Template with expanded elements.</returns>
    /// <exception cref="ArgumentNullException">Thrown when template or data is null.</exception>
    /// <exception cref="TemplateEngineException">Thrown when maximum expansion depth is exceeded.</exception>
    public Template Expand(Template template, ObjectValue data)
    {
        ArgumentNullException.ThrowIfNull(template);
        ArgumentNullException.ThrowIfNull(data);

        var context = new TemplateContext(data);
        var expandedElements = ExpandElements(template.Elements, context, 0);

        var result = new Template
        {
            Name = template.Name,
            Version = template.Version,
            Canvas = template.Canvas,
            Elements = expandedElements
        };

        // Copy fonts
        foreach (var font in template.Fonts)
        {
            result.Fonts[font.Key] = font.Value;
        }

        return result;
    }

    private List<TemplateElement> ExpandElements(IReadOnlyList<TemplateElement> elements, TemplateContext context, int depth)
    {
        if (depth > _limits.MaxRenderDepth)
        {
            throw new TemplateEngineException($"Maximum expansion depth ({_limits.MaxRenderDepth}) exceeded");
        }

        var result = new List<TemplateElement>(elements.Count);

        foreach (var element in elements)
        {
            var expanded = ExpandElement(element, context, depth);
            result.AddRange(expanded);
        }

        return result;
    }

    private IEnumerable<TemplateElement> ExpandElement(TemplateElement element, TemplateContext context, int depth)
    {
        return element switch
        {
            EachElement each => ExpandEach(each, context, depth),
            IfElement ifEl => ExpandIf(ifEl, context, depth),
            TableElement table => [ExpandTable(table, context, depth)],
            FlexElement flex => [ExpandFlex(flex, context, depth)],
            _ => [CloneWithVariableSubstitution(element, context)]
        };
    }

    private IEnumerable<TemplateElement> ExpandEach(EachElement each, TemplateContext context, int depth)
    {
        // Check depth limit - Each element increases nesting depth
        var childDepth = depth + 1;
        if (childDepth > _limits.MaxRenderDepth)
        {
            throw new TemplateEngineException($"Maximum expansion depth ({_limits.MaxRenderDepth}) exceeded");
        }

        // Pre-validate nested control flow depth before checking data
        ValidateNestedDepth(each.ItemTemplate, childDepth);

        var arrayValue = ExpressionEvaluator.Resolve(each.ArrayPath, context);

        if (arrayValue is not ArrayValue array)
        {
            yield break;
        }

        var count = array.Count;
        for (var i = 0; i < count; i++)
        {
            var item = array[i];

            // Push item scope
            if (each.ItemVariable != null)
            {
                // Create a new scope with the item variable
                var scopeData = new ObjectValue
                {
                    [each.ItemVariable] = item
                };
                context.PushScope(scopeData);
            }
            else
            {
                context.PushScope(item);
            }

            context.SetLoopVariables(i, count);

            // Expand children
            var expandedChildren = ExpandElements(each.ItemTemplate, context, childDepth);

            // Pop scope
            context.ClearLoopVariables();
            context.PopScope();

            foreach (var child in expandedChildren)
            {
                yield return child;
            }
        }
    }

    private void ValidateNestedDepth(IReadOnlyList<TemplateElement> elements, int depth)
    {
        if (depth > _limits.MaxRenderDepth)
        {
            throw new TemplateEngineException($"Maximum expansion depth ({_limits.MaxRenderDepth}) exceeded");
        }

        foreach (var element in elements)
        {
            switch (element)
            {
                case EachElement each:
                    ValidateNestedDepth(each.ItemTemplate, depth + 1);
                    break;
                case IfElement ifEl:
                    ValidateNestedDepth(ifEl.ThenBranch, depth + 1);
                    ValidateNestedDepth(ifEl.ElseBranch, depth + 1);
                    if (ifEl.ElseIf != null)
                    {
                        ValidateNestedDepth(new[] { ifEl.ElseIf }, depth);
                    }
                    break;
                case FlexElement flex:
                    ValidateNestedDepth(flex.Children, depth + 1);
                    break;
            }
        }
    }

    private IEnumerable<TemplateElement> ExpandIf(IfElement ifEl, TemplateContext context, int depth)
    {
        // Check depth limit - If element increases nesting depth
        var childDepth = depth + 1;
        if (childDepth > _limits.MaxRenderDepth)
        {
            throw new TemplateEngineException($"Maximum expansion depth ({_limits.MaxRenderDepth}) exceeded");
        }

        // Pre-validate nested control flow depth before checking data
        ValidateNestedDepth(ifEl.ThenBranch, childDepth);
        ValidateNestedDepth(ifEl.ElseBranch, childDepth);
        if (ifEl.ElseIf != null)
        {
            ValidateNestedDepth(new[] { ifEl.ElseIf }, depth);
        }

        if (EvaluateCondition(ifEl, context))
        {
            return ExpandElements(ifEl.ThenBranch, context, childDepth);
        }

        // Check else-if chain
        if (ifEl.ElseIf != null)
        {
            return ExpandIf(ifEl.ElseIf, context, depth + 1);
        }

        // Return else branch
        return ExpandElements(ifEl.ElseBranch, context, childDepth);
    }

    private bool EvaluateCondition(IfElement ifEl, TemplateContext context)
    {
        // Check if the condition contains an inline expression
        TemplateValue value;
        if (_expressionEvaluator is not null && InlineExpressionParser.NeedsFullParsing(ifEl.ConditionPath))
        {
            var ast = InlineExpressionParser.Parse(ifEl.ConditionPath);
            value = _expressionEvaluator.Evaluate(ast, context);
        }
        else
        {
            value = ExpressionEvaluator.Resolve(ifEl.ConditionPath, context);
        }

        if (ifEl.Operator == null)
        {
            // Truthy check
            return ExpressionEvaluator.IsTruthy(value);
        }

        return ifEl.Operator.Value switch
        {
            ConditionOperator.Equals => AreEqual(value, ifEl.CompareValue),
            ConditionOperator.NotEquals => !AreEqual(value, ifEl.CompareValue),
            ConditionOperator.In => IsIn(value, ifEl.CompareValue as IEnumerable<string>),
            ConditionOperator.NotIn => !IsIn(value, ifEl.CompareValue as IEnumerable<string>),
            ConditionOperator.Contains => ArrayContains(value, ifEl.CompareValue),
            ConditionOperator.GreaterThan => CompareNumeric(value, ifEl.CompareValue) > 0,
            ConditionOperator.GreaterThanOrEqual => CompareNumeric(value, ifEl.CompareValue) >= 0,
            ConditionOperator.LessThan => CompareNumeric(value, ifEl.CompareValue) < 0,
            ConditionOperator.LessThanOrEqual => CompareNumeric(value, ifEl.CompareValue) <= 0,
            ConditionOperator.HasItems => HasItems(value, ifEl.CompareValue),
            ConditionOperator.CountEquals => GetCount(value) == Convert.ToInt32(ifEl.CompareValue, CultureInfo.InvariantCulture),
            ConditionOperator.CountGreaterThan => GetCount(value) > Convert.ToInt32(ifEl.CompareValue, CultureInfo.InvariantCulture),
            _ => false
        };
    }

    /// <summary>
    /// Performs universal equality comparison between a template value and a compare value.
    /// Supports strings, numbers, booleans, arrays, and null.
    /// </summary>
    /// <param name="templateValue">The template value to compare.</param>
    /// <param name="compareValue">The comparison value (can be string, double, bool, or null).</param>
    /// <returns>True if the values are equal; otherwise, false.</returns>
    private static bool AreEqual(TemplateValue? templateValue, object? compareValue)
    {
        // Handle null comparisons
        if (templateValue is NullValue || templateValue == null)
        {
            return compareValue == null;
        }

        if (compareValue == null)
        {
            return false;
        }

        return templateValue switch
        {
            StringValue s => s.Value == compareValue.ToString(),
            NumberValue n when compareValue is double d => n.Value == (decimal)d,
            NumberValue n when compareValue is string str && double.TryParse(str, NumberStyles.Float, CultureInfo.InvariantCulture, out var d) => n.Value == (decimal)d,
            NumberValue n => n.Value.ToString(CultureInfo.InvariantCulture) == compareValue.ToString(),
            BoolValue b when compareValue is bool boolCompare => b.Value == boolCompare,
            BoolValue b when compareValue is string str => b.Value.ToString().Equals(str, StringComparison.OrdinalIgnoreCase),
            ArrayValue arr when compareValue is ArrayValue arrCompare => arr.Equals(arrCompare),
            _ => false
        };
    }

    /// <summary>
    /// Checks if a value is in a list of strings.
    /// </summary>
    /// <param name="templateValue">The template value to check.</param>
    /// <param name="list">The list of strings to check against.</param>
    /// <returns>True if the value is in the list; otherwise, false.</returns>
    private static bool IsIn(TemplateValue? templateValue, IEnumerable<string>? list)
    {
        if (list == null)
        {
            return false;
        }

        var stringValue = templateValue switch
        {
            StringValue s => s.Value,
            NumberValue n => n.Value.ToString(CultureInfo.InvariantCulture),
            BoolValue b => b.Value.ToString().ToLowerInvariant(),
            NullValue => null,
            _ => null
        };

        if (stringValue == null)
        {
            return false;
        }

        return list.Contains(stringValue, StringComparer.Ordinal);
    }

    /// <summary>
    /// Checks if an array contains a specified element.
    /// </summary>
    /// <param name="templateValue">The template value (expected to be an array).</param>
    /// <param name="element">The element to search for.</param>
    /// <returns>True if the array contains the element; otherwise, false.</returns>
    private static bool ArrayContains(TemplateValue? templateValue, object? element)
    {
        if (templateValue is not ArrayValue array || element == null)
        {
            return false;
        }

        var searchString = element.ToString();

        foreach (var item in array)
        {
            var itemString = item switch
            {
                StringValue s => s.Value,
                NumberValue n => n.Value.ToString(CultureInfo.InvariantCulture),
                BoolValue b => b.Value.ToString().ToLowerInvariant(),
                _ => null
            };

            if (itemString != null && itemString == searchString)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Compares a numeric template value against a comparison value.
    /// </summary>
    /// <param name="templateValue">The template value to compare.</param>
    /// <param name="compareValue">The comparison value (expected to be double).</param>
    /// <returns>-1 if less, 0 if equal, 1 if greater; or 0 if comparison is not possible.</returns>
    private static int CompareNumeric(TemplateValue? templateValue, object? compareValue)
    {
        if (templateValue is not NumberValue numberValue)
        {
            return 0;
        }

        double compareDouble;
        if (compareValue is double d)
        {
            compareDouble = d;
        }
        else if (compareValue != null && double.TryParse(compareValue.ToString(), NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed))
        {
            compareDouble = parsed;
        }
        else
        {
            return 0;
        }

        return numberValue.Value.CompareTo((decimal)compareDouble);
    }

    /// <summary>
    /// Checks if an array has items based on the expected boolean value.
    /// </summary>
    /// <param name="templateValue">The template value (expected to be an array).</param>
    /// <param name="expectedHasItems">True to check if array has items, false to check if empty.</param>
    /// <returns>True if the condition matches; otherwise, false.</returns>
    private static bool HasItems(TemplateValue? templateValue, object? expectedHasItems)
    {
        if (templateValue is not ArrayValue array)
        {
            return false;
        }

        var hasItems = array.Count > 0;

        if (expectedHasItems is bool expected)
        {
            return hasItems == expected;
        }

        // Default behavior: check if has items
        return hasItems;
    }

    /// <summary>
    /// Gets the count of items in an array.
    /// </summary>
    /// <param name="templateValue">The template value (expected to be an array).</param>
    /// <returns>The count of items, or -1 if not an array.</returns>
    private static int GetCount(TemplateValue? templateValue)
    {
        if (templateValue is ArrayValue array)
        {
            return array.Count;
        }

        return -1;
    }

    private FlexElement ExpandTable(TableElement table, TemplateContext context, int depth)
    {
        var childDepth = depth + 1;
        if (childDepth > _limits.MaxRenderDepth)
        {
            throw new TemplateEngineException($"Maximum expansion depth ({_limits.MaxRenderDepth}) exceeded");
        }

        // Build the outer column container
        var outerFlex = new FlexElement
        {
            Direction = Layout.FlexDirection.Column,
            Rotate = table.Rotate,
            Background = SubstituteVariables(table.Background.Value, context),
            Padding = table.Padding,
            Margin = table.Margin
        };

        if (table.RowGap != null)
        {
            outerFlex.Gap = table.RowGap;
        }

        TemplateElement.CopyBaseProperties(table, outerFlex);

        // Build header row if any column has a label
        var hasHeaders = false;
        foreach (var col in table.Columns)
        {
            if (col.Label != null)
            {
                hasHeaders = true;
                break;
            }
        }

        if (hasHeaders)
        {
            var headerRow = BuildHeaderRow(table, context);
            outerFlex.AddChild(headerRow);

            // Add header border bottom separator if specified
            if (table.HeaderBorderBottom != null)
            {
                var separator = new SeparatorElement
                {
                    Orientation = SeparatorOrientation.Horizontal,
                    Color = table.HeaderColor ?? table.Color,
                    Thickness = 1f
                };

                var styleLower = table.HeaderBorderBottom.ToLowerInvariant();
                separator.Style = styleLower switch
                {
                    "solid" => SeparatorStyle.Solid,
                    "dashed" => SeparatorStyle.Dashed,
                    "dotted" => SeparatorStyle.Dotted,
                    "true" => SeparatorStyle.Dotted,
                    _ => SeparatorStyle.Dotted
                };

                outerFlex.AddChild(separator);
            }
        }

        // Build data rows
        if (!string.IsNullOrEmpty(table.ArrayPath))
        {
            // Dynamic table: wrap rows in an EachElement
            var rowTemplate = BuildRowTemplate(table);
            var each = new EachElement(new List<TemplateElement> { rowTemplate })
            {
                ArrayPath = table.ArrayPath,
                ItemVariable = table.ItemVariable
            };

            // Expand the each element inline
            var expandedRows = ExpandEach(each, context, childDepth);
            foreach (var row in expandedRows)
            {
                outerFlex.AddChild(row);
            }
        }
        else
        {
            // Static table: create a FlexElement row for each static row
            foreach (var row in table.Rows)
            {
                var rowFlex = BuildStaticRow(table, row, context);
                outerFlex.AddChild(rowFlex);
            }
        }

        return outerFlex;
    }

    private FlexElement BuildHeaderRow(TableElement table, TemplateContext context)
    {
        var headerRow = new FlexElement
        {
            Direction = Layout.FlexDirection.Row
        };

        if (table.ColumnGap != null)
        {
            headerRow.Gap = table.ColumnGap;
        }

        foreach (var col in table.Columns)
        {
            var headerCell = new TextElement
            {
                Content = SubstituteVariables(col.Label ?? "", context),
                Font = table.HeaderFont ?? col.Font ?? table.Font,
                Color = table.HeaderColor ?? col.Color ?? table.Color,
                Size = table.HeaderSize ?? col.Size ?? table.Size,
                Align = col.Align
            };

            if (col.Width != null)
            {
                headerCell.Width = col.Width;
            }

            if (col.Grow > 0)
            {
                headerCell.Grow = col.Grow;
            }

            headerRow.AddChild(headerCell);
        }

        return headerRow;
    }

    private static FlexElement BuildRowTemplate(TableElement table)
    {
        var rowFlex = new FlexElement
        {
            Direction = Layout.FlexDirection.Row
        };

        if (table.ColumnGap != null)
        {
            rowFlex.Gap = table.ColumnGap;
        }

        foreach (var col in table.Columns)
        {
            // Determine content expression
            string content;
            if (col.Format != null)
            {
                content = col.Format;
            }
            else if (table.ItemVariable != null)
            {
                content = $"{{{{{table.ItemVariable}.{col.Key}}}}}";
            }
            else
            {
                content = $"{{{{{col.Key}}}}}";
            }

            var cell = new TextElement
            {
                Content = content,
                Font = col.Font ?? table.Font,
                Color = col.Color ?? table.Color,
                Size = col.Size ?? table.Size,
                Align = col.Align
            };

            if (col.Width != null)
            {
                cell.Width = col.Width;
            }

            if (col.Grow > 0)
            {
                cell.Grow = col.Grow;
            }

            rowFlex.AddChild(cell);
        }

        return rowFlex;
    }

    private FlexElement BuildStaticRow(TableElement table, TableRow row, TemplateContext context)
    {
        var rowFlex = new FlexElement
        {
            Direction = Layout.FlexDirection.Row
        };

        if (table.ColumnGap != null)
        {
            rowFlex.Gap = table.ColumnGap;
        }

        foreach (var col in table.Columns)
        {
            row.Values.TryGetValue(col.Key, out var rawValue);
            var content = SubstituteVariables(rawValue ?? "", context);

            var cell = new TextElement
            {
                Content = content,
                Font = row.Font ?? col.Font ?? table.Font,
                Color = row.Color ?? col.Color ?? table.Color,
                Size = row.Size ?? col.Size ?? table.Size,
                Align = col.Align
            };

            if (col.Width != null)
            {
                cell.Width = col.Width;
            }

            if (col.Grow > 0)
            {
                cell.Grow = col.Grow;
            }

            rowFlex.AddChild(cell);
        }

        return rowFlex;
    }

    private FlexElement ExpandFlex(FlexElement flex, TemplateContext context, int depth)
    {
        var expandedChildren = ExpandElements(flex.Children, context, depth + 1);

        var clone = new FlexElement
        {
            // Flex container-specific properties
            Direction = flex.Direction,
            Wrap = flex.Wrap,
            Gap = flex.Gap,
            Justify = flex.Justify,
            Align = flex.Align,
            AlignContent = flex.AlignContent,
            Overflow = flex.Overflow,
            RowGap = flex.RowGap,
            ColumnGap = flex.ColumnGap,

            // Base element properties requiring per-element handling
            Rotate = flex.Rotate,
            Background = SubstituteVariables(flex.Background.Value, context),
            Padding = flex.Padding,
            Margin = flex.Margin,

            // Expanded children
            Children = expandedChildren
        };

        TemplateElement.CopyBaseProperties(flex, clone);
        return clone;
    }

    /// <summary>
    /// Clones an element and substitutes variables in its string properties.
    /// </summary>
    /// <param name="element">The element to clone.</param>
    /// <param name="context">The template context for variable resolution.</param>
    /// <returns>A new element with variables substituted.</returns>
    /// <exception cref="InvalidOperationException">Thrown for control flow elements that should be expanded, not cloned.</exception>
    private TemplateElement CloneWithVariableSubstitution(TemplateElement element, TemplateContext context)
    {
        return element switch
        {
            TextElement text => CloneTextElement(text, context),
            ImageElement image => CloneImageElement(image, context),
            QrElement qr => CloneQrElement(qr, context),
            BarcodeElement barcode => CloneBarcodeElement(barcode, context),
            SvgElement svg => CloneSvgElement(svg, context),
            SeparatorElement sep => CloneSeparatorElement(sep, context),
            FlexElement => throw new InvalidOperationException("FlexElement should be handled by ExpandFlex"),
            EachElement => throw new InvalidOperationException("EachElement should be expanded, not cloned"),
            IfElement => throw new InvalidOperationException("IfElement should be expanded, not cloned"),
            TableElement => throw new InvalidOperationException("TableElement should be handled by ExpandTable"),
            _ => element
        };
    }

    /// <summary>
    /// Substitutes variables in a string using the current context.
    /// Supports inline expressions when a filter registry is configured.
    /// </summary>
    /// <param name="text">The text containing variables like {{path}} or expressions like {{price * qty}}.</param>
    /// <param name="context">The template context for variable resolution.</param>
    /// <returns>The text with all variables substituted, or null if input was null.</returns>
    [return: System.Diagnostics.CodeAnalysis.NotNullIfNotNull(nameof(text))]
    private string? SubstituteVariables(string? text, TemplateContext context)
    {
        if (text is null)
        {
            return null;
        }

        if (text.Length == 0)
        {
            return text;
        }

        // Quick check: if no template markers, return as-is
        if (!text.Contains("{{", StringComparison.Ordinal))
        {
            return text;
        }

        var tokens = ExpressionLexer.Tokenize(text);
        var result = new System.Text.StringBuilder();

        foreach (var token in tokens)
        {
            switch (token)
            {
                case TextToken textToken:
                    result.Append(textToken.Value);
                    break;
                case VariableToken variable:
                    var value = ExpressionEvaluator.Resolve(variable.Path, context);
                    result.Append(ValueToString(value));
                    break;
                case InlineExpressionToken inlineExpr when _expressionEvaluator is not null:
                    var exprValue = _expressionEvaluator.Evaluate(inlineExpr.Expression, context);
                    result.Append(ValueToString(exprValue));
                    break;
                case InlineExpressionToken inlineExpr:
                    // No evaluator configured; fall back to empty string
                    result.Append(string.Empty);
                    break;
            }
        }

        return result.ToString();
    }

    /// <summary>
    /// Converts a template value to its string representation.
    /// </summary>
    /// <param name="value">The value to convert.</param>
    /// <returns>The string representation.</returns>
    private static string ValueToString(TemplateValue value)
    {
        return value switch
        {
            NullValue => string.Empty,
            StringValue s => s.Value,
            NumberValue n => n.Value.ToString("G", CultureInfo.InvariantCulture),
            BoolValue b => b.Value ? "true" : "false",
            _ => string.Empty
        };
    }

    private TextElement CloneTextElement(TextElement text, TemplateContext context)
    {
        var clone = new TextElement
        {
            Content = SubstituteVariables(text.Content.Value, context),
            Font = text.Font,
            Size = text.Size,
            Color = text.Color,
            Align = text.Align,
            Wrap = text.Wrap,
            MaxLines = text.MaxLines,
            Overflow = text.Overflow,
            LineHeight = text.LineHeight,
            Rotate = text.Rotate,
            Background = SubstituteVariables(text.Background.Value, context),
            Padding = text.Padding,
            Margin = text.Margin
        };

        TemplateElement.CopyBaseProperties(text, clone);
        return clone;
    }

    private ImageElement CloneImageElement(ImageElement image, TemplateContext context)
    {
        var clone = new ImageElement
        {
            Src = SubstituteVariables(image.Src.Value, context),
            ImageWidth = image.ImageWidth,
            ImageHeight = image.ImageHeight,
            Fit = image.Fit,
            Rotate = image.Rotate,
            Background = SubstituteVariables(image.Background.Value, context),
            Padding = image.Padding,
            Margin = image.Margin
        };

        TemplateElement.CopyBaseProperties(image, clone);
        return clone;
    }

    private SvgElement CloneSvgElement(SvgElement svg, TemplateContext context)
    {
        var clone = new SvgElement
        {
            Src = SubstituteVariables(svg.Src.Value, context),
            Content = SubstituteVariables(svg.Content.Value, context),
            SvgWidth = svg.SvgWidth,
            SvgHeight = svg.SvgHeight,
            Fit = svg.Fit,
            Rotate = svg.Rotate,
            Background = SubstituteVariables(svg.Background.Value, context),
            Padding = svg.Padding,
            Margin = svg.Margin
        };

        TemplateElement.CopyBaseProperties(svg, clone);
        return clone;
    }

    private QrElement CloneQrElement(QrElement qr, TemplateContext context)
    {
        var clone = new QrElement
        {
            Data = SubstituteVariables(qr.Data.Value, context),
            Size = qr.Size,
            ErrorCorrection = qr.ErrorCorrection,
            Foreground = qr.Foreground,
            Rotate = qr.Rotate,
            Background = SubstituteVariables(qr.Background.Value, context),
            Padding = qr.Padding,
            Margin = qr.Margin
        };

        TemplateElement.CopyBaseProperties(qr, clone);
        return clone;
    }

    private BarcodeElement CloneBarcodeElement(BarcodeElement barcode, TemplateContext context)
    {
        var clone = new BarcodeElement
        {
            Data = SubstituteVariables(barcode.Data.Value, context),
            Format = barcode.Format,
            BarcodeWidth = barcode.BarcodeWidth,
            BarcodeHeight = barcode.BarcodeHeight,
            ShowText = barcode.ShowText,
            Foreground = barcode.Foreground,
            Rotate = barcode.Rotate,
            Background = SubstituteVariables(barcode.Background.Value, context),
            Padding = barcode.Padding,
            Margin = barcode.Margin
        };

        TemplateElement.CopyBaseProperties(barcode, clone);
        return clone;
    }

    private SeparatorElement CloneSeparatorElement(SeparatorElement sep, TemplateContext context)
    {
        var clone = new SeparatorElement
        {
            Orientation = sep.Orientation,
            Style = sep.Style,
            Thickness = sep.Thickness,
            Color = sep.Color,
            Rotate = sep.Rotate,
            Background = SubstituteVariables(sep.Background.Value, context),
            Padding = sep.Padding,
            Margin = sep.Margin
        };

        TemplateElement.CopyBaseProperties(sep, clone);
        return clone;
    }
}
