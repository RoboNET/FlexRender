using FlexRender.Providers;
using FlexRender.Skia;

namespace FlexRender.QrCode;

/// <summary>
/// Extension methods for configuring QR code support in <see cref="SkiaBuilder"/>.
/// </summary>
public static class SkiaBuilderExtensions
{
    /// <summary>
    /// Adds QR code generation support to the Skia renderer.
    /// </summary>
    /// <param name="builder">The Skia builder to configure.</param>
    /// <returns>The same builder instance for method chaining.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="builder"/> is null.</exception>
    /// <remarks>
    /// <para>
    /// This method registers the <see cref="QrProvider"/> which enables rendering of
    /// QR code elements in templates. QR codes are generated using the QRCoder library.
    /// </para>
    /// <para>
    /// After calling this method, templates can include QR code elements that will be
    /// rendered with the specified data, size, and optional error correction level.
    /// </para>
    /// </remarks>
    /// <example>
    /// <code>
    /// var render = new FlexRenderBuilder()
    ///     .WithSkia(skia => skia.WithQr())
    ///     .Build();
    /// </code>
    /// </example>
    public static SkiaBuilder WithQr(this SkiaBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        builder.SetQrProvider(new QrProvider());
        return builder;
    }
}
