using System.Collections.Concurrent;
using System.Globalization;
using FlexRender.Abstractions;
using FlexRender.Layout;
using FlexRender.Parsing.Ast;
using SkiaSharp;

namespace FlexRender.Rendering;

/// <summary>
/// Manages fonts for rendering, including loading and fallback handling.
/// Thread-safe implementation using ConcurrentDictionary for all caches.
/// </summary>
public sealed class FontManager : IFontManager, IDisposable
{
    private readonly ConcurrentDictionary<string, SKTypeface> _typefaces = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<TypefaceVariantKey, SKTypeface> _variantTypefaces = new();
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
    /// Gets a typeface by font name and optional font family with specific weight and style.
    /// Priority: if <paramref name="fontName"/> is not "main" and not empty, resolves by registered name.
    /// Otherwise, if <paramref name="fontFamily"/> is not empty, resolves by family name.
    /// Otherwise, falls back to the default font.
    /// </summary>
    /// <param name="fontName">The registered font name.</param>
    /// <param name="fontFamily">CSS-like font family name to search registered fonts and system fonts.</param>
    /// <param name="weight">The desired font weight (100-900).</param>
    /// <param name="style">The desired font style (normal, italic, oblique).</param>
    /// <returns>The typeface (never null - falls back to system font).</returns>
    public SKTypeface GetTypeface(string fontName, string fontFamily, FontWeight weight, FontStyle style)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        // If fontName is explicitly set (not the default "main"), use the registered name lookup
        if (!string.Equals(fontName, "main", StringComparison.OrdinalIgnoreCase)
            && !string.IsNullOrEmpty(fontName))
        {
            return GetTypeface(fontName, weight, style);
        }

        // If fontFamily is specified, search by family name
        if (!string.IsNullOrEmpty(fontFamily))
        {
            return GetTypefaceByFamily(fontFamily, weight, style);
        }

