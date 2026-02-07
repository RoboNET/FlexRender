using FlexRender.Skia;
using FlexRender.SvgElement.Providers;

namespace FlexRender.SvgElement;

/// <summary>
/// Extension methods for configuring SVG element support in <see cref="SkiaBuilder"/>.
/// </summary>
public static class SkiaBuilderExtensions
{
    /// <summary>
    /// Adds SVG element rendering support to the Skia renderer.
    /// </summary>
    /// <param name="builder">The Skia builder to configure.</param>
    /// <returns>The same builder instance for method chaining.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="builder"/> is null.</exception>
    /// <remarks>
    /// <para>
    /// This method registers the <see cref="SvgElementProvider"/> which enables rendering of
    /// SVG elements in templates. SVG content is parsed and rasterized using the Svg.Skia library.
    /// </para>
    /// <para>
    /// After calling this method, templates can include SVG elements with either inline
    /// SVG markup (<c>content</c>) or external SVG file references (<c>src</c>).
    /// </para>
    /// </remarks>
    /// <example>
    /// <code>
    /// var render = new FlexRenderBuilder()
    ///     .WithSkia(skia => skia.WithSvgElement())
    ///     .Build();
    /// </code>
    /// </example>
    public static SkiaBuilder WithSvgElement(this SkiaBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        builder.SetSvgProvider(new SvgElementProvider());
        return builder;
    }
}
