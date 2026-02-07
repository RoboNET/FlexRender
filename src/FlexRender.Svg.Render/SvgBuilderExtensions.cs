using FlexRender.Configuration;
using FlexRender.Parsing.Ast;
using FlexRender.Providers;
using FlexRender.Svg;

namespace FlexRender;

/// <summary>
/// Extension methods for configuring SVG rendering with <see cref="FlexRenderBuilder"/>.
/// </summary>
public static class SvgBuilderExtensions
{
    /// <summary>
    /// Configures the builder to use the SVG rendering backend.
    /// </summary>
    /// <param name="builder">The builder to configure.</param>
    /// <param name="configure">
    /// Optional action to configure SVG-specific options, including an optional Skia
    /// backend for raster output alongside SVG.
    /// </param>
    /// <returns>The builder instance for method chaining.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="builder"/> is null.</exception>
    /// <remarks>
    /// <para>
    /// This method registers the <see cref="SvgRender"/> as the rendering implementation.
    /// By default, only SVG output is supported. To also support raster formats
    /// (PNG, JPEG, BMP, Raw), call <c>WithSkia()</c> within the configure action.
    /// </para>
    /// </remarks>
    /// <example>
    /// SVG-only configuration:
    /// <code>
    /// var render = new FlexRenderBuilder()
    ///     .WithSvg()
    ///     .Build();
    /// </code>
    /// SVG with raster backend:
    /// <code>
    /// var render = new FlexRenderBuilder()
    ///     .WithSvg(svg => svg.WithSkia())
    ///     .Build();
    /// </code>
    /// SVG with full Skia configuration:
    /// <code>
    /// var render = new FlexRenderBuilder()
    ///     .WithSvg(svg => svg.WithSkia(skia => skia
    ///         .WithQr()
    ///         .WithBarcode()))
    ///     .Build();
    /// </code>
    /// SVG-only with native SVG providers:
    /// <code>
    /// var render = new FlexRenderBuilder()
    ///     .WithSvg(svg => svg
    ///         .WithQrSvg()
    ///         .WithBarcodeSvg())
    ///     .Build();
    /// </code>
    /// </example>
    public static FlexRenderBuilder WithSvg(
        this FlexRenderBuilder builder,
        Action<SvgBuilder>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(builder);

        var svgBuilder = new SvgBuilder();
        configure?.Invoke(svgBuilder);

        builder.SetRendererFactory(b =>
        {
            Abstractions.IFlexRender? rasterRenderer = null;
            IContentProvider<QrElement>? qrProvider = svgBuilder.RasterQrProvider;
            IContentProvider<BarcodeElement>? barcodeProvider = svgBuilder.RasterBarcodeProvider;

            if (svgBuilder.RasterFactory is not null)
            {
                rasterRenderer = svgBuilder.RasterFactory(b);
            }

            // Inject resource loaders into any SVG-native providers that need them
            InjectResourceLoaders(svgBuilder.QrSvgProvider, b.ResourceLoaders);
            InjectResourceLoaders(svgBuilder.BarcodeSvgProvider, b.ResourceLoaders);
            InjectResourceLoaders(svgBuilder.SvgElementSvgProvider, b.ResourceLoaders);

            return new SvgRender(
                b.Limits,
                b.Options,
                rasterRenderer,
                qrProvider,
                barcodeProvider,
                svgBuilder.QrSvgProvider,
                svgBuilder.BarcodeSvgProvider,
                svgBuilder.SvgElementSvgProvider);
        });

        return builder;
    }

    /// <summary>
    /// Injects resource loaders into a provider if it implements <see cref="IResourceLoaderAware"/>.
    /// </summary>
    /// <param name="provider">The provider to inject loaders into, or null.</param>
    /// <param name="loaders">The resource loaders to inject.</param>
    private static void InjectResourceLoaders(object? provider, IReadOnlyList<Abstractions.IResourceLoader> loaders)
    {
        if (provider is IResourceLoaderAware resourceAware)
        {
            resourceAware.SetResourceLoaders(loaders);
        }
    }
}
