using FlexRender.Svg;
using FlexRender.SvgElement.Svg.Providers;

namespace FlexRender.SvgElement.Svg;

/// <summary>
/// Extension methods for configuring SVG-native SVG element support on <see cref="SvgBuilder"/>.
/// </summary>
public static class SvgBuilderSvgElementExtensions
{
    /// <summary>
    /// Adds SVG-native SVG element support to the SVG renderer.
    /// </summary>
    /// <param name="builder">The SVG builder to configure.</param>
    /// <returns>The same builder instance for method chaining.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="builder"/> is null.</exception>
    public static SvgBuilder WithSvgElementSvg(this SvgBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        builder.SetSvgElementSvgProvider(new SvgElementSvgProvider());
        return builder;
    }
}
