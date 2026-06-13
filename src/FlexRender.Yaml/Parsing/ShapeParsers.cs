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

    /// <summary>
    /// Parses a <c>draw</c> element together with its ordered list of absolute-coordinate shapes.
    /// </summary>
    /// <param name="node">The YAML node containing the draw element definition.</param>
    /// <param name="maxShapes">The maximum number of shapes allowed per draw element.</param>
    /// <returns>The parsed <see cref="DrawElement"/>.</returns>
    /// <exception cref="TemplateParseException">
    /// Thrown when the number of shapes exceeds <paramref name="maxShapes"/>, when a shape mapping
    /// is malformed, or when path data cannot be parsed.
    /// </exception>
    internal static TemplateElement ParseDrawElement(YamlMappingNode node, int maxShapes)
    {
        ArgumentNullException.ThrowIfNull(node);

        var shapes = new List<DrawShape>();

        if (TryGetSequence(node, "shapes", out var shapesSeq))
        {
            if (shapesSeq.Children.Count > maxShapes)
            {
                throw new TemplateParseException(
                    $"Draw element has {shapesSeq.Children.Count} shapes, which exceeds the maximum of {maxShapes} shapes per draw element.");
            }

            foreach (var item in shapesSeq.Children)
            {
                if (item is YamlMappingNode shapeNode)
                {
                    shapes.Add(ParseDrawShape(shapeNode));
                }
            }
        }

        var draw = new DrawElement(shapes)
        {
            Background = GetStringValue(node, "background")!,
            Rotate = GetExprStringValue(node, "rotate", "none"),
            Padding = GetExprStringValue(node, "padding", "0"),
            Margin = GetExprStringValue(node, "margin", "0")
        };

        ElementParsers.ApplyFlexItemProperties(node, draw);
        return draw;
    }

    /// <summary>
    /// Parses a single shape mapping, dispatching on its single shape-kind key
    /// (<c>line</c>, <c>polyline</c>, <c>rect</c>, <c>circle</c>, or <c>path</c>).
    /// </summary>
    /// <param name="node">The YAML mapping node wrapping a single shape.</param>
    /// <returns>The parsed <see cref="DrawShape"/>.</returns>
    /// <exception cref="TemplateParseException">Thrown when the shape kind is missing or unknown.</exception>
    private static DrawShape ParseDrawShape(YamlMappingNode node)
    {
        if (TryGetMapping(node, "line", out var lineNode))
        {
            return ParseDrawLine(lineNode);
        }

        if (TryGetMapping(node, "polyline", out var polylineNode))
        {
            return ParseDrawPolyline(polylineNode);
        }

        if (TryGetMapping(node, "rect", out var rectNode))
        {
            return ParseDrawRect(rectNode);
        }

        if (TryGetMapping(node, "circle", out var circleNode))
        {
            return ParseDrawCircle(circleNode);
        }

        if (TryGetMapping(node, "path", out var pathNode))
        {
            return ParseDrawPath(pathNode);
        }

        throw new TemplateParseException(
            "Unknown shape in 'draw' element. Each shape must be one of: 'line', 'polyline', 'rect', 'circle', 'path'.");
    }

    /// <summary>
    /// Parses a <c>line</c> shape.
    /// </summary>
    /// <param name="node">The mapping node describing the line.</param>
    /// <returns>The parsed <see cref="DrawLine"/>.</returns>
    private static DrawLine ParseDrawLine(YamlMappingNode node)
        => new(
            GetFloatValue(node, "x1", 0f),
            GetFloatValue(node, "y1", 0f),
            GetFloatValue(node, "x2", 0f),
            GetFloatValue(node, "y2", 0f),
            GetStringValue(node, "stroke"),
            GetFloatValue(node, "stroke-width", 1f));

    /// <summary>
    /// Parses a <c>polyline</c> shape.
    /// </summary>
    /// <param name="node">The mapping node describing the polyline.</param>
    /// <returns>The parsed <see cref="DrawPolyline"/>.</returns>
    private static DrawPolyline ParseDrawPolyline(YamlMappingNode node)
        => new(
            ParsePoints(node),
            GetStringValue(node, "stroke"),
            GetFloatValue(node, "stroke-width", 1f),
            GetStringValue(node, "fill"));

    /// <summary>
    /// Parses a <c>rect</c> shape (absolute coordinates).
    /// </summary>
    /// <param name="node">The mapping node describing the rectangle.</param>
    /// <returns>The parsed <see cref="DrawRect"/>.</returns>
    private static DrawRect ParseDrawRect(YamlMappingNode node)
        => new(
            GetFloatValue(node, "x", 0f),
            GetFloatValue(node, "y", 0f),
            GetFloatValue(node, "width", 0f),
            GetFloatValue(node, "height", 0f),
            GetStringValue(node, "fill"),
            GetStringValue(node, "stroke"),
            GetFloatValue(node, "stroke-width", 0f),
            GetFloatValue(node, "radius", 0f));

    /// <summary>
    /// Parses a <c>circle</c> shape (absolute coordinates).
    /// </summary>
    /// <param name="node">The mapping node describing the circle.</param>
    /// <returns>The parsed <see cref="DrawCircle"/>.</returns>
    private static DrawCircle ParseDrawCircle(YamlMappingNode node)
        => new(
            GetFloatValue(node, "cx", 0f),
            GetFloatValue(node, "cy", 0f),
            GetFloatValue(node, "r", 0f),
            GetStringValue(node, "fill"),
            GetStringValue(node, "stroke"),
            GetFloatValue(node, "stroke-width", 0f));

    /// <summary>
    /// Parses a <c>path</c> shape, tokenizing its <c>d</c> attribute via <see cref="PathDataParser"/>.
    /// </summary>
    /// <param name="node">The mapping node describing the path.</param>
    /// <returns>The parsed <see cref="DrawPath"/>.</returns>
    /// <exception cref="TemplateParseException">Thrown when the path data is malformed.</exception>
    private static DrawPath ParseDrawPath(YamlMappingNode node)
    {
        var d = GetStringValue(node, "d") ?? string.Empty;

        IReadOnlyList<PathCommand> commands;
        try
        {
            commands = PathDataParser.Parse(d);
        }
        catch (PathParseException ex)
        {
            throw new TemplateParseException($"Invalid path data in 'draw' shape: {ex.Message}", ex);
        }

        return new DrawPath(
            commands,
            GetStringValue(node, "fill"),
            GetStringValue(node, "stroke"),
            GetFloatValue(node, "stroke-width", 0f));
    }

    /// <summary>
    /// Parses a <c>points</c> sequence of the form <c>[[x, y], [x, y], ...]</c> into path points.
    /// </summary>
    /// <param name="node">The mapping node containing the optional <c>points</c> sequence.</param>
    /// <returns>The parsed points in order; empty when no valid points are present.</returns>
    private static List<PathPoint> ParsePoints(YamlMappingNode node)
    {
        var points = new List<PathPoint>();

        if (!TryGetSequence(node, "points", out var pointsSeq))
        {
            return points;
        }

        foreach (var item in pointsSeq.Children)
        {
            if (item is not YamlSequenceNode pair || pair.Children.Count < 2)
            {
                continue;
            }

            if (pair.Children[0] is YamlScalarNode xScalar
                && pair.Children[1] is YamlScalarNode yScalar
                && float.TryParse(xScalar.Value, NumberStyles.Float, CultureInfo.InvariantCulture, out var x)
                && float.TryParse(yScalar.Value, NumberStyles.Float, CultureInfo.InvariantCulture, out var y))
            {
                points.Add(new PathPoint(x, y));
            }
        }

        return points;
    }
}
