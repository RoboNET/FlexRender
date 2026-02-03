using System.Globalization;
using System.Text.RegularExpressions;

namespace FlexRender.TemplateEngine;

/// <summary>
/// Evaluates path expressions against template data.
/// </summary>
public partial class ExpressionEvaluator
{
    /// <summary>
    /// Maximum allowed length for path expressions to prevent ReDoS attacks.
    /// </summary>
    public const int MaxPathLength = 1000;

    /// <summary>
    /// Maximum allowed array index value to prevent resource exhaustion.
    /// </summary>
    public const int MaxArrayIndex = 10000;

    [GeneratedRegex(@"\[(\d+)\]")]
    private static partial Regex ArrayIndexRegex();

    /// <summary>
    /// Resolves a path expression to a value.
    /// </summary>
    /// <param name="path">The path expression (e.g., "user.name" or "items[0].price").</param>
    /// <param name="context">The template context containing current scope.</param>
    /// <returns>The resolved value, or NullValue if not found.</returns>
    /// <exception cref="TemplateEngineException">Thrown when the path exceeds the maximum allowed length.</exception>
    public static TemplateValue Resolve(string path, TemplateContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (string.IsNullOrEmpty(path))
        {
            return NullValue.Instance;
        }

        // Security: Validate input length to prevent ReDoS attacks
        if (path.Length > MaxPathLength)
        {
            throw new TemplateEngineException(
                $"Path expression length ({path.Length}) exceeds maximum allowed length ({MaxPathLength})");
        }

        // Handle current scope reference
        if (path == ".")
        {
            return context.CurrentScope;
        }

        // Handle loop variables
        if (path.StartsWith('@'))
        {
            return ResolveLoopVariable(path, context);
        }

        return ResolvePath(path, context.CurrentScope);
    }

    private static TemplateValue ResolveLoopVariable(string path, TemplateContext context)
    {
        return path switch
        {
            "@index" => context.LoopIndex.HasValue
                ? new NumberValue(context.LoopIndex.Value)
                : NullValue.Instance,
            "@first" => new BoolValue(context.IsFirst),
            "@last" => new BoolValue(context.IsLast),
            _ => NullValue.Instance
        };
    }

    private static TemplateValue ResolvePath(string path, TemplateValue current)
    {
        var segments = ParsePathSegments(path);

        foreach (var segment in segments)
        {
            current = ResolveSegment(segment, current);
            if (current is NullValue)
            {
                return NullValue.Instance;
            }
        }

        return current;
    }

    private static List<PathSegment> ParsePathSegments(string path)
    {
        // Estimate capacity based on path separators (most paths have 2-4 segments)
        var estimatedSegments = 1;
        foreach (var c in path)
        {
            if (c == '.' || c == '[') estimatedSegments++;
        }
        var segments = new List<PathSegment>(estimatedSegments);
        var remaining = path;

        while (!string.IsNullOrEmpty(remaining))
        {
            // Check for array index
            var indexMatch = ArrayIndexRegex().Match(remaining);
            if (indexMatch.Success && indexMatch.Index == 0)
            {
                var index = int.Parse(indexMatch.Groups[1].Value, CultureInfo.InvariantCulture);

                // Security: Validate array index bounds to prevent resource exhaustion
                if (index > MaxArrayIndex)
                {
                    throw new TemplateEngineException(
                        $"Array index ({index}) exceeds maximum allowed value ({MaxArrayIndex})");
                }

                segments.Add(new ArrayIndexSegment(index));
                remaining = remaining[indexMatch.Length..];
                if (remaining.StartsWith('.'))
                {
                    remaining = remaining[1..];
                }
                continue;
            }

            // Find next delimiter
            var dotIndex = remaining.IndexOf('.');
            var bracketIndex = remaining.IndexOf('[');

            int endIndex;
            if (dotIndex == -1 && bracketIndex == -1)
            {
                endIndex = remaining.Length;
            }
            else if (dotIndex == -1)
            {
                endIndex = bracketIndex;
            }
            else if (bracketIndex == -1)
            {
                endIndex = dotIndex;
            }
            else
            {
                endIndex = Math.Min(dotIndex, bracketIndex);
            }

            if (endIndex > 0)
            {
                segments.Add(new PropertySegment(remaining[..endIndex]));
            }

            remaining = remaining[endIndex..];
            if (remaining.StartsWith('.'))
            {
                remaining = remaining[1..];
            }
        }

        return segments;
    }

    private static TemplateValue ResolveSegment(PathSegment segment, TemplateValue current)
    {
        return segment switch
        {
            PropertySegment prop when current is ObjectValue obj => obj[prop.Name],
            ArrayIndexSegment idx when current is ArrayValue arr && idx.Index < arr.Count => arr[idx.Index],
            _ => NullValue.Instance
        };
    }

    /// <summary>
    /// Determines if a value is "truthy" for conditional evaluation.
    /// </summary>
    /// <param name="value">The value to check.</param>
    /// <returns>True if the value is truthy; otherwise, false.</returns>
    public static bool IsTruthy(TemplateValue value)
    {
        return value switch
        {
            NullValue => false,
            BoolValue b => b.Value,
            StringValue s => !string.IsNullOrEmpty(s.Value),
            NumberValue n => n.Value != 0,
            ArrayValue a => a.Count > 0,
            ObjectValue o => o.Count > 0,
            _ => false
        };
    }

    private abstract record PathSegment;
    private sealed record PropertySegment(string Name) : PathSegment;
    private sealed record ArrayIndexSegment(int Index) : PathSegment;
}
