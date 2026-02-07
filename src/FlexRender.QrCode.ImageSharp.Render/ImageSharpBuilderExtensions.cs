using FlexRender.ImageSharp;
using FlexRender.QrCode.ImageSharp.Providers;

namespace FlexRender.QrCode.ImageSharp;

/// <summary>
/// Extension methods for configuring QR code support in <see cref="ImageSharpBuilder"/>.
/// </summary>
public static class ImageSharpBuilderExtensions
{
    /// <summary>
    /// Adds QR code generation support to the ImageSharp renderer.
    /// </summary>
    /// <param name="builder">The ImageSharp builder to configure.</param>
    /// <returns>The same builder instance for method chaining.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="builder"/> is null.</exception>
    public static ImageSharpBuilder WithQr(this ImageSharpBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        builder.SetQrProvider(new QrImageSharpProvider());
        return builder;
    }
}
