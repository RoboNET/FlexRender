using System.Text;
using System.Text.RegularExpressions;
using FlexRender.Configuration;

namespace FlexRender.Parsing;

/// <summary>
/// Preprocesses YAML templates to expand <c>{{#each}}</c> and <c>{{#if}}</c> blocks before parsing.
/// This allows dynamic generation of YAML structure based on data arrays and conditional logic.
/// </summary>
/// <remarks>
/// Only standalone block tags (those appearing as the sole content on a line) are processed.
/// Inline expressions like <c>content: "{{#if isProfit}}+{{/if}}"</c> are left untouched
/// for the runtime <c>TemplateProcessor</c> to handle.
/// </remarks>
public static partial class YamlPreprocessor
{
    [GeneratedRegex(@"\{\{#each\s+(\w+(?:\.\w+)*)\}\}", RegexOptions.None, matchTimeoutMilliseconds: 1000)]
    private static partial Regex EachStartRegex();

    [GeneratedRegex(@"\{\{/each\}\}", RegexOptions.None, matchTimeoutMilliseconds: 1000)]
    private static partial Regex EachEndRegex();

    /// <summary>
    /// Matches a standalone <c>{{#if conditionPath}}</c> tag on its own line with optional surrounding whitespace.
    /// </summary>
    [GeneratedRegex(@"^[ \t]*\{\{#if\s+(\w+(?:\.\w+)*)\}\}[ \t]*$", RegexOptions.Multiline, matchTimeoutMilliseconds: 1000)]
    private static partial Regex IfStartRegex();

    /// <summary>
    /// Matches a standalone <c>{{/if}}</c> tag on its own line with optional surrounding whitespace.
    /// </summary>
    [GeneratedRegex(@"^[ \t]*\{\{/if\}\}[ \t]*$", RegexOptions.Multiline, matchTimeoutMilliseconds: 1000)]
    private static partial Regex IfEndRegex();

    /// <summary>
    /// Matches a standalone <c>{{else}}</c> tag on its own line with optional surrounding whitespace.
    /// </summary>
    [GeneratedRegex(@"^[ \t]*\{\{else\}\}[ \t]*$", RegexOptions.Multiline, matchTimeoutMilliseconds: 1000)]
    private static partial Regex ElseRegex();

    /// <summary>
    /// Preprocesses YAML template, expanding <c>{{#each}}</c> and <c>{{#if}}</c> blocks with data.
    /// Uses default resource limits.
    /// </summary>
    /// <param name="yaml">The YAML template with block expressions.</param>
    /// <param name="data">The data to use for expansion.</param>
    /// <returns>Expanded YAML with block expressions replaced by their evaluated content.</returns>
    /// <exception cref="ArgumentException">Thrown when input exceeds maximum allowed size.</exception>
    /// <exception cref="RegexMatchTimeoutException">Thrown when regex matching exceeds timeout.</exception>
    public static string Preprocess(string yaml, TemplateValue? data)
    {
        return Preprocess(yaml, data, new ResourceLimits());
    }

    /// <summary>
    /// Preprocesses YAML template, expanding <c>{{#each}}</c> and <c>{{#if}}</c> blocks with data.
    /// </summary>
    /// <param name="yaml">The YAML template with block expressions.</param>
    /// <param name="data">The data to use for expansion.</param>
    /// <param name="limits">The resource limits to apply.</param>
    /// <returns>Expanded YAML with block expressions replaced by their evaluated content.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="limits"/> is null.</exception>
    /// <exception cref="ArgumentException">Thrown when input exceeds maximum allowed size.</exception>
    /// <exception cref="RegexMatchTimeoutException">Thrown when regex matching exceeds timeout.</exception>
    public static string Preprocess(string yaml, TemplateValue? data, ResourceLimits limits)
    {
        ArgumentNullException.ThrowIfNull(limits);

        if (string.IsNullOrEmpty(yaml) || data == null)
            return yaml;

        // Validate input size to prevent denial of service attacks
        var inputSizeBytes = Encoding.UTF8.GetByteCount(yaml);
        if (inputSizeBytes > limits.MaxPreprocessorInputSize)
        {
            throw new ArgumentException(
                $"Input YAML exceeds maximum allowed size of {limits.MaxPreprocessorInputSize / 1024.0 / 1024.0:F2} MB. " +
                $"Actual size: {inputSizeBytes / 1024.0 / 1024.0:F2} MB.",
                nameof(yaml));
        }

        return ProcessBlocks(yaml, data, 0, limits);
    }

