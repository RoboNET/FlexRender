using FlexRender.Abstractions;
using FlexRender.Configuration;
using FlexRender.ImageSharp;

namespace FlexRender;

/// <summary>
/// Extension methods for configuring ImageSharp rendering with <see cref="FlexRenderBuilder"/>.
/// </summary>
public static class ImageSharpFlexRenderBuilderExtensions
{
    /// <summary>
    /// Configures the builder to use the ImageSharp rendering backend.
    /// </summary>
    /// <param name="builder">The builder to configure.</param>
    /// <param name="configure">
    /// Optional action to configure ImageSharp-specific options.
    /// </param>
    /// <returns>The builder instance for method chaining.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="builder"/> is null.</exception>
    /// <remarks>
    /// <para>
    /// This method registers the <see cref="ImageSharpRender"/> as the rendering implementation.
    /// The ImageSharp renderer uses SixLabors.ImageSharp for cross-platform 2D graphics rendering
    /// without any native library dependencies.
    /// </para>
    /// </remarks>
    /// <example>
    /// Minimal configuration:
    /// <code>
    /// var render = new FlexRenderBuilder()
    ///     .WithImageSharp()
    ///     .Build();
    /// </code>
    /// With configuration:
    /// <code>
    /// var render = new FlexRenderBuilder()
    ///     .WithBasePath("./templates")
    ///     .WithImageSharp(imageSharp => { /* future options */ })
    ///     .Build();
    /// </code>
    /// </example>
    public static FlexRenderBuilder WithImageSharp(
        this FlexRenderBuilder builder,
        Action<ImageSharpBuilder>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(builder);

        var imageSharpBuilder = new ImageSharpBuilder();
        configure?.Invoke(imageSharpBuilder);

        builder.SetRendererFactory(b => new ImageSharpRender(
            b.Limits,
            b.Options,
            b.ResourceLoaders,
            imageSharpBuilder,
            b.FilterRegistry));

        return builder;
    }

    /// <summary>
    /// Creates a factory function that produces an <see cref="ImageSharpRender"/> from a
    /// <see cref="FlexRenderBuilder"/>. This is intended for use with
    /// <c>SvgBuilder.WithRasterBackend</c> to enable SVG output with ImageSharp as the raster fallback.
    /// </summary>
    /// <param name="configure">
    /// Optional action to configure ImageSharp-specific options.
    /// </param>
    /// <returns>
    /// A factory function compatible with <c>SvgBuilder.WithRasterBackend</c>.
    /// </returns>
    /// <example>
    /// <code>
    /// var render = new FlexRenderBuilder()
    ///     .WithSvg(svg => svg.WithRasterBackend(
    ///         ImageSharpFlexRenderBuilderExtensions.CreateRendererFactory()))
    ///     .Build();
    /// </code>
    /// </example>
    public static Func<FlexRenderBuilder, IFlexRender> CreateRendererFactory(
        Action<ImageSharpBuilder>? configure = null)
    {
        var imageSharpBuilder = new ImageSharpBuilder();
        configure?.Invoke(imageSharpBuilder);

        return b => new ImageSharpRender(
            b.Limits,
            b.Options,
            b.ResourceLoaders,
            imageSharpBuilder,
            b.FilterRegistry);
    }
}
