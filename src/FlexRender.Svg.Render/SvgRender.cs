using System.Text;
using FlexRender.Abstractions;
using FlexRender.Configuration;
using FlexRender.Layout;
using FlexRender.Parsing.Ast;
using FlexRender.Providers;
using FlexRender.Svg.Rendering;
using FlexRender.TemplateEngine;

namespace FlexRender.Svg;

/// <summary>
/// SVG-based implementation of <see cref="IFlexRender"/>.
/// Generates scalable vector graphics from the same layout tree as SkiaRenderer.
/// </summary>
/// <remarks>
/// <para>
/// This class reuses the existing template expansion, processing, and layout pipeline
/// from FlexRender.Core, and only replaces the final rendering step with SVG XML generation.
/// </para>
/// <para>
/// Raster output methods (RenderToPng, RenderToJpeg, etc.) delegate to the underlying
/// Skia renderer if configured, or throw <see cref="NotSupportedException"/>.
/// </para>
/// <para>
/// Instances are created through <see cref="FlexRenderBuilder"/> using the
/// <see cref="SvgBuilderExtensions.WithSvg"/> extension method.
/// </para>
/// </remarks>
public sealed class SvgRender : IFlexRender
{
    private readonly SvgRenderingEngine _svgEngine;
    private readonly IFlexRender? _rasterRenderer;
    private int _disposed;

    /// <summary>
    /// Initializes a new instance of the <see cref="SvgRender"/> class.
    /// </summary>
    /// <param name="limits">Resource limits for rendering operations.</param>
    /// <param name="options">Rendering configuration options.</param>
    /// <param name="rasterRenderer">Optional underlying raster renderer for PNG/JPEG/BMP output.</param>
    /// <param name="qrProvider">Optional raster QR code content provider for bitmap embedding in SVG output.</param>
    /// <param name="barcodeProvider">Optional raster barcode content provider for bitmap embedding in SVG output.</param>
    /// <param name="qrSvgProvider">Optional SVG-native QR code provider for vector QR code embedding.</param>
    /// <param name="barcodeSvgProvider">Optional SVG-native barcode provider for vector barcode embedding.</param>
    /// <param name="svgElementSvgProvider">Optional SVG-native SVG element provider.</param>
    internal SvgRender(
        ResourceLimits limits,
        FlexRenderOptions options,
        IFlexRender? rasterRenderer = null,
        IContentProvider<QrElement>? qrProvider = null,
        IContentProvider<BarcodeElement>? barcodeProvider = null,
        ISvgContentProvider<QrElement>? qrSvgProvider = null,
        ISvgContentProvider<BarcodeElement>? barcodeSvgProvider = null,
        ISvgContentProvider<SvgElement>? svgElementSvgProvider = null)
    {
        ArgumentNullException.ThrowIfNull(limits);
        ArgumentNullException.ThrowIfNull(options);

        _rasterRenderer = rasterRenderer;

        var templateProcessor = new TemplateProcessor(limits);
        var expander = new TemplateExpander(limits);
        var pipeline = new TemplatePipeline(expander, templateProcessor);
        var layoutEngine = new LayoutEngine(limits);
        layoutEngine.TextShaper = new ApproximateTextShaper();
        layoutEngine.BaseFontSize = options.BaseFontSize;

        _svgEngine = new SvgRenderingEngine(
            limits,
            pipeline,
            layoutEngine,
            options.BaseFontSize,
            options,
            qrProvider,
            barcodeProvider,
            qrSvgProvider,
            barcodeSvgProvider,
            svgElementSvgProvider);
    }

    // ========================================================================
    // SVG-SPECIFIC METHODS
    // ========================================================================

    /// <summary>
    /// Renders a template to an SVG string.
    /// </summary>
    /// <param name="layoutTemplate">The template AST to render.</param>
    /// <param name="data">Optional data for template variable substitution.</param>
    /// <param name="renderOptions">Per-call rendering options (unused for SVG).</param>
    /// <param name="cancellationToken">Token to cancel the rendering operation.</param>
    /// <returns>The SVG markup as a string.</returns>
    /// <exception cref="ObjectDisposedException">Thrown when the renderer has been disposed.</exception>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="layoutTemplate"/> is null.</exception>
    public Task<string> RenderToSvg(
        Template layoutTemplate,
        ObjectValue? data = null,
        RenderOptions? renderOptions = null,
        CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(Volatile.Read(ref _disposed) == 1, this);
        ArgumentNullException.ThrowIfNull(layoutTemplate);

        cancellationToken.ThrowIfCancellationRequested();

        var effectiveData = data ?? new ObjectValue();
        var svg = _svgEngine.RenderToSvg(layoutTemplate, effectiveData);

        return Task.FromResult(svg);
    }

