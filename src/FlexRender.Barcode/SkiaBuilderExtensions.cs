using FlexRender.Providers;
using FlexRender.Skia;

namespace FlexRender.Barcode;

/// <summary>
/// Extension methods for configuring barcode support in <see cref="SkiaBuilder"/>.
/// </summary>
public static class SkiaBuilderExtensions
{
    /// <summary>
    /// Adds barcode generation support to the Skia renderer.
    /// </summary>
    /// <param name="builder">The Skia builder to configure.</param>
    /// <returns>The same builder instance for method chaining.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="builder"/> is null.</exception>
    /// <remarks>
    /// <para>
    /// This method registers the <see cref="BarcodeProvider"/> which enables rendering of
    /// barcode elements in templates. Barcodes are generated using pure SkiaSharp drawing
    /// with no external dependencies.
    /// </para>
    /// <para>
    /// After calling this method, templates can include barcode elements that will be
    /// rendered with the specified data, format, and dimensions.
    /// </para>
    /// <para>
    /// Currently supported barcode formats:
    /// <list type="bullet">
    /// <item><description>Code 128 (character set B)</description></item>
    /// </list>
    /// </para>
    /// </remarks>
    /// <example>
    /// <code>
    /// var render = new FlexRenderBuilder()
    ///     .WithSkia(skia => skia.WithBarcode())
    ///     .Build();
    /// </code>
    /// </example>
    public static SkiaBuilder WithBarcode(this SkiaBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        builder.SetBarcodeProvider(new BarcodeProvider());
        return builder;
    }
}
