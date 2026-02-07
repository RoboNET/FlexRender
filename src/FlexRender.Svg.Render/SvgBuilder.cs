using FlexRender.Abstractions;
using FlexRender.Configuration;
using FlexRender.Parsing.Ast;
using FlexRender.Providers;

namespace FlexRender.Svg;

/// <summary>
/// Builder for configuring SVG rendering options and optional raster backend.
/// </summary>
/// <remarks>
/// <para>
/// This builder is used within the <see cref="SvgBuilderExtensions.WithSvg"/> extension method
/// to configure the SVG renderer. By default, only SVG output is supported.
/// </para>
/// <para>
/// To enable both SVG and raster (PNG, JPEG, BMP) output, call <c>WithSkia()</c>
/// or <see cref="WithRasterBackend"/> within the configuration action:
/// </para>
/// <code>
/// var render = new FlexRenderBuilder()
///     .WithSvg(svg => svg.WithSkia())
///     .Build();
/// </code>
/// <para>
/// SVG-native providers can be registered directly on this builder for SVG-only rendering
/// without the Skia backend. Use the extension methods from the provider packages
/// (e.g., <c>WithQrSvg()</c>, <c>WithBarcodeSvg()</c>, <c>WithSvgElementSvg()</c>).
/// </para>
/// </remarks>
public sealed class SvgBuilder
{
    private Func<FlexRenderBuilder, IFlexRender>? _rasterFactory;

    /// <summary>
    /// Gets the custom raster renderer factory, if any.
    /// </summary>
    internal Func<FlexRenderBuilder, IFlexRender>? RasterFactory => _rasterFactory;

    /// <summary>
    /// Gets the configured SVG-native QR code provider, if any.
    /// </summary>
    /// <remarks>
    /// This provider is set by calling the <c>WithQrSvg()</c> extension method
    /// from the FlexRender.QrCode.Svg package. When set, QR code elements are rendered
    /// as native SVG paths instead of rasterized bitmaps.
    /// </remarks>
    internal ISvgContentProvider<QrElement>? QrSvgProvider { get; private set; }

    /// <summary>
    /// Gets the configured SVG-native barcode provider, if any.
    /// </summary>
    /// <remarks>
    /// This provider is set by calling the <c>WithBarcodeSvg()</c> extension method
    /// from the FlexRender.Barcode.Svg package. When set, barcode elements are rendered
    /// as native SVG paths instead of rasterized bitmaps.
    /// </remarks>
    internal ISvgContentProvider<BarcodeElement>? BarcodeSvgProvider { get; private set; }

    /// <summary>
    /// Gets the configured SVG-native SVG element provider, if any.
    /// </summary>
    /// <remarks>
    /// This provider is set by calling the <c>WithSvgElementSvg()</c> extension method
    /// from the FlexRender.SvgElement.Svg package.
    /// </remarks>
    internal ISvgContentProvider<SvgElement>? SvgElementSvgProvider { get; private set; }

    /// <summary>
    /// Gets the configured raster QR code content provider for bitmap fallback in SVG rendering.
    /// </summary>
    internal IContentProvider<QrElement>? RasterQrProvider { get; private set; }

    /// <summary>
    /// Gets the configured raster barcode content provider for bitmap fallback in SVG rendering.
    /// </summary>
    internal IContentProvider<BarcodeElement>? RasterBarcodeProvider { get; private set; }

    /// <summary>
    /// Sets the raster QR code content provider for bitmap fallback.
    /// </summary>
    /// <param name="provider">The provider to use, or <c>null</c> to clear any previous registration.</param>
    internal void SetRasterQrProvider(IContentProvider<QrElement>? provider) => RasterQrProvider = provider;

    /// <summary>
    /// Sets the raster barcode content provider for bitmap fallback.
    /// </summary>
    /// <param name="provider">The provider to use, or <c>null</c> to clear any previous registration.</param>
    internal void SetRasterBarcodeProvider(IContentProvider<BarcodeElement>? provider) => RasterBarcodeProvider = provider;

    /// <summary>
    /// Sets the SVG-native QR code provider.
    /// </summary>
    /// <param name="provider">The QR code SVG provider to use.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="provider"/> is null.</exception>
    /// <exception cref="InvalidOperationException">Thrown when a QR SVG provider is already configured.</exception>
    internal void SetQrSvgProvider(ISvgContentProvider<QrElement> provider)
    {
        ArgumentNullException.ThrowIfNull(provider);
        if (QrSvgProvider is not null)
        {
            throw new InvalidOperationException("QR SVG provider is already configured. WithQrSvg() can only be called once.");
        }
        QrSvgProvider = provider;
    }

    /// <summary>
    /// Sets the SVG-native barcode provider.
    /// </summary>
    /// <param name="provider">The barcode SVG provider to use.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="provider"/> is null.</exception>
    /// <exception cref="InvalidOperationException">Thrown when a barcode SVG provider is already configured.</exception>
    internal void SetBarcodeSvgProvider(ISvgContentProvider<BarcodeElement> provider)
    {
        ArgumentNullException.ThrowIfNull(provider);
        if (BarcodeSvgProvider is not null)
        {
            throw new InvalidOperationException("Barcode SVG provider is already configured. WithBarcodeSvg() can only be called once.");
        }
        BarcodeSvgProvider = provider;
    }

    /// <summary>
    /// Sets the SVG-native SVG element provider.
    /// </summary>
    /// <param name="provider">The SVG element SVG provider to use.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="provider"/> is null.</exception>
    /// <exception cref="InvalidOperationException">Thrown when an SVG element SVG provider is already configured.</exception>
    internal void SetSvgElementSvgProvider(ISvgContentProvider<SvgElement> provider)
    {
        ArgumentNullException.ThrowIfNull(provider);
        if (SvgElementSvgProvider is not null)
        {
            throw new InvalidOperationException("SVG element SVG provider is already configured. WithSvgElementSvg() can only be called once.");
        }
        SvgElementSvgProvider = provider;
    }

    /// <summary>
    /// Sets a custom raster rendering backend for PNG, JPEG, BMP, and Raw output alongside SVG.
    /// </summary>
    /// <param name="factory">
    /// A factory function that receives the <see cref="FlexRenderBuilder"/> and returns an
    /// <see cref="IFlexRender"/> instance to use for raster output.
    /// </param>
    /// <returns>This builder instance for method chaining.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="factory"/> is null.</exception>
    /// <remarks>
    /// <para>
    /// Use this method when you want to use a non-Skia renderer (e.g., ImageSharp) for raster output
    /// while still producing SVG output. The factory receives the fully-configured
    /// <see cref="FlexRenderBuilder"/> so it can access resource limits, options, and loaders.
    /// </para>
    /// </remarks>
    /// <example>
    /// <code>
    /// var render = new FlexRenderBuilder()
    ///     .WithSvg(svg => svg.WithRasterBackend(b =>
    ///         new MyCustomRender(b.Limits, b.Options, b.ResourceLoaders)))
    ///     .Build();
    /// </code>
    /// </example>
    public SvgBuilder WithRasterBackend(Func<FlexRenderBuilder, IFlexRender> factory)
    {
        ArgumentNullException.ThrowIfNull(factory);
        _rasterFactory = factory;
        return this;
    }
}
