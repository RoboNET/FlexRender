using System.Collections.Concurrent;
using System.Globalization;
using FlexRender.Abstractions;
using FlexRender.Layout;
using SkiaSharp;

namespace FlexRender.Rendering;

/// <summary>
/// Manages fonts for rendering, including loading and fallback handling.
/// Thread-safe implementation using ConcurrentDictionary for all caches.
/// </summary>
public sealed class FontManager : IFontManager, IDisposable
{
    private readonly ConcurrentDictionary<string, SKTypeface> _typefaces = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, string> _fontPaths = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, string> _fontFallbacks = new(StringComparer.OrdinalIgnoreCase);
    private string _defaultFallback = "Arial";
    private bool _disposed;

    /// <summary>
    /// Gets a typeface by font name, using fallback if necessary.
    /// This method is thread-safe and uses atomic GetOrAdd operations.
    /// </summary>
    /// <param name="fontName">The font name.</param>
    /// <returns>The typeface (never null - falls back to system font).</returns>
    public SKTypeface GetTypeface(string fontName)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        return _typefaces.GetOrAdd(fontName, LoadTypeface);
    }

    /// <summary>
    /// Factory method to load a typeface for the given font name.
    /// Called by GetOrAdd when the typeface is not in the cache.
    /// </summary>
    /// <param name="fontName">The font name to load.</param>
    /// <returns>The loaded typeface or a fallback.</returns>
    private SKTypeface LoadTypeface(string fontName)
    {
        // Try to load from registered path
        if (_fontPaths.TryGetValue(fontName, out var path) && File.Exists(path))
        {
            var typeface = SKTypeface.FromFile(path);
            if (typeface != null)
            {
                return typeface;
            }
        }

        // Try fallback font
        if (_fontFallbacks.TryGetValue(fontName, out var fallbackName))
        {
            var fallback = SKTypeface.FromFamilyName(fallbackName);
            if (fallback != null)
            {
                return fallback;
            }
        }

        // Use default fallback
        return SKTypeface.FromFamilyName(_defaultFallback) ?? SKTypeface.Default;
    }

    /// <summary>
    /// Registers a font with a file path and optional fallback.
    /// This method is thread-safe.
    /// </summary>
    /// <param name="name">The font name to register.</param>
    /// <param name="path">Path to the font file (.ttf, .otf).</param>
    /// <param name="fallback">Optional system font fallback name.</param>
    /// <returns>True if the font file exists and was registered; otherwise, false.</returns>
    public bool RegisterFont(string name, string path, string? fallback = null)
    {
        _fontPaths[name] = path;

        if (!string.IsNullOrEmpty(fallback))
            _fontFallbacks[name] = fallback;

        // Clear cached typeface so it gets reloaded
        // TryRemove is the thread-safe equivalent of Remove for ConcurrentDictionary
        _typefaces.TryRemove(name, out var removedTypeface);
        removedTypeface?.Dispose();

        return File.Exists(path);
    }

    /// <summary>
    /// Sets the default fallback font family name.
    /// </summary>
    /// <param name="fontFamily">The font family name (e.g., "Arial", "Helvetica").</param>
    public void SetDefaultFallback(string fontFamily)
    {
        _defaultFallback = fontFamily;
    }

    /// <summary>
    /// Parses a font size string to pixels.
    /// Supports: pixels (number or "px" suffix), em (relative to base), % (relative to parent).
    /// </summary>
    /// <param name="sizeStr">The size string (e.g., "16", "48px", "1.5em", "50%").</param>
    /// <param name="baseFontSize">The base font size for em calculations.</param>
    /// <param name="parentSize">The parent element size for percentage calculations.</param>
    /// <returns>The size in pixels.</returns>
    public float ParseFontSize(string? sizeStr, float baseFontSize, float parentSize)
    {
        // For font-size, percentage is relative to parent font size (CSS semantics).
        // When baseFontSize == parentSize (the common case), FontSizeResolver handles this directly.
        // For the general case, we still need to handle parentSize separately for backward compatibility.
        if (string.IsNullOrEmpty(sizeStr))
            return baseFontSize;

        var trimmed = sizeStr.Trim();

        // Special case: if parentSize differs from baseFontSize, handle % separately
        if (trimmed.EndsWith('%') && Math.Abs(parentSize - baseFontSize) > 0.001f)
        {
            var numStr = trimmed[..^1];
            if (float.TryParse(numStr, NumberStyles.Float, CultureInfo.InvariantCulture, out var pct))
                return (pct / 100f) * parentSize;
            return baseFontSize;
        }

        return FontSizeResolver.Resolve(sizeStr, baseFontSize);
    }

    /// <summary>
    /// Disposes all loaded typefaces.
    /// This method is thread-safe but should only be called once.
    /// </summary>
    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;

        // Dispose and remove each typeface atomically
        foreach (var key in _typefaces.Keys)
        {
            if (_typefaces.TryRemove(key, out var typeface))
            {
                typeface.Dispose();
            }
        }
    }
}
