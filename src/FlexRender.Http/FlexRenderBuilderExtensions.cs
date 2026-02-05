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
    /// <returns>The builder instance for method chaining.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="builder"/> is null.</exception>
    /// <remarks>
    /// <para>
    /// When providing an <see cref="HttpClient"/>, the caller retains ownership and is responsible
    /// for its lifecycle. This is recommended when using <c>IHttpClientFactory</c> or a shared client.
    /// </para>
    /// <para>
    /// When no <see cref="HttpClient"/> is provided, an internal client is created with the
    /// <see cref="ResourceLimits.HttpTimeout"/> from the builder's options.
    /// </para>
    /// </remarks>
    /// <example>
    /// Using default HttpClient:
    /// <code>
    /// var render = new FlexRenderBuilder()
    ///     .WithHttpLoader()
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
        HttpClient? httpClient = null)
    {
        ArgumentNullException.ThrowIfNull(builder);

        if (httpClient is not null)
        {
            builder.ResourceLoaders.Add(new HttpResourceLoader(httpClient, builder.Options));
        }
        else
        {
            var client = new HttpClient
            {
                Timeout = builder.Options.Limits.HttpTimeout
            };
            try
            {
                builder.ResourceLoaders.Add(new HttpResourceLoader(client, builder.Options, ownsHttpClient: true));
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
