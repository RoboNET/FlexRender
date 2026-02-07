using FlexRender.Barcode.ImageSharp.Providers;
using FlexRender.ImageSharp;

namespace FlexRender.Barcode.ImageSharp;

/// <summary>
/// Extension methods for configuring barcode support in <see cref="ImageSharpBuilder"/>.
/// </summary>
public static class ImageSharpBuilderExtensions
{
    /// <summary>
    /// Adds barcode generation support to the ImageSharp renderer.
    /// </summary>
    /// <param name="builder">The ImageSharp builder to configure.</param>
    /// <returns>The same builder instance for method chaining.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="builder"/> is null.</exception>
    public static ImageSharpBuilder WithBarcode(this ImageSharpBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        builder.SetBarcodeProvider(new BarcodeImageSharpProvider());
        return builder;
    }
}
