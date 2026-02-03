using FlexRender.Parsing.Ast;
using SkiaSharp;

namespace FlexRender.Abstractions;

/// <summary>
/// Renders templates to SkiaSharp bitmaps and streams.
/// </summary>
/// <remarks>
/// This is the main entry point for rendering templates.
/// Implements both <see cref="IAsyncDisposable"/> and <see cref="IDisposable"/>
/// for proper resource cleanup.
/// </remarks>
public interface IFlexRenderer : IAsyncDisposable, IDisposable
{
    /// <summary>
    /// Renders a template to a new bitmap.
    /// </summary>
    /// <param name="template">The template to render.</param>
    /// <param name="data">The data context for template expressions.</param>
    /// <param name="cancellationToken">Cancellation token for async operation.</param>
    /// <returns>A new bitmap containing the rendered template.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="template"/> or <paramref name="data"/> is null.</exception>
    /// <exception cref="OperationCanceledException">Thrown when the operation is cancelled.</exception>
    Task<SKBitmap> Render(
        Template template,
        ObjectValue data,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Renders a template to an existing bitmap.
    /// </summary>
    /// <param name="bitmap">The target bitmap to render onto.</param>
    /// <param name="template">The template to render.</param>
    /// <param name="data">The data context for template expressions.</param>
    /// <param name="offset">Optional offset for rendering position.</param>
    /// <param name="cancellationToken">Cancellation token for async operation.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="bitmap"/>, <paramref name="template"/>, or <paramref name="data"/> is null.</exception>
    /// <exception cref="OperationCanceledException">Thrown when the operation is cancelled.</exception>
    Task Render(
        SKBitmap bitmap,
        Template template,
        ObjectValue data,
        SKPoint offset = default,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Renders a template to a PNG stream.
    /// </summary>
    /// <param name="output">The output stream to write PNG data to.</param>
    /// <param name="template">The template to render.</param>
    /// <param name="data">The data context for template expressions.</param>
    /// <param name="cancellationToken">Cancellation token for async operation.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="output"/>, <paramref name="template"/>, or <paramref name="data"/> is null.</exception>
    /// <exception cref="OperationCanceledException">Thrown when the operation is cancelled.</exception>
    Task RenderToPng(
        Stream output,
        Template template,
        ObjectValue data,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Renders a template to a JPEG stream.
    /// </summary>
    /// <param name="output">The output stream to write JPEG data to.</param>
    /// <param name="template">The template to render.</param>
    /// <param name="data">The data context for template expressions.</param>
    /// <param name="quality">JPEG quality (1-100, default 90).</param>
    /// <param name="cancellationToken">Cancellation token for async operation.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="output"/>, <paramref name="template"/>, or <paramref name="data"/> is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="quality"/> is not between 1 and 100.</exception>
    /// <exception cref="OperationCanceledException">Thrown when the operation is cancelled.</exception>
    Task RenderToJpeg(
        Stream output,
        Template template,
        ObjectValue data,
        int quality = 90,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Measures template size without rendering.
    /// </summary>
    /// <param name="template">The template to measure.</param>
    /// <param name="data">The data context for template expressions.</param>
    /// <param name="cancellationToken">Cancellation token for async operation.</param>
    /// <returns>The size of the template in pixels.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="template"/> or <paramref name="data"/> is null.</exception>
    /// <exception cref="OperationCanceledException">Thrown when the operation is cancelled.</exception>
    Task<SKSize> Measure(
        Template template,
        ObjectValue data,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the font manager for font registration and configuration.
    /// </summary>
    IFontManager FontManager { get; }
}
