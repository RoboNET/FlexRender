using FlexRender.Parsing.Ast;
using FlexRender.Parsing.Nodes;

namespace FlexRender.Parsing;

/// <summary>
/// Provides static helper methods for extracting typed property values from neutral mapping nodes.
/// </summary>
internal static class NodePropertyHelpers
{
    /// <summary>
    /// Tries to get a mapping node from a parent node by key.
    /// </summary>
    /// <param name="parent">The parent mapping node.</param>
    /// <param name="key">The key to look up.</param>
    /// <param name="result">The resulting mapping node if found.</param>
    /// <returns>True if the key exists and is a mapping node; otherwise, false.</returns>
    internal static bool TryGetMapping(TemplateMapping parent, string key, out TemplateMapping result)
        => parent.TryGetMapping(key, out result);

    /// <summary>
    /// Tries to get a sequence node from a parent node by key.
    /// </summary>
    /// <param name="parent">The parent mapping node.</param>
    /// <param name="key">The key to look up.</param>
    /// <param name="result">The resulting sequence node if found.</param>
    /// <returns>True if the key exists and is a sequence node; otherwise, false.</returns>
    internal static bool TryGetSequence(TemplateMapping parent, string key, out TemplateSequence result)
        => parent.TryGetSequence(key, out result);

    /// <summary>
    /// Gets a string value from a mapping node by key.
    /// </summary>
    /// <param name="node">The mapping node to search.</param>
    /// <param name="key">The key to look up.</param>
    /// <returns>The string value if found; otherwise, null.</returns>
    internal static string? GetStringValue(TemplateMapping node, string key) => node.GetScalar(key);

    /// <summary>
    /// Gets a string value from a mapping node by key with a default value.
    /// </summary>
    /// <param name="node">The mapping node to search.</param>
    /// <param name="key">The key to look up.</param>
    /// <param name="defaultValue">The default value if the key is not found.</param>
    /// <returns>The string value if found; otherwise, the default value.</returns>
    internal static string GetStringValue(TemplateMapping node, string key, string defaultValue)
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
    internal static int GetIntValue(TemplateMapping node, string key, int defaultValue)
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
    /// <param name="node">The mapping node to search.</param>
    /// <param name="key">The key to look up.</param>
    /// <param name="defaultValue">The default value if the key is not found or cannot be parsed.</param>
    /// <returns>The float value if found and valid; otherwise, the default value.</returns>
    internal static float GetFloatValue(TemplateMapping node, string key, float defaultValue)
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
    internal static int? GetNullableIntValue(TemplateMapping node, string key)
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
    internal static bool GetBoolValue(TemplateMapping node, string key, bool defaultValue)
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
    internal static bool? GetNullableBoolValue(TemplateMapping node, string key)
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
    internal static float? GetNullableFloatValue(TemplateMapping node, string key)
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
    internal static double? GetDoubleValue(TemplateMapping node, string key)
    {
        var strValue = GetStringValue(node, key);
        if (strValue != null && double.TryParse(strValue, System.Globalization.NumberStyles.Float,
            System.Globalization.CultureInfo.InvariantCulture, out var doubleValue))
        {
            return doubleValue;
        }
        return null;
    }

    /// <summary>
    /// Determines whether a raw string value contains a template expression ({{ }}).
    /// </summary>
    /// <param name="value">The raw string to check.</param>
    /// <returns>True if the value contains a template expression; otherwise, false.</returns>
    internal static bool ContainsExpression(string? value)
    {
        return value is not null && value.Contains("{{", StringComparison.Ordinal);
    }

    /// <summary>
    /// Gets a string <see cref="ExprValue{T}"/> from a mapping node by key.
    /// If the raw value contains a template expression, returns <see cref="ExprValue{T}.Expression(string)"/>
    /// so that the pipeline can resolve it; otherwise, returns a literal <see cref="ExprValue{T}"/>.
    /// </summary>
    /// <param name="node">The mapping node to search.</param>
    /// <param name="key">The key to look up.</param>
    /// <param name="defaultValue">The default value if the key is not found.</param>
    /// <returns>An <see cref="ExprValue{T}"/> containing the string literal, expression, or default.</returns>
    internal static ExprValue<string> GetExprStringValue(TemplateMapping node, string key, string defaultValue)
    {
        var strValue = GetStringValue(node, key);
        if (strValue is null)
            return defaultValue;

        if (ContainsExpression(strValue))
            return ExprValue<string>.Expression(strValue);

        return strValue;
    }