        // Fall back to default
        return GetTypeface(fontName, weight, style);
    }

    /// <summary>
    /// Gets a typeface by font family name with specific weight and style.
    /// Searches registered fonts by FamilyName metadata, then system fonts.
    /// </summary>
    /// <param name="familyName">The font family name (e.g., "Inter 18pt", "Arial").</param>
    /// <param name="weight">The desired font weight (100-900).</param>
    /// <param name="style">The desired font style (normal, italic, oblique).</param>
    /// <returns>The typeface (never null - falls back to system font).</returns>
    public SKTypeface GetTypefaceByFamily(string familyName, FontWeight weight, FontStyle style)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentNullException.ThrowIfNull(familyName);

        var key = new TypefaceVariantKey($"__family__{familyName}", weight, style);
        return _variantTypefaces.GetOrAdd(key, _ => LoadTypefaceByFamily(familyName, weight, style));
    }

    /// <summary>
    /// Gets a typeface by font name with specific weight and style.
    /// When both weight and style are their default values, delegates to <see cref="GetTypeface(string)"/>.
    /// Otherwise, resolves the base font family name and uses <see cref="SKFontManager"/> to
    /// match a typeface variant with the requested weight and slant.
    /// </summary>
    /// <param name="fontName">The font name.</param>
    /// <param name="weight">The desired font weight (100-900).</param>
    /// <param name="style">The desired font style (normal, italic, oblique).</param>
    /// <returns>The typeface (never null - falls back to system font).</returns>
    public SKTypeface GetTypeface(string fontName, FontWeight weight, FontStyle style)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        // Fast path: default weight+style, use existing cache
        if (weight == FontWeight.Normal && style == FontStyle.Normal)
        {
            return GetTypeface(fontName);
        }

        var key = new TypefaceVariantKey(fontName, weight, style);
        return _variantTypefaces.GetOrAdd(key, LoadTypefaceVariant);
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
    /// Loads a typeface by searching registered fonts' FamilyName metadata, then system fonts.
    /// </summary>
    /// <param name="familyName">The font family name to search for.</param>
    /// <param name="weight">The desired font weight.</param>
    /// <param name="style">The desired font style.</param>
    /// <returns>The best matching typeface, or a fallback.</returns>
    private SKTypeface LoadTypefaceByFamily(string familyName, FontWeight weight, FontStyle style)
    {
        var skFontStyle = ToSkFontStyle(weight, style);
        var targetWeight = (int)weight;

        // 1. Search registered fonts by loading each and checking FamilyName
        SKTypeface? bestRegisteredMatch = null;
        var bestRegisteredWeightDiff = int.MaxValue;

        foreach (var fontPath in _fontPaths)
        {
            var typeface = GetTypeface(fontPath.Key);
            if (!string.Equals(typeface.FamilyName, familyName, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            // Found a registered font with matching family name; now try sibling discovery for weight/style
            if (weight == FontWeight.Normal && style == FontStyle.Normal)
            {
                return typeface;
            }

            // Try to find the exact variant via the existing variant logic
            var variantKey = new TypefaceVariantKey(fontPath.Key, weight, style);
            var variant = _variantTypefaces.GetOrAdd(variantKey, LoadTypefaceVariant);
            var weightDiff = Math.Abs((int)variant.FontStyle.Weight - targetWeight);

            if (weightDiff < bestRegisteredWeightDiff)
            {
                bestRegisteredMatch = variant;
                bestRegisteredWeightDiff = weightDiff;
            }
        }

        if (bestRegisteredMatch is not null && bestRegisteredWeightDiff <= 100)
        {
            return bestRegisteredMatch;
        }

        // 2. Try system fonts via SKFontManager
        var systemMatch = SKFontManager.Default.MatchFamily(familyName, skFontStyle);
        if (systemMatch is not null)
        {
            var weightDiff = Math.Abs((int)systemMatch.FontStyle.Weight - targetWeight);
            if (string.Equals(systemMatch.FamilyName, familyName, StringComparison.OrdinalIgnoreCase)
                && weightDiff <= 100)
            {
                return systemMatch;
            }

            // System returned an unrelated font; dispose it
            systemMatch.Dispose();
        }

        // 3. Return registered match even if weight is off, or fall back to default
        return bestRegisteredMatch ?? GetTypeface("main");
    }

    /// <summary>
    /// Factory method to load a typeface variant with specific weight and style.
    /// Resolves the base font family name from the registered font, then attempts to find
    /// a matching variant through the system font manager first. If the system match returns
    /// an unrelated font (different family or distant weight), scans sibling font files in
    /// the same directory as the base font for a better match.
    /// </summary>
    /// <param name="key">The variant key containing font name, weight, and style.</param>
    /// <returns>The loaded typeface variant or a fallback to the base typeface.</returns>
    private SKTypeface LoadTypefaceVariant(TypefaceVariantKey key)
    {
        var skFontStyle = ToSkFontStyle(key.Weight, key.Style);
        var targetWeight = (int)key.Weight;

        // Resolve the family name from the base typeface so that
        // named fonts (e.g. "main" mapped to a file) resolve correctly.
        var baseTypeface = GetTypeface(key.FontName);
        var familyName = baseTypeface.FamilyName;

        // 1. Try system font manager, but verify the result actually matches
        var systemMatch = SKFontManager.Default.MatchFamily(familyName, skFontStyle);
        if (systemMatch is not null)
        {
            var weightDiff = Math.Abs((int)systemMatch.FontStyle.Weight - targetWeight);
            if (string.Equals(systemMatch.FamilyName, familyName, StringComparison.OrdinalIgnoreCase)
                && weightDiff <= 100)
            {
                return systemMatch;
            }

            // System returned an unrelated font; dispose and try sibling scan
            systemMatch.Dispose();
        }

        // 2. Scan sibling font files in the same directory as the base font
        var siblingMatch = FindSiblingTypeface(key.FontName, familyName, targetWeight, skFontStyle.Slant);
        if (siblingMatch is not null)
        {
            return siblingMatch;
        }

        // 3. Fall back to base typeface
        return baseTypeface;
    }

    /// <summary>
    /// Scans the directory containing the registered font file for sibling <c>.ttf</c> and <c>.otf</c>
    /// files that belong to the same font family. Returns the best weight match within 100 units
    /// of the target weight with matching slant, or <c>null</c> if no suitable sibling is found.
    /// Rejected typefaces are disposed immediately to prevent memory leaks.
    /// </summary>
    /// <param name="fontName">The registered font name used to look up the file path.</param>
    /// <param name="familyName">The expected font family name (case-insensitive match).</param>
    /// <param name="targetWeight">The desired font weight (100-900).</param>
    /// <param name="targetSlant">The desired font slant.</param>
    /// <returns>The best matching sibling typeface, or <c>null</c> if none found.</returns>
    private SKTypeface? FindSiblingTypeface(string fontName, string familyName, int targetWeight, SKFontStyleSlant targetSlant)
    {
        if (!_fontPaths.TryGetValue(fontName, out var basePath))
        {
            return null;
        }

        var directory = Path.GetDirectoryName(basePath);
        if (string.IsNullOrEmpty(directory) || !Directory.Exists(directory))
        {
            return null;
        }

        SKTypeface? bestMatch = null;
        var bestWeightDiff = int.MaxValue;

        var fontFiles = Directory.EnumerateFiles(directory, "*.*")
            .Where(static f =>
            {
                var ext = Path.GetExtension(f);
                return ext.Equals(".ttf", StringComparison.OrdinalIgnoreCase)
                    || ext.Equals(".otf", StringComparison.OrdinalIgnoreCase);
            });

        foreach (var filePath in fontFiles)
        {
            SKTypeface? candidate = null;
            try
            {
                candidate = SKTypeface.FromFile(filePath);
                if (candidate is null)
                {
                    continue;
                }

                // Must match family name (case-insensitive) and slant
                if (!string.Equals(candidate.FamilyName, familyName, StringComparison.OrdinalIgnoreCase)
                    || candidate.FontStyle.Slant != targetSlant)
                {
                    candidate.Dispose();
                    continue;
                }

                var weightDiff = Math.Abs((int)candidate.FontStyle.Weight - targetWeight);
                if (weightDiff > 100)
                {
                    candidate.Dispose();
                    continue;
                }

                if (weightDiff < bestWeightDiff)
                {
                    bestMatch?.Dispose();
                    bestMatch = candidate;
                    bestWeightDiff = weightDiff;
                }
                else
                {
                    candidate.Dispose();
                }
            }
            catch
            {
                // Corrupted or unreadable font file; skip it
                candidate?.Dispose();
            }
        }

        return bestMatch;
    }

    /// <summary>
    /// Converts <see cref="FontWeight"/> and <see cref="FontStyle"/> to an <see cref="SKFontStyle"/>.
    /// </summary>
    /// <param name="weight">The font weight (100-900).</param>
    /// <param name="style">The font style (normal, italic, oblique).</param>
    /// <returns>The corresponding <see cref="SKFontStyle"/>.</returns>
    internal static SKFontStyle ToSkFontStyle(FontWeight weight, FontStyle style)
    {
        var slant = style switch
        {
            FontStyle.Italic => SKFontStyleSlant.Italic,
            FontStyle.Oblique => SKFontStyleSlant.Oblique,
            _ => SKFontStyleSlant.Upright
        };

        return new SKFontStyle((SKFontStyleWeight)(int)weight, SKFontStyleWidth.Normal, slant);
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

        // Clear matching variant typefaces for re-registered font
        foreach (var variantKey in _variantTypefaces.Keys)
        {
            if (string.Equals(variantKey.FontName, name, StringComparison.OrdinalIgnoreCase)
                && _variantTypefaces.TryRemove(variantKey, out var variantTypeface))
            {
                variantTypeface.Dispose();
            }
        }

        return File.Exists(path);
    }

    /// <summary>
    /// Returns the registered font names and their file paths for diagnostic purposes.
    /// </summary>
    public IReadOnlyDictionary<string, string> RegisteredFontPaths =>
        new Dictionary<string, string>(_fontPaths, StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Gets the resolved typeface info (family name, fixed-pitch) for a registered font.
    /// Returns null if the font is not registered or cannot be loaded.
    /// </summary>
    /// <param name="fontName">The registered font name.</param>
    /// <returns>Tuple of (FamilyName, IsFixedPitch) or null.</returns>
    public (string FamilyName, bool IsFixedPitch)? GetTypefaceInfo(string fontName)
    {
        var typeface = GetTypeface(fontName);
        return (typeface.FamilyName, typeface.IsFixedPitch);
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
    /// Disposes all loaded typefaces (both base and variant caches).
    /// This method is thread-safe but should only be called once.
    /// </summary>
    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;

        // Dispose and remove each base typeface atomically
        foreach (var key in _typefaces.Keys)
        {
            if (_typefaces.TryRemove(key, out var typeface))
            {
                typeface.Dispose();
            }
        }

        // Dispose and remove each variant typeface atomically
        foreach (var key in _variantTypefaces.Keys)
        {
            if (_variantTypefaces.TryRemove(key, out var typeface))
            {
                typeface.Dispose();
            }
        }
    }

    /// <summary>
    /// Cache key for typeface variants identified by font name, weight, and style.
    /// Uses case-insensitive font name comparison.
    /// </summary>
    private readonly record struct TypefaceVariantKey
    {
        /// <summary>The registered font name.</summary>
        public string FontName { get; }

        /// <summary>The font weight (100-900).</summary>
        public FontWeight Weight { get; }

        /// <summary>The font style (normal, italic, oblique).</summary>
        public FontStyle Style { get; }

        /// <summary>
        /// Creates a new <see cref="TypefaceVariantKey"/>.
        /// </summary>
        /// <param name="fontName">The registered font name.</param>
        /// <param name="weight">The font weight.</param>
        /// <param name="style">The font style.</param>
        public TypefaceVariantKey(string fontName, FontWeight weight, FontStyle style)
        {
            ArgumentNullException.ThrowIfNull(fontName);
            FontName = fontName;
            Weight = weight;
            Style = style;
        }

        /// <summary>
        /// Case-insensitive equality for font names.
        /// </summary>
        public bool Equals(TypefaceVariantKey other) =>
            string.Equals(FontName, other.FontName, StringComparison.OrdinalIgnoreCase)
            && Weight == other.Weight
            && Style == other.Style;

        /// <summary>
        /// Case-insensitive hash code for font names.
        /// </summary>
        public override int GetHashCode() =>
            HashCode.Combine(StringComparer.OrdinalIgnoreCase.GetHashCode(FontName), Weight, Style);
    }
}
