using System.Collections.Concurrent;
using System.Globalization;
using SixLabors.Fonts;
using AstFontWeight = FlexRender.Parsing.Ast.FontWeight;
using AstFontStyle = FlexRender.Parsing.Ast.FontStyle;

namespace FlexRender.ImageSharp.Rendering;

/// <summary>
/// Manages font loading and caching for ImageSharp rendering.
/// Thread-safe implementation using ConcurrentDictionary.
/// Each font file is loaded into an isolated FontCollection to prevent
/// family name grouping (e.g. Inter-Regular and Inter-Bold both declare
/// family "Inter" but must remain separate for correct weight selection).
/// When a specific font weight or style is requested, sibling font files
/// in the same directory are scanned and loaded into a shared FontCollection
/// so that SixLabors.Fonts can resolve the correct variant.
/// </summary>
internal sealed class ImageSharpFontManager : IDisposable
{
    private readonly ConcurrentDictionary<string, FontFamily> _families = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, FontStyle> _fontStyles = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, string> _fontPaths = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Cache of shared FontCollections keyed by directory path.
    /// Each shared collection contains ALL .ttf/.otf files from that directory,
    /// enabling SixLabors.Fonts to resolve font family variants (Bold, Italic, etc.).
    /// </summary>
    private readonly ConcurrentDictionary<string, FontCollection> _sharedCollections = new(StringComparer.OrdinalIgnoreCase);

    private int _disposed;

    /// <summary>
    /// Registers a font from a file path. Each font is loaded into its own
    /// FontCollection to avoid family name collisions between font weights.
    /// </summary>
    /// <param name="name">The logical font name (case-insensitive).</param>
    /// <param name="path">Path to the .ttf or .otf file.</param>
    /// <returns>True if the font file exists and was registered; otherwise, false.</returns>
    public bool RegisterFont(string name, string path)
    {
        ArgumentNullException.ThrowIfNull(name);
        ArgumentNullException.ThrowIfNull(path);

        if (_families.ContainsKey(name))
            return true;

        _fontPaths[name] = path;

        if (!File.Exists(path))
            return false;

        // Use an isolated FontCollection per font file to avoid family grouping.
        // E.g., Inter-Regular.ttf and Inter-Bold.ttf both declare family "Inter",
        // but we need them as separate entries so "bold" -> Bold, "default" -> Regular.
        var isolatedCollection = new FontCollection();
        var family = isolatedCollection.Add(path, CultureInfo.InvariantCulture);
        _families[name] = family;

        // Detect the FontStyle from the loaded font file
        var detectedStyle = FontStyle.Regular;
        foreach (var s in family.GetAvailableStyles())
        {
            detectedStyle = s;
            break; // Take the first (and typically only) style from this isolated font
        }
        _fontStyles[name] = detectedStyle;

        return true;
    }

    /// <summary>
    /// Gets a font with the specified size for the given logical font name.
    /// Uses the detected FontStyle from the registered font file.
    /// Falls back to "default" registration, then system fonts.
    /// </summary>
    /// <param name="fontName">The logical font name.</param>
    /// <param name="size">The font size in pixels.</param>
    /// <param name="style">The font style. Used only when no detected style is stored.</param>
    /// <returns>A configured Font instance. Never returns null.</returns>
    public Font GetFont(string fontName, float size, FontStyle style = FontStyle.Regular)
    {
        ObjectDisposedException.ThrowIf(Volatile.Read(ref _disposed) == 1, this);

        var family = GetFontFamily(fontName);

        // Use the detected style from the font file (e.g. Bold for Inter-Bold.ttf)
        if (_fontStyles.TryGetValue(fontName, out var detectedStyle))
            return family.CreateFont(size, detectedStyle);

        return family.CreateFont(size, style);
    }

    /// <summary>
    /// Gets a font with the specified size, weight, and style for the given logical font name.
    /// Maps AST font weight and style enums to the closest SixLabors.Fonts.FontStyle value.
    /// When the requested weight or style differs from Normal, scans sibling font files in the
    /// same directory as the registered font to find the correct variant (e.g. Inter-Bold.ttf
    /// for <see cref="AstFontWeight.Bold"/>). Falls back to the isolated collection if no
    /// sibling match is found.
    /// </summary>
    /// <param name="fontName">The logical font name.</param>
    /// <param name="size">The font size in pixels.</param>
    /// <param name="weight">The AST font weight (100-900).</param>
    /// <param name="style">The AST font style (Normal, Italic, Oblique).</param>
    /// <returns>A configured Font instance. Never returns null.</returns>
    public Font GetFont(string fontName, float size, AstFontWeight weight, AstFontStyle style)
    {
        ObjectDisposedException.ThrowIf(Volatile.Read(ref _disposed) == 1, this);

        if (weight == AstFontWeight.Normal && style == AstFontStyle.Normal)
            return GetFont(fontName, size);

        var sixLaborsStyle = (weight >= AstFontWeight.Bold, style) switch
        {
            (true, AstFontStyle.Italic or AstFontStyle.Oblique) => FontStyle.BoldItalic,
            (true, _) => FontStyle.Bold,
            (_, AstFontStyle.Italic or AstFontStyle.Oblique) => FontStyle.Italic,
            _ => FontStyle.Regular
        };

        // Try to find the variant in a shared collection built from sibling font files
        var familyFont = GetFamilyFont(fontName, size, sixLaborsStyle);
        if (familyFont is not null)
            return familyFont;

        // Fall back to the isolated collection approach
        var family = GetFontFamily(fontName);
        return family.CreateFont(size, sixLaborsStyle);
    }

