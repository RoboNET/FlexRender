using System.Globalization;
using FlexRender.Abstractions;
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
    private readonly ContentParserRegistry? _contentParserRegistry;
    private readonly IReadOnlyList<IResourceLoader>? _resourceLoaders;

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
    /// <param name="contentParserRegistry">Optional content parser registry for ContentElement expansion.</param>
    /// <param name="resourceLoaders">Optional resource loaders for resolving file-based content sources.</param>
    /// <exception cref="ArgumentNullException">Thrown when limits is null.</exception>
    public TemplateExpander(ResourceLimits limits, ContentParserRegistry? contentParserRegistry = null,
        IReadOnlyList<IResourceLoader>? resourceLoaders = null)
    {
        ArgumentNullException.ThrowIfNull(limits);
        _limits = limits;
        _expressionEvaluator = new InlineExpressionEvaluator();
        _contentParserRegistry = contentParserRegistry;
        _resourceLoaders = resourceLoaders;
    }

    /// <summary>
    /// Creates a new TemplateExpander with the specified resource limits and filter registry.
    /// </summary>
    /// <param name="limits">Resource limits for expansion depth protection.</param>
    /// <param name="filterRegistry">The filter registry for expression evaluation.</param>
    /// <param name="contentParserRegistry">Optional content parser registry for ContentElement expansion.</param>
    /// <param name="resourceLoaders">Optional resource loaders for resolving file-based content sources.</param>
    /// <exception cref="ArgumentNullException">Thrown when limits or filterRegistry is null.</exception>
    public TemplateExpander(ResourceLimits limits, FilterRegistry filterRegistry,
        ContentParserRegistry? contentParserRegistry = null,
        IReadOnlyList<IResourceLoader>? resourceLoaders = null)
        : this(limits, filterRegistry, CultureInfo.InvariantCulture, contentParserRegistry, resourceLoaders)
    {
    }

    /// <summary>
    /// Creates a new TemplateExpander with the specified resource limits, filter registry, and culture.
    /// </summary>
    /// <param name="limits">Resource limits for expansion depth protection.</param>
    /// <param name="filterRegistry">The filter registry for expression evaluation.</param>
    /// <param name="culture">The culture to use for culture-aware filter formatting.</param>
    /// <param name="contentParserRegistry">Optional content parser registry for ContentElement expansion.</param>
    /// <param name="resourceLoaders">Optional resource loaders for resolving file-based content sources.</param>
    /// <exception cref="ArgumentNullException">Thrown when any parameter is null.</exception>
    public TemplateExpander(ResourceLimits limits, FilterRegistry filterRegistry, CultureInfo culture,
        ContentParserRegistry? contentParserRegistry = null,
        IReadOnlyList<IResourceLoader>? resourceLoaders = null)
    {
        ArgumentNullException.ThrowIfNull(limits);
        ArgumentNullException.ThrowIfNull(filterRegistry);
        ArgumentNullException.ThrowIfNull(culture);
        _limits = limits;
        _expressionEvaluator = new InlineExpressionEvaluator(filterRegistry, culture);
        _contentParserRegistry = contentParserRegistry;
        _resourceLoaders = resourceLoaders;
    }

    /// <summary>
    /// Asynchronously expands EachElement and IfElement instances into concrete elements based on data.
    /// Returns a new Template with all control flow elements resolved.
    /// Uses <c>await</c> for content source resolution instead of blocking synchronously.
    /// </summary>
    /// <param name="template">The template containing control flow elements.</param>
    /// <param name="data">The data for evaluating conditions and iterating arrays.</param>
    /// <returns>A new Template with expanded elements.</returns>
    /// <exception cref="ArgumentNullException">Thrown when template or data is null.</exception>
    /// <exception cref="TemplateEngineException">Thrown when maximum expansion depth is exceeded.</exception>
    public async Task<Template> ExpandAsync(Template template, ObjectValue data)
    {
        ArgumentNullException.ThrowIfNull(template);
        ArgumentNullException.ThrowIfNull(data);

        var context = new TemplateContext(data);
        var expandedElements = await ExpandElementsAsync(template.Elements, context, 0, template).ConfigureAwait(false);

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

    private async Task<FlexElement> ExpandTableAsync(TableElement table, TemplateContext context, int depth, Template template)
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
            var expandedRows = await ExpandEachAsync(each, context, childDepth, template).ConfigureAwait(false);
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
                FontWeight = table.HeaderFontWeight ?? FontWeight.Normal,
                FontStyle = table.HeaderFontStyle ?? FontStyle.Normal,
                FontFamily = table.HeaderFontFamily ?? "",
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

    /// <summary>
    /// Attempts to parse a plain pixel width value from a size string.
    /// Returns the integer pixel value for plain numeric strings (e.g., "200"),
    /// or null for percentage, em, auto, or empty/null values.
    /// </summary>
    /// <param name="widthValue">The width string from <see cref="TemplateElement.Width"/>.</param>
    /// <returns>The pixel width if parseable; otherwise, null.</returns>
    private static int? TryParsePixelWidth(string? widthValue)
    {
        if (string.IsNullOrEmpty(widthValue))
            return null;

        // Skip relative/auto values -- only accept plain numeric pixel widths
        if (widthValue.Contains('%', StringComparison.Ordinal)
            || widthValue.Contains("em", StringComparison.OrdinalIgnoreCase)
            || widthValue.Equals("auto", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        return int.TryParse(widthValue, System.Globalization.NumberStyles.None,
            System.Globalization.CultureInfo.InvariantCulture, out var px) && px > 0
            ? px
            : null;
    }

    private async Task<List<TemplateElement>> ExpandElementsAsync(IReadOnlyList<TemplateElement> elements, TemplateContext context, int depth, Template template, int? parentWidth = null)
    {
        if (depth > _limits.MaxRenderDepth)
        {
            throw new TemplateEngineException($"Maximum expansion depth ({_limits.MaxRenderDepth}) exceeded");
        }

        var result = new List<TemplateElement>(elements.Count);

        foreach (var element in elements)
        {
            var expanded = await ExpandElementAsync(element, context, depth, template, parentWidth).ConfigureAwait(false);
            result.AddRange(expanded);
        }

        return result;
    }

    private async ValueTask<IEnumerable<TemplateElement>> ExpandElementAsync(TemplateElement element, TemplateContext context, int depth, Template template, int? parentWidth = null)
    {
        return element switch
        {
            EachElement each => await ExpandEachAsync(each, context, depth, template, parentWidth).ConfigureAwait(false),
            IfElement ifEl => await ExpandIfAsync(ifEl, context, depth, template, parentWidth).ConfigureAwait(false),
            TableElement table => [await ExpandTableAsync(table, context, depth, template).ConfigureAwait(false)],
            FlexElement flex => [await ExpandFlexAsync(flex, context, depth, template).ConfigureAwait(false)],
            ContentElement content => await ExpandContentAsync(content, context, depth, template, parentWidth).ConfigureAwait(false),
            ImageElement image => [await CloneImageElementAsync(image, context).ConfigureAwait(false)],
            SvgElement svg => [await CloneSvgElementAsync(svg, context).ConfigureAwait(false)],
            _ => [CloneWithVariableSubstitution(element, context)]
        };
    }

    private async Task<List<TemplateElement>> ExpandEachAsync(EachElement each, TemplateContext context, int depth, Template template, int? parentWidth = null)
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
        var result = new List<TemplateElement>();

        if (arrayValue is ArrayValue array)
        {
            var count = array.Count;
            for (var i = 0; i < count; i++)
            {
                var item = array[i];

                if (each.ItemVariable != null)
                {
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

                var expandedChildren = await ExpandElementsAsync(each.ItemTemplate, context, childDepth, template, parentWidth).ConfigureAwait(false);

                context.ClearLoopVariables();
                context.PopScope();

                result.AddRange(expandedChildren);
            }
        }
        else if (arrayValue is ObjectValue obj)
        {
            var keys = obj.Keys.ToList();
            var count = keys.Count;
            for (var i = 0; i < count; i++)
            {
                var key = keys[i];
                var value = obj[key];

                if (each.ItemVariable != null)
                {
                    var scopeData = new ObjectValue
                    {
                        [each.ItemVariable] = value
                    };
                    context.PushScope(scopeData);
                }
                else
                {
                    context.PushScope(value);
                }

                context.SetLoopVariables(i, count);
                context.SetLoopKey(key);

                var expandedChildren = await ExpandElementsAsync(each.ItemTemplate, context, childDepth, template, parentWidth).ConfigureAwait(false);

                context.ClearLoopVariables();
                context.PopScope();

                result.AddRange(expandedChildren);
            }
        }

        return result;
    }

    private async Task<IEnumerable<TemplateElement>> ExpandIfAsync(IfElement ifEl, TemplateContext context, int depth, Template template, int? parentWidth = null)
    {
        var childDepth = depth + 1;
        if (childDepth > _limits.MaxRenderDepth)
        {
            throw new TemplateEngineException($"Maximum expansion depth ({_limits.MaxRenderDepth}) exceeded");
        }

        ValidateNestedDepth(ifEl.ThenBranch, childDepth);
        ValidateNestedDepth(ifEl.ElseBranch, childDepth);
        if (ifEl.ElseIf != null)
        {
            ValidateNestedDepth(new[] { ifEl.ElseIf }, depth);
        }

        if (EvaluateCondition(ifEl, context))
        {
            return await ExpandElementsAsync(ifEl.ThenBranch, context, childDepth, template, parentWidth).ConfigureAwait(false);
        }

        if (ifEl.ElseIf != null)
        {
            return await ExpandIfAsync(ifEl.ElseIf, context, depth + 1, template, parentWidth).ConfigureAwait(false);
        }

        return await ExpandElementsAsync(ifEl.ElseBranch, context, childDepth, template, parentWidth).ConfigureAwait(false);
    }

    private async Task<FlexElement> ExpandFlexAsync(FlexElement flex, TemplateContext context, int depth, Template template)
    {
        var childParentWidth = TryParsePixelWidth(flex.Width.Value);
        var expandedChildren = await ExpandElementsAsync(flex.Children, context, depth + 1, template, childParentWidth).ConfigureAwait(false);
        return flex.CloneWithChildren(expandedChildren, text => SubstituteVariables(text, context));
    }

    private async Task<IEnumerable<TemplateElement>> ExpandContentAsync(ContentElement content, TemplateContext context, int depth, Template template, int? parentWidth = null)
    {
        var childDepth = depth + 1;
        if (childDepth > _limits.MaxRenderDepth)
        {
            throw new TemplateEngineException($"Maximum expansion depth ({_limits.MaxRenderDepth}) exceeded");
        }

        var resolvedFormat = SubstituteVariables(content.Format.RawValue ?? content.Format.Value, context);

        if (_contentParserRegistry is null)
        {
            throw new TemplateEngineException(
                $"ContentElement with format '{resolvedFormat}' cannot be expanded: no content parsers are registered. " +
                "Register a parser via FlexRenderBuilder.WithContentParser().");
        }

        var parserContext = new ContentParserContext
        {
            Canvas = template.Canvas,
            Template = template,
            ParentWidth = parentWidth
        };

        // Step 1: Try resolve BytesValue from template variable (e.g. {{payload}})
        var source = content.Source;
        var bytesValue = TryResolveBytesValue(source, context);

        // Step 2: Resolve source string with variable substitution
        var rawText = source.RawValue ?? source.Value;
        var resolvedSource = SubstituteVariables(rawText, context);

        // Step 3: If no BytesValue from variable, try loading via resource loaders
        if (bytesValue is null)
        {
            bytesValue = await TryLoadBytesFromLoadersAsync(resolvedSource).ConfigureAwait(false);
        }

        // Step 4: Dispatch binary if bytes were resolved
        if (bytesValue is not null)
        {
            var binary = new BinaryContent(bytesValue.Memory, bytesValue.MimeType);
            return DispatchBinary(binary, resolvedFormat, parserContext, content.Options);
        }

        // Step 5: "text:" prefix — strip prefix and treat as text
        if (resolvedSource is not null && resolvedSource.StartsWith("text:", StringComparison.Ordinal))
        {
            var text = new TextContent(resolvedSource["text:".Length..]);
            return DispatchText(text, resolvedFormat, parserContext, content.Options);
        }

        // Step 6: Plain text fallback
        var textContent = new TextContent(resolvedSource ?? string.Empty);
        return DispatchText(textContent, resolvedFormat, parserContext, content.Options);
    }

    private IReadOnlyList<TemplateElement> DispatchBinary(BinaryContent binary, string format, ContentParserContext context, IReadOnlyDictionary<string, object>? options)
    {
        var parser = _contentParserRegistry!.GetBinaryParser(format)
            ?? throw new TemplateEngineException(
                $"No binary content parser registered for format '{format}'. " +
                "Register a binary parser via FlexRenderBuilder.WithBinaryContentParser().");

        return parser.Parse(binary.Data, context, options);
    }

    private IReadOnlyList<TemplateElement> DispatchText(TextContent text, string format, ContentParserContext context, IReadOnlyDictionary<string, object>? options)
    {
        var parser = _contentParserRegistry!.GetParser(format)
            ?? throw new TemplateEngineException(
                $"No content parser registered for format '{format}'. " +
                "Register a parser via FlexRenderBuilder.WithContentParser().");

        return parser.Parse(text.Text, context, options);
    }

    /// <summary>
    /// Clones an element and substitutes variables in its string properties.
    /// Delegates to <see cref="TemplateElement.CloneWithSubstitution"/> on the element itself.
    /// </summary>
    /// <param name="element">The element to clone.</param>
    /// <param name="context">The template context for variable resolution.</param>
    /// <returns>A new element with variables substituted.</returns>
    private TemplateElement CloneWithVariableSubstitution(TemplateElement element, TemplateContext context)
    {
        return element.CloneWithSubstitution(text => SubstituteVariables(text, context));
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
            BytesValue bv => $"bytes[{bv.Memory.Length}]",
            _ => string.Empty
        };
    }

    /// <summary>
    /// If the source expression is a single <c>{{variable}}</c> that resolves to a <see cref="BytesValue"/>,
    /// returns that value so it can be attached to <see cref="ExprValue{T}.Bytes"/> without
    /// base64 encoding/decoding overhead.
    /// Returns <c>null</c> when the expression is not a single variable or does not resolve to bytes.
    /// </summary>
    /// <param name="source">The expression value holding the image/svg source.</param>
    /// <param name="context">The template context for variable resolution.</param>
    /// <returns>The resolved <see cref="BytesValue"/>, or <c>null</c> if not applicable.</returns>
    private static BytesValue? TryResolveBytesValue(ExprValue<string> source, TemplateContext context)
    {
        var raw = source.RawValue ?? source.Value;
        if (raw is null)
            return null;

        // Only handle simple {{variable}} expressions, not mixed content
        if (!raw.StartsWith("{{", StringComparison.Ordinal) || !raw.EndsWith("}}", StringComparison.Ordinal))
            return null;

        var inner = raw[2..^2].Trim();

        // Reject if there are nested expressions (e.g. "{{a}} + {{b}}")
        if (inner.Contains("{{", StringComparison.Ordinal))
            return null;

        var resolved = ExpressionEvaluator.Resolve(inner, context);
        return resolved as BytesValue;
    }



    /// <summary>
    /// Clones an image element asynchronously, resolving bytes from variables or URI loaders.
    /// </summary>
    /// <param name="image">The image element to clone.</param>
    /// <param name="context">The template context for variable resolution.</param>
    /// <returns>A new image element with resolved bytes when available.</returns>
    private async ValueTask<ImageElement> CloneImageElementAsync(ImageElement image, TemplateContext context)
    {
        var bytesValue = TryResolveBytesValue(image.Src, context);
        string? resolvedSrc;

        if (bytesValue is not null)
        {
            resolvedSrc = image.Src.Value ?? "";
        }
        else
        {
            resolvedSrc = SubstituteVariables(image.Src.Value, context);
            bytesValue = await TryLoadBytesFromLoadersAsync(resolvedSrc).ConfigureAwait(false);
        }

        ExprValue<string> srcExpr = resolvedSrc ?? "";
        if (bytesValue is not null)
            srcExpr = srcExpr.WithBytes(bytesValue);

        return image.WithSrc(srcExpr, text => SubstituteVariables(text, context));
    }

    /// <summary>
    /// Clones an SVG element asynchronously, resolving bytes from variables or URI loaders.
    /// </summary>
    /// <param name="svg">The SVG element to clone.</param>
    /// <param name="context">The template context for variable resolution.</param>
    /// <returns>A new SVG element with resolved bytes when available.</returns>
    private async ValueTask<SvgElement> CloneSvgElementAsync(SvgElement svg, TemplateContext context)
    {
        var bytesValue = TryResolveBytesValue(svg.Src, context);
        string? resolvedSrc;

        if (bytesValue is not null)
        {
            resolvedSrc = svg.Src.Value ?? "";
        }
        else
        {
            resolvedSrc = SubstituteVariables(svg.Src.Value, context);
            bytesValue = await TryLoadBytesFromLoadersAsync(resolvedSrc).ConfigureAwait(false);
        }

        ExprValue<string> srcExpr = resolvedSrc ?? "";
        if (bytesValue is not null)
            srcExpr = srcExpr.WithBytes(bytesValue);

        return svg.WithSrc(srcExpr, text => SubstituteVariables(text, context));
    }

    /// <summary>
    /// Tries to load binary content from a URI source via the resource loader chain.
    /// Returns null if no loader can handle the source or no loaders are registered.
    /// </summary>
    /// <param name="source">The URI source string to load from.</param>
    /// <returns>The loaded bytes, or null if no loader handles the source.</returns>
    private async ValueTask<BytesValue?> TryLoadBytesFromLoadersAsync(string? source)
    {
        if (string.IsNullOrEmpty(source) || _resourceLoaders is null)
            return null;

        foreach (var loader in _resourceLoaders)
        {
            if (loader.CanHandle(source))
            {
                try
                {
                    var stream = await loader.Load(source).ConfigureAwait(false);
                    if (stream is not null)
                    {
                        using (stream)
                        {
                            return BytesValue.FromStream(stream);
                        }
                    }
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    // Loader claimed it can handle this URI but failed —
                    // continue to next loader.
                }
            }
        }

        return null;
    }

}
