using FlexRender.Abstractions;
using FlexRender.Configuration;
using FlexRender.Layout;
using FlexRender.Parsing.Ast;
using FlexRender.Providers;
using FlexRender.TemplateEngine;
using SkiaSharp;

namespace FlexRender.Rendering;

/// <summary>
/// Renders templates to SkiaSharp canvases and bitmaps.
/// Delegates rendering to <see cref="RenderingEngine"/> and template
/// preprocessing to <see cref="TemplatePreprocessor"/>.
/// </summary>
internal sealed class SkiaRenderer : IDisposable, IAsyncDisposable
{
    /// <summary>
    /// Default maximum render depth.
    /// </summary>
    /// <remarks>
    /// This constant is preserved for backward compatibility. The actual limit
    /// at runtime comes from <see cref="ResourceLimits.MaxRenderDepth"/>.
    /// </remarks>
    private const int DefaultMaxRenderDepth = 100;

    private readonly FontManager _fontManager;
    private readonly TextRenderer _textRenderer;
    private readonly LayoutEngine _layoutEngine;
    private readonly IImageLoader? _imageLoader;
    private readonly TemplatePipeline _pipeline;
    private readonly TemplatePreprocessor _preprocessor;
    private readonly RenderingEngine _renderingEngine;
    private readonly ResourceLimits _limits;
    private readonly RenderOptions _defaultRenderOptions;
    private int _disposed;

    /// <summary>
    /// Default base font size in pixels.
    /// </summary>
    public float BaseFontSize { get; set; } = 12f;

    /// <summary>
    /// Creates a new renderer instance with default resource limits.
    /// </summary>
    public SkiaRenderer() : this(new ResourceLimits())
    {
    }

    /// <summary>
    /// Creates a new renderer instance with custom resource limits.
    /// </summary>
    /// <param name="limits">The resource limits to apply.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="limits"/> is null.</exception>
    public SkiaRenderer(ResourceLimits limits) : this(limits, null, null)
    {
    }

    /// <summary>
    /// Creates a new renderer instance with custom resource limits and optional content providers.
    /// </summary>
    /// <param name="limits">The resource limits to apply.</param>
    /// <param name="qrProvider">Optional QR code content provider.</param>
    /// <param name="barcodeProvider">Optional barcode content provider.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="limits"/> is null.</exception>
    public SkiaRenderer(
        ResourceLimits limits,
        IContentProvider<QrElement>? qrProvider,
        IContentProvider<BarcodeElement>? barcodeProvider)
        : this(limits, qrProvider, barcodeProvider, null)
    {
    }

    /// <summary>
    /// Creates a new renderer instance with custom resource limits, optional content providers,
    /// and optional image loader for async image pre-loading (HTTP, embedded, etc.).
    /// </summary>
    /// <param name="limits">The resource limits to apply.</param>
    /// <param name="qrProvider">Optional QR code content provider.</param>
    /// <param name="barcodeProvider">Optional barcode content provider.</param>
    /// <param name="imageLoader">Optional image loader for async pre-loading of images from various sources.</param>
    /// <param name="deterministicRendering">
    /// Legacy parameter. When <c>true</c>, equivalent to using <see cref="RenderOptions.Deterministic"/>.
    /// Prefer passing <see cref="RenderOptions"/> per-call instead.
    /// </param>
    /// <param name="options">Optional configuration options for path resolution and other settings.</param>
    /// <param name="svgProvider">Optional SVG content provider for rendering SVG elements.</param>
    /// <param name="filterRegistry">Optional filter registry for expression filter evaluation.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="limits"/> is null.</exception>
    public SkiaRenderer(
        ResourceLimits limits,
        IContentProvider<QrElement>? qrProvider,
        IContentProvider<BarcodeElement>? barcodeProvider,
        IImageLoader? imageLoader,
        bool deterministicRendering = false,
        FlexRenderOptions? options = null,
        IContentProvider<SvgElement>? svgProvider = null,
        FilterRegistry? filterRegistry = null)
    {
        ArgumentNullException.ThrowIfNull(limits);
        _limits = limits;
        _imageLoader = imageLoader;
        var templateProcessor = filterRegistry is not null
            ? new TemplateProcessor(limits, filterRegistry)
            : new TemplateProcessor(limits);
        var expander = filterRegistry is not null
            ? new TemplateExpander(limits, filterRegistry)
            : new TemplateExpander(limits);
        _fontManager = new FontManager();
        _defaultRenderOptions = deterministicRendering ? RenderOptions.Deterministic : RenderOptions.Default;
        _textRenderer = new TextRenderer(_fontManager);
        _layoutEngine = new LayoutEngine(_limits);
        var textShaper = new SkiaTextShaper(_fontManager, _defaultRenderOptions);
        _layoutEngine.TextShaper = textShaper;
        _layoutEngine.BaseFontSize = BaseFontSize;

        _pipeline = new TemplatePipeline(expander, templateProcessor);
        _preprocessor = new TemplatePreprocessor(_fontManager, options);
        _renderingEngine = new RenderingEngine(
            _textRenderer,
            qrProvider,
            barcodeProvider,
            svgProvider,
            imageLoader,
            _pipeline,
            _preprocessor,
            _layoutEngine,
            _limits,
            BaseFontSize,
            filterRegistry,
            _fontManager,
            options);
    }