    /// <summary>
    /// Processes all block expressions (<c>{{#each}}</c> and <c>{{#if}}</c>) in the YAML content.
    /// Scans for whichever block tag appears first and processes it accordingly.
    /// </summary>
    /// <param name="yaml">The YAML content to process.</param>
    /// <param name="context">The current data context for resolving values.</param>
    /// <param name="depth">The current nesting depth for recursion protection.</param>
    /// <param name="limits">The resource limits to apply.</param>
    /// <returns>The processed YAML content with all block expressions expanded.</returns>
    /// <exception cref="TemplateParseException">Thrown when nesting depth is exceeded or blocks are unclosed.</exception>
    private static string ProcessBlocks(string yaml, TemplateValue context, int depth, ResourceLimits limits)
    {
        if (depth > limits.MaxPreprocessorNestingDepth)
            throw new TemplateParseException($"Maximum nesting depth ({limits.MaxPreprocessorNestingDepth}) exceeded in template preprocessing");

        var result = new StringBuilder();
        int position = 0;

        while (position < yaml.Length)
        {
            var eachMatch = EachStartRegex().Match(yaml, position);
            var ifMatch = IfStartRegex().Match(yaml, position);

            bool hasEach = eachMatch.Success;
            bool hasIf = ifMatch.Success;

            // No more blocks found, append the rest and exit
            if (!hasEach && !hasIf)
            {
                result.Append(yaml[position..]);
                break;
            }

            // Determine which block comes first
            bool processEach;
            if (hasEach && hasIf)
                processEach = eachMatch.Index <= ifMatch.Index;
            else
                processEach = hasEach;

            if (processEach)
            {
                // Add content before {{#each}}
                result.Append(yaml[position..eachMatch.Index]);

                // Find matching {{/each}}
                var blockEnd = FindMatchingEachEnd(yaml, eachMatch.Index + eachMatch.Length);
                if (blockEnd == -1)
                    throw new TemplateParseException("Unclosed {{#each}} block");

                var blockContent = yaml[(eachMatch.Index + eachMatch.Length)..blockEnd];
                var arrayPath = eachMatch.Groups[1].Value;

                // Get array from context
                var arrayValue = ResolveValue(context, arrayPath);
                if (arrayValue is ArrayValue array)
                {
                    for (int i = 0; i < array.Count; i++)
                    {
                        var itemContext = CreateItemContext(context, array[i], i, array.Count);
                        var expandedBlock = ProcessBlocks(blockContent, itemContext, depth + 1, limits);

                        // Replace {{this}} and item properties
                        expandedBlock = ReplaceItemReferences(expandedBlock, array[i], i, array.Count);
                        result.Append(expandedBlock);
                    }
                }

                // Move past {{/each}} -- consume the entire end-tag line including the trailing newline
                var endMatch = EachEndRegex().Match(yaml, blockEnd);
                position = endMatch.Index + endMatch.Length;

                // Consume trailing newline after {{/each}} tag line
                position = ConsumeTrailingNewline(yaml, position);
            }
            else
            {
                // Add content before {{#if}}
                result.Append(yaml[position..ifMatch.Index]);

                // Find matching {{/if}}
                var blockEnd = FindMatchingIfEnd(yaml, ifMatch.Index + ifMatch.Length);
                if (blockEnd == -1)
                    throw new TemplateParseException("Unclosed {{#if}} block");

                var blockContent = yaml[(ifMatch.Index + ifMatch.Length)..blockEnd];
                var conditionPath = ifMatch.Groups[1].Value;

                // Evaluate condition
                var conditionValue = ResolveValue(context, conditionPath);
                bool isTruthy = IsTruthy(conditionValue);

                // Split into then/else blocks
                var (thenBlock, elseBlock) = SplitThenElse(blockContent);

                // Select the appropriate block
                var selectedBlock = isTruthy ? thenBlock : elseBlock;

                if (selectedBlock != null)
                {
                    // Recursively process the selected block
                    var expandedBlock = ProcessBlocks(selectedBlock, context, depth + 1, limits);
                    result.Append(expandedBlock);
                }

                // Move past {{/if}} -- consume the entire end-tag line including the trailing newline
                var endMatch = IfEndRegex().Match(yaml, blockEnd);
                position = endMatch.Index + endMatch.Length;

                // Consume trailing newline after {{/if}} tag line
                position = ConsumeTrailingNewline(yaml, position);
            }
        }

        return result.ToString();
    }

    /// <summary>
    /// Determines whether a <see cref="TemplateValue"/> is truthy for conditional evaluation.
    /// </summary>
    /// <param name="value">The value to evaluate.</param>
    /// <returns>
    /// <c>true</c> if the value is truthy; <c>false</c> if the value is null, <see cref="NullValue"/>,
    /// false, empty string, zero, an empty array, or an empty object.
    /// </returns>
    private static bool IsTruthy(TemplateValue? value) => value switch
    {
        null => false,
        NullValue => false,
        BoolValue b => b.Value,
        StringValue s => !string.IsNullOrEmpty(s.Value),
        NumberValue n => n.Value != 0,
        ArrayValue a => a.Count > 0,
        ObjectValue o => o.Count > 0,
        _ => false
    };

