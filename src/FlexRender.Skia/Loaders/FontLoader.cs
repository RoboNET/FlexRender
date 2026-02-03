using FlexRender.Abstractions;
using SkiaSharp;

namespace FlexRender.Loaders;

/// <summary>
/// Loads fonts by coordinating with a chain of resource loaders.
/// </summary>
/// <remarks>
/// This implementation uses the Chain of Responsibility pattern to try
/// multiple resource loaders in priority order. If no resource loader
/// can handle the URI, it falls back to system font lookup by family name.
/// </remarks>
public sealed class FontLoader : IFontLoader
{
    private static readonly string[] FontExtensions = [".ttf", ".otf", ".ttc"];

    private readonly IEnumerable<IResourceLoader> _loaders;

    /// <summary>
    /// Initializes a new instance of the <see cref="FontLoader"/> class.
    /// </summary>
    /// <param name="loaders">The collection of resource loaders to use.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="loaders"/> is null.</exception>
    public FontLoader(IEnumerable<IResourceLoader> loaders)
    {
        ArgumentNullException.ThrowIfNull(loaders);
        _loaders = loaders.OrderBy(l => l.Priority);
    }

    /// <inheritdoc />
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="fontNameOrUri"/> is null.</exception>
    /// <exception cref="InvalidOperationException">Thrown when the font file cannot be loaded.</exception>
    public async Task<SKTypeface?> Load(string fontNameOrUri, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(fontNameOrUri);

        if (string.IsNullOrWhiteSpace(fontNameOrUri))
        {
            return null;
        }

        // First, try to load as a font file via resource loaders
        if (IsFontFile(fontNameOrUri) || IsResourceUri(fontNameOrUri))
        {
            cancellationToken.ThrowIfCancellationRequested();

            foreach (var loader in _loaders.Where(l => l.CanHandle(fontNameOrUri)))
            {
                var stream = await loader.Load(fontNameOrUri, cancellationToken).ConfigureAwait(false);
                if (stream is not null)
                {
                    using (stream)
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        var typeface = SKTypeface.FromStream(stream);
                        if (typeface is null)
                        {
                            throw new InvalidOperationException($"Failed to load font from: {fontNameOrUri}");
                        }
                        return typeface;
                    }
                }
            }
        }

        // Fall back to system font by family name
        return SKTypeface.FromFamilyName(fontNameOrUri);
    }

    /// <summary>
    /// Checks if the URI points to a font file based on extension.
    /// </summary>
    /// <param name="uri">The URI to check.</param>
    /// <returns>True if the URI has a font file extension; otherwise, false.</returns>
    private static bool IsFontFile(string uri)
    {
        foreach (var extension in FontExtensions)
        {
            if (uri.EndsWith(extension, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }
        return false;
    }

    /// <summary>
    /// Checks if the URI is a resource URI (data:, embedded://, http://).
    /// </summary>
    /// <param name="uri">The URI to check.</param>
    /// <returns>True if the URI is a resource URI; otherwise, false.</returns>
    private static bool IsResourceUri(string uri)
    {
        return uri.StartsWith("data:", StringComparison.OrdinalIgnoreCase) ||
               uri.StartsWith("embedded://", StringComparison.OrdinalIgnoreCase) ||
               uri.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
               uri.StartsWith("https://", StringComparison.OrdinalIgnoreCase);
    }
}