    /// <summary>
    /// Gets the font manager for font registration.
    /// </summary>
    public FontManager FontManager => _fontManager;

    /// <summary>
    /// Computes the layout tree for a template with data.
    /// Uses the same layout engine configuration as rendering (including text measurement).
    /// </summary>
    /// <param name="template">The template to lay out.</param>
    /// <param name="data">The data for variable substitution.</param>
    /// <returns>The root layout node with computed positions and sizes.</returns>
    public LayoutNode ComputeLayout(Template template, ObjectValue data)
    {
        ObjectDisposedException.ThrowIf(Volatile.Read(ref _disposed) == 1, this);
        ArgumentNullException.ThrowIfNull(template);
        ArgumentNullException.ThrowIfNull(data);

        var processedTemplate = _pipeline.Process(template, data);
        _preprocessor.RegisterFonts(processedTemplate);
        return _layoutEngine.ComputeLayout(processedTemplate);
    }

    /// <summary>
    /// Measures the size required to render the template.
    /// Takes into account canvas rotation which may swap width and height.
    /// </summary>
    /// <param name="template">The template to measure.</param>
    /// <param name="data">The data for variable substitution.</param>
    /// <returns>The required size after rotation is applied.</returns>
    public SKSize Measure(Template template, ObjectValue data)
    {
        ObjectDisposedException.ThrowIf(Volatile.Read(ref _disposed) == 1, this);
        ArgumentNullException.ThrowIfNull(template);
        ArgumentNullException.ThrowIfNull(data);

        var processedTemplate = _pipeline.Process(template, data);
        _preprocessor.RegisterFonts(processedTemplate);

        // Use LayoutEngine to compute accurate sizes
        var rootNode = _layoutEngine.ComputeLayout(processedTemplate);

        var width = rootNode.Width;
        var height = rootNode.Height;

        // Check if canvas rotation swaps dimensions
        var rotationDegrees = RotationHelper.ParseRotation(processedTemplate.Canvas.Rotate.Value);
        if (RotationHelper.SwapsDimensions(rotationDegrees))
        {
            return new SKSize(height, width);
        }

        return new SKSize(width, height);
    }

    /// <summary>
    /// Renders the template to a canvas.
    /// </summary>
    /// <param name="canvas">The canvas to render to.</param>
    /// <param name="template">The template to render.</param>
    /// <param name="data">The data for variable substitution.</param>
    /// <param name="offset">Optional offset for rendering position.</param>
    public void Render(SKCanvas canvas, Template template, ObjectValue data, SKPoint offset = default)
    {
        ObjectDisposedException.ThrowIf(Volatile.Read(ref _disposed) == 1, this);
        ArgumentNullException.ThrowIfNull(canvas);
        ArgumentNullException.ThrowIfNull(template);
        ArgumentNullException.ThrowIfNull(data);

        _renderingEngine.RenderToCanvas(canvas, template, data, offset, imageCache: null, _defaultRenderOptions);
    }

    /// <summary>
    /// Renders the template to a bitmap.
    /// Applies canvas rotation after rendering if specified in template settings.
    /// </summary>
    /// <param name="bitmap">The bitmap to render to.</param>
    /// <param name="template">The template to render.</param>
    /// <param name="data">The data for variable substitution.</param>
    /// <param name="offset">Optional offset for rendering position.</param>
    public void Render(SKBitmap bitmap, Template template, ObjectValue data, SKPoint offset = default)
    {
        ObjectDisposedException.ThrowIf(Volatile.Read(ref _disposed) == 1, this);
        ArgumentNullException.ThrowIfNull(bitmap);
        ArgumentNullException.ThrowIfNull(template);
        ArgumentNullException.ThrowIfNull(data);

        _renderingEngine.RenderToBitmapCore(bitmap, template, data, offset, imageCache: null, _defaultRenderOptions);
    }

