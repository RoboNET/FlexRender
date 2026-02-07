using FlexRender.Configuration;
using FlexRender.Skia;

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
/// To enable both SVG and raster (PNG, JPEG, BMP) output, call <see cref="WithSkia"/>
/// within the configuration action:
/// </para>
/// <code>
/// var render = new FlexRenderBuilder()
///     .WithSvg(svg => svg.WithSkia())
///     .Build();
/// </code>
/// </remarks>
public sealed class SvgBuilder
{
    private Action<SkiaBuilder>? _skiaConfigureAction;
    private bool _skiaEnabled;

    /// <summary>
    /// Gets whether the Skia raster backend is enabled.
    /// </summary>
    internal bool IsSkiaEnabled => _skiaEnabled;

    /// <summary>
    /// Gets the Skia configuration action, if any.
    /// </summary>
    internal Action<SkiaBuilder>? SkiaConfigureAction => _skiaConfigureAction;

    /// <summary>
    /// Enables the Skia raster backend for PNG, JPEG, BMP, and Raw output alongside SVG.
    /// </summary>
    /// <param name="configure">
    /// Optional action to configure Skia-specific options such as QR code and barcode providers.
    /// </param>
    /// <returns>This builder instance for method chaining.</returns>
    /// <remarks>
    /// Without calling this method, attempts to use raster output methods
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
    public SvgBuilder WithSkia(Action<SkiaBuilder>? configure = null)
    {
        _skiaEnabled = true;
        _skiaConfigureAction = configure;
        return this;
    }
}
