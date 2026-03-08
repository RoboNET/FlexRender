using YamlDotNet.RepresentationModel;

namespace FlexRender.Parsing;

/// <summary>
/// Defines the set of known YAML property names for each element type.
/// Used to detect and report unknown/misspelled properties during parsing.
/// </summary>
internal static class KnownProperties
{
    /// <summary>
    /// The 'type' key is always valid on every element and is excluded from validation.
    /// </summary>
    private const string TypeKey = "type";

    /// <summary>
    /// Common flex-item properties applied to all elements via
    /// <see cref="ElementParsers.ApplyFlexItemProperties"/>.
    /// </summary>
    private static readonly string[] FlexItemProperties =
    [
        "grow", "shrink", "basis", "order", "display", "alignSelf",
        "width", "height",
        "min-width", "minWidth", "max-width", "maxWidth",
        "min-height", "minHeight", "max-height", "maxHeight",
        "position", "top", "right", "bottom", "left",
        "aspectRatio", "aspect-ratio",
        "border", "border-width", "borderWidth", "border-color", "borderColor",
        "border-style", "borderStyle",
        "border-top", "borderTop", "border-right", "borderRight",
        "border-bottom", "borderBottom", "border-left", "borderLeft",
        "border-radius", "borderRadius",
        "text-direction",
        "opacity", "box-shadow", "boxShadow"
    ];

    /// <summary>
    /// Known properties for the 'text' element type.
    /// </summary>
    internal static readonly HashSet<string> Text = BuildSet(FlexItemProperties,
    [
        "content", "font", "fontFamily", "font-family", "size", "color",
        "align", "wrap", "overflow", "maxLines", "rotate",
        "background", "padding", "margin", "lineHeight",
        "fontWeight", "fontStyle"
    ]);

    /// <summary>
    /// Known properties for the 'flex' element type.
    /// </summary>
    internal static readonly HashSet<string> Flex = BuildSet(FlexItemProperties,
    [
        "direction", "wrap", "gap", "justify", "align",
        "align-content", "alignContent",
        "row-gap", "rowGap", "column-gap", "columnGap",
        "overflow", "children",
        "padding", "margin", "background", "rotate",
        "font_size", "font-size"
    ]);

    /// <summary>
    /// Known properties for the 'qr' element type.
    /// </summary>
    internal static readonly HashSet<string> Qr = BuildSet(FlexItemProperties,
    [
        "data", "size", "foreground", "errorCorrection",
        "background", "rotate", "padding", "margin"
    ]);

    /// <summary>
    /// Known properties for the 'barcode' element type.
    /// </summary>
    internal static readonly HashSet<string> Barcode = BuildSet(FlexItemProperties,
    [
        "data", "width", "height", "showText", "foreground", "format",
        "background", "rotate", "padding", "margin"
    ]);

    /// <summary>
    /// Known properties for the 'image' element type.
    /// </summary>
    internal static readonly HashSet<string> Image = BuildSet(FlexItemProperties,
    [
        "src", "width", "height", "fit",
        "background", "rotate", "padding", "margin"
    ]);

    /// <summary>
    /// Known properties for the 'separator' element type.
    /// </summary>
    internal static readonly HashSet<string> Separator = BuildSet(FlexItemProperties,
    [
        "orientation", "style", "thickness", "color",
        "background", "rotate", "padding", "margin"
    ]);

    /// <summary>
    /// Known properties for the 'svg' element type.
    /// </summary>
    internal static readonly HashSet<string> Svg = BuildSet(FlexItemProperties,
    [
        "src", "content", "width", "height", "fit",
        "background", "rotate", "padding", "margin"
    ]);

    /// <summary>
    /// Known properties for the 'table' element type.
    /// </summary>
    internal static readonly HashSet<string> Table = BuildSet(FlexItemProperties,
    [
        "columns", "array", "rows", "as",
        "font", "size", "color",
        "rowGap", "row-gap", "columnGap", "column-gap",
        "headerFont", "header-font",
        "headerFontWeight", "header-fontWeight",
        "headerFontStyle", "header-fontStyle",
        "headerFontFamily", "header-fontFamily",
        "headerColor", "header-color",
        "headerSize", "header-size",
        "headerBorderBottom", "header-border-bottom",
        "background", "rotate", "padding", "margin"
    ]);

    /// <summary>
    /// Known properties for the 'each' element type.
    /// </summary>
    internal static readonly HashSet<string> Each = new(StringComparer.Ordinal)
    {
        "array", "as", "children"
    };

    /// <summary>
    /// Known properties for the 'if' element type.
    /// </summary>
    internal static readonly HashSet<string> If = new(StringComparer.Ordinal)
    {
        "condition", "equals", "notEquals",
        "in", "notIn", "contains",
        "greaterThan", "greaterThanOrEqual",
        "lessThan", "lessThanOrEqual",
        "hasItems", "countEquals", "countGreaterThan",
        "then", "else", "elseIf"
    };

    /// <summary>
    /// Known properties for the 'content' element type.
    /// </summary>
    internal static readonly HashSet<string> Content = BuildSet(FlexItemProperties,
    [
        "source", "format", "options",
        "rotate", "padding", "margin"
    ]);

