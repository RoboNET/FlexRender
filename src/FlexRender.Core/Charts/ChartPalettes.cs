using System;
using System.Collections.Generic;

namespace FlexRender.Charts;

/// <summary>
/// Static registry of named series-color palettes (AOT-safe, no config files).
/// </summary>
public static class ChartPalettes
{
    /// <summary>The default palette used when none is specified.</summary>
    public static ChartPalette Default { get; } = new(new[]
    {
        "#4A90D9", "#E2725B", "#7FB069", "#F4C430", "#9B6DD6", "#54B8B1", "#E0719C", "#A0A0A0"
    });

    private static readonly ChartPalette Ocean = new(new[]
    {
        "#264653", "#2A9D8F", "#48BFE3", "#56CFE1", "#64DFDF", "#80FFDB"
    });

    private static readonly ChartPalette Sunset = new(new[]
    {
        "#003049", "#D62828", "#F77F00", "#FCBF49", "#EAE2B7"
    });

    private static readonly ChartPalette Forest = new(new[]
    {
        "#1B4332", "#2D6A4F", "#40916C", "#52B788", "#74C69D", "#95D5B2"
    });

    private static readonly ChartPalette Mono = new(new[]
    {
        "#222222", "#444444", "#666666", "#888888", "#AAAAAA", "#CCCCCC"
    });

    private static readonly ChartPalette Vivid = new(new[]
    {
        "#E63946", "#F1A208", "#2A9D8F", "#3A86FF", "#8338EC", "#FF006E"
    });

    private static readonly Dictionary<string, ChartPalette> Registry = new(StringComparer.OrdinalIgnoreCase)
    {
        ["default"] = Default,
        ["ocean"] = Ocean,
        ["sunset"] = Sunset,
        ["forest"] = Forest,
        ["mono"] = Mono,
        ["vivid"] = Vivid
    };

    /// <summary>
    /// Resolves a named palette case-insensitively.
    /// </summary>
    /// <param name="name">The palette name (e.g. "ocean").</param>
    /// <returns>The matching <see cref="ChartPalette"/>, or null when the name is unknown.</returns>
    public static ChartPalette? Resolve(string? name)
    {
        if (string.IsNullOrWhiteSpace(name))
            return null;
        return Registry.TryGetValue(name, out var palette) ? palette : null;
    }
}