    /// <summary>
    /// Gets an optional string <see cref="ExprValue{T}"/> from a mapping node by key.
    /// Returns <c>default</c> if the key is not found; otherwise, detects expressions and
    /// returns either an expression or literal <see cref="ExprValue{T}"/>.
    /// </summary>
    /// <param name="node">The mapping node to search.</param>
    /// <param name="key">The key to look up.</param>
    /// <returns>An <see cref="ExprValue{T}"/> containing the string value or expression, or <c>default</c> if not found.</returns>
    internal static ExprValue<string> GetExprStringValueOptional(TemplateMapping node, string key)
    {
        var strValue = GetStringValue(node, key);
        if (strValue is null)
            return default;

        if (ContainsExpression(strValue))
            return ExprValue<string>.Expression(strValue);

        return strValue;
    }

    /// <summary>
    /// Gets an optional string <see cref="ExprValue{T}"/> trying two keys (e.g., kebab-case and camelCase).
    /// Returns <c>default</c> if neither key is found; otherwise, detects expressions.
    /// </summary>
    /// <param name="node">The mapping node to search.</param>
    /// <param name="key1">The first key to try (e.g., kebab-case).</param>
    /// <param name="key2">The fallback key to try (e.g., camelCase).</param>
    /// <returns>An <see cref="ExprValue{T}"/> containing the string value or expression, or <c>default</c> if not found.</returns>
    internal static ExprValue<string> GetExprStringValueOptional(TemplateMapping node, string key1, string key2)
    {
        var strValue = GetStringValue(node, key1) ?? GetStringValue(node, key2);
        if (strValue is null)
            return default;

        if (ContainsExpression(strValue))
            return ExprValue<string>.Expression(strValue);

        return strValue;
    }

    /// <summary>
    /// Gets a float <see cref="ExprValue{T}"/> from a mapping node by key.
    /// If the raw value contains a template expression, returns <see cref="ExprValue{T}.Expression(string)"/>
    /// instead of trying to parse; otherwise, parses normally or returns the default.
    /// </summary>
    /// <param name="node">The mapping node to search.</param>
    /// <param name="key">The key to look up.</param>
    /// <param name="defaultValue">The default value if the key is not found or cannot be parsed.</param>
    /// <returns>An <see cref="ExprValue{T}"/> containing the parsed float, expression, or default.</returns>
    internal static ExprValue<float> GetExprFloatValue(TemplateMapping node, string key, float defaultValue)
    {
        var strValue = GetStringValue(node, key);
        if (strValue is null)
            return defaultValue;

        if (ContainsExpression(strValue))
            return ExprValue<float>.Expression(strValue);

        if (float.TryParse(strValue, System.Globalization.NumberStyles.Float,
            System.Globalization.CultureInfo.InvariantCulture, out var floatValue))
            return floatValue;

        return defaultValue;
    }

    /// <summary>
    /// Gets a bool <see cref="ExprValue{T}"/> from a mapping node by key.
    /// If the raw value contains a template expression, returns <see cref="ExprValue{T}.Expression(string)"/>
    /// instead of trying to parse; otherwise, parses normally or returns the default.
    /// </summary>
    /// <param name="node">The mapping node to search.</param>
    /// <param name="key">The key to look up.</param>
    /// <param name="defaultValue">The default value if the key is not found or cannot be parsed.</param>
    /// <returns>An <see cref="ExprValue{T}"/> containing the parsed bool, expression, or default.</returns>
    internal static ExprValue<bool> GetExprBoolValue(TemplateMapping node, string key, bool defaultValue)
    {
        var strValue = GetStringValue(node, key);
        if (strValue is null)
            return defaultValue;

        if (ContainsExpression(strValue))
            return ExprValue<bool>.Expression(strValue);

        if (bool.TryParse(strValue, out var boolValue))
            return boolValue;

        return defaultValue;
    }

