using FlexRender.Configuration;
using FlexRender.Skia;

namespace FlexRender;

/// <summary>
/// Extension methods for configuring Skia rendering with <see cref="FlexRenderBuilder"/>.
/// </summary>
public static class FlexRenderBuilderExtensions
{
    /// <summary>
    /// Configures the builder to use the Skia rendering backend.
    /// </summary>
    /// <param name="builder">The builder to configure.</param>
    /// <param name="configure">
    /// Optional action to configure Skia-specific options such as QR code and barcode providers.
    /// </param>
    /// <returns>The builder instance for method chaining.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="builder"/> is null.</exception>
    /// <remarks>
    /// <para>
    /// This method registers the <see cref="SkiaRender"/> as the rendering implementation.
    /// The Skia renderer uses SkiaSharp for cross-platform 2D graphics rendering.
    /// </para>
    /// <para>
    /// Optional features like QR codes and barcodes must be explicitly enabled by calling
    /// the corresponding extension methods within the configure action:
    /// </para>
    /// <list type="bullet">
    /// <item><description><c>WithQr()</c> - from FlexRender.QrCode package</description></item>
    /// <item><description><c>WithBarcode()</c> - from FlexRender.Barcode package</description></item>
    /// </list>
    /// </remarks>
    /// <example>
    /// Minimal configuration:
    /// <code>
    /// var render = new FlexRenderBuilder()
    ///     .WithSkia()
    ///     .Build();
    /// </code>
    /// Full configuration with providers:
    /// <code>
    /// var render = new FlexRenderBuilder()
    ///     .WithBasePath("./templates")
    ///     .WithLimits(limits => limits.MaxRenderDepth = 200)
    ///     .WithSkia(skia => skia
    ///         .WithQr()
    ///         .WithBarcode())
    ///     .Build();
    /// </code>
    /// </example>
    public static FlexRenderBuilder WithSkia(
        this FlexRenderBuilder builder,
        Action<SkiaBuilder>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(builder);

        var skiaBuilder = new SkiaBuilder();
        configure?.Invoke(skiaBuilder);

        builder.SetRendererFactory(b => new SkiaRender(
            b.Limits,
            b.Options,
            b.ResourceLoaders,
            skiaBuilder));

        return builder;
    }
}
