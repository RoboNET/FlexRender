using FlexRender.Abstractions;
using FlexRender.Configuration;
using FlexRender.Loaders;
using FlexRender.Parsing.Ast;
using FlexRender.Rendering;
using SkiaSharp;

namespace FlexRender.Skia;

/// <summary>
/// SkiaSharp-based implementation of <see cref="IFlexRender"/>.
/// </summary>
/// <remarks>
/// <para>
/// This class provides the Skia rendering backend for FlexRender. It uses SkiaSharp
/// for cross-platform 2D graphics rendering and supports PNG, JPEG, BMP, and Raw output formats.
/// </para>
/// <para>
/// Instances are typically created through <see cref="FlexRenderBuilder"/> using the
/// <see cref="FlexRenderBuilderExtensions.WithSkia"/> extension method:
/// </para>
/// <code>
/// var render = new FlexRenderBuilder()
///     .WithSkia()
///     .Build();
/// </code>
/// <para>
/// Optional content providers for QR codes and barcodes can be configured through
/// the <see cref="SkiaBuilder"/> passed to <c>WithSkia()</c>. If a template contains
/// a QR code or barcode element and the corresponding provider is not configured,
/// an <see cref="InvalidOperationException"/> will be thrown.
/// </para>
/// </remarks>
public sealed class SkiaRender : IFlexRender
{
    private readonly SkiaRenderer _renderer;
    private readonly IReadOnlyList<IResourceLoader> _resourceLoaders;
    private int _disposed;

    /// <summary>
    /// Gets or sets the BMP color mode used when rendering to BMP format.
    /// Defaults to <see cref="BmpColorMode.Bgra32"/>.
    /// </summary>
    public BmpColorMode BmpColorMode { get; set; } = BmpColorMode.Bgra32;

    /// <summary>
    /// Initializes a new instance of the <see cref="SkiaRender"/> class.
    /// </summary>
    /// <param name="limits">Resource limits for rendering operations.</param>
    /// <param name="options">Rendering configuration options.</param>
    /// <param name="resourceLoaders">Collection of resource loaders for images and other assets.</param>
    /// <param name="skiaBuilder">Skia-specific configuration including content providers.</param>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="limits"/>, <paramref name="options"/>,
    /// <paramref name="resourceLoaders"/>, or <paramref name="skiaBuilder"/> is null.
    /// </exception>
    internal SkiaRender(
        ResourceLimits limits,
        FlexRenderOptions options,
        IReadOnlyList<IResourceLoader> resourceLoaders,
        SkiaBuilder skiaBuilder)
    {
        ArgumentNullException.ThrowIfNull(limits);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(resourceLoaders);
        ArgumentNullException.ThrowIfNull(skiaBuilder);

        _resourceLoaders = resourceLoaders;

        var imageLoader = new ImageLoader(resourceLoaders, options);

        var qrProvider = skiaBuilder.QrProvider;
        var barcodeProvider = skiaBuilder.BarcodeProvider;

        _renderer = new SkiaRenderer(
            limits,
            qrProvider,
            barcodeProvider,
            imageLoader,
            options.DeterministicRendering,
            options);

        _renderer.BaseFontSize = options.BaseFontSize;
    }

    /// <inheritdoc />
    /// <exception cref="ObjectDisposedException">Thrown when the renderer has been disposed.</exception>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="layoutTemplate"/> is null.</exception>
    /// <exception cref="InvalidOperationException">
    /// Thrown when the template contains a QR code element but no QR provider is configured,
    /// or when the template contains a barcode element but no barcode provider is configured.
    /// </exception>
    public async Task<byte[]> Render(
        Template layoutTemplate,
        ObjectValue? data = null,
        ImageFormat format = ImageFormat.Png,
        CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(Volatile.Read(ref _disposed) == 1, this);
        ArgumentNullException.ThrowIfNull(layoutTemplate);

        var effectiveData = data ?? new ObjectValue();

        using var stream = new MemoryStream();
        await RenderToStream(stream, layoutTemplate, effectiveData, format, cancellationToken)
            .ConfigureAwait(false);

        return stream.ToArray();
    }

    /// <inheritdoc />
    /// <exception cref="ObjectDisposedException">Thrown when the renderer has been disposed.</exception>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="output"/> or <paramref name="layoutTemplate"/> is null.
    /// </exception>
    /// <exception cref="InvalidOperationException">
    /// Thrown when the template contains a QR code element but no QR provider is configured,
    /// or when the template contains a barcode element but no barcode provider is configured.
    /// </exception>
    public async Task Render(
        Stream output,
        Template layoutTemplate,
        ObjectValue? data = null,
        ImageFormat format = ImageFormat.Png,
        CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(Volatile.Read(ref _disposed) == 1, this);
        ArgumentNullException.ThrowIfNull(output);
        ArgumentNullException.ThrowIfNull(layoutTemplate);

        var effectiveData = data ?? new ObjectValue();

        await RenderToStream(output, layoutTemplate, effectiveData, format, cancellationToken)
            .ConfigureAwait(false);
    }

    /// <summary>
    /// Renders the template to a stream in the specified format.
    /// </summary>
    private async Task RenderToStream(
        Stream output,
        Template layoutTemplate,
        ObjectValue data,
        ImageFormat format,
        CancellationToken cancellationToken)
    {
        switch (format)
        {
            case ImageFormat.Png:
                await _renderer.RenderToPng(output, layoutTemplate, data, cancellationToken)
                    .ConfigureAwait(false);
                break;

            case ImageFormat.Jpeg:
                await _renderer.RenderToJpeg(output, layoutTemplate, data, cancellationToken: cancellationToken)
                    .ConfigureAwait(false);
                break;

            case ImageFormat.Bmp:
                await _renderer.RenderToBmp(output, layoutTemplate, data, BmpColorMode, cancellationToken)
                    .ConfigureAwait(false);
                break;

            case ImageFormat.Raw:
                await _renderer.RenderToRaw(output, layoutTemplate, data, cancellationToken)
                    .ConfigureAwait(false);
                break;

            default:
                throw new ArgumentOutOfRangeException(nameof(format), format, "Unsupported image format.");
        }
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) == 1)
        {
            return;
        }

        _renderer.Dispose();

        foreach (var loader in _resourceLoaders)
        {
            if (loader is IDisposable disposable)
            {
                disposable.Dispose();
            }
        }
    }
}