    /// <summary>
    /// Gets a nullable int <see cref="ExprValue{T}"/> from a mapping node by key.
    /// If the raw value contains a template expression, returns <see cref="ExprValue{T}.Expression(string)"/>
    /// instead of trying to parse; otherwise, parses normally or returns null.
    /// </summary>
    /// <param name="node">The mapping node to search.</param>
    /// <param name="key">The key to look up.</param>
    /// <returns>An <see cref="ExprValue{T}"/> containing the parsed int, expression, or null.</returns>
    internal static ExprValue<int?> GetExprNullableIntValue(TemplateMapping node, string key)
    {
        var strValue = GetStringValue(node, key);
        if (strValue is null)
            return new ExprValue<int?>((int?)null);

        if (ContainsExpression(strValue))
            return ExprValue<int?>.Expression(strValue);

        if (int.TryParse(strValue, out var intValue))
            return new ExprValue<int?>((int?)intValue);

        return new ExprValue<int?>((int?)null);
    }

    /// <summary>
    /// Gets an int <see cref="ExprValue{T}"/> from a mapping node by key.
    /// If the raw value contains a template expression, returns <see cref="ExprValue{T}.Expression(string)"/>
    /// instead of trying to parse; otherwise, parses normally or returns the default.
    /// </summary>
    /// <param name="node">The mapping node to search.</param>
    /// <param name="key">The key to look up.</param>
    /// <param name="defaultValue">The default value if the key is not found or cannot be parsed.</param>
    /// <returns>An <see cref="ExprValue{T}"/> containing the parsed int, expression, or default.</returns>
    internal static ExprValue<int> GetExprIntValue(TemplateMapping node, string key, int defaultValue)
    {
        var strValue = GetStringValue(node, key);
        if (strValue is null)
            return defaultValue;

        if (ContainsExpression(strValue))
            return ExprValue<int>.Expression(strValue);

        if (int.TryParse(strValue, out var intValue))
            return intValue;

        return defaultValue;
    }

    /// <summary>
    /// Recursively converts a mapping node to a string-keyed dictionary.
    /// </summary>
    /// <param name="mapping">The mapping node to convert.</param>
    /// <param name="depth">Current recursion depth (max 10).</param>
    /// <returns>A case-insensitive dictionary representing the mapping contents.</returns>
    internal static IReadOnlyDictionary<string, object> ConvertMappingToDictionary(TemplateMapping mapping, int depth = 0)
    {
        if (depth > 10)
            throw new InvalidOperationException("Options nesting depth exceeded (max 10).");

        var dict = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
        foreach (var key in mapping.Keys)
        {
            mapping.TryGet(key, out var valueNode);
            dict[key] = valueNode switch
            {
                TemplateScalar scalar => scalar.Value ?? string.Empty,
                TemplateMapping nested => ConvertMappingToDictionary(nested, depth + 1),
                TemplateSequence seq => ConvertSequenceToList(seq, depth + 1),
                _ => string.Empty
            };
        }
        return dict;
    }

    /// <summary>
    /// Recursively converts a sequence node to a list of objects.
    /// </summary>
    /// <param name="sequence">The sequence node to convert.</param>
    /// <param name="depth">Current recursion depth.</param>
    /// <returns>A list of objects representing the sequence contents.</returns>
    private static List<object> ConvertSequenceToList(TemplateSequence sequence, int depth = 0)
    {
        if (depth > 10)
            throw new InvalidOperationException("Options nesting depth exceeded (max 10).");

        var list = new List<object>(sequence.Items.Count);
        foreach (var child in sequence.Items)
        {
            list.Add(child switch
            {
                TemplateScalar scalar => scalar.Value ?? string.Empty,
                TemplateMapping nested => ConvertMappingToDictionary(nested, depth + 1),
                TemplateSequence seq => ConvertSequenceToList(seq, depth + 1),
                _ => string.Empty
            });
        }
        return list;
    }
}
