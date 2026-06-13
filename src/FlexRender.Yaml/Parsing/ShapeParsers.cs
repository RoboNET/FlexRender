using System.Globalization;
using FlexRender.Parsing.Ast;
using YamlDotNet.RepresentationModel;
using static FlexRender.Parsing.YamlPropertyHelpers;

namespace FlexRender.Parsing;

/// <summary>
/// Provides static helpers for parsing shape-related YAML constructs.
/// </summary>
public static class ShapeParsers
{
    /// <summary>
    /// Converts a YAML gradient object (the <c>fill</c> object form) into FlexRender's
    /// CSS gradient string so that the existing CSS gradient parser can be reused.
    /// </summary>
    /// <param name="node">The mapping node describing the gradient.</param>
    /// <returns>A CSS gradient string, e.g. <c>linear-gradient(45deg, #ff0000, #0000ff)</c>.</returns>
    /// <exception cref="TemplateParseException">
    /// Thrown when fewer than two colors are provided, or when the gradient type is not
    /// <c>linear</c> or <c>radial</c>.
    /// </exception>
    public static string ConvertGradientObjectToCss(YamlMappingNode node)
    {
        ArgumentNullException.ThrowIfNull(node);

        var type = GetStringValue(node, "gradient", "linear").Trim().ToLowerInvariant();

        var colors = new List<string>();
        if (TryGetSequence(node, "colors", out var colorsSeq))
        {
            foreach (var item in colorsSeq.Children)
            {
                if (item is YamlScalarNode scalar && !string.IsNullOrWhiteSpace(scalar.Value))
                {
                    colors.Add(scalar.Value.Trim());
                }
            }
        }

        if (colors.Count < 2)
        {
            throw new TemplateParseException("Gradient fill requires at least two colors.");
        }

        var colorList = string.Join(", ", colors);

        switch (type)
        {
            case "linear":
                var angle = GetFloatValue(node, "angle", 0f);
                return $"linear-gradient({angle.ToString(CultureInfo.InvariantCulture)}deg, {colorList})";
            case "radial":
                return $"radial-gradient({colorList})";
            default:
                throw new TemplateParseException(
                    $"Unknown gradient type '{type}'. Expected 'linear' or 'radial'.");
        }
    }

    /// <summary>
    /// Parses the <c>fill</c> property of a shape element.
    /// A scalar value (a solid color, an existing gradient string, or a <c>{{expression}}</c>)
    /// passes through unchanged; a mapping (the gradient object form) is converted to a CSS
    /// gradient string via <see cref="ConvertGradientObjectToCss"/>.
    /// </summary>
    /// <param name="node">The YAML mapping node of the shape element.</param>
    /// <returns>
    /// An <see cref="ExprValue{T}"/> containing the fill value (literal color/gradient string or
    /// an expression), or <c>default</c> when no <c>fill</c> property is present.
    /// </returns>
    internal static ExprValue<string> ParseFill(YamlMappingNode node)
    {
        ArgumentNullException.ThrowIfNull(node);

        if (TryGetMapping(node, "fill", out var fillMapping))
        {
            return ConvertGradientObjectToCss(fillMapping);
        }

        return GetExprStringValueOptional(node, "fill");
    }

    /// <summary>
    /// Parses a <c>rect</c> shape element from YAML.
    /// </summary>
    /// <param name="node">The YAML node containing the rect element definition.</param>
    /// <returns>The parsed <see cref="RectElement"/>.</returns>
    internal static TemplateElement ParseRectElement(YamlMappingNode node)
    {
        ArgumentNullException.ThrowIfNull(node);

        var rect = new RectElement
        {
            Fill = ParseFill(node),
            Stroke = GetExprStringValueOptional(node, "stroke"),
            StrokeWidth = GetExprFloatValue(node, "stroke-width", 0f),
            Radius = GetExprStringValueOptional(node, "radius"),
            Background = GetStringValue(node, "background")!,
            Rotate = GetExprStringValue(node, "rotate", "none"),
            Padding = GetExprStringValue(node, "padding", "0"),
            Margin = GetExprStringValue(node, "margin", "0")
        };

        ElementParsers.ApplyFlexItemProperties(node, rect);
        return rect;
    }

    /// <summary>
    /// Parses a <c>circle</c> shape element from YAML.
    /// The <c>size</c> shorthand sets both Width and Height; it is applied after the common
    /// flex-item properties so that it overrides any explicit width/height.
    /// </summary>
    /// <param name="node">The YAML node containing the circle element definition.</param>
    /// <returns>The parsed <see cref="CircleElement"/>.</returns>
    internal static TemplateElement ParseCircleElement(YamlMappingNode node)
    {
        ArgumentNullException.ThrowIfNull(node);

        var circle = new CircleElement
        {
            Fill = ParseFill(node),
            Stroke = GetExprStringValueOptional(node, "stroke"),
            StrokeWidth = GetExprFloatValue(node, "stroke-width", 0f),
            Background = GetStringValue(node, "background")!,
            Rotate = GetExprStringValue(node, "rotate", "none"),
            Padding = GetExprStringValue(node, "padding", "0"),
            Margin = GetExprStringValue(node, "margin", "0")
        };

        ElementParsers.ApplyFlexItemProperties(node, circle);

        // 'size' shorthand: a circle is square, so size sets both Width and Height.
        // Applied after flex-item properties so it overrides any explicit width/height.
        var size = GetExprStringValueOptional(node, "size");
        if (size.Value is not null || size.IsExpression)
        {
            circle.Width = size;
            circle.Height = size;
        }

        return circle;
    }

    /// <summary>
    /// Parses an <c>ellipse</c> shape element from YAML.
    /// </summary>
    /// <param name="node">The YAML node containing the ellipse element definition.</param>
    /// <returns>The parsed <see cref="EllipseElement"/>.</returns>
    internal static TemplateElement ParseEllipseElement(YamlMappingNode node)
    {
        ArgumentNullException.ThrowIfNull(node);

        var ellipse = new EllipseElement
        {
            Fill = ParseFill(node),
            Stroke = GetExprStringValueOptional(node, "stroke"),
            StrokeWidth = GetExprFloatValue(node, "stroke-width", 0f),
            Background = GetStringValue(node, "background")!,
            Rotate = GetExprStringValue(node, "rotate", "none"),
            Padding = GetExprStringValue(node, "padding", "0"),
            Margin = GetExprStringValue(node, "margin", "0")
        };

        ElementParsers.ApplyFlexItemProperties(node, ellipse);
        return ellipse;
    }
}
