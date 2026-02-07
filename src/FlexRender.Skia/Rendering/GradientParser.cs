using System.Globalization;
using System.Text.RegularExpressions;
using SkiaSharp;

namespace FlexRender.Rendering;

/// <summary>
/// The type of gradient.
/// </summary>
public enum GradientType
{
    /// <summary>Linear gradient with a direction.</summary>
    Linear,

    /// <summary>Radial gradient from center outward.</summary>
    Radial
}

/// <summary>
/// A single color stop in a gradient definition.
/// </summary>
/// <param name="Color">The color at this stop.</param>
/// <param name="Position">Optional position as a fraction (0.0-1.0). Null means evenly distributed.</param>
public sealed record GradientColorStop(SKColor Color, float? Position);

/// <summary>
/// Parsed gradient definition ready for shader creation.
/// </summary>
/// <param name="Type">The type of gradient (linear or radial).</param>
/// <param name="AngleDegrees">Direction angle in degrees (0 = to top, 90 = to right). Only for linear gradients.</param>
/// <param name="Stops">The color stops defining the gradient.</param>
public sealed record GradientDefinition(GradientType Type, float AngleDegrees, IReadOnlyList<GradientColorStop> Stops);

/// <summary>
/// Parses CSS-like gradient strings into <see cref="GradientDefinition"/> values.
/// </summary>
/// <remarks>
/// Supported formats:
/// <list type="bullet">
///   <item><c>linear-gradient(to right, #ff0000, #0000ff)</c></item>
///   <item><c>linear-gradient(45deg, #ff0000, #00ff00 50%, #0000ff)</c></item>
///   <item><c>radial-gradient(#ffffff, #000000)</c></item>
/// </list>
/// Direction keywords: to right, to left, to top, to bottom, to top right, to bottom left, etc.
/// </remarks>
public static partial class GradientParser
{
    [GeneratedRegex(@"^linear-gradient\s*\((.+)\)$", RegexOptions.IgnoreCase | RegexOptions.Singleline)]
    private static partial Regex LinearGradientRegex();

    [GeneratedRegex(@"^radial-gradient\s*\((.+)\)$", RegexOptions.IgnoreCase | RegexOptions.Singleline)]
    private static partial Regex RadialGradientRegex();

