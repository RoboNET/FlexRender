using FlexRender.Barcode.Svg.Providers;
using FlexRender.Svg;

namespace FlexRender.Barcode.Svg;

/// <summary>
/// Extension methods for configuring SVG-native barcode support on <see cref="SvgBuilder"/>.
/// </summary>
public static class SvgBuilderBarcodeExtensions
{
    /// <summary>
    /// Adds SVG-native barcode generation support to the SVG renderer.
    /// </summary>
    /// <param name="builder">The SVG builder to configure.</param>
    /// <returns>The same builder instance for method chaining.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="builder"/> is null.</exception>
    public static SvgBuilder WithBarcodeSvg(this SvgBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        builder.SetBarcodeSvgProvider(new BarcodeSvgProvider());
        return builder;
    }
}
