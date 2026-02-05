using FlexRender.Configuration;

namespace FlexRender.Http;

/// <summary>
/// Extension methods for adding HTTP resource loading to <see cref="FlexRenderBuilder"/>.
/// </summary>
public static class FlexRenderBuilderExtensions
{
    /// <summary>
    /// Adds an HTTP resource loader to the builder, enabling loading of images and fonts from HTTP/HTTPS URLs.
    /// </summary>
    /// <param name="builder">The <see cref="FlexRenderBuilder"/> to configure.</param>
    /// <param name="httpClient">
    /// An optional <see cref="HttpClient"/> instance to use for HTTP requests.
    /// If <c>null</c>, a new <see cref="HttpClient"/> will be created and managed internally.
    /// </param>
    /// <param name="configure">
    /// An optional action to configure <see cref="HttpResourceLoaderOptions"/> including timeout and max resource size.
    /// </param>
    /// <returns>The builder instance for method chaining.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="builder"/> is null.</exception>
    /// <remarks>
    /// <para>
    /// When providing an <see cref="HttpClient"/>, the caller retains ownership and is responsible
    /// for its lifecycle. This is recommended when using <c>IHttpClientFactory</c> or a shared client.
    /// </para>
    /// <para>
    /// When no <see cref="HttpClient"/> is provided, an internal client is created with the
    /// configured timeout from <see cref="HttpResourceLoaderOptions"/>.
    /// </para>
    /// </remarks>
    /// <example>
    /// Using default HttpClient with default options:
    /// <code>
    /// var render = new FlexRenderBuilder()
    ///     .WithHttpLoader()
    ///     .WithSkia()
    ///     .Build();
    /// </code>
    /// Using default HttpClient with custom timeout:
    /// <code>
    /// var render = new FlexRenderBuilder()
    ///     .WithHttpLoader(configure: opts => opts.Timeout = TimeSpan.FromMinutes(1))
    ///     .WithSkia()
    ///     .Build();
    /// </code>
    /// Using custom HttpClient:
    /// <code>
    /// var httpClient = httpClientFactory.CreateClient("FlexRender");
    /// var render = new FlexRenderBuilder()
    ///     .WithHttpLoader(httpClient)
    ///     .WithSkia()
    ///     .Build();
    /// </code>
    /// </example>
    public static FlexRenderBuilder WithHttpLoader(
        this FlexRenderBuilder builder,
        HttpClient? httpClient = null,
        Action<HttpResourceLoaderOptions>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(builder);

        var options = new HttpResourceLoaderOptions();
        configure?.Invoke(options);

        if (httpClient is not null)
        {
            builder.ResourceLoaders.Add(new HttpResourceLoader(httpClient, options));
        }
        else
        {
            var client = new HttpClient
            {
                Timeout = options.Timeout
            };
            try
            {
                builder.ResourceLoaders.Add(new HttpResourceLoader(client, options, ownsHttpClient: true));
            }
            catch
            {
                client.Dispose();
                throw;
            }
        }

        return builder;
    }
}