    /// <summary>
    /// Attempts to find a font variant by scanning sibling font files in the same directory
    /// as the registered font. Loads all <c>.ttf</c> and <c>.otf</c> files from that directory
    /// into a shared <see cref="FontCollection"/>, then looks for a <see cref="FontFamily"/>
    /// matching the base font's family name that supports the requested style.
    /// </summary>
    /// <param name="fontName">The logical font name.</param>
    /// <param name="size">The font size in pixels.</param>
    /// <param name="requestedStyle">The desired SixLabors font style.</param>
    /// <returns>A <see cref="Font"/> if the variant was found; otherwise, <c>null</c>.</returns>
    private Font? GetFamilyFont(string fontName, float size, FontStyle requestedStyle)
    {
        if (!_fontPaths.TryGetValue(fontName, out var basePath))
            return null;

        var directory = Path.GetDirectoryName(basePath);
        if (string.IsNullOrEmpty(directory) || !Directory.Exists(directory))
            return null;

        // Get or create a shared collection for this directory
        var sharedCollection = _sharedCollections.GetOrAdd(directory, LoadDirectoryFonts);

        // Determine the family name from the base font's isolated collection
        if (!_families.TryGetValue(fontName, out var isolatedFamily))
            return null;

        var familyName = isolatedFamily.Name;

        // Look for the family in the shared collection
        if (!sharedCollection.TryGet(familyName, out var sharedFamily))
            return null;

        // Check if the requested style is available
        var availableStyles = sharedFamily.GetAvailableStyles();
        if (!availableStyles.Contains(requestedStyle))
            return null;

        return sharedFamily.CreateFont(size, requestedStyle);
    }

    /// <summary>
    /// Loads all <c>.ttf</c> and <c>.otf</c> font files from the specified directory
    /// into a new <see cref="FontCollection"/>. Files that fail to load (corrupt or
    /// unsupported format) are silently skipped.
    /// </summary>
    /// <param name="directory">The directory path to scan for font files.</param>
    /// <returns>A <see cref="FontCollection"/> containing all loadable fonts from the directory.</returns>
    private static FontCollection LoadDirectoryFonts(string directory)
    {
        var collection = new FontCollection();

        var fontFiles = Directory.EnumerateFiles(directory, "*.*")
            .Where(static f =>
            {
                var ext = Path.GetExtension(f);
                return ext.Equals(".ttf", StringComparison.OrdinalIgnoreCase)
                    || ext.Equals(".otf", StringComparison.OrdinalIgnoreCase);
            });

        foreach (var filePath in fontFiles)
        {
            try
            {
                collection.Add(filePath, CultureInfo.InvariantCulture);
            }
            catch
            {
                // Corrupted or unreadable font file; skip it
            }
        }

        return collection;
    }

    /// <summary>
    /// Gets the FontFamily for the given logical font name.
    /// Falls back to "default", then "main", then system Arial.
    /// </summary>
    /// <param name="fontName">The logical font name.</param>
    /// <returns>A FontFamily instance. Never returns null.</returns>
    public FontFamily GetFontFamily(string fontName)
    {
        ObjectDisposedException.ThrowIf(Volatile.Read(ref _disposed) == 1, this);

        // Try exact match
        if (_families.TryGetValue(fontName, out var family))
            return family;

        // Try loading from path if registered but not yet loaded
        if (_fontPaths.TryGetValue(fontName, out var path) && File.Exists(path))
        {
            var isolatedCollection = new FontCollection();
            family = isolatedCollection.Add(path, CultureInfo.InvariantCulture);
            _families[fontName] = family;

            // Detect style for lazy-loaded font
            foreach (var s in family.GetAvailableStyles())
            {
                _fontStyles[fontName] = s;
                break;
            }

            return family;
        }

        // Fallback chain: "default" -> "main" -> system font
        if (!string.Equals(fontName, "default", StringComparison.OrdinalIgnoreCase) &&
            _families.TryGetValue("default", out var defaultFamily))
            return defaultFamily;

        if (!string.Equals(fontName, "main", StringComparison.OrdinalIgnoreCase) &&
            _families.TryGetValue("main", out var mainFamily))
            return mainFamily;

        // Last resort: try system fonts
        if (SystemFonts.TryGet("Arial", out var arialFamily))
            return arialFamily;

        if (SystemFonts.TryGet("Liberation Sans", out var liberationFamily))
            return liberationFamily;

        // Final fallback: use the first registered font or first system font
        if (!_families.IsEmpty)
            return _families.Values.First();

        if (SystemFonts.Families.Any())
            return SystemFonts.Families.First();

        throw new InvalidOperationException(
            $"No font found for '{fontName}'. Register at least one font in the template or install system fonts.");
    }

    /// <summary>
    /// Disposes the font manager.
    /// </summary>
    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) == 1)
            return;
        // cleanup if any
    }
}