    /// <summary>
    /// Renders a template to SVG written to a stream.
    /// </summary>
    /// <param name="output">The output stream to write SVG data to.</param>
    /// <param name="layoutTemplate">The template AST to render.</param>
    /// <param name="data">Optional data for template variable substitution.</param>
    /// <param name="renderOptions">Per-call rendering options (unused for SVG).</param>
    /// <param name="cancellationToken">Token to cancel the rendering operation.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    /// <exception cref="ObjectDisposedException">Thrown when the renderer has been disposed.</exception>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="output"/> or <paramref name="layoutTemplate"/> is null.</exception>
    public async Task RenderToSvg(
        Stream output,
        Template layoutTemplate,
        ObjectValue? data = null,
        RenderOptions? renderOptions = null,
        CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(Volatile.Read(ref _disposed) == 1, this);
        ArgumentNullException.ThrowIfNull(output);
        ArgumentNullException.ThrowIfNull(layoutTemplate);

        var svg = await RenderToSvg(layoutTemplate, data, renderOptions, cancellationToken)
            .ConfigureAwait(false);

        var bytes = Encoding.UTF8.GetBytes(svg);
        await output.WriteAsync(bytes, cancellationToken).ConfigureAwait(false);
    }

    // ========================================================================
    // RASTER DELEGATION (delegates to underlying Skia renderer if available)
    // ========================================================================

    /// <inheritdoc />
    public Task<byte[]> Render(
        Template layoutTemplate,
        ObjectValue? data = null,
        ImageFormat format = ImageFormat.Png,
        CancellationToken cancellationToken = default)
    {
        return GetRasterRenderer().Render(layoutTemplate, data, format, cancellationToken);
    }

    /// <inheritdoc />
    public Task Render(
        Stream output,
        Template layoutTemplate,
        ObjectValue? data = null,
        ImageFormat format = ImageFormat.Png,
        CancellationToken cancellationToken = default)
    {
        return GetRasterRenderer().Render(output, layoutTemplate, data, format, cancellationToken);
    }

    /// <inheritdoc />
    public Task<byte[]> RenderToPng(
        Template layoutTemplate,
        ObjectValue? data = null,
        PngOptions? options = null,
        RenderOptions? renderOptions = null,
        CancellationToken cancellationToken = default)
    {
        return GetRasterRenderer().RenderToPng(layoutTemplate, data, options, renderOptions, cancellationToken);
    }

    /// <inheritdoc />
    public Task RenderToPng(
        Stream output,
        Template layoutTemplate,
        ObjectValue? data = null,
        PngOptions? options = null,
        RenderOptions? renderOptions = null,
        CancellationToken cancellationToken = default)
    {
        return GetRasterRenderer().RenderToPng(output, layoutTemplate, data, options, renderOptions, cancellationToken);
    }

    /// <inheritdoc />
    public Task<byte[]> RenderToJpeg(
        Template layoutTemplate,
        ObjectValue? data = null,
        JpegOptions? options = null,
        RenderOptions? renderOptions = null,
        CancellationToken cancellationToken = default)
    {
        return GetRasterRenderer().RenderToJpeg(layoutTemplate, data, options, renderOptions, cancellationToken);
    }

    /// <inheritdoc />
    public Task RenderToJpeg(
        Stream output,
        Template layoutTemplate,
        ObjectValue? data = null,
        JpegOptions? options = null,
        RenderOptions? renderOptions = null,
        CancellationToken cancellationToken = default)
    {
        return GetRasterRenderer().RenderToJpeg(output, layoutTemplate, data, options, renderOptions, cancellationToken);
    }

    /// <inheritdoc />
    public Task<byte[]> RenderToBmp(
        Template layoutTemplate,
        ObjectValue? data = null,
        BmpOptions? options = null,
        RenderOptions? renderOptions = null,
        CancellationToken cancellationToken = default)
    {
        return GetRasterRenderer().RenderToBmp(layoutTemplate, data, options, renderOptions, cancellationToken);
    }

    /// <inheritdoc />
    public Task RenderToBmp(
        Stream output,
        Template layoutTemplate,
        ObjectValue? data = null,
        BmpOptions? options = null,
        RenderOptions? renderOptions = null,
        CancellationToken cancellationToken = default)
    {
        return GetRasterRenderer().RenderToBmp(output, layoutTemplate, data, options, renderOptions, cancellationToken);
    }

    /// <inheritdoc />
    public Task<byte[]> RenderToRaw(
        Template layoutTemplate,
        ObjectValue? data = null,
        RenderOptions? renderOptions = null,
        CancellationToken cancellationToken = default)
    {
        return GetRasterRenderer().RenderToRaw(layoutTemplate, data, renderOptions, cancellationToken);
    }

    /// <inheritdoc />
    public Task RenderToRaw(
        Stream output,
        Template layoutTemplate,
        ObjectValue? data = null,
        RenderOptions? renderOptions = null,
        CancellationToken cancellationToken = default)
    {
        return GetRasterRenderer().RenderToRaw(output, layoutTemplate, data, renderOptions, cancellationToken);
    }

    private IFlexRender GetRasterRenderer()
    {
        ObjectDisposedException.ThrowIf(Volatile.Read(ref _disposed) == 1, this);

        return _rasterRenderer
               ?? throw new NotSupportedException(
                   "Raster output (PNG, JPEG, BMP, Raw) requires a raster backend. " +
                   "Use WithSvg(svg => svg.WithSkia()) or WithSvg(svg => svg.WithRasterBackend(...)) " +
                   "to enable both SVG and raster output.");
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

        _rasterRenderer?.Dispose();
    }
}
