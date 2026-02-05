using FlexRender.Abstractions;
using FlexRender.Configuration;

namespace FlexRender.Http;

/// <summary>
/// Loads resources from HTTP and HTTPS URLs.
/// </summary>
/// <remarks>
/// <para>
/// This loader downloads resources from remote web servers using <see cref="HttpClient"/>.
/// It respects the <see cref="ResourceLimits.HttpTimeout"/> and
/// <see cref="ResourceLimits.MaxImageSize"/> settings from <see cref="FlexRenderOptions"/>.
/// </para>
/// <para>
/// The loader implements retry logic with exponential backoff for transient failures
/// (server errors and timeouts). Client errors (4xx) are not retried.
/// </para>
/// </remarks>
/// <example>
/// <code>
/// // Using with FlexRenderBuilder
/// var render = new FlexRenderBuilder()
///     .WithHttpLoader()
///     .WithSkia()
///     .Build();
///
/// // Using with custom HttpClient
/// var httpClient = new HttpClient();
/// var render = new FlexRenderBuilder()
///     .WithHttpLoader(httpClient)
///     .WithSkia()
///     .Build();
/// </code>
/// </example>
public sealed class HttpResourceLoader : IResourceLoader, IDisposable
{
    private const string HttpPrefix = "http://";
    private const string HttpsPrefix = "https://";
    private const int MaxRetries = 3;

    private static readonly TimeSpan[] RetryDelays =
    [
        TimeSpan.FromMilliseconds(100),
        TimeSpan.FromMilliseconds(500),
        TimeSpan.FromSeconds(1)
    ];

    private readonly FlexRenderOptions _options;
    private readonly HttpClient _httpClient;
    private readonly bool _ownsHttpClient;
    private bool _disposed;

    /// <summary>
    /// Initializes a new instance of the <see cref="HttpResourceLoader"/> class.
    /// </summary>
    /// <param name="httpClient">The HTTP client to use for requests.</param>
    /// <param name="options">The FlexRender configuration options.</param>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="httpClient"/> or <paramref name="options"/> is null.
    /// </exception>
    /// <remarks>
    /// When using this constructor, the caller retains ownership of the <see cref="HttpClient"/>
    /// and is responsible for its lifecycle. This is the recommended approach when using
    /// <c>IHttpClientFactory</c> or a shared <see cref="HttpClient"/> instance.
    /// </remarks>
    public HttpResourceLoader(HttpClient httpClient, FlexRenderOptions options)
        : this(httpClient, options, ownsHttpClient: false)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="HttpResourceLoader"/> class with ownership control.
    /// </summary>
    /// <param name="httpClient">The HTTP client to use for requests.</param>
    /// <param name="options">The FlexRender configuration options.</param>
    /// <param name="ownsHttpClient">
    /// If <c>true</c>, the loader will dispose the <paramref name="httpClient"/> when disposed.
    /// If <c>false</c>, the caller retains ownership and is responsible for disposing the client.
    /// </param>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="httpClient"/> or <paramref name="options"/> is null.
    /// </exception>
    public HttpResourceLoader(HttpClient httpClient, FlexRenderOptions options, bool ownsHttpClient)
    {
        ArgumentNullException.ThrowIfNull(httpClient);
        ArgumentNullException.ThrowIfNull(options);

        _httpClient = httpClient;
        _options = options;
        _ownsHttpClient = ownsHttpClient;
    }

    /// <inheritdoc />
    /// <remarks>
    /// HTTP loader has low priority (200) as remote resources are typically
    /// slower to load than local or embedded resources.
    /// </remarks>
    public int Priority => 200;

    /// <inheritdoc />
    /// <remarks>
    /// Returns <c>true</c> for URIs that start with "http://" or "https://".
    /// </remarks>
    public bool CanHandle(string uri)
    {
        if (string.IsNullOrWhiteSpace(uri))
        {
            return false;
        }

        return uri.StartsWith(HttpPrefix, StringComparison.OrdinalIgnoreCase) ||
               uri.StartsWith(HttpsPrefix, StringComparison.OrdinalIgnoreCase);
    }