    /// <summary>
    /// Splits a block into "then" and "else" parts by finding a standalone <c>{{else}}</c> at nesting depth zero.
    /// </summary>
    /// <param name="blockContent">The content between <c>{{#if}}</c> and <c>{{/if}}</c> tags.</param>
    /// <returns>
    /// A tuple of (thenBlock, elseBlock) where elseBlock is null if no <c>{{else}}</c> was found.
    /// </returns>
    private static (string thenBlock, string? elseBlock) SplitThenElse(string blockContent)
    {
        int ifDepth = 0;
        int eachDepth = 0;
        int position = 0;

        while (position < blockContent.Length)
        {
            // Look for the next interesting tag
            var nextIfStart = IfStartRegex().Match(blockContent, position);
            var nextIfEnd = IfEndRegex().Match(blockContent, position);
            var nextEachStart = EachStartRegex().Match(blockContent, position);
            var nextEachEnd = EachEndRegex().Match(blockContent, position);
            var nextElse = ElseRegex().Match(blockContent, position);

            // Find the earliest match
            int earliestIndex = int.MaxValue;
            string earliestType = "";
            Match? earliestMatch = null;

            if (nextIfStart.Success && nextIfStart.Index < earliestIndex)
            {
                earliestIndex = nextIfStart.Index;
                earliestType = "ifStart";
                earliestMatch = nextIfStart;
            }
            if (nextIfEnd.Success && nextIfEnd.Index < earliestIndex)
            {
                earliestIndex = nextIfEnd.Index;
                earliestType = "ifEnd";
                earliestMatch = nextIfEnd;
            }
            if (nextEachStart.Success && nextEachStart.Index < earliestIndex)
            {
                earliestIndex = nextEachStart.Index;
                earliestType = "eachStart";
                earliestMatch = nextEachStart;
            }
            if (nextEachEnd.Success && nextEachEnd.Index < earliestIndex)
            {
                earliestIndex = nextEachEnd.Index;
                earliestType = "eachEnd";
                earliestMatch = nextEachEnd;
            }
            if (nextElse.Success && nextElse.Index < earliestIndex)
            {
                earliestIndex = nextElse.Index;
                earliestType = "else";
                earliestMatch = nextElse;
            }

            if (earliestMatch == null)
                break;

            switch (earliestType)
            {
                case "ifStart":
                    ifDepth++;
                    position = earliestMatch.Index + earliestMatch.Length;
                    break;
                case "ifEnd":
                    ifDepth--;
                    position = earliestMatch.Index + earliestMatch.Length;
                    break;
                case "eachStart":
                    eachDepth++;
                    position = earliestMatch.Index + earliestMatch.Length;
                    break;
                case "eachEnd":
                    eachDepth--;
                    position = earliestMatch.Index + earliestMatch.Length;
                    break;
                case "else":
                    if (ifDepth == 0 && eachDepth == 0)
                    {
                        // Found the top-level {{else}} -- split here
                        // The then block is everything before the {{else}} line
                        var thenBlock = blockContent[..earliestMatch.Index];
                        // The else block is everything after the {{else}} line
                        var elseStart = earliestMatch.Index + earliestMatch.Length;
                        elseStart = ConsumeTrailingNewline(blockContent, elseStart);
                        var elseBlock = blockContent[elseStart..];
                        return (thenBlock, elseBlock);
                    }
                    position = earliestMatch.Index + earliestMatch.Length;
                    break;
            }
        }

        // No {{else}} found
        return (blockContent, null);
    }

    /// <summary>
    /// Finds the position of the matching <c>{{/each}}</c> for a block, accounting for nested
    /// <c>{{#each}}</c> blocks.
    /// </summary>
    /// <param name="yaml">The full YAML content.</param>
    /// <param name="startPosition">The position after the opening <c>{{#each}}</c> tag.</param>
    /// <returns>The index of the matching <c>{{/each}}</c> tag, or -1 if not found.</returns>
    private static int FindMatchingEachEnd(string yaml, int startPosition)
    {
        int depth = 1;
        int position = startPosition;

        while (position < yaml.Length && depth > 0)
        {
            var nextStart = EachStartRegex().Match(yaml, position);
            var nextEnd = EachEndRegex().Match(yaml, position);

            if (!nextEnd.Success)
                return -1;

            if (nextStart.Success && nextStart.Index < nextEnd.Index)
            {
                depth++;
                position = nextStart.Index + nextStart.Length;
            }
            else
            {
                depth--;
                if (depth == 0)
                    return nextEnd.Index;
                position = nextEnd.Index + nextEnd.Length;
            }
        }

        return -1;
    }

