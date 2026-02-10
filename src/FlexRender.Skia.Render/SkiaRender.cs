using FlexRender.Abstractions;
using FlexRender.Configuration;
using FlexRender.Loaders;
using FlexRender.Parsing.Ast;
using FlexRender.Providers;
using FlexRender.Rendering;
using FlexRender.TemplateEngine;

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
    private readonly bool _legacyDeterministicRendering;
    private readonly RenderOptions _defaultRenderOptions;
    private int _disposed;

    /// <summary>
    /// Gets or sets the BMP color mode used when rendering via the legacy
    /// <see cref="Render(Template, ObjectValue?, ImageFormat, CancellationToken)"/> methods
    /// with <see cref="ImageFormat.Bmp"/>.
    /// </summary>
    /// <remarks>
    /// This property exists for backward compatibility. Prefer using
    /// <see cref="RenderToBmp(Template, ObjectValue?, BmpOptions?, RenderOptions?, CancellationToken)"/>
    /// with <see cref="BmpOptions"/> instead.
    /// </remarks>
    [Obsolete("Use RenderToBmp() with BmpOptions instead. This property will be removed in a future version.")]
    public BmpColorMode BmpColorMode { get; set; } = BmpColorMode.Bgra32;

    /// <summary>
    /// Initializes a new instance of the <see cref="SkiaRender"/> class.
    /// </summary>
    /// <param name="limits">Resource limits for rendering operations.</param>
    /// <param name="options">Rendering configuration options.</param>
    /// <param name="resourceLoaders">Collection of resource loaders for images and other assets.</param>
    /// <param name="skiaBuilder">Skia-specific configuration including content providers.</param>
    /// <param name="filterRegistry">Optional filter registry for expression filter evaluation.</param>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="limits"/>, <paramref name="options"/>,
    /// <paramref name="resourceLoaders"/>, or <paramref name="skiaBuilder"/> is null.
    /// </exception>
    internal SkiaRender(
        ResourceLimits limits,
        FlexRenderOptions options,
        IReadOnlyList<IResourceLoader> resourceLoaders,
        SkiaBuilder skiaBuilder,
        FilterRegistry? filterRegistry = null)
    {
        ArgumentNullException.ThrowIfNull(limits);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(resourceLoaders);
        ArgumentNullException.ThrowIfNull(skiaBuilder);

        _resourceLoaders = resourceLoaders;

        var imageLoader = new ImageLoader(resourceLoaders, options);

        var qrProvider = skiaBuilder.QrProvider;
        var barcodeProvider = skiaBuilder.BarcodeProvider;
        var svgProvider = skiaBuilder.SvgProvider;

        // Inject resource loaders into providers that support it
        if (svgProvider is IResourceLoaderAware resourceLoaderAware)
        {
            resourceLoaderAware.SetResourceLoaders(resourceLoaders);
        }

#pragma warning disable CS0618 // Obsolete DeterministicRendering property - captured for legacy Render() backward compatibility
        _legacyDeterministicRendering = options.DeterministicRendering;
#pragma warning restore CS0618
        _defaultRenderOptions = options.DefaultRenderOptions;

        _renderer = new SkiaRenderer(
            limits,
            qrProvider,
            barcodeProvider,
            imageLoader,
            _legacyDeterministicRendering,
            options,
            svgProvider,
            filterRegistry);

        _renderer.BaseFontSize = options.BaseFontSize;
    }

    // ========================================================================
    // EXISTING METHODS (unchanged signatures, updated internals)
    // ========================================================================

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
    /// Delegates to format-specific methods for actual rendering.
    /// Uses <see cref="RenderOptions.Deterministic"/> when the legacy
    /// <c>FlexRenderOptions.DeterministicRendering</c> flag was set at build time.
    /// </summary>
    private async Task RenderToStream(
        Stream output,
        Template layoutTemplate,
        ObjectValue data,
        ImageFormat format,
        CancellationToken cancellationToken)
    {
        var legacyRenderOptions = _legacyDeterministicRendering
            ? RenderOptions.Deterministic
            : null;

        switch (format)
        {
            case ImageFormat.Png:
                await RenderToPng(output, layoutTemplate, data, null, legacyRenderOptions, cancellationToken)
                    .ConfigureAwait(false);
                break;

            case ImageFormat.Jpeg:
                await RenderToJpeg(output, layoutTemplate, data, null, legacyRenderOptions, cancellationToken)
                    .ConfigureAwait(false);
                break;

#pragma warning disable CS0618 // Obsolete BmpColorMode property
            case ImageFormat.Bmp:
                await RenderToBmp(output, layoutTemplate, data, new BmpOptions { ColorMode = BmpColorMode }, legacyRenderOptions, cancellationToken)
                    .ConfigureAwait(false);
                break;
#pragma warning restore CS0618

            case ImageFormat.Raw:
                await RenderToRaw(output, layoutTemplate, data, legacyRenderOptions, cancellationToken)
                    .ConfigureAwait(false);
                break;

            default:
                throw new ArgumentOutOfRangeException(nameof(format), format, "Unsupported image format.");
        }
    }

    // ========================================================================
    // FORMAT-SPECIFIC METHODS
    // ========================================================================

    // --- PNG ---

    /// <inheritdoc />
    public async Task<byte[]> RenderToPng(
        Template layoutTemplate,
        ObjectValue? data = null,
        PngOptions? options = null,
        RenderOptions? renderOptions = null,
        CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(Volatile.Read(ref _disposed) == 1, this);
        ArgumentNullException.ThrowIfNull(layoutTemplate);

        var effectiveOptions = options ?? PngOptions.Default;
        ValidatePngOptions(effectiveOptions);

        var effectiveData = data ?? new ObjectValue();
        var effectiveRenderOptions = renderOptions ?? _defaultRenderOptions;

        using var stream = new MemoryStream();
        await _renderer.RenderToPng(
            stream, layoutTemplate, effectiveData,
            effectiveOptions.CompressionLevel,
            effectiveRenderOptions,
            cancellationToken).ConfigureAwait(false);

        return stream.ToArray();
    }

    /// <inheritdoc />
    public async Task RenderToPng(
        Stream output,
        Template layoutTemplate,
        ObjectValue? data = null,
        PngOptions? options = null,
        RenderOptions? renderOptions = null,
        CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(Volatile.Read(ref _disposed) == 1, this);
        ArgumentNullException.ThrowIfNull(output);
        ArgumentNullException.ThrowIfNull(layoutTemplate);

        var effectiveOptions = options ?? PngOptions.Default;
        ValidatePngOptions(effectiveOptions);

        var effectiveData = data ?? new ObjectValue();
        var effectiveRenderOptions = renderOptions ?? _defaultRenderOptions;

        await _renderer.RenderToPng(
            output, layoutTemplate, effectiveData,
            effectiveOptions.CompressionLevel,
            effectiveRenderOptions,
            cancellationToken).ConfigureAwait(false);
    }

    // --- JPEG ---

    /// <inheritdoc />
    public async Task<byte[]> RenderToJpeg(
        Template layoutTemplate,
        ObjectValue? data = null,
        JpegOptions? options = null,
        RenderOptions? renderOptions = null,
        CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(Volatile.Read(ref _disposed) == 1, this);
        ArgumentNullException.ThrowIfNull(layoutTemplate);

        var effectiveOptions = options ?? JpegOptions.Default;
        ValidateJpegOptions(effectiveOptions);

        var effectiveData = data ?? new ObjectValue();
        var effectiveRenderOptions = renderOptions ?? _defaultRenderOptions;

        using var stream = new MemoryStream();
        await _renderer.RenderToJpeg(
            stream, layoutTemplate, effectiveData,
            effectiveOptions.Quality,
            effectiveRenderOptions,
            cancellationToken).ConfigureAwait(false);

        return stream.ToArray();
    }

    /// <inheritdoc />
    public async Task RenderToJpeg(
        Stream output,
        Template layoutTemplate,
        ObjectValue? data = null,
        JpegOptions? options = null,
        RenderOptions? renderOptions = null,
        CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(Volatile.Read(ref _disposed) == 1, this);
        ArgumentNullException.ThrowIfNull(output);
        ArgumentNullException.ThrowIfNull(layoutTemplate);

        var effectiveOptions = options ?? JpegOptions.Default;
        ValidateJpegOptions(effectiveOptions);

        var effectiveData = data ?? new ObjectValue();
        var effectiveRenderOptions = renderOptions ?? _defaultRenderOptions;

        await _renderer.RenderToJpeg(
            output, layoutTemplate, effectiveData,
            effectiveOptions.Quality,
            effectiveRenderOptions,
            cancellationToken).ConfigureAwait(false);
    }

    // --- BMP ---

    /// <inheritdoc />
    public async Task<byte[]> RenderToBmp(
        Template layoutTemplate,
        ObjectValue? data = null,
        BmpOptions? options = null,
        RenderOptions? renderOptions = null,
        CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(Volatile.Read(ref _disposed) == 1, this);
        ArgumentNullException.ThrowIfNull(layoutTemplate);

        var effectiveOptions = options ?? BmpOptions.Default;
        var effectiveData = data ?? new ObjectValue();
        var effectiveRenderOptions = renderOptions ?? _defaultRenderOptions;

        using var stream = new MemoryStream();
        await _renderer.RenderToBmp(
            stream, layoutTemplate, effectiveData,
            effectiveOptions.ColorMode,
            effectiveRenderOptions,
            cancellationToken).ConfigureAwait(false);

        return stream.ToArray();
    }

    /// <inheritdoc />
    public async Task RenderToBmp(
        Stream output,
        Template layoutTemplate,
        ObjectValue? data = null,
        BmpOptions? options = null,
        RenderOptions? renderOptions = null,
        CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(Volatile.Read(ref _disposed) == 1, this);
        ArgumentNullException.ThrowIfNull(output);
        ArgumentNullException.ThrowIfNull(layoutTemplate);

        var effectiveOptions = options ?? BmpOptions.Default;
        var effectiveData = data ?? new ObjectValue();
        var effectiveRenderOptions = renderOptions ?? _defaultRenderOptions;

        await _renderer.RenderToBmp(
            output, layoutTemplate, effectiveData,
            effectiveOptions.ColorMode,
            effectiveRenderOptions,
            cancellationToken).ConfigureAwait(false);
    }

    // --- Raw ---

    /// <inheritdoc />
    public async Task<byte[]> RenderToRaw(
        Template layoutTemplate,
        ObjectValue? data = null,
        RenderOptions? renderOptions = null,
        CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(Volatile.Read(ref _disposed) == 1, this);
        ArgumentNullException.ThrowIfNull(layoutTemplate);

        var effectiveData = data ?? new ObjectValue();
        var effectiveRenderOptions = renderOptions ?? _defaultRenderOptions;

        using var stream = new MemoryStream();
        await _renderer.RenderToRaw(
            stream, layoutTemplate, effectiveData,
            effectiveRenderOptions,
            cancellationToken).ConfigureAwait(false);

        return stream.ToArray();
    }

    /// <inheritdoc />
    public async Task RenderToRaw(
        Stream output,
        Template layoutTemplate,
        ObjectValue? data = null,
        RenderOptions? renderOptions = null,
        CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(Volatile.Read(ref _disposed) == 1, this);
        ArgumentNullException.ThrowIfNull(output);
        ArgumentNullException.ThrowIfNull(layoutTemplate);

        var effectiveData = data ?? new ObjectValue();
        var effectiveRenderOptions = renderOptions ?? _defaultRenderOptions;

        await _renderer.RenderToRaw(
            output, layoutTemplate, effectiveData,
            effectiveRenderOptions,
            cancellationToken).ConfigureAwait(false);
    }

    // ========================================================================
    // VALIDATION
    // ========================================================================

    private static void ValidatePngOptions(PngOptions options)
    {
        if (options.CompressionLevel is < 0 or > 100)
        {
            throw new ArgumentOutOfRangeException(
                nameof(options),
                options.CompressionLevel,
                "PNG compression level must be between 0 and 100.");
        }
    }

    private static void ValidateJpegOptions(JpegOptions options)
    {
        if (options.Quality is < 1 or > 100)
        {
            throw new ArgumentOutOfRangeException(
                nameof(options),
                options.Quality,
                "JPEG quality must be between 1 and 100.");
        }
    }

    // ========================================================================
    // DISPOSE
    // ========================================================================

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
