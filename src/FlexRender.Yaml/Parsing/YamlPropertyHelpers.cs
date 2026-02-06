using YamlDotNet.RepresentationModel;

namespace FlexRender.Parsing;

/// <summary>
/// Provides static helper methods for extracting typed property values from YAML mapping nodes.
/// </summary>
internal static class YamlPropertyHelpers
{
    /// <summary>
    /// Tries to get a mapping node from a parent node by key.
    /// </summary>
    /// <param name="parent">The parent mapping node.</param>
    /// <param name="key">The key to look up.</param>
    /// <param name="result">The resulting mapping node if found.</param>
    /// <returns>True if the key exists and is a mapping node; otherwise, false.</returns>
    internal static bool TryGetMapping(YamlMappingNode parent, string key, out YamlMappingNode result)
    {
        result = null!;
        var scalarKey = new YamlScalarNode(key);
        if (parent.Children.TryGetValue(scalarKey, out var node) && node is YamlMappingNode mapping)
        {
            result = mapping;
            return true;
        }
        return false;
    }

    /// <summary>
    /// Tries to get a sequence node from a parent node by key.
    /// </summary>
    /// <param name="parent">The parent mapping node.</param>
    /// <param name="key">The key to look up.</param>
    /// <param name="result">The resulting sequence node if found.</param>
    /// <returns>True if the key exists and is a sequence node; otherwise, false.</returns>
    internal static bool TryGetSequence(YamlMappingNode parent, string key, out YamlSequenceNode result)
    {
        result = null!;
        var scalarKey = new YamlScalarNode(key);
        if (parent.Children.TryGetValue(scalarKey, out var node) && node is YamlSequenceNode sequence)
        {
            result = sequence;
            return true;
        }
        return false;
    }

    /// <summary>
    /// Gets a string value from a mapping node by key.
    /// </summary>
    /// <param name="node">The mapping node to search.</param>
    /// <param name="key">The key to look up.</param>
    /// <returns>The string value if found; otherwise, null.</returns>
    internal static string? GetStringValue(YamlMappingNode node, string key)
    {
        var scalarKey = new YamlScalarNode(key);
        if (node.Children.TryGetValue(scalarKey, out var value) && value is YamlScalarNode scalar)
        {
            return scalar.Value;
        }
        return null;
    }

    /// <summary>
    /// Gets a string value from a mapping node by key with a default value.
    /// </summary>
    /// <param name="node">The mapping node to search.</param>
    /// <param name="key">The key to look up.</param>
    /// <param name="defaultValue">The default value if the key is not found.</param>
    /// <returns>The string value if found; otherwise, the default value.</returns>
    internal static string GetStringValue(YamlMappingNode node, string key, string defaultValue)
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
    internal static int GetIntValue(YamlMappingNode node, string key, int defaultValue)
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
    internal static float GetFloatValue(YamlMappingNode node, string key, float defaultValue)
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
    internal static int? GetNullableIntValue(YamlMappingNode node, string key)
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
    internal static bool GetBoolValue(YamlMappingNode node, string key, bool defaultValue)
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
    internal static bool? GetNullableBoolValue(YamlMappingNode node, string key)
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
    internal static float? GetNullableFloatValue(YamlMappingNode node, string key)
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
    internal static double? GetDoubleValue(YamlMappingNode node, string key)
    {
        var strValue = GetStringValue(node, key);
        if (strValue != null && double.TryParse(strValue, System.Globalization.NumberStyles.Float,
            System.Globalization.CultureInfo.InvariantCulture, out var doubleValue))
        {
            return doubleValue;
        }
        return null;
    }
}