    /// <summary>
    /// Renders the template using typed data.
    /// </summary>
    /// <typeparam name="T">The data type implementing ITemplateData.</typeparam>
    /// <param name="canvas">The canvas to render to.</param>
    /// <param name="template">The template to render.</param>
    /// <param name="data">The typed data.</param>
    /// <param name="offset">Optional offset for rendering position.</param>
    public void Render<T>(SKCanvas canvas, Template template, T data, SKPoint offset = default)
        where T : ITemplateData
    {
        Render(canvas, template, data.ToTemplateValue(), offset);
    }

    /// <summary>
    /// Renders the template using typed data to a bitmap.
    /// </summary>
    /// <typeparam name="T">The data type implementing ITemplateData.</typeparam>
    /// <param name="bitmap">The bitmap to render to.</param>
    /// <param name="template">The template to render.</param>
    /// <param name="data">The typed data.</param>
    /// <param name="offset">Optional offset for rendering position.</param>
    public void Render<T>(SKBitmap bitmap, Template template, T data, SKPoint offset = default)
        where T : ITemplateData
    {
        Render(bitmap, template, data.ToTemplateValue(), offset);
    }

    /// <summary>
    /// Renders a template to a new bitmap.
    /// </summary>
    /// <param name="layoutTemplate">The template to render.</param>
    /// <param name="data">The data context for template expressions.</param>
    /// <param name="cancellationToken">Cancellation token for async operation.</param>
    /// <returns>A new bitmap containing the rendered template.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="layoutTemplate"/> or <paramref name="data"/> is null.</exception>
    /// <exception cref="OperationCanceledException">Thrown when the operation is cancelled.</exception>
    public async Task<SKBitmap> Render(
        Template layoutTemplate,
        ObjectValue data,
        CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(Volatile.Read(ref _disposed) == 1, this);
        ArgumentNullException.ThrowIfNull(layoutTemplate);
        ArgumentNullException.ThrowIfNull(data);
        cancellationToken.ThrowIfCancellationRequested();

        var imageCache = _imageLoader is not null
            ? await _renderingEngine.PreloadImagesAsync(layoutTemplate, data, cancellationToken).ConfigureAwait(false)
            : null;

        try
        {
            // Measure returns the final size after rotation
            var size = Measure(layoutTemplate, data);
            var bitmap = new SKBitmap((int)size.Width, (int)size.Height);
            try
            {
                _renderingEngine.RenderToBitmapCore(bitmap, layoutTemplate, data, default, imageCache);
                return bitmap;
            }
            catch
            {
                bitmap.Dispose();
                throw;
            }
        }
        finally
        {
            if (imageCache is not null)
            {
                foreach (var bmp in imageCache.Values)
                    bmp.Dispose();
            }
        }
    }

    /// <summary>
    /// Renders a template to an existing bitmap asynchronously.
    /// </summary>
    /// <param name="bitmap">The target bitmap to render onto.</param>
    /// <param name="layoutTemplate">The template to render.</param>
    /// <param name="data">The data context for template expressions.</param>
    /// <param name="offset">Optional offset for rendering position.</param>
    /// <param name="cancellationToken">Cancellation token for async operation.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="bitmap"/>, <paramref name="layoutTemplate"/>, or <paramref name="data"/> is null.</exception>
    /// <exception cref="OperationCanceledException">Thrown when the operation is cancelled.</exception>
    public async Task Render(
        SKBitmap bitmap,
        Template layoutTemplate,
        ObjectValue data,
        SKPoint offset,
        CancellationToken cancellationToken)
    {
        ObjectDisposedException.ThrowIf(Volatile.Read(ref _disposed) == 1, this);
        ArgumentNullException.ThrowIfNull(bitmap);
        ArgumentNullException.ThrowIfNull(layoutTemplate);
        ArgumentNullException.ThrowIfNull(data);
        cancellationToken.ThrowIfCancellationRequested();

        var imageCache = _imageLoader is not null
            ? await _renderingEngine.PreloadImagesAsync(layoutTemplate, data, cancellationToken).ConfigureAwait(false)
            : null;

        try
        {
            _renderingEngine.RenderToBitmapCore(bitmap, layoutTemplate, data, offset, imageCache);
        }
        finally
        {
            if (imageCache is not null)
            {
                foreach (var bmp in imageCache.Values)
                    bmp.Dispose();
            }
        }
    }

