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

    private static bool EvaluateCondition(IfElement ifEl, TemplateContext context)
    {
        var value = ExpressionEvaluator.Resolve(ifEl.ConditionPath, context);

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

    /// <summary>
    /// Copies all base flex-item and positioning properties from source to target element.
    /// This avoids duplication across all Clone methods.
    /// Properties that require per-element transformation (Background via SubstituteVariables,
    /// Rotate, Padding, Margin) are intentionally excluded and must be set in each caller.
    /// </summary>
    /// <param name="source">The source element to copy properties from.</param>
    /// <param name="target">The target element to copy properties to.</param>
    private static void CopyBaseProperties(TemplateElement source, TemplateElement target)
    {
        // Flex-item properties
        target.Grow = source.Grow;
        target.Shrink = source.Shrink;
        target.Basis = source.Basis;
        target.AlignSelf = source.AlignSelf;
        target.Order = source.Order;
        target.Width = source.Width;
        target.Height = source.Height;
        target.MinWidth = source.MinWidth;
        target.MaxWidth = source.MaxWidth;
        target.MinHeight = source.MinHeight;
        target.MaxHeight = source.MaxHeight;

        // Position properties
        target.Position = source.Position;
        target.Top = source.Top;
        target.Right = source.Right;
        target.Bottom = source.Bottom;
        target.Left = source.Left;

        // Other base properties
        target.Display = source.Display;
        target.AspectRatio = source.AspectRatio;

        // Border properties
        target.Border = source.Border;
        target.BorderWidth = source.BorderWidth;
        target.BorderColor = source.BorderColor;
        target.BorderStyle = source.BorderStyle;
        target.BorderTop = source.BorderTop;
        target.BorderRight = source.BorderRight;
        target.BorderBottom = source.BorderBottom;
        target.BorderLeft = source.BorderLeft;
        target.BorderRadius = source.BorderRadius;

        // Text direction
        target.TextDirection = source.TextDirection;
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
            Background = SubstituteVariables(flex.Background, context),
            Padding = flex.Padding,
            Margin = flex.Margin,

            // Expanded children
            Children = expandedChildren
        };

        CopyBaseProperties(flex, clone);
        return clone;
    }

    /// <summary>
    /// Clones an element and substitutes variables in its string properties.
    /// </summary>
    /// <param name="element">The element to clone.</param>
    /// <param name="context">The template context for variable resolution.</param>
    /// <returns>A new element with variables substituted.</returns>
    /// <exception cref="InvalidOperationException">Thrown for control flow elements that should be expanded, not cloned.</exception>
    private static TemplateElement CloneWithVariableSubstitution(TemplateElement element, TemplateContext context)
    {
        return element switch
        {
            TextElement text => CloneTextElement(text, context),
            ImageElement image => CloneImageElement(image, context),
            QrElement qr => CloneQrElement(qr, context),
            BarcodeElement barcode => CloneBarcodeElement(barcode, context),
            SeparatorElement sep => CloneSeparatorElement(sep, context),
            FlexElement => throw new InvalidOperationException("FlexElement should be handled by ExpandFlex"),
            EachElement => throw new InvalidOperationException("EachElement should be expanded, not cloned"),
            IfElement => throw new InvalidOperationException("IfElement should be expanded, not cloned"),
            _ => element
        };
    }

    /// <summary>
    /// Substitutes variables in a string using the current context.
    /// </summary>
    /// <param name="text">The text containing variables like {{path}}.</param>
    /// <param name="context">The template context for variable resolution.</param>
    /// <returns>The text with all variables substituted, or null if input was null.</returns>
    [return: System.Diagnostics.CodeAnalysis.NotNullIfNotNull(nameof(text))]
    private static string? SubstituteVariables(string? text, TemplateContext context)
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

    private static TextElement CloneTextElement(TextElement text, TemplateContext context)
    {
        var clone = new TextElement
        {
            Content = SubstituteVariables(text.Content, context),
            Font = text.Font,
            Size = text.Size,
            Color = text.Color,
            Align = text.Align,
            Wrap = text.Wrap,
            MaxLines = text.MaxLines,
            Overflow = text.Overflow,
            LineHeight = text.LineHeight,
            Rotate = text.Rotate,
            Background = SubstituteVariables(text.Background, context),
            Padding = text.Padding,
            Margin = text.Margin
        };

        CopyBaseProperties(text, clone);
        return clone;
    }

    private static ImageElement CloneImageElement(ImageElement image, TemplateContext context)
    {
        var clone = new ImageElement
        {
            Src = SubstituteVariables(image.Src, context),
            ImageWidth = image.ImageWidth,
            ImageHeight = image.ImageHeight,
            Fit = image.Fit,
            Rotate = image.Rotate,
            Background = SubstituteVariables(image.Background, context),
            Padding = image.Padding,
            Margin = image.Margin
        };

        CopyBaseProperties(image, clone);
        return clone;
    }

    private static QrElement CloneQrElement(QrElement qr, TemplateContext context)
    {
        var clone = new QrElement
        {
            Data = SubstituteVariables(qr.Data, context),
            Size = qr.Size,
            ErrorCorrection = qr.ErrorCorrection,
            Foreground = qr.Foreground,
            Rotate = qr.Rotate,
            Background = SubstituteVariables(qr.Background, context),
            Padding = qr.Padding,
            Margin = qr.Margin
        };

        CopyBaseProperties(qr, clone);
        return clone;
    }

    private static BarcodeElement CloneBarcodeElement(BarcodeElement barcode, TemplateContext context)
    {
        var clone = new BarcodeElement
        {
            Data = SubstituteVariables(barcode.Data, context),
            Format = barcode.Format,
            BarcodeWidth = barcode.BarcodeWidth,
            BarcodeHeight = barcode.BarcodeHeight,
            ShowText = barcode.ShowText,
            Foreground = barcode.Foreground,
            Rotate = barcode.Rotate,
            Background = SubstituteVariables(barcode.Background, context),
            Padding = barcode.Padding,
            Margin = barcode.Margin
        };

        CopyBaseProperties(barcode, clone);
        return clone;
    }

    private static SeparatorElement CloneSeparatorElement(SeparatorElement sep, TemplateContext context)
    {
        var clone = new SeparatorElement
        {
            Orientation = sep.Orientation,
            Style = sep.Style,
            Thickness = sep.Thickness,
            Color = sep.Color,
            Rotate = sep.Rotate,
            Background = SubstituteVariables(sep.Background, context),
            Padding = sep.Padding,
            Margin = sep.Margin
        };

        CopyBaseProperties(sep, clone);
        return clone;
    }
}
