using FlexRender.Parsing.Ast;

namespace FlexRender.Layout.Units;

/// <summary>
/// Parses CSS-like border shorthand and per-side properties into resolved <see cref="BorderValues"/>.
/// </summary>
/// <remarks>
/// <para>Shorthand format: <c>"width style color"</c></para>
/// <list type="bullet">
///   <item><c>"2 solid #333"</c> -- width=2, style=solid, color=#333</item>
///   <item><c>"1 dashed"</c> -- width=1, style=dashed, color=#000000</item>
///   <item><c>"3"</c> -- width=3, style=solid, color=#000000</item>
/// </list>
/// <para>Resolution priority (CSS cascade):</para>
/// <list type="number">
///   <item><c>border</c> shorthand sets all four sides</item>
///   <item><c>borderWidth</c>, <c>borderColor</c>, <c>borderStyle</c> override on all sides</item>
///   <item><c>borderTop</c>, <c>borderRight</c>, <c>borderBottom</c>, <c>borderLeft</c> override specific sides</item>
/// </list>
/// </remarks>
public static class BorderParser
{
    private const string DefaultColor = "#000000";

    /// <summary>
    /// Resolves border properties from a <see cref="TemplateElement"/> into pixel values.
    /// Applies CSS cascade: shorthand -> individual properties -> per-side overrides.
    /// </summary>
    /// <param name="element">The element whose border properties to resolve.</param>
    /// <param name="parentSize">Parent container size for percentage resolution.</param>
    /// <param name="fontSize">Font size for em resolution.</param>
    /// <returns>Resolved border values for all four sides.</returns>
    public static BorderValues Resolve(TemplateElement element, float parentSize, float fontSize)
    {
        // Step 1: Start from shorthand (applies to all sides)
        var baseSide = ParseShorthand(element.Border, parentSize, fontSize);

        var top = baseSide;
        var right = baseSide;
        var bottom = baseSide;
        var left = baseSide;

        // Step 2: Apply individual property overrides (borderWidth, borderColor, borderStyle)
        if (!string.IsNullOrEmpty(element.BorderWidth) ||
            !string.IsNullOrEmpty(element.BorderColor) ||
            !string.IsNullOrEmpty(element.BorderStyle))
        {
            var overrideWidth = !string.IsNullOrEmpty(element.BorderWidth)
                ? ResolveWidth(element.BorderWidth, parentSize, fontSize)
                : (float?)null;

            var overrideColor = element.BorderColor;

            var overrideStyle = !string.IsNullOrEmpty(element.BorderStyle)
                ? ParseStyle(element.BorderStyle)
                : (BorderLineStyle?)null;

            top = ApplyOverrides(top, overrideWidth, overrideStyle, overrideColor);
            right = ApplyOverrides(right, overrideWidth, overrideStyle, overrideColor);
            bottom = ApplyOverrides(bottom, overrideWidth, overrideStyle, overrideColor);
            left = ApplyOverrides(left, overrideWidth, overrideStyle, overrideColor);
        }

        // Step 3: Apply per-side overrides
        if (!string.IsNullOrEmpty(element.BorderTop))
            top = ParseShorthand(element.BorderTop, parentSize, fontSize);

        if (!string.IsNullOrEmpty(element.BorderRight))
            right = ParseShorthand(element.BorderRight, parentSize, fontSize);

        if (!string.IsNullOrEmpty(element.BorderBottom))
            bottom = ParseShorthand(element.BorderBottom, parentSize, fontSize);

        if (!string.IsNullOrEmpty(element.BorderLeft))
            left = ParseShorthand(element.BorderLeft, parentSize, fontSize);

        // Quick path: if all sides are none, return Zero
        if (!top.IsVisible && !right.IsVisible && !bottom.IsVisible && !left.IsVisible &&
            top.Width == 0f && right.Width == 0f && bottom.Width == 0f && left.Width == 0f)
        {
            return BorderValues.Zero;
        }

        return new BorderValues(top, right, bottom, left);
    }

    /// <summary>
    /// Resolves border properties using absolute pixel resolution only (no layout context).
    /// Used during the intrinsic measurement pass.
    /// </summary>
    /// <param name="element">The element whose border properties to resolve.</param>
    /// <returns>Resolved border values in pixels.</returns>
    public static BorderValues ResolveAbsolute(TemplateElement element)
    {
        return Resolve(element, 0f, 16f);
    }

    /// <summary>
    /// Parses a border shorthand string: <c>"width style color"</c>.
    /// </summary>
    /// <param name="value">The border shorthand (e.g., "2 solid #333", "1 dashed", "3").</param>
    /// <param name="parentSize">Parent container size for percentage resolution.</param>
    /// <param name="fontSize">Font size for em resolution.</param>
    /// <returns>A resolved <see cref="BorderSide"/>.</returns>
    public static BorderSide ParseShorthand(string? value, float parentSize, float fontSize)
    {
        if (string.IsNullOrWhiteSpace(value))
            return BorderSide.None;

        var tokens = value.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (tokens.Length == 0)
            return BorderSide.None;

        // Token 1 (required): width
        var width = ResolveWidth(tokens[0], parentSize, fontSize);
        if (width < 0f) width = 0f;

        // Token 2 (optional): style
        var style = tokens.Length >= 2 ? ParseStyle(tokens[1]) : BorderLineStyle.Solid;

        // Token 3 (optional): color
        var color = tokens.Length >= 3 ? tokens[2] : DefaultColor;

        // border-style:none means no border rendered and no space consumed
        if (style == BorderLineStyle.None)
            width = 0f;

        return new BorderSide(width, style, color);
    }

    private static float ResolveWidth(string token, float parentSize, float fontSize)
    {
        var unit = UnitParser.Parse(token);
        return Math.Max(0f, unit.Resolve(parentSize, fontSize) ?? 0f);
    }

    private static BorderLineStyle ParseStyle(string value)
    {
        return value.ToLowerInvariant() switch
        {
            "solid" => BorderLineStyle.Solid,
            "dashed" => BorderLineStyle.Dashed,
            "dotted" => BorderLineStyle.Dotted,
            "none" => BorderLineStyle.None,
            _ => BorderLineStyle.Solid
        };
    }

    private static BorderSide ApplyOverrides(
        BorderSide current,
        float? width,
        BorderLineStyle? style,
        string? color)
    {
        return new BorderSide(
            width ?? current.Width,
            style ?? current.Style,
            color ?? current.Color);
    }
}
