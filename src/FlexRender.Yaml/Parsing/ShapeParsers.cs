using System.Globalization;
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
}
