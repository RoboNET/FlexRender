using FlexRender.Parsing.Ast;
using FlexRender.Providers;

namespace FlexRender.Skia;

/// <summary>
/// Builder for configuring Skia-specific rendering options and providers.
/// </summary>
/// <remarks>
/// <para>
/// This builder is used to configure optional content providers for QR codes and barcodes.
/// Content providers are registered through extension methods from their respective packages
/// (e.g., <c>WithQr()</c> from FlexRender.QrCode, <c>WithBarcode()</c> from FlexRender.Barcode).
/// </para>
/// <para>
/// If a QR code or barcode element is encountered in a template and the corresponding
/// provider is not configured, an <see cref="InvalidOperationException"/> will be thrown
/// at render time.
/// </para>
/// </remarks>
/// <example>
/// <code>
/// var render = new FlexRenderBuilder()
///     .WithSkia(skia => skia
///         .WithQr()
///         .WithBarcode())
///     .Build();
/// </code>
/// </example>
public sealed class SkiaBuilder
{
    /// <summary>
    /// Gets the configured QR code content provider, if any.
    /// </summary>
    /// <remarks>
    /// This provider is set by calling the <c>WithQr()</c> extension method
    /// from the FlexRender.QrCode package.
    /// </remarks>
    internal IContentProvider<QrElement>? QrProvider { get; private set; }

    /// <summary>
    /// Gets the configured barcode content provider, if any.
    /// </summary>
    /// <remarks>
    /// This provider is set by calling the <c>WithBarcode()</c> extension method
    /// from the FlexRender.Barcode package.
    /// </remarks>
    internal IContentProvider<BarcodeElement>? BarcodeProvider { get; private set; }

    /// <summary>
    /// Sets the QR code content provider.
    /// </summary>
    /// <param name="provider">The QR code provider to use.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="provider"/> is null.</exception>
    /// <remarks>
    /// This method is intended to be called by extension methods from the FlexRender.QrCode package.
    /// </remarks>
    /// <exception cref="InvalidOperationException">Thrown when a QR provider is already configured.</exception>
    internal void SetQrProvider(IContentProvider<QrElement> provider)
    {
        ArgumentNullException.ThrowIfNull(provider);
        if (QrProvider != null)
        {
            throw new InvalidOperationException("QR provider is already configured. WithQr() can only be called once.");
        }
        QrProvider = provider;
    }

    /// <summary>
    /// Sets the barcode content provider.
    /// </summary>
    /// <param name="provider">The barcode provider to use.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="provider"/> is null.</exception>
    /// <exception cref="InvalidOperationException">Thrown when a barcode provider is already configured.</exception>
    /// <remarks>
    /// This method is intended to be called by extension methods from the FlexRender.Barcode package.
    /// </remarks>
    internal void SetBarcodeProvider(IContentProvider<BarcodeElement> provider)
    {
        ArgumentNullException.ThrowIfNull(provider);
        if (BarcodeProvider != null)
        {
            throw new InvalidOperationException("Barcode provider is already configured. WithBarcode() can only be called once.");
        }
        BarcodeProvider = provider;
    }
}
