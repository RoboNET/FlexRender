using FlexRender.Parsing.Ast;

namespace FlexRender.Abstractions;

/// <summary>
/// Core interface for rendering templates to images.
/// </summary>
/// <remarks>
/// <para>
/// This interface provides the minimal API for rendering pre-built <see cref="Template"/> objects.
/// For convenience methods like <c>RenderFile</c> and <c>RenderYaml</c>, use the extension methods
/// from the FlexRender.Yaml package.
/// </para>
/// <para>
/// Implementations are typically created using <c>FlexRenderBuilder</c>:
/// <code>
/// var render = new FlexRenderBuilder()
///     .WithSkia()
///     .Build();
/// </code>
/// </para>
/// </remarks>
public interface IFlexRender : IDisposable
{
    // ========================================================================
    // EXISTING METHODS (unchanged, backward compatible)
    // ========================================================================

    /// <summary>
    /// Renders a template to a byte array.
    /// </summary>
    /// <param name="layoutTemplate">The template AST to render.</param>
    /// <param name="data">Optional data for template variable substitution.</param>
    /// <param name="format">Output image format. Defaults to PNG.</param>
    /// <param name="cancellationToken">Token to cancel the rendering operation.</param>
    /// <returns>A task that represents the asynchronous operation. The task result contains the image bytes in the specified format.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="layoutTemplate"/> is null.</exception>
    /// <exception cref="InvalidOperationException">Thrown when rendering fails due to invalid template structure or resource loading errors.</exception>
    Task<byte[]> Render(
        Template layoutTemplate,
        ObjectValue? data = null,
        ImageFormat format = ImageFormat.Png,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Renders a template to a stream.
    /// </summary>
    /// <param name="output">The output stream to write image data to.</param>
    /// <param name="layoutTemplate">The template AST to render.</param>
    /// <param name="data">Optional data for template variable substitution.</param>
    /// <param name="format">Output image format. Defaults to PNG.</param>
    /// <param name="cancellationToken">Token to cancel the rendering operation.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="output"/> or <paramref name="layoutTemplate"/> is null.</exception>
    /// <exception cref="InvalidOperationException">Thrown when rendering fails due to invalid template structure or resource loading errors.</exception>
    Task Render(
        Stream output,
        Template layoutTemplate,
        ObjectValue? data = null,
        ImageFormat format = ImageFormat.Png,
        CancellationToken cancellationToken = default);

    // ========================================================================
    // FORMAT-SPECIFIC METHODS
    // ========================================================================

    // --- PNG ---

    /// <summary>
    /// Renders a template to a PNG byte array.
    /// </summary>
    /// <param name="layoutTemplate">The template AST to render.</param>
    /// <param name="data">Optional data for template variable substitution.</param>
    /// <param name="options">PNG encoding options. Pass <c>null</c> for defaults.</param>
    /// <param name="renderOptions">Per-call rendering options. Pass <c>null</c> for defaults.</param>
    /// <param name="cancellationToken">Token to cancel the rendering operation.</param>
    /// <returns>The image bytes in PNG format.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="layoutTemplate"/> is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <see cref="PngOptions.CompressionLevel"/> is not between 0 and 100.</exception>
    Task<byte[]> RenderToPng(
        Template layoutTemplate,
        ObjectValue? data = null,
        PngOptions? options = null,
        RenderOptions? renderOptions = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Renders a template to a PNG stream.
    /// </summary>
    /// <param name="output">The output stream to write PNG data to.</param>
    /// <param name="layoutTemplate">The template AST to render.</param>
    /// <param name="data">Optional data for template variable substitution.</param>
    /// <param name="options">PNG encoding options. Pass <c>null</c> for defaults.</param>
    /// <param name="renderOptions">Per-call rendering options. Pass <c>null</c> for defaults.</param>
    /// <param name="cancellationToken">Token to cancel the rendering operation.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="output"/> or <paramref name="layoutTemplate"/> is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <see cref="PngOptions.CompressionLevel"/> is not between 0 and 100.</exception>
    Task RenderToPng(
        Stream output,
        Template layoutTemplate,
        ObjectValue? data = null,
        PngOptions? options = null,
        RenderOptions? renderOptions = null,
        CancellationToken cancellationToken = default);

    // --- JPEG ---

    /// <summary>
    /// Renders a template to a JPEG byte array.
    /// </summary>
    /// <param name="layoutTemplate">The template AST to render.</param>
    /// <param name="data">Optional data for template variable substitution.</param>
    /// <param name="options">JPEG encoding options. Pass <c>null</c> for defaults (quality 90).</param>
    /// <param name="renderOptions">Per-call rendering options. Pass <c>null</c> for defaults.</param>
    /// <param name="cancellationToken">Token to cancel the rendering operation.</param>
    /// <returns>The image bytes in JPEG format.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="layoutTemplate"/> is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <see cref="JpegOptions.Quality"/> is not between 1 and 100.</exception>
    Task<byte[]> RenderToJpeg(
        Template layoutTemplate,
        ObjectValue? data = null,
        JpegOptions? options = null,
        RenderOptions? renderOptions = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Renders a template to a JPEG stream.
    /// </summary>
    /// <param name="output">The output stream to write JPEG data to.</param>
    /// <param name="layoutTemplate">The template AST to render.</param>
    /// <param name="data">Optional data for template variable substitution.</param>
    /// <param name="options">JPEG encoding options. Pass <c>null</c> for defaults (quality 90).</param>
    /// <param name="renderOptions">Per-call rendering options. Pass <c>null</c> for defaults.</param>
    /// <param name="cancellationToken">Token to cancel the rendering operation.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="output"/> or <paramref name="layoutTemplate"/> is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <see cref="JpegOptions.Quality"/> is not between 1 and 100.</exception>
    Task RenderToJpeg(
        Stream output,
        Template layoutTemplate,
        ObjectValue? data = null,
        JpegOptions? options = null,
        RenderOptions? renderOptions = null,
        CancellationToken cancellationToken = default);

    // --- BMP ---

    /// <summary>
    /// Renders a template to a BMP byte array.
    /// </summary>
    /// <param name="layoutTemplate">The template AST to render.</param>
    /// <param name="data">Optional data for template variable substitution.</param>
    /// <param name="options">BMP encoding options. Pass <c>null</c> for defaults (Bgra32).</param>
    /// <param name="renderOptions">Per-call rendering options. Pass <c>null</c> for defaults.</param>
    /// <param name="cancellationToken">Token to cancel the rendering operation.</param>
    /// <returns>The image bytes in BMP format.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="layoutTemplate"/> is null.</exception>
    Task<byte[]> RenderToBmp(
        Template layoutTemplate,
        ObjectValue? data = null,
        BmpOptions? options = null,
        RenderOptions? renderOptions = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Renders a template to a BMP stream.
    /// </summary>
    /// <param name="output">The output stream to write BMP data to.</param>
    /// <param name="layoutTemplate">The template AST to render.</param>
    /// <param name="data">Optional data for template variable substitution.</param>
    /// <param name="options">BMP encoding options. Pass <c>null</c> for defaults (Bgra32).</param>
    /// <param name="renderOptions">Per-call rendering options. Pass <c>null</c> for defaults.</param>
    /// <param name="cancellationToken">Token to cancel the rendering operation.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="output"/> or <paramref name="layoutTemplate"/> is null.</exception>
    Task RenderToBmp(
        Stream output,
        Template layoutTemplate,
        ObjectValue? data = null,
        BmpOptions? options = null,
        RenderOptions? renderOptions = null,
        CancellationToken cancellationToken = default);

    // --- Raw ---

    /// <summary>
    /// Renders a template to raw BGRA8888 pixel data as a byte array.
    /// </summary>
    /// <param name="layoutTemplate">The template AST to render.</param>
    /// <param name="data">Optional data for template variable substitution.</param>
    /// <param name="renderOptions">Per-call rendering options. Pass <c>null</c> for defaults.</param>
    /// <param name="cancellationToken">Token to cancel the rendering operation.</param>
    /// <returns>Raw pixel bytes in BGRA8888 format.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="layoutTemplate"/> is null.</exception>
    Task<byte[]> RenderToRaw(
        Template layoutTemplate,
        ObjectValue? data = null,
        RenderOptions? renderOptions = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Renders a template to raw BGRA8888 pixel data written to a stream.
    /// </summary>
    /// <param name="output">The output stream to write raw pixel data to.</param>
    /// <param name="layoutTemplate">The template AST to render.</param>
    /// <param name="data">Optional data for template variable substitution.</param>
    /// <param name="renderOptions">Per-call rendering options. Pass <c>null</c> for defaults.</param>
    /// <param name="cancellationToken">Token to cancel the rendering operation.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="output"/> or <paramref name="layoutTemplate"/> is null.</exception>
    Task RenderToRaw(
        Stream output,
        Template layoutTemplate,
        ObjectValue? data = null,
        RenderOptions? renderOptions = null,
        CancellationToken cancellationToken = default);
}
