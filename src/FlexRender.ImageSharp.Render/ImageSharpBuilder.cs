using FlexRender.Parsing.Ast;
using FlexRender.Providers;

namespace FlexRender.ImageSharp;

/// <summary>
/// Builder for configuring ImageSharp-specific rendering options.
/// </summary>
/// <remarks>
/// <para>
/// This builder follows the same pattern as <c>SkiaBuilder</c> for consistency.
/// Future versions may add ImageSharp-specific configuration such as content
/// providers for QR codes, barcodes, and SVG elements.
/// </para>
/// <para>
/// Instances are created internally by the <c>WithImageSharp()</c> extension method.
/// </para>
/// </remarks>
public sealed class ImageSharpBuilder
{
    internal IImageSharpContentProvider<QrElement>? QrProvider { get; private set; }
    internal IImageSharpContentProvider<BarcodeElement>? BarcodeProvider { get; private set; }

    /// <summary>
    /// Sets the ImageSharp QR code provider.
    /// </summary>
    /// <param name="provider">The provider to set.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="provider"/> is null.</exception>
    /// <exception cref="InvalidOperationException">Thrown when a provider is already configured.</exception>
    internal void SetQrProvider(IImageSharpContentProvider<QrElement> provider)
    {
        ArgumentNullException.ThrowIfNull(provider);
        if (QrProvider is not null)
        {
            throw new InvalidOperationException("QR provider is already configured. WithQr() can only be called once.");
        }
        QrProvider = provider;
    }

    /// <summary>
    /// Sets the ImageSharp barcode provider.
    /// </summary>
    /// <param name="provider">The provider to set.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="provider"/> is null.</exception>
    /// <exception cref="InvalidOperationException">Thrown when a provider is already configured.</exception>
    internal void SetBarcodeProvider(IImageSharpContentProvider<BarcodeElement> provider)
    {
        ArgumentNullException.ThrowIfNull(provider);
        if (BarcodeProvider is not null)
        {
            throw new InvalidOperationException("Barcode provider is already configured. WithBarcode() can only be called once.");
        }
        BarcodeProvider = provider;
    }
}
