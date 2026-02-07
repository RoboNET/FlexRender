using SkiaSharp;

namespace FlexRender.Abstractions;

/// <summary>
/// Loads images from various sources by coordinating with resource loaders.
/// </summary>
/// <remarks>
/// This interface provides a simplified API for loading images.
/// The implementation uses <see cref="IResourceLoader"/> chain internally.
/// </remarks>
public interface IImageLoader
{
    /// <summary>
    /// Loads an image from the specified URI.
    /// </summary>
    /// <param name="uri">Image URI (file path, http://, data:, embedded://).</param>
    /// <param name="cancellationToken">Cancellation token for async operation.</param>
    /// <returns>Loaded bitmap or null if the image cannot be loaded.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="uri"/> is null.</exception>
    Task<SKBitmap?> Load(string uri, CancellationToken cancellationToken = default);
}
