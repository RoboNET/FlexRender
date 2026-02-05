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
}