    /// <summary>
    /// Finds the position of the matching <c>{{/if}}</c> for a block, accounting for nested
    /// <c>{{#if}}</c> blocks.
    /// </summary>
    /// <param name="yaml">The full YAML content.</param>
    /// <param name="startPosition">The position after the opening <c>{{#if}}</c> tag.</param>
    /// <returns>The index of the matching <c>{{/if}}</c> tag, or -1 if not found.</returns>
    private static int FindMatchingIfEnd(string yaml, int startPosition)
    {
        int depth = 1;
        int position = startPosition;

        while (position < yaml.Length && depth > 0)
        {
            var nextStart = IfStartRegex().Match(yaml, position);
            var nextEnd = IfEndRegex().Match(yaml, position);

            if (!nextEnd.Success)
                return -1;

            if (nextStart.Success && nextStart.Index < nextEnd.Index)
            {
                depth++;
                position = nextStart.Index + nextStart.Length;
            }
            else
            {
                depth--;
                if (depth == 0)
                    return nextEnd.Index;
                position = nextEnd.Index + nextEnd.Length;
            }
        }

        return -1;
    }

    /// <summary>
    /// Consumes a single trailing newline character (LF or CRLF) at the given position.
    /// </summary>
    /// <param name="text">The text to scan.</param>
    /// <param name="position">The current position.</param>
    /// <returns>The position after consuming the newline, or the original position if no newline is present.</returns>
    private static int ConsumeTrailingNewline(string text, int position)
    {
        if (position < text.Length && text[position] == '\r')
            position++;
        if (position < text.Length && text[position] == '\n')
            position++;
        return position;
    }

    /// <summary>
    /// Resolves a dot-notation path against the current data context.
    /// </summary>
    /// <param name="context">The data context to resolve against.</param>
    /// <param name="path">The dot-notation path (e.g., "user.name").</param>
    /// <returns>The resolved value, or null if the path cannot be resolved.</returns>
    private static TemplateValue? ResolveValue(TemplateValue context, string path)
    {
        var segments = path.Split('.');
        TemplateValue? current = context;

        foreach (var segment in segments)
        {
            if (current is ObjectValue obj && obj.TryGetValue(segment, out var value))
            {
                current = value;
            }
            else
            {
                return null;
            }
        }

        return current;
    }

    /// <summary>
    /// Creates a merged item context for <c>{{#each}}</c> iteration.
    /// Currently returns the item as-is; can be extended to merge with parent context.
    /// </summary>
    /// <param name="parentContext">The parent data context.</param>
    /// <param name="item">The current array item.</param>
    /// <param name="index">The zero-based index of the current item.</param>
    /// <param name="count">The total number of items in the array.</param>
    /// <returns>The data context for the current iteration.</returns>
    private static TemplateValue CreateItemContext(TemplateValue parentContext, TemplateValue item, int index, int count)
    {
        // For now, just return the item - could be extended to merge with parent
        return item;
    }

    /// <summary>
    /// Replaces item-level template references within <c>{{#each}}</c> block content.
    /// Handles <c>{{this}}</c>, <c>{{@index}}</c>, <c>{{@first}}</c>, <c>{{@last}}</c>,
    /// and object property placeholders.
    /// </summary>
    /// <param name="content">The block content to process.</param>
    /// <param name="item">The current array item value.</param>
    /// <param name="index">The zero-based index of the current item.</param>
    /// <param name="count">The total number of items in the array.</param>
    /// <returns>The content with item references replaced.</returns>
    private static string ReplaceItemReferences(string content, TemplateValue item, int index, int count)
    {
        // Replace @index, @first, @last
        content = content.Replace("{{@index}}", index.ToString());
        content = content.Replace("{{@first}}", (index == 0).ToString().ToLowerInvariant());
        content = content.Replace("{{@last}}", (index == count - 1).ToString().ToLowerInvariant());

        // Replace {{this}} with item value
        if (item is StringValue sv)
        {
            content = content.Replace("{{this}}", sv.Value);
        }

        // Replace {{propertyName}} from item object
        if (item is ObjectValue obj)
        {
            foreach (var key in obj.Keys)
            {
                var placeholder = "{{" + key + "}}";
                var propValue = obj[key];
                var value = propValue switch
                {
                    StringValue s => s.Value,
                    NumberValue n => n.Value.ToString(System.Globalization.CultureInfo.InvariantCulture),
                    BoolValue b => b.Value.ToString().ToLowerInvariant(),
                    _ => propValue.ToString() ?? ""
                };
                content = content.Replace(placeholder, value);
            }
        }

        return content;
    }
}
