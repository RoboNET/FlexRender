using FlexRender.Abstractions;
using FlexRender.Configuration;
using FlexRender.Loaders;
using FlexRender.Parsing.Ast;
using FlexRender.Providers;
using FlexRender.Rendering;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Extension methods for registering FlexRender services with the dependency injection container.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Adds FlexRender services to the specified <see cref="IServiceCollection"/>.
    /// </summary>
    /// <param name="services">The <see cref="IServiceCollection"/> to add services to.</param>
    /// <param name="configure">An optional action to configure the <see cref="FlexRenderBuilder"/>.</param>
    /// <returns>The <see cref="IServiceCollection"/> so that additional calls can be chained.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="services"/> is null.</exception>
    /// <remarks>
    /// This method registers the following services:
    /// <list type="bullet">
    /// <item><description>Default resource loaders: <see cref="FileResourceLoader"/>, <see cref="Base64ResourceLoader"/>, <see cref="EmbeddedResourceLoader"/>, <see cref="HttpResourceLoader"/></description></item>
    /// <item><description>Image and font loaders: <see cref="ImageLoader"/>, <see cref="FontLoader"/></description></item>
    /// <item><description>Core services: <see cref="IFontManager"/>, <see cref="IFlexRenderer"/></description></item>
    /// <item><description>Content providers: <see cref="IContentProvider{T}"/> for QR codes and barcodes</description></item>
    /// </list>
    /// </remarks>
    /// <example>
    /// <code>
    /// services.AddFlexRender(builder => builder
    ///     .WithBasePath("/app/templates")
    ///     .WithDefaultFont("Roboto")
    ///     .EnableCaching());
    /// </code>
    /// </example>
    public static IServiceCollection AddFlexRender(
        this IServiceCollection services,
        Action<FlexRenderBuilder>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(services);

        var builder = new FlexRenderBuilder(services);

        // Register default resource loaders
        builder.AddResourceLoader<FileResourceLoader>();
        builder.AddResourceLoader<Base64ResourceLoader>();
        builder.AddResourceLoader<EmbeddedResourceLoader>();
        builder.AddResourceLoader<HttpResourceLoader>();

        configure?.Invoke(builder);
        builder.Build();

        // Register image and font loaders that use resource loaders
        services.AddSingleton<IImageLoader, ImageLoader>();
        services.AddSingleton<IFontLoader, FontLoader>();

        // Register content providers
        services.AddSingleton<IContentProvider<QrElement>, QrProvider>();
        services.AddSingleton<IContentProvider<BarcodeElement>, BarcodeProvider>();

        // Register core services
        services.AddSingleton<IFontManager, FontManager>();
        services.AddSingleton<IFlexRenderer>(sp =>
        {
            var opts = sp.GetRequiredService<FlexRenderOptions>();
            var qrProvider = sp.GetService<IContentProvider<QrElement>>();
            var barcodeProvider = sp.GetService<IContentProvider<BarcodeElement>>();
            var imageLoader = sp.GetService<IImageLoader>();
            return new SkiaRenderer(opts.Limits, qrProvider, barcodeProvider, imageLoader);
        });

        return services;
    }
}
