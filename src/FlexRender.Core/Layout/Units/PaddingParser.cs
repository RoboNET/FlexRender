namespace FlexRender.Layout.Units;

/// <summary>
/// Parses CSS-like padding shorthand strings into <see cref="PaddingValues"/>.
/// Supports 1 to 4 space-separated values with px, %, and em units.
/// </summary>
/// <remarks>
/// <list type="bullet">
///   <item><c>"20"</c> -- all sides = 20</item>
///   <item><c>"20 40"</c> -- top/bottom = 20, left/right = 40</item>
///   <item><c>"20 40 30"</c> -- top = 20, left/right = 40, bottom = 30</item>
///   <item><c>"20 40 30 10"</c> -- top = 20, right = 40, bottom = 30, left = 10</item>
/// </list>
/// </remarks>
public static class PaddingParser
{
    /// <summary>
    /// Default font size in pixels used for em resolution when no layout context is available.
    /// </summary>
    private const float DefaultFontSize = 16f;

    /// <summary>
    /// Parses a padding string with layout context for unit resolution.
    /// Percentage values resolve against <paramref name="parentSize"/>;
    /// em values resolve against <paramref name="fontSize"/>.
    /// </summary>
    /// <param name="value">The padding string (e.g., "20", "10 20", "10 20 30 40").</param>
    /// <param name="parentSize">Parent container size for percentage resolution.</param>
    /// <param name="fontSize">Font size for em resolution.</param>
    /// <returns>Resolved padding values in pixels.</returns>
    public static PaddingValues Parse(string? value, float parentSize, float fontSize)
    {
        if (string.IsNullOrWhiteSpace(value))
            return PaddingValues.Zero;

        var tokens = SplitTokens(value);
        return tokens.Length switch
        {
            1 => UniformFromToken(tokens[0], parentSize, fontSize),
            2 => TwoValueFromTokens(tokens, parentSize, fontSize),
            3 => ThreeValueFromTokens(tokens, parentSize, fontSize),
            _ => FourValueFromTokens(tokens, parentSize, fontSize)
        };
    }

    /// <summary>
    /// Parses a padding string using absolute pixel resolution only (no layout context).
    /// Used during the intrinsic measurement pass where no parent size is available.
    /// Percentage and em units resolve against 0 and default font size (16px) respectively.
    /// </summary>
    /// <param name="value">The padding string.</param>
    /// <returns>Resolved padding values in pixels.</returns>
    public static PaddingValues ParseAbsolute(string? value)
    {
        return Parse(value, 0f, DefaultFontSize);
    }

    private static string[] SplitTokens(string value)
    {
        return value.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
    }

    private static float ResolveToken(string token, float parentSize, float fontSize)
    {
        var unit = UnitParser.Parse(token);
        return unit.Resolve(parentSize, fontSize) ?? 0f;
    }

    private static PaddingValues UniformFromToken(string token, float parentSize, float fontSize)
    {
        var v = ResolveToken(token, parentSize, fontSize);
        return PaddingValues.Uniform(v);
    }

    private static PaddingValues TwoValueFromTokens(string[] tokens, float parentSize, float fontSize)
    {
        var vertical = ResolveToken(tokens[0], parentSize, fontSize);
        var horizontal = ResolveToken(tokens[1], parentSize, fontSize);
        return new PaddingValues(vertical, horizontal, vertical, horizontal);
    }

    private static PaddingValues ThreeValueFromTokens(string[] tokens, float parentSize, float fontSize)
    {
        var top = ResolveToken(tokens[0], parentSize, fontSize);
        var horizontal = ResolveToken(tokens[1], parentSize, fontSize);
        var bottom = ResolveToken(tokens[2], parentSize, fontSize);
        return new PaddingValues(top, horizontal, bottom, horizontal);
    }

    private static PaddingValues FourValueFromTokens(string[] tokens, float parentSize, float fontSize)
    {
        var top = ResolveToken(tokens[0], parentSize, fontSize);
        var right = ResolveToken(tokens[1], parentSize, fontSize);
        var bottom = ResolveToken(tokens[2], parentSize, fontSize);
        var left = ResolveToken(tokens[3], parentSize, fontSize);
        return new PaddingValues(top, right, bottom, left);
    }

    // ============================================
    // Margin parsing (with auto support)
    // ============================================

    /// <summary>
    /// Parses a margin string that may contain "auto" tokens.
    /// Follows CSS shorthand rules (1-4 values) with auto support.
    /// Auto margins consume free space during flexbox layout.
    /// </summary>
    /// <param name="value">The margin string (e.g., "0 auto", "auto 0 0 0", "10px auto").</param>
    /// <param name="parentSize">Parent container size for percentage resolution.</param>
    /// <param name="fontSize">Font size for em resolution.</param>
    /// <returns>Resolved margin values with auto support.</returns>
    public static MarginValues ParseMargin(string? value, float parentSize, float fontSize)
    {
        if (string.IsNullOrWhiteSpace(value))
            return MarginValues.Zero;

        var tokens = SplitTokens(value);
        return tokens.Length switch
        {
            1 => ParseMarginUniform(tokens[0], parentSize, fontSize),
            2 => ParseMarginTwoValue(tokens, parentSize, fontSize),
            3 => ParseMarginThreeValue(tokens, parentSize, fontSize),
            _ => ParseMarginFourValue(tokens, parentSize, fontSize)
        };
    }

    private static MarginValue ResolveMarginToken(string token, float parentSize, float fontSize)
    {
        if (string.Equals(token, "auto", StringComparison.OrdinalIgnoreCase))
            return MarginValue.Auto;

        var unit = UnitParser.Parse(token);
        return MarginValue.Fixed(unit.Resolve(parentSize, fontSize) ?? 0f);
    }

    private static MarginValues ParseMarginUniform(string token, float parentSize, float fontSize)
    {
        var v = ResolveMarginToken(token, parentSize, fontSize);
        return new MarginValues(v, v, v, v);
    }

    private static MarginValues ParseMarginTwoValue(string[] tokens, float parentSize, float fontSize)
    {
        var vertical = ResolveMarginToken(tokens[0], parentSize, fontSize);
        var horizontal = ResolveMarginToken(tokens[1], parentSize, fontSize);
        return new MarginValues(vertical, horizontal, vertical, horizontal);
    }

    private static MarginValues ParseMarginThreeValue(string[] tokens, float parentSize, float fontSize)
    {
        var top = ResolveMarginToken(tokens[0], parentSize, fontSize);
        var horizontal = ResolveMarginToken(tokens[1], parentSize, fontSize);
        var bottom = ResolveMarginToken(tokens[2], parentSize, fontSize);
        return new MarginValues(top, horizontal, bottom, horizontal);
    }

    private static MarginValues ParseMarginFourValue(string[] tokens, float parentSize, float fontSize)
    {
        var top = ResolveMarginToken(tokens[0], parentSize, fontSize);
        var right = ResolveMarginToken(tokens[1], parentSize, fontSize);
        var bottom = ResolveMarginToken(tokens[2], parentSize, fontSize);
        var left = ResolveMarginToken(tokens[3], parentSize, fontSize);
        return new MarginValues(top, right, bottom, left);
    }
}