    /// <summary>
    /// Renders a template to a PNG stream.
    /// </summary>
    /// <param name="output">The output stream to write PNG data to.</param>
    /// <param name="layoutTemplate">The template to render.</param>
    /// <param name="data">The data context for template expressions.</param>
    /// <param name="compressionLevel">PNG compression level (0-100).</param>
    /// <param name="renderOptions">Per-call rendering options.</param>
    /// <param name="cancellationToken">Cancellation token for async operation.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="output"/>, <paramref name="layoutTemplate"/>, or <paramref name="data"/> is null.</exception>
    /// <exception cref="OperationCanceledException">Thrown when the operation is cancelled.</exception>
    public async Task RenderToPng(
        Stream output,
        Template layoutTemplate,
        ObjectValue data,
        int compressionLevel = 100,
        RenderOptions? renderOptions = null,
        CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(Volatile.Read(ref _disposed) == 1, this);
        ArgumentNullException.ThrowIfNull(output);
        ArgumentNullException.ThrowIfNull(layoutTemplate);
        ArgumentNullException.ThrowIfNull(data);
        cancellationToken.ThrowIfCancellationRequested();

        var imageCache = _imageLoader is not null
            ? await _renderingEngine.PreloadImagesAsync(layoutTemplate, data, cancellationToken).ConfigureAwait(false)
            : null;

        try
        {
            // Measure returns the final size after rotation
            var size = Measure(layoutTemplate, data);
            using var bitmap = new SKBitmap((int)size.Width, (int)size.Height);

            _renderingEngine.RenderToBitmapCore(bitmap, layoutTemplate, data, default, imageCache, renderOptions);

            using var image = SKImage.FromBitmap(bitmap);
            using var encodedData = image.Encode(SKEncodedImageFormat.Png, compressionLevel);
            encodedData.SaveTo(output);
        }
        finally
        {
            if (imageCache is not null)
            {
                foreach (var bmp in imageCache.Values)
                    bmp.Dispose();
            }
        }
    }

    /// <summary>
    /// Renders a template to a JPEG stream.
    /// </summary>
    /// <param name="output">The output stream to write JPEG data to.</param>
    /// <param name="layoutTemplate">The template to render.</param>
    /// <param name="data">The data context for template expressions.</param>
    /// <param name="quality">JPEG quality (1-100, default 90).</param>
    /// <param name="renderOptions">Per-call rendering options.</param>
    /// <param name="cancellationToken">Cancellation token for async operation.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="output"/>, <paramref name="layoutTemplate"/>, or <paramref name="data"/> is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="quality"/> is not between 1 and 100.</exception>
    /// <exception cref="OperationCanceledException">Thrown when the operation is cancelled.</exception>
    public async Task RenderToJpeg(
        Stream output,
        Template layoutTemplate,
        ObjectValue data,
        int quality = 90,
        RenderOptions? renderOptions = null,
        CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(Volatile.Read(ref _disposed) == 1, this);
        ArgumentNullException.ThrowIfNull(output);
        ArgumentNullException.ThrowIfNull(layoutTemplate);
        ArgumentNullException.ThrowIfNull(data);

        if (quality < 1 || quality > 100)
        {
            throw new ArgumentOutOfRangeException(nameof(quality), quality, "Quality must be between 1 and 100.");
        }

        cancellationToken.ThrowIfCancellationRequested();

        var imageCache = _imageLoader is not null
            ? await _renderingEngine.PreloadImagesAsync(layoutTemplate, data, cancellationToken).ConfigureAwait(false)
            : null;

        try
        {
            // Measure returns the final size after rotation
            var size = Measure(layoutTemplate, data);
            using var bitmap = new SKBitmap((int)size.Width, (int)size.Height);

            _renderingEngine.RenderToBitmapCore(bitmap, layoutTemplate, data, default, imageCache, renderOptions);

            using var image = SKImage.FromBitmap(bitmap);
            using var encodedData = image.Encode(SKEncodedImageFormat.Jpeg, quality);
            encodedData.SaveTo(output);
        }
        finally
        {
            if (imageCache is not null)
            {
                foreach (var bmp in imageCache.Values)
                    bmp.Dispose();
            }
        }
    }

