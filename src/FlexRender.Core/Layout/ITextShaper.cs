using FlexRender.Parsing.Ast;

namespace FlexRender.Layout;

/// <summary>
/// Result of text shaping: pre-computed line breaks, total size, and line height.
/// </summary>
/// <param name="Lines">The text split into individual lines after wrapping, max-lines, and ellipsis processing.</param>
/// <param name="TotalSize">The bounding box size of all lines combined.</param>
/// <param name="LineHeight">The computed line height in pixels used for vertical spacing.</param>
public readonly record struct TextShapingResult(
    IReadOnlyList<string> Lines,
    LayoutSize TotalSize,
    float LineHeight);

/// <summary>
/// Abstraction for measuring text and computing line breaks.
/// Implementations provide font-metric-aware text shaping (e.g., SkiaSharp)
/// or heuristic-based approximation for backends without font access.
/// </summary>
public interface ITextShaper
{
    /// <summary>
    /// Measures the specified text element and computes line breaks.
    /// </summary>
    /// <param name="element">The text element containing content, font, wrap, max-lines, and overflow settings.</param>
    /// <param name="fontSize">The resolved font size in pixels.</param>
    /// <param name="maxWidth">The maximum width available for text before wrapping. Use <see cref="float.MaxValue"/> for unconstrained width.</param>
    /// <returns>A <see cref="TextShapingResult"/> with pre-computed lines, total size, and line height.</returns>
    TextShapingResult ShapeText(TextElement element, float fontSize, float maxWidth);
}
