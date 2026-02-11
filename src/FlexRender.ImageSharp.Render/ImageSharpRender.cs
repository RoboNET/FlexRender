using FlexRender.Abstractions;
using FlexRender.Configuration;
using FlexRender.ImageSharp.Rendering;
using FlexRender.Parsing.Ast;
using FlexRender.TemplateEngine;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Bmp;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.Formats.Png;
using SixLabors.ImageSharp.PixelFormats;

namespace FlexRender.ImageSharp;

/// <summary>
/// ImageSharp-based implementation of <see cref="IFlexRender"/>.
/// Provides a pure .NET rendering backend with zero native dependencies.
/// </summary>
/// <remarks>
/// <para>
/// This class provides the ImageSharp rendering backend for FlexRender. It uses
/// SixLabors.ImageSharp for cross-platform 2D graphics rendering without any
/// native library dependencies.
/// </para>
/// <para>
/// Instances are typically created through <see cref="FlexRenderBuilder"/> using the
/// <c>WithImageSharp()</c> extension method:
/// </para>
/// <code>
/// var render = new FlexRenderBuilder()
///     .WithImageSharp()
///     .Build();
/// </code>
/// </remarks>
public sealed class ImageSharpRender : IFlexRender
{
    private readonly ImageSharpRenderingEngine _engine;
    private readonly ImageSharpFontManager _fontManager;
    private readonly IReadOnlyList<IResourceLoader> _resourceLoaders;
    private readonly FilterRegistry? _filterRegistry;
    private readonly ResourceLimits _limits;
    private readonly FlexRenderOptions _options;
    private readonly RenderOptions _defaultRenderOptions;
    private int _disposed;

    /// <summary>
    /// Initializes a new instance of the <see cref="ImageSharpRender"/> class.
    /// </summary>
    /// <param name="limits">Resource limits for rendering operations.</param>
    /// <param name="options">Rendering configuration options.</param>
    /// <param name="resourceLoaders">Collection of resource loaders for assets.</param>
    /// <param name="builder">ImageSharp-specific configuration.</param>
    /// <param name="filterRegistry">Optional filter registry for expression filter evaluation.</param>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="limits"/>, <paramref name="options"/>,
    /// <paramref name="resourceLoaders"/>, or <paramref name="builder"/> is null.
    /// </exception>
    internal ImageSharpRender(
        ResourceLimits limits,
        FlexRenderOptions options,
        IReadOnlyList<IResourceLoader> resourceLoaders,
        ImageSharpBuilder builder,
        FilterRegistry? filterRegistry = null)
    {
        ArgumentNullException.ThrowIfNull(limits);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(resourceLoaders);
        ArgumentNullException.ThrowIfNull(builder);

        _limits = limits;
        _options = options;
        _defaultRenderOptions = options.DefaultRenderOptions;
        _resourceLoaders = resourceLoaders;
        _filterRegistry = filterRegistry;

        _fontManager = new ImageSharpFontManager();
        var textRenderer = new ImageSharpTextRenderer(_fontManager);

        _engine = new ImageSharpRenderingEngine(
            textRenderer,
            _fontManager,
            limits,
            options.BaseFontSize,
            builder.QrProvider,
            builder.BarcodeProvider,
            options);
    }

    // ========================================================================
    // EXISTING METHODS (unchanged signatures from IFlexRender)
    // ========================================================================

    /// <inheritdoc />
    /// <exception cref="ObjectDisposedException">Thrown when the renderer has been disposed.</exception>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="layoutTemplate"/> is null.</exception>
    public async Task<byte[]> Render(
        Template layoutTemplate,
        ObjectValue? data = null,
        ImageFormat format = ImageFormat.Png,
        CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        ArgumentNullException.ThrowIfNull(layoutTemplate);

        using var stream = new MemoryStream();
        await Render(stream, layoutTemplate, data, format, cancellationToken).ConfigureAwait(false);
        return stream.ToArray();
    }