    /// <summary>
    /// Maps element type names (case-insensitive) to their known property sets.
    /// </summary>
    private static readonly Dictionary<string, HashSet<string>> Registry =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ["text"] = Text,
            ["flex"] = Flex,
            ["qr"] = Qr,
            ["barcode"] = Barcode,
            ["image"] = Image,
            ["separator"] = Separator,
            ["svg"] = Svg,
            ["table"] = Table,
            ["each"] = Each,
            ["if"] = If,
            ["content"] = Content
        };

    /// <summary>
    /// Validates that all keys in the given YAML mapping node are known properties for the specified element type.
    /// Throws <see cref="TemplateParseException"/> if unknown properties are found.
    /// </summary>
    /// <param name="node">The YAML mapping node representing the element.</param>
    /// <param name="elementType">The element type name (e.g., "text", "flex").</param>
    /// <exception cref="TemplateParseException">
    /// Thrown when one or more unknown properties are found on the element.
    /// </exception>
    internal static void Validate(YamlMappingNode node, string elementType)
    {
        if (!Registry.TryGetValue(elementType, out var knownProperties))
        {
            // Unknown element type — skip validation (handled elsewhere).
            return;
        }

        List<string>? unknown = null;

        foreach (var key in node.Children.Keys)
        {
            if (key is not YamlScalarNode scalarKey || scalarKey.Value is null)
            {
                continue;
            }

            var keyName = scalarKey.Value;

            if (string.Equals(keyName, TypeKey, StringComparison.Ordinal))
            {
                continue;
            }

            if (!knownProperties.Contains(keyName))
            {
                unknown ??= [];
                unknown.Add(keyName);
            }
        }

        if (unknown is { Count: > 0 })
        {
            var unknownList = string.Join(", ", unknown.Select(u => $"'{u}'"));
            var suggestion = BuildSuggestion(unknown, knownProperties);

            var message = $"Unknown properties on '{elementType}' element: [{unknownList}].";
            if (suggestion.Length > 0)
            {
                message += $" Did you mean: {suggestion}?";
            }

            throw new TemplateParseException(message);
        }
    }

    /// <summary>
    /// Builds a suggestion string for unknown properties by finding close matches in the known set
    /// using Levenshtein distance.
    /// </summary>
    /// <param name="unknownProps">The list of unknown property names.</param>
    /// <param name="knownSet">The set of known property names to match against.</param>
    /// <returns>A comma-separated string of suggested replacements, or empty if none found.</returns>
    private static string BuildSuggestion(List<string> unknownProps, HashSet<string> knownSet)
    {
        var suggestions = new List<string>();

        foreach (var unknown in unknownProps)
        {
            string? bestMatch = null;
            var bestDistance = int.MaxValue;

            foreach (var known in knownSet)
            {
                var distance = LevenshteinDistance(unknown, known);
                if (distance < bestDistance && distance <= 3)
                {
                    bestDistance = distance;
                    bestMatch = known;
                }
            }

            if (bestMatch is not null)
            {
                suggestions.Add($"'{bestMatch}'");
            }
        }

        return string.Join(", ", suggestions);
    }

    /// <summary>
    /// Computes the Levenshtein edit distance between two strings.
    /// Used for fuzzy matching of unknown property names to known ones.
    /// </summary>
    /// <param name="source">The source string.</param>
    /// <param name="target">The target string.</param>
    /// <returns>The minimum number of single-character edits needed to transform source into target.</returns>
    private static int LevenshteinDistance(string source, string target)
    {
        var sourceLen = source.Length;
        var targetLen = target.Length;

        if (sourceLen == 0) return targetLen;
        if (targetLen == 0) return sourceLen;

        // Use a single-row buffer to reduce memory allocation
        var previousRow = new int[targetLen + 1];
        var currentRow = new int[targetLen + 1];

        for (var j = 0; j <= targetLen; j++)
        {
            previousRow[j] = j;
        }

        for (var i = 1; i <= sourceLen; i++)
        {
            currentRow[0] = i;

            for (var j = 1; j <= targetLen; j++)
            {
                var cost = source[i - 1] == target[j - 1] ? 0 : 1;
                currentRow[j] = Math.Min(
                    Math.Min(currentRow[j - 1] + 1, previousRow[j] + 1),
                    previousRow[j - 1] + cost);
            }

            (previousRow, currentRow) = (currentRow, previousRow);
        }

        return previousRow[targetLen];
    }

    /// <summary>
    /// Creates a <see cref="HashSet{T}"/> containing all strings from the base array
    /// combined with the element-specific properties.
    /// </summary>
    /// <param name="baseProperties">The common/flex-item property names.</param>
    /// <param name="elementProperties">The element-specific property names.</param>
    /// <returns>A combined set of all known property names.</returns>
    private static HashSet<string> BuildSet(string[] baseProperties, string[] elementProperties)
    {
        var set = new HashSet<string>(baseProperties.Length + elementProperties.Length, StringComparer.Ordinal);
        foreach (var prop in baseProperties)
        {
            set.Add(prop);
        }
        foreach (var prop in elementProperties)
        {
            set.Add(prop);
        }
        return set;
    }
}
