using FlexRender.Layout;
using FlexRender.Skia;

namespace FlexRender.HarfBuzz;

/// <summary>
/// Extension methods for configuring HarfBuzz text shaping in <see cref="SkiaBuilder"/>.
/// </summary>
public static class SkiaBuilderExtensions
{
    /// <summary>
    /// Adds HarfBuzz text shaping support to the Skia renderer.
    /// When enabled, text rendering uses HarfBuzz for proper RTL glyph ordering,
    /// Arabic contextual forms, ligatures, and other complex script features.
    /// </summary>
    /// <param name="builder">The Skia builder to configure.</param>
    /// <returns>The same builder instance for method chaining.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="builder"/> is null.</exception>
    /// <remarks>
    /// <para>
    /// This method registers <see cref="HarfBuzzTextShaper"/> delegates which provide text shaping
    /// via the native HarfBuzz library. The integration uses P/Invoke (AOT-safe, no reflection).
    /// </para>
    /// <para>
    /// When HarfBuzz is enabled, text measurement and rendering will produce correct results
    /// for complex scripts including Arabic, Hebrew, Thai, and other languages that require
    /// glyph shaping for proper display.
    /// </para>
    /// </remarks>
    /// <example>
    /// <code>
    /// var render = new FlexRenderBuilder()
    ///     .WithSkia(skia => skia
    ///         .WithHarfBuzz()
    ///         .WithQr()
    ///         .WithBarcode())
    ///     .Build();
    /// </code>
    /// </example>
    public static SkiaBuilder WithHarfBuzz(this SkiaBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.SetShapedTextMeasurer((text, font, direction) =>
            HarfBuzzTextShaper.MeasureShapedText(text, font, (TextDirection)direction));

        builder.SetShapedTextDrawer((canvas, text, x, y, font, paint, direction) =>
            HarfBuzzTextShaper.DrawShapedText(canvas, text, x, y, font, paint, (TextDirection)direction));

        return builder;
    }
}
