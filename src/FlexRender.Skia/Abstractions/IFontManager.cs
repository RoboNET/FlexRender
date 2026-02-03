using SkiaSharp;

namespace FlexRender.Abstractions;

/// <summary>
/// Manages font loading, caching, and fallback handling.
/// </summary>
/// <remarks>
/// The font manager provides a unified interface for font operations,
/// including registration, retrieval, and font size parsing.
/// </remarks>
public interface IFontManager
{
    /// <summary>
    /// Gets a typeface by font name, using fallback if necessary.
    /// </summary>
    /// <param name="fontName">The font family name.</param>
    /// <returns>The requested typeface, or a fallback if not found. Never returns null.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="fontName"/> is null or empty.</exception>
    SKTypeface GetTypeface(string fontName);

    /// <summary>
    /// Registers a font with a file path and optional fallback.
    /// </summary>
    /// <param name="name">The font name to register (case-insensitive).</param>
    /// <param name="path">Path to the font file (.ttf, .otf).</param>
    /// <param name="fallback">Optional system font fallback name.</param>
    /// <returns>True if the font file exists and was registered; otherwise, false.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="name"/> or <paramref name="path"/> is null.</exception>
    bool RegisterFont(string name, string path, string? fallback = null);

    /// <summary>
    /// Sets the default fallback font family name.
    /// </summary>
    /// <param name="fontFamily">The font family name (e.g., "Arial", "Helvetica").</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="fontFamily"/> is null or empty.</exception>
    void SetDefaultFallback(string fontFamily);

    /// <summary>
    /// Parses a font size string to pixels.
    /// </summary>
    /// <param name="sizeStr">The size string (e.g., "16", "48px", "1.5em", "50%").</param>
    /// <param name="baseFontSize">The base font size for em calculations.</param>
    /// <param name="parentSize">The parent element size for percentage calculations.</param>
    /// <returns>The size in pixels.</returns>
    /// <remarks>
    /// Supported formats:
    /// <list type="bullet">
    /// <item><description>Plain number: pixels (e.g., "16")</description></item>
    /// <item><description>px: explicit pixels (e.g., "48px")</description></item>
    /// <item><description>em: relative to base font size (e.g., "1.5em")</description></item>
    /// <item><description>%: relative to parent size (e.g., "50%")</description></item>
    /// </list>
    /// </remarks>
    float ParseFontSize(string? sizeStr, float baseFontSize, float parentSize);
}