    /// <inheritdoc />
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="uri"/> is null.</exception>
    /// <exception cref="HttpRequestException">Thrown when the HTTP request fails after all retries.</exception>
    /// <exception cref="InvalidOperationException">
    /// Thrown when the response exceeds <see cref="ResourceLimits.MaxImageSize"/>.
    /// </exception>
    /// <exception cref="TaskCanceledException">Thrown when the request times out or is cancelled.</exception>
    /// <exception cref="ObjectDisposedException">Thrown when this instance has been disposed.</exception>
    public async Task<Stream?> Load(string uri, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(uri);
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (!CanHandle(uri))
        {
            return null;
        }

        Exception? lastException = null;

        for (var attempt = 0; attempt < MaxRetries; attempt++)
        {
            try
            {
                using var response = await _httpClient
                    .GetAsync(uri, HttpCompletionOption.ResponseHeadersRead, cancellationToken)
                    .ConfigureAwait(false);

                // Don't retry on client errors (4xx)
                if ((int)response.StatusCode >= 400 && (int)response.StatusCode < 500)
                {
                    response.EnsureSuccessStatusCode();
                }

                // Retry on server errors (5xx)
                if ((int)response.StatusCode >= 500)
                {
                    lastException = new HttpRequestException($"Server error: {response.StatusCode}");
                    if (attempt < MaxRetries - 1)
                    {
                        await Task.Delay(RetryDelays[attempt], cancellationToken).ConfigureAwait(false);
                        continue;
                    }

                    response.EnsureSuccessStatusCode();
                }

                ValidateContentLength(response, uri);

                var bytes = await response.Content
                    .ReadAsByteArrayAsync(cancellationToken)
                    .ConfigureAwait(false);

                ValidateActualSize(bytes, uri);

                return new MemoryStream(bytes);
            }
            catch (HttpRequestException ex)
            {
                lastException = ex;

                // Retry on transient errors
                if (attempt < MaxRetries - 1)
                {
                    await Task.Delay(RetryDelays[attempt], cancellationToken).ConfigureAwait(false);
                    continue;
                }

                throw;
            }
            catch (TaskCanceledException ex) when (!cancellationToken.IsCancellationRequested)
            {
                // Timeout - retry
                lastException = ex;

                if (attempt < MaxRetries - 1)
                {
                    await Task.Delay(RetryDelays[attempt], cancellationToken).ConfigureAwait(false);
                    continue;
                }

                throw;
            }
        }

        // Should not reach here, but throw last exception if we do
        throw lastException ?? new InvalidOperationException("Retry loop completed without result.");
    }

    /// <summary>
    /// Releases the resources used by the <see cref="HttpResourceLoader"/>.
    /// </summary>
    /// <remarks>
    /// The <see cref="HttpClient"/> is only disposed if this instance owns it
    /// (i.e., if <c>ownsHttpClient</c> was set to <c>true</c> in the constructor).
    /// Clients provided by <c>IHttpClientFactory</c> should not be disposed by this class.
    /// </remarks>
    public void Dispose()
    {
        if (!_disposed)
        {
            if (_ownsHttpClient)
            {
                _httpClient.Dispose();
            }

            _disposed = true;
        }
    }

    /// <summary>
    /// Validates the Content-Length header if present.
    /// </summary>
    /// <param name="response">The HTTP response.</param>
    /// <param name="uri">The request URI for error messages.</param>
    /// <exception cref="InvalidOperationException">Thrown when content length exceeds maximum allowed size.</exception>
    private void ValidateContentLength(HttpResponseMessage response, string uri)
    {
        var contentLength = response.Content.Headers.ContentLength;

        if (contentLength.HasValue && contentLength.Value > _options.MaxImageSize)
        {
            throw new InvalidOperationException(
                $"Resource at '{uri}' exceeds maximum allowed size of {_options.MaxImageSize} bytes " +
                $"(Content-Length: {contentLength.Value} bytes).");
        }
    }

    /// <summary>
    /// Validates the actual downloaded data size.
    /// </summary>
    /// <param name="bytes">The downloaded bytes.</param>
    /// <param name="uri">The request URI for error messages.</param>
    /// <exception cref="InvalidOperationException">Thrown when actual size exceeds maximum allowed size.</exception>
    private void ValidateActualSize(byte[] bytes, string uri)
    {
        if (bytes.Length > _options.MaxImageSize)
        {
            throw new InvalidOperationException(
                $"Resource at '{uri}' exceeds maximum allowed size of {_options.MaxImageSize} bytes " +
                $"(actual size: {bytes.Length} bytes).");
        }
    }
}
