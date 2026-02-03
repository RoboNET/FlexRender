using FlexRender.Abstractions;
using FlexRender.Configuration;

namespace FlexRender.Loaders;

/// <summary>
/// Loads resources from base64-encoded data URLs.
/// </summary>
/// <remarks>
/// This loader handles data URLs in the format: data:[mediatype][;base64],data
/// For example: data:image/png;base64,iVBORw0KGgo...
/// </remarks>
public sealed class Base64ResourceLoader : IResourceLoader
{
    private const string DataPrefix = "data:";

    private readonly FlexRenderOptions _options;

    /// <summary>
    /// Initializes a new instance of the <see cref="Base64ResourceLoader"/> class.
    /// </summary>
    /// <param name="options">The FlexRender configuration options.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="options"/> is null.</exception>
    public Base64ResourceLoader(FlexRenderOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        _options = options;
    }

    /// <inheritdoc />
    /// <remarks>
    /// Base64 loader has priority 50, allowing it to be processed before file loaders
    /// as data URLs are self-contained and don't require file system access.
    /// </remarks>
    public int Priority => 50;

    /// <inheritdoc />
    /// <remarks>
    /// Returns <c>true</c> for URIs that start with "data:".
    /// </remarks>
    public bool CanHandle(string uri)
    {
        if (string.IsNullOrWhiteSpace(uri))
        {
            return false;
        }

        return uri.StartsWith(DataPrefix, StringComparison.OrdinalIgnoreCase);
    }

    /// <inheritdoc />
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="uri"/> is null.</exception>
    /// <exception cref="ArgumentException">
    /// Thrown when the data URL format is invalid or the base64 data exceeds <see cref="FlexRenderOptions.MaxImageSize"/>.
    /// </exception>
    public Task<Stream?> Load(string uri, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(uri);

        if (!CanHandle(uri))
        {
            return Task.FromResult<Stream?>(null);
        }

        cancellationToken.ThrowIfCancellationRequested();

        var base64Data = ExtractBase64Data(uri);
        ValidateDataSize(base64Data);

        byte[] bytes;
        try
        {
            bytes = Convert.FromBase64String(base64Data);
        }
        catch (FormatException ex)
        {
            throw new ArgumentException(
                "Invalid base64 data in data URL.",
                nameof(uri),
                ex);
        }

        Stream stream = new MemoryStream(bytes);
        return Task.FromResult<Stream?>(stream);
    }

    /// <summary>
    /// Extracts the base64 data portion from a data URL.
    /// </summary>
    /// <param name="dataUrl">The full data URL.</param>
    /// <returns>The base64-encoded data portion.</returns>
    /// <exception cref="ArgumentException">Thrown when the data URL format is invalid.</exception>
    private static string ExtractBase64Data(string dataUrl)
    {
        var commaIndex = dataUrl.IndexOf(',');

        if (commaIndex == -1)
        {
            throw new ArgumentException(
                "Invalid data URL format. Expected 'data:[mediatype][;base64],data'.",
                nameof(dataUrl));
        }

        return dataUrl[(commaIndex + 1)..];
    }

    /// <summary>
    /// Validates that the base64 data does not exceed the maximum allowed size.
    /// </summary>
    /// <param name="base64Data">The base64-encoded data string.</param>
    /// <exception cref="ArgumentException">Thrown when the decoded data would exceed the maximum size.</exception>
    private void ValidateDataSize(string base64Data)
    {
        // Base64 encoding is approximately 33% larger than the decoded data
        // Formula: decoded_size = encoded_size * 3 / 4
        var estimatedDecodedSize = (base64Data.Length * 3) / 4;

        if (estimatedDecodedSize > _options.MaxImageSize)
        {
            throw new ArgumentException(
                $"Base64 data exceeds maximum allowed size of {_options.MaxImageSize} bytes " +
                $"(estimated {estimatedDecodedSize} bytes).");
        }
    }
}
