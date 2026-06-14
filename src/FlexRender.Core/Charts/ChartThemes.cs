using System;
using System.Collections.Generic;

namespace FlexRender.Charts;

/// <summary>
/// Static registry of named chart themes (AOT-safe, no config files).
/// </summary>
public static class ChartThemes
{
    /// <summary>The light theme, used as the default.</summary>
    public static ChartTheme Default { get; } = new(
        BackgroundColor: "#FFFFFF",
        GridColor: "#E6E6E6",
        AxisColor: "#999999",
        LabelColor: "#555555",
        TitleColor: "#222222",
        LabelSize: 12f,
        TitleSize: 16f,
        LineWidth: 2.5f,
        BarCornerRadius: 3f);

    private static readonly ChartTheme Dark = new(
        BackgroundColor: "#1E1E1E",
        GridColor: "#3A3A3A",
        AxisColor: "#777777",
        LabelColor: "#CCCCCC",
        TitleColor: "#F0F0F0",
        LabelSize: 12f,
        TitleSize: 16f,
        LineWidth: 2.5f,
        BarCornerRadius: 3f);

    private static readonly ChartTheme Minimal = new(
        BackgroundColor: "#FFFFFF",
        GridColor: "#F0F0F0",
        AxisColor: "#CCCCCC",
        LabelColor: "#666666",
        TitleColor: "#333333",
        LabelSize: 11f,
        TitleSize: 15f,
        LineWidth: 2f,
        BarCornerRadius: 0f);

    private static readonly Dictionary<string, ChartTheme> Registry = new(StringComparer.OrdinalIgnoreCase)
    {
        ["light"] = Default,
        ["dark"] = Dark,
        ["minimal"] = Minimal
    };

    /// <summary>
    /// Resolves a named theme case-insensitively.
    /// </summary>
    /// <param name="name">The theme name (e.g. "dark").</param>
    /// <returns>The matching <see cref="ChartTheme"/>, or null when the name is unknown.</returns>
    public static ChartTheme? Resolve(string? name)
    {
        if (string.IsNullOrWhiteSpace(name))
            return null;
        return Registry.TryGetValue(name, out var theme) ? theme : null;
    }
}
