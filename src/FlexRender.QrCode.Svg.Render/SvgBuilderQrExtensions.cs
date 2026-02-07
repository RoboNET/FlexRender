using FlexRender.QrCode.Svg.Providers;
using FlexRender.Svg;

namespace FlexRender.QrCode.Svg;

/// <summary>
/// Extension methods for configuring SVG-native QR code support on <see cref="SvgBuilder"/>.
/// </summary>
public static class SvgBuilderQrExtensions
{
    /// <summary>
    /// Adds SVG-native QR code generation support to the SVG renderer.
    /// </summary>
    /// <param name="builder">The SVG builder to configure.</param>
    /// <returns>The same builder instance for method chaining.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="builder"/> is null.</exception>
    /// <remarks>
    /// <para>
    /// This method registers the <see cref="QrSvgProvider"/> as the SVG-native QR code provider.
    /// QR code elements are rendered as native SVG path elements instead of rasterized bitmaps,
    /// producing smaller, scalable, pixel-perfect vector QR codes.
    /// </para>
    /// <para>
    /// This is intended for SVG-only rendering without the Skia raster backend. When using
    /// <c>WithSkia(skia => skia.WithQr())</c>, the QR provider is automatically available for
    /// both raster and SVG output.
    /// </para>
    /// </remarks>
    /// <example>
    /// <code>
    /// var render = new FlexRenderBuilder()
    ///     .WithSvg(svg => svg.WithQrSvg())
    ///     .Build();
    /// </code>
    /// </example>
    public static SvgBuilder WithQrSvg(this SvgBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        builder.SetQrSvgProvider(new QrSvgProvider());
        return builder;
    }
}