    /// <summary>
    /// Checks if a background string is a gradient definition.
    /// </summary>
    /// <param name="background">The background string to check.</param>
    /// <returns>True if the string represents a gradient; otherwise, false.</returns>
    public static bool IsGradient(string? background)
    {
        if (string.IsNullOrWhiteSpace(background))
            return false;

        var trimmed = background.AsSpan().Trim();
        return trimmed.StartsWith("linear-gradient(", StringComparison.OrdinalIgnoreCase) ||
               trimmed.StartsWith("radial-gradient(", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Tries to parse a gradient string into a <see cref="GradientDefinition"/>.
    /// </summary>
    /// <param name="value">The gradient string to parse.</param>
    /// <param name="gradient">The parsed gradient definition if successful.</param>
    /// <returns>True if parsing succeeded; otherwise, false.</returns>
    public static bool TryParse(string? value, out GradientDefinition? gradient)
    {
        gradient = null;

        if (string.IsNullOrWhiteSpace(value))
            return false;

        var trimmed = value.Trim();

        var linearMatch = LinearGradientRegex().Match(trimmed);
        if (linearMatch.Success)
        {
            return TryParseLinear(linearMatch.Groups[1].Value, out gradient);
        }

        var radialMatch = RadialGradientRegex().Match(trimmed);
        if (radialMatch.Success)
        {
            return TryParseRadial(radialMatch.Groups[1].Value, out gradient);
        }

        return false;
    }

    /// <summary>
    /// Creates an <see cref="SKShader"/> for a gradient within the given bounds.
    /// </summary>
    /// <param name="gradient">The gradient definition.</param>
    /// <param name="x">X position of the element.</param>
    /// <param name="y">Y position of the element.</param>
    /// <param name="width">Width of the element.</param>
    /// <param name="height">Height of the element.</param>
    /// <returns>An SKShader for the gradient, or null if the gradient has fewer than 2 stops.</returns>
    public static SKShader? CreateShader(GradientDefinition gradient, float x, float y, float width, float height)
    {
        ArgumentNullException.ThrowIfNull(gradient);

        if (gradient.Stops.Count < 2)
            return null;

        var colors = new SKColor[gradient.Stops.Count];
        var positions = ResolvePositions(gradient.Stops);

        for (var i = 0; i < gradient.Stops.Count; i++)
        {
            colors[i] = gradient.Stops[i].Color;
        }

        return gradient.Type switch
        {
            GradientType.Linear => CreateLinearShader(gradient.AngleDegrees, colors, positions, x, y, width, height),
            GradientType.Radial => CreateRadialShader(colors, positions, x, y, width, height),
            _ => null
        };
    }

    private static bool TryParseLinear(string inner, out GradientDefinition? gradient)
    {
        gradient = null;

        // Split by top-level commas (not inside parentheses)
        var segments = SplitTopLevelCommas(inner);
        if (segments.Count < 2)
            return false;

        var startIndex = 0;
        var angle = 180f; // Default: "to bottom" (CSS default)

        // Check if first segment is a direction
        var firstSegment = segments[0].Trim();
        if (TryParseDirection(firstSegment, out var parsedAngle))
        {
            angle = parsedAngle;
            startIndex = 1;
        }

        // Parse color stops
        var stops = new List<GradientColorStop>(segments.Count - startIndex);
        for (var i = startIndex; i < segments.Count; i++)
        {
            if (TryParseColorStop(segments[i].Trim(), out var stop))
            {
                stops.Add(stop);
            }
            else
            {
                return false;
            }
        }

        if (stops.Count < 2)
            return false;

        gradient = new GradientDefinition(GradientType.Linear, angle, stops);
        return true;
    }

    private static bool TryParseRadial(string inner, out GradientDefinition? gradient)
    {
        gradient = null;

        var segments = SplitTopLevelCommas(inner);
        if (segments.Count < 2)
            return false;

        var stops = new List<GradientColorStop>(segments.Count);
        foreach (var segment in segments)
        {
            if (TryParseColorStop(segment.Trim(), out var stop))
            {
                stops.Add(stop);
            }
            else
            {
                return false;
            }
        }

        if (stops.Count < 2)
            return false;

        gradient = new GradientDefinition(GradientType.Radial, 0f, stops);
        return true;
    }

    private static bool TryParseDirection(string segment, out float angle)
    {
        angle = 180f;

        // Check for "Ndeg" format
        if (segment.EndsWith("deg", StringComparison.OrdinalIgnoreCase))
        {
            var degStr = segment[..^3].Trim();
            if (float.TryParse(degStr, NumberStyles.Float, CultureInfo.InvariantCulture, out angle))
                return true;
        }

        // Check for keyword directions
        var normalized = segment.ToLowerInvariant().Trim();
        switch (normalized)
        {
            case "to top":
                angle = 0f;
                return true;
            case "to top right" or "to right top":
                angle = 45f;
                return true;
            case "to right":
                angle = 90f;
                return true;
            case "to bottom right" or "to right bottom":
                angle = 135f;
                return true;
            case "to bottom":
                angle = 180f;
                return true;
            case "to bottom left" or "to left bottom":
                angle = 225f;
                return true;
            case "to left":
                angle = 270f;
                return true;
            case "to top left" or "to left top":
                angle = 315f;
                return true;
            default:
                return false;
        }
    }

    private static bool TryParseColorStop(string segment, out GradientColorStop stop)
    {
        stop = default!;

        if (string.IsNullOrWhiteSpace(segment))
            return false;

        // Check if the segment ends with a percentage
        float? position = null;
        var colorStr = segment;

        // Try to find trailing percentage: look for the last space-separated token ending with %
        var lastSpaceIdx = segment.LastIndexOf(' ');
        if (lastSpaceIdx >= 0)
        {
            var lastToken = segment[(lastSpaceIdx + 1)..].Trim();
            if (lastToken.EndsWith('%') &&
                float.TryParse(lastToken[..^1], NumberStyles.Float, CultureInfo.InvariantCulture, out var pct))
            {
                position = pct / 100f;
                colorStr = segment[..lastSpaceIdx].Trim();
            }
        }

        if (!ColorParser.TryParse(colorStr, out var color))
            return false;

        stop = new GradientColorStop(color, position);
        return true;
    }

    /// <summary>
    /// Resolves color stop positions. Stops without explicit positions are evenly distributed
    /// between the nearest explicitly positioned stops.
    /// </summary>
    private static float[] ResolvePositions(IReadOnlyList<GradientColorStop> stops)
    {
        var positions = new float[stops.Count];

        // First pass: copy explicit positions
        for (var i = 0; i < stops.Count; i++)
        {
            positions[i] = stops[i].Position ?? -1f; // -1 means unresolved
        }

        // Ensure first and last are set
        if (positions[0] < 0f)
            positions[0] = 0f;
        if (positions[^1] < 0f)
            positions[^1] = 1f;

        // Fill in gaps with evenly distributed values
        var i2 = 0;
        while (i2 < positions.Length)
        {
            if (positions[i2] >= 0f)
            {
                i2++;
                continue;
            }

            // Find the start of unresolved range
            var gapStart = i2 - 1;
            var gapEnd = i2;
            while (gapEnd < positions.Length && positions[gapEnd] < 0f)
                gapEnd++;

            // Evenly distribute between gapStart and gapEnd
            var startVal = positions[gapStart];
            var endVal = positions[gapEnd];
            var count = gapEnd - gapStart;

            for (var j = gapStart + 1; j < gapEnd; j++)
            {
                positions[j] = startVal + (endVal - startVal) * (j - gapStart) / count;
            }

            i2 = gapEnd + 1;
        }

        return positions;
    }

    /// <summary>
    /// Splits a string by commas that are not inside parentheses.
    /// This handles color functions like rgba(0,0,0,0.5) correctly.
    /// </summary>
    private static List<string> SplitTopLevelCommas(string input)
    {
        var result = new List<string>(4);
        var depth = 0;
        var start = 0;

        for (var i = 0; i < input.Length; i++)
        {
            switch (input[i])
            {
                case '(':
                    depth++;
                    break;
                case ')':
                    depth--;
                    break;
                case ',' when depth == 0:
                    result.Add(input[start..i]);
                    start = i + 1;
                    break;
            }
        }

        if (start < input.Length)
            result.Add(input[start..]);

        return result;
    }

    private static SKShader CreateLinearShader(
        float angleDegrees,
        SKColor[] colors,
        float[] positions,
        float x,
        float y,
        float width,
        float height)
    {
        // Convert CSS angle to start/end points
        // CSS angles: 0deg = to top, 90deg = to right, 180deg = to bottom
        var radians = (angleDegrees - 90f) * MathF.PI / 180f;
        var centerX = x + width / 2f;
        var centerY = y + height / 2f;

        // Calculate the gradient line length to cover the entire rectangle
        var halfDiag = MathF.Sqrt(width * width + height * height) / 2f;
        var cos = MathF.Cos(radians);
        var sin = MathF.Sin(radians);

        var startPoint = new SKPoint(centerX - cos * halfDiag, centerY - sin * halfDiag);
        var endPoint = new SKPoint(centerX + cos * halfDiag, centerY + sin * halfDiag);

        return SKShader.CreateLinearGradient(startPoint, endPoint, colors, positions, SKShaderTileMode.Clamp);
    }

    private static SKShader CreateRadialShader(
        SKColor[] colors,
        float[] positions,
        float x,
        float y,
        float width,
        float height)
    {
        var centerX = x + width / 2f;
        var centerY = y + height / 2f;
        var radius = MathF.Sqrt(width * width + height * height) / 2f;

        return SKShader.CreateRadialGradient(
            new SKPoint(centerX, centerY),
            radius,
            colors,
            positions,
            SKShaderTileMode.Clamp);
    }
}
