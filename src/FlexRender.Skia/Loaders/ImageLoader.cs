using FlexRender.Abstractions;
using FlexRender.Configuration;
using SkiaSharp;

namespace FlexRender.Loaders;

/// <summary>
/// Loads images by coordinating with a chain of resource loaders.
/// </summary>
/// <remarks>
/// This implementation uses the Chain of Responsibility pattern to try
/// multiple resource loaders in priority order until one succeeds.
/// </remarks>
public sealed class ImageLoader : IImageLoader
{
    private readonly IEnumerable<IResourceLoader> _loaders;
    private readonly FlexRenderOptions _options;

    /// <summary>
    /// Initializes a new instance of the <see cref="ImageLoader"/> class.
    /// </summary>
    /// <param name="loaders">The collection of resource loaders to use.</param>
    /// <param name="options">The FlexRender configuration options.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="loaders"/> or <paramref name="options"/> is null.</exception>
    public ImageLoader(IEnumerable<IResourceLoader> loaders, FlexRenderOptions options)
    {
        ArgumentNullException.ThrowIfNull(loaders);
        ArgumentNullException.ThrowIfNull(options);
        _loaders = loaders.OrderBy(l => l.Priority);
        _options = options;
    }

    /// <inheritdoc />
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="uri"/> is null.</exception>
    /// <exception cref="InvalidOperationException">
    /// Thrown when the image cannot be decoded or exceeds <see cref="FlexRenderOptions.MaxImageSize"/>.
    /// </exception>
    /// <exception cref="OperationCanceledException">Thrown when the operation is cancelled.</exception>
    public async Task<SKBitmap?> Load(string uri, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(uri);

        if (string.IsNullOrWhiteSpace(uri))
        {
            return null;
        }

        foreach (var loader in _loaders.Where(l => l.CanHandle(uri)))
        {
            cancellationToken.ThrowIfCancellationRequested();

            var stream = await loader.Load(uri, cancellationToken).ConfigureAwait(false);
            if (stream is not null)
            {
                using (stream)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    ValidateStreamSize(stream, uri);

                    cancellationToken.ThrowIfCancellationRequested();

                    var bitmap = SKBitmap.Decode(stream);
                    if (bitmap is null)
                    {
                        throw new InvalidOperationException($"Failed to decode image from: {uri}");
                    }

                    return bitmap;
                }
            }
        }

        return null;
    }

    /// <summary>
    /// Validates that the stream size does not exceed the maximum allowed image size.
    /// </summary>
    /// <param name="stream">The stream to validate.</param>
    /// <param name="uri">The URI for error messages.</param>
    /// <exception cref="InvalidOperationException">Thrown when the stream size exceeds <see cref="FlexRenderOptions.MaxImageSize"/>.</exception>
    private void ValidateStreamSize(Stream stream, string uri)
    {
        if (stream.CanSeek && stream.Length > _options.MaxImageSize)
        {
            throw new InvalidOperationException(
                $"Image at '{uri}' exceeds maximum allowed size of {_options.MaxImageSize} bytes " +
                $"(actual size: {stream.Length} bytes).");
        }
    }
}
