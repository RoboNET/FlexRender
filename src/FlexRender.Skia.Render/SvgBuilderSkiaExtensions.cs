using FlexRender.Skia;
using FlexRender.Svg;

namespace FlexRender;

/// <summary>
/// Extension methods for enabling the Skia raster backend on <see cref="SvgBuilder"/>.
/// </summary>
public static class SvgBuilderSkiaExtensions
{
    /// <summary>
    /// Enables the Skia raster backend for PNG, JPEG, BMP, and Raw output alongside SVG.
    /// </summary>
    /// <param name="svgBuilder">The SVG builder to configure.</param>
    /// <param name="configure">
    /// Optional action to configure Skia-specific options such as QR code and barcode providers.
    /// </param>
    /// <returns>The SVG builder instance for method chaining.</returns>
    /// <remarks>
    /// Without calling this method or <see cref="SvgBuilder.WithRasterBackend"/>, attempts to use raster output methods
    /// (RenderToPng, RenderToJpeg, etc.) will throw <see cref="NotSupportedException"/>.
    /// </remarks>
    /// <example>
    /// <code>
    /// var render = new FlexRenderBuilder()
    ///     .WithSvg(svg => svg
    ///         .WithSkia(skia => skia
    ///             .WithQr()
    ///             .WithBarcode()))
    ///     .Build();
    /// </code>
    /// </example>
    public static SvgBuilder WithSkia(this SvgBuilder svgBuilder, Action<SkiaBuilder>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(svgBuilder);

        var skiaBuilder = new SkiaBuilder();
        configure?.Invoke(skiaBuilder);

        // Register raster providers for SVG bitmap fallback
        svgBuilder.SetRasterQrProvider(skiaBuilder.QrProvider);
        svgBuilder.SetRasterBarcodeProvider(skiaBuilder.BarcodeProvider);

        return svgBuilder.WithRasterBackend(b => new SkiaRender(
            b.Limits,
            b.Options,
            b.ResourceLoaders,
            skiaBuilder));
    }
}
