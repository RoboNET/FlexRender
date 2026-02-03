using SkiaSharp;

namespace FlexRender.Abstractions;

/// <summary>
/// Loads fonts from various sources by coordinating with resource loaders.
/// </summary>
/// <remarks>
/// This interface provides a simplified API for loading fonts.
/// The implementation uses <see cref="IResourceLoader"/> chain internally
/// and falls back to system fonts when necessary.
/// </remarks>
public interface IFontLoader
{
    /// <summary>
    /// Loads a font from the specified URI or font family name.
    /// </summary>
    /// <param name="fontNameOrUri">Font family name or URI to the font file.</param>
    /// <param name="cancellationToken">Cancellation token for async operation.</param>
    /// <returns>Loaded typeface or null if the font cannot be loaded.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="fontNameOrUri"/> is null.</exception>
    Task<SKTypeface?> Load(string fontNameOrUri, CancellationToken cancellationToken = default);
}