    /// <summary>
    /// Renders a template to a BMP stream.
    /// </summary>
    /// <param name="output">The output stream to write BMP data to.</param>
    /// <param name="layoutTemplate">The template to render.</param>
    /// <param name="data">The data context for template expressions.</param>
    /// <param name="colorMode">The BMP color depth mode to use for encoding.</param>
    /// <param name="renderOptions">Per-call rendering options.</param>
    /// <param name="cancellationToken">Cancellation token for async operation.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="output"/>, <paramref name="layoutTemplate"/>, or <paramref name="data"/> is null.</exception>
    /// <exception cref="OperationCanceledException">Thrown when the operation is cancelled.</exception>
    public async Task RenderToBmp(
        Stream output,
        Template layoutTemplate,
        ObjectValue data,
        BmpColorMode colorMode = BmpColorMode.Bgra32,
        RenderOptions? renderOptions = null,
        CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(Volatile.Read(ref _disposed) == 1, this);
        ArgumentNullException.ThrowIfNull(output);
        ArgumentNullException.ThrowIfNull(layoutTemplate);
        ArgumentNullException.ThrowIfNull(data);
        cancellationToken.ThrowIfCancellationRequested();

        var imageCache = _imageLoader is not null
            ? await _renderingEngine.PreloadImagesAsync(layoutTemplate, data, cancellationToken).ConfigureAwait(false)
            : null;

        try
        {
            // Measure returns the final size after rotation
            var size = Measure(layoutTemplate, data);
            using var bitmap = new SKBitmap((int)size.Width, (int)size.Height);

            _renderingEngine.RenderToBitmapCore(bitmap, layoutTemplate, data, default, imageCache, renderOptions);

            BmpEncoder.Encode(bitmap, output, colorMode);
        }
        finally
        {
            if (imageCache is not null)
            {
                foreach (var bmp in imageCache.Values)
                    bmp.Dispose();
            }
        }
    }

    /// <summary>
    /// Renders a template to raw pixel data in BGRA8888 format.
    /// </summary>
    /// <param name="output">The output stream to write raw pixel data to.</param>
    /// <param name="layoutTemplate">The template to render.</param>
    /// <param name="data">The data context for template expressions.</param>
    /// <param name="renderOptions">Per-call rendering options.</param>
    /// <param name="cancellationToken">Cancellation token for async operation.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="output"/>, <paramref name="layoutTemplate"/>, or <paramref name="data"/> is null.</exception>
    /// <exception cref="OperationCanceledException">Thrown when the operation is cancelled.</exception>
    public async Task RenderToRaw(
        Stream output,
        Template layoutTemplate,
        ObjectValue data,
        RenderOptions? renderOptions = null,
        CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(Volatile.Read(ref _disposed) == 1, this);
        ArgumentNullException.ThrowIfNull(output);
        ArgumentNullException.ThrowIfNull(layoutTemplate);
        ArgumentNullException.ThrowIfNull(data);
        cancellationToken.ThrowIfCancellationRequested();

        var imageCache = _imageLoader is not null
            ? await _renderingEngine.PreloadImagesAsync(layoutTemplate, data, cancellationToken).ConfigureAwait(false)
            : null;

        try
        {
            // Measure returns the final size after rotation
            var size = Measure(layoutTemplate, data);
            using var bitmap = new SKBitmap((int)size.Width, (int)size.Height);

            _renderingEngine.RenderToBitmapCore(bitmap, layoutTemplate, data, default, imageCache, renderOptions);

            // Copy raw pixel bytes directly from the bitmap
            var pixels = bitmap.Bytes;
            await output.WriteAsync(pixels, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            if (imageCache is not null)
            {
                foreach (var bmp in imageCache.Values)
                    bmp.Dispose();
            }
        }
    }

    /// <summary>
    /// Measures template size without rendering.
    /// </summary>
    /// <param name="layoutTemplate">The template to measure.</param>
    /// <param name="data">The data context for template expressions.</param>
    /// <param name="cancellationToken">Cancellation token for async operation.</param>
    /// <returns>The size of the template in pixels.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="layoutTemplate"/> or <paramref name="data"/> is null.</exception>
    /// <exception cref="OperationCanceledException">Thrown when the operation is cancelled.</exception>
    public Task<SKSize> Measure(
        Template layoutTemplate,
        ObjectValue data,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(Measure(layoutTemplate, data));
    }

    /// <summary>
    /// Disposes the renderer and releases resources.
    /// </summary>
    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) == 1)
            return;

        _fontManager.Dispose();
    }

    /// <summary>
    /// Asynchronously disposes the renderer and releases resources.
    /// </summary>
    /// <returns>A value task representing the asynchronous dispose operation.</returns>
    public ValueTask DisposeAsync()
    {
        Dispose();
        return ValueTask.CompletedTask;
    }
}
