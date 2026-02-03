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

        var result = new List<TemplateElement>();

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
            return ExpandIf(ifEl.ElseIf, context, depth);
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

        // Comparison
        var stringValue = value switch
        {
            StringValue s => s.Value,
            NumberValue n => n.Value.ToString(CultureInfo.InvariantCulture),
            BoolValue b => b.Value.ToString().ToLowerInvariant(),
            _ => ""
        };

        return ifEl.Operator switch
        {
            ConditionOperator.Equals => stringValue == ifEl.CompareValue,
            ConditionOperator.NotEquals => stringValue != ifEl.CompareValue,
            _ => false
        };
    }

    private FlexElement ExpandFlex(FlexElement flex, TemplateContext context, int depth)
    {
        var expandedChildren = ExpandElements(flex.Children, context, depth + 1);

        return new FlexElement
        {
            // Container properties
            Direction = flex.Direction,
            Wrap = flex.Wrap,
            Gap = flex.Gap,
            Justify = flex.Justify,
            Align = flex.Align,
            AlignContent = flex.AlignContent,

            // Item properties
            Grow = flex.Grow,
            Shrink = flex.Shrink,
            Basis = flex.Basis,
            AlignSelf = flex.AlignSelf,
            Order = flex.Order,
            Width = flex.Width,
            Height = flex.Height,

            // Base element properties (from TemplateElement)
            Rotate = flex.Rotate,
            Background = SubstituteVariables(flex.Background, context),
            Padding = flex.Padding,
            Margin = flex.Margin,

            // Expanded children
            Children = expandedChildren
        };
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
        return new TextElement
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
            Margin = text.Margin,
            // Flex item properties
            Grow = text.Grow,
            Shrink = text.Shrink,
            Basis = text.Basis,
            AlignSelf = text.AlignSelf,
            Order = text.Order,
            Width = text.Width,
            Height = text.Height
        };
    }

    private static ImageElement CloneImageElement(ImageElement image, TemplateContext context)
    {
        return new ImageElement
        {
            Src = SubstituteVariables(image.Src, context),
            ImageWidth = image.ImageWidth,
            ImageHeight = image.ImageHeight,
            Fit = image.Fit,
            Rotate = image.Rotate,
            Background = SubstituteVariables(image.Background, context),
            Padding = image.Padding,
            Margin = image.Margin,
            // Flex item properties
            Grow = image.Grow,
            Shrink = image.Shrink,
            Basis = image.Basis,
            AlignSelf = image.AlignSelf,
            Order = image.Order,
            Width = image.Width,
            Height = image.Height
        };
    }

    private static QrElement CloneQrElement(QrElement qr, TemplateContext context)
    {
        return new QrElement
        {
            Data = SubstituteVariables(qr.Data, context),
            Size = qr.Size,
            ErrorCorrection = qr.ErrorCorrection,
            Foreground = qr.Foreground,
            Rotate = qr.Rotate,
            Background = SubstituteVariables(qr.Background, context),
            Padding = qr.Padding,
            Margin = qr.Margin,
            // Flex item properties
            Grow = qr.Grow,
            Shrink = qr.Shrink,
            Basis = qr.Basis,
            AlignSelf = qr.AlignSelf,
            Order = qr.Order,
            Width = qr.Width,
            Height = qr.Height
        };
    }

    private static BarcodeElement CloneBarcodeElement(BarcodeElement barcode, TemplateContext context)
    {
        return new BarcodeElement
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
            Margin = barcode.Margin,
            // Flex item properties
            Grow = barcode.Grow,
            Shrink = barcode.Shrink,
            Basis = barcode.Basis,
            AlignSelf = barcode.AlignSelf,
            Order = barcode.Order,
            Width = barcode.Width,
            Height = barcode.Height
        };
    }

    private static SeparatorElement CloneSeparatorElement(SeparatorElement sep, TemplateContext context)
    {
        return new SeparatorElement
        {
            Orientation = sep.Orientation,
            Style = sep.Style,
            Thickness = sep.Thickness,
            Color = sep.Color,
            Rotate = sep.Rotate,
            Background = SubstituteVariables(sep.Background, context),
            Padding = sep.Padding,
            Margin = sep.Margin,
            // Flex item properties
            Grow = sep.Grow,
            Shrink = sep.Shrink,
            Basis = sep.Basis,
            AlignSelf = sep.AlignSelf,
            Order = sep.Order,
            Width = sep.Width,
            Height = sep.Height
        };
    }
}
