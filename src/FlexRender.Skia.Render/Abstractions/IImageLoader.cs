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

    /// <summary>
    /// Preloads an image from a stream and returns it for caching under the specified key.
    /// Used for injecting in-memory image data (e.g., from <see cref="FlexRender.BytesValue"/>)
    /// into the image cache so that rendering can find it by key.
    /// </summary>
    /// <param name="key">The cache key (e.g., "var://variableName").</param>
    /// <param name="stream">The image data stream.</param>
    /// <param name="cancellationToken">Cancellation token for async operation.</param>
    /// <returns>The loaded bitmap, or null if the stream cannot be decoded.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="key"/> or <paramref name="stream"/> is null.</exception>
    Task<SKBitmap?> Preload(string key, Stream stream, CancellationToken cancellationToken = default);
}