    /// <inheritdoc />
    /// <exception cref="ObjectDisposedException">Thrown when the renderer has been disposed.</exception>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="output"/> or <paramref name="layoutTemplate"/> is null.
    /// </exception>
    public async Task Render(
        Stream output,
        Template layoutTemplate,
        ObjectValue? data = null,
        ImageFormat format = ImageFormat.Png,
        CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        ArgumentNullException.ThrowIfNull(output);
        ArgumentNullException.ThrowIfNull(layoutTemplate);

        var effectiveData = data ?? new ObjectValue();

        switch (format)
        {
            case ImageFormat.Png:
                await RenderToPng(output, layoutTemplate, effectiveData, null, null, cancellationToken).ConfigureAwait(false);
                break;
            case ImageFormat.Jpeg:
                await RenderToJpeg(output, layoutTemplate, effectiveData, null, null, cancellationToken).ConfigureAwait(false);
                break;
            case ImageFormat.Bmp:
                await RenderToBmp(output, layoutTemplate, effectiveData, null, null, cancellationToken).ConfigureAwait(false);
                break;
            case ImageFormat.Raw:
                await RenderToRaw(output, layoutTemplate, effectiveData, null, cancellationToken).ConfigureAwait(false);
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
        ThrowIfDisposed();
        ArgumentNullException.ThrowIfNull(layoutTemplate);

        using var stream = new MemoryStream();
        await RenderToPng(stream, layoutTemplate, data, options, renderOptions, cancellationToken).ConfigureAwait(false);
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
        ThrowIfDisposed();
        ArgumentNullException.ThrowIfNull(output);
        ArgumentNullException.ThrowIfNull(layoutTemplate);
        cancellationToken.ThrowIfCancellationRequested();

        var effectiveData = data ?? new ObjectValue();
        var (processedTemplate, imageCache) = await PreloadImages(layoutTemplate, effectiveData, cancellationToken)
            .ConfigureAwait(false);

        try
        {
            using var image = _engine.RenderToImage(
                layoutTemplate, effectiveData, _filterRegistry, imageCache, processedTemplate);

            var encoder = new PngEncoder();
            await image.SaveAsync(output, encoder, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            DisposeImageCache(imageCache);
        }
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
        ThrowIfDisposed();
        ArgumentNullException.ThrowIfNull(layoutTemplate);

        using var stream = new MemoryStream();
        await RenderToJpeg(stream, layoutTemplate, data, options, renderOptions, cancellationToken).ConfigureAwait(false);
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
        ThrowIfDisposed();
        ArgumentNullException.ThrowIfNull(output);
        ArgumentNullException.ThrowIfNull(layoutTemplate);
        cancellationToken.ThrowIfCancellationRequested();

        var effectiveData = data ?? new ObjectValue();
        var effectiveOptions = options ?? JpegOptions.Default;
        var (processedTemplate, imageCache) = await PreloadImages(layoutTemplate, effectiveData, cancellationToken)
            .ConfigureAwait(false);

        try
        {
            using var image = _engine.RenderToImage(
                layoutTemplate, effectiveData, _filterRegistry, imageCache, processedTemplate);

            var encoder = new JpegEncoder { Quality = effectiveOptions.Quality };
            await image.SaveAsync(output, encoder, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            DisposeImageCache(imageCache);
        }
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
        ThrowIfDisposed();
        ArgumentNullException.ThrowIfNull(layoutTemplate);

        using var stream = new MemoryStream();
        await RenderToBmp(stream, layoutTemplate, data, options, renderOptions, cancellationToken).ConfigureAwait(false);
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
        ThrowIfDisposed();
        ArgumentNullException.ThrowIfNull(output);
        ArgumentNullException.ThrowIfNull(layoutTemplate);
        cancellationToken.ThrowIfCancellationRequested();

        var effectiveData = data ?? new ObjectValue();
        var (processedTemplate, imageCache) = await PreloadImages(layoutTemplate, effectiveData, cancellationToken)
            .ConfigureAwait(false);

        try
        {
            using var image = _engine.RenderToImage(
                layoutTemplate, effectiveData, _filterRegistry, imageCache, processedTemplate);

            var encoder = new BmpEncoder { BitsPerPixel = BmpBitsPerPixel.Pixel32 };
            await image.SaveAsync(output, encoder, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            DisposeImageCache(imageCache);
        }
    }

    // --- Raw ---

    /// <inheritdoc />
    public async Task<byte[]> RenderToRaw(
        Template layoutTemplate,
        ObjectValue? data = null,
        RenderOptions? renderOptions = null,
        CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        ArgumentNullException.ThrowIfNull(layoutTemplate);

        using var stream = new MemoryStream();
        await RenderToRaw(stream, layoutTemplate, data, renderOptions, cancellationToken).ConfigureAwait(false);
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
        ThrowIfDisposed();
        ArgumentNullException.ThrowIfNull(output);
        ArgumentNullException.ThrowIfNull(layoutTemplate);
        cancellationToken.ThrowIfCancellationRequested();

        var effectiveData = data ?? new ObjectValue();
        var (processedTemplate, imageCache) = await PreloadImages(layoutTemplate, effectiveData, cancellationToken)
            .ConfigureAwait(false);

        try
        {
            using var image = _engine.RenderToImage(
                layoutTemplate, effectiveData, _filterRegistry, imageCache, processedTemplate);

            // Write raw RGBA pixel data
            var pixelCount = checked(image.Width * image.Height);
            var bufferSize = checked(pixelCount * 4);
            var buffer = new byte[bufferSize];
            image.CopyPixelDataTo(buffer);
            await output.WriteAsync(buffer, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            DisposeImageCache(imageCache);
        }
    }

    // ========================================================================
    // IMAGE PRELOADING
    // ========================================================================

    /// <summary>
    /// Pre-loads all images referenced in the template using the configured resource loaders.
    /// Also returns the processed template so callers can skip redundant expand+preprocess steps.
    /// </summary>
    /// <param name="template">The template to scan for image references.</param>
    /// <param name="data">The data context for expression evaluation.</param>
    /// <param name="cancellationToken">Cancellation token for async operations.</param>
    /// <returns>
    /// A tuple of the processed template and an image cache. The processed template is non-null
    /// when resource loaders are configured (since expand+preprocess was already performed).
    /// The image cache maps URIs to pre-loaded images, or is <c>null</c> when no images were found.
    /// Caller is responsible for disposing images via <see cref="DisposeImageCache"/>.
    /// </returns>
    private async Task<(Template? processedTemplate, Dictionary<string, Image<Rgba32>>? imageCache)> PreloadImages(
        Template template,
        ObjectValue data,
        CancellationToken cancellationToken)
    {
        if (_resourceLoaders.Count == 0)
            return (null, null);

        // Expand, resolve, and materialize template to resolve expressions in image src attributes
        var expander = _filterRegistry is not null
            ? new TemplateExpander(_limits, _filterRegistry)
            : new TemplateExpander(_limits);
        var templateProcessor = _filterRegistry is not null
            ? new TemplateProcessor(_limits, _filterRegistry)
            : new TemplateProcessor(_limits);

        var pipeline = new TemplatePipeline(expander, templateProcessor);
        var processedTemplate = pipeline.Process(template, data);

        var uris = ImageSharpRenderingEngine.CollectImageUris(processedTemplate);
        if (uris.Count == 0)
            return (processedTemplate, null);

        var cache = new Dictionary<string, Image<Rgba32>>(
            uris.Count, StringComparer.Ordinal);

        foreach (var uri in uris)
        {
            cancellationToken.ThrowIfCancellationRequested();

            foreach (var loader in _resourceLoaders
                .OrderBy(l => l.Priority)
                .Where(l => l.CanHandle(uri)))
            {
                var stream = await loader.Load(uri, cancellationToken)
                    .ConfigureAwait(false);

                if (stream is not null)
                {
                    using (stream)
                    {
                        var image = Image.Load<Rgba32>(stream);
                        cache[uri] = image;
                    }

                    break;
                }
            }
        }

        return (processedTemplate, cache.Count > 0 ? cache : null);
    }

    /// <summary>
    /// Disposes all images in the pre-loaded image cache.
    /// </summary>
    /// <param name="imageCache">The cache to dispose, or <c>null</c>.</param>
    private static void DisposeImageCache(
        Dictionary<string, Image<Rgba32>>? imageCache)
    {
        if (imageCache is null)
            return;

        foreach (var image in imageCache.Values)
        {
            image.Dispose();
        }
    }

    // ========================================================================
    // DISPOSE
    // ========================================================================

    /// <inheritdoc />
    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) == 1)
            return;

        _fontManager.Dispose();

        foreach (var loader in _resourceLoaders)
        {
            if (loader is IDisposable disposable)
                disposable.Dispose();
        }
    }

    /// <summary>
    /// Throws <see cref="ObjectDisposedException"/> if this instance has been disposed.
    /// </summary>
    /// <exception cref="ObjectDisposedException">Thrown when the renderer has been disposed.</exception>
    private void ThrowIfDisposed()
    {
        ObjectDisposedException.ThrowIf(Volatile.Read(ref _disposed) == 1, this);
    }
}
