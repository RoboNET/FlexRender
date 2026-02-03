using FlexRender.Parsing.Ast;

namespace FlexRender.Abstractions;

/// <summary>
/// Renders templates to a backend-specific output format.
/// Each render backend implements this interface with its own output type.
/// </summary>
/// <typeparam name="TOutput">The output type produced by the renderer (e.g., SKBitmap for Skia).</typeparam>
public interface ILayoutRenderer<TOutput> : IAsyncDisposable, IDisposable
    where TOutput : IDisposable
{
    /// <summary>
    /// Renders a template with data to the backend-specific output type.
    /// </summary>
    /// <param name="template">The template to render.</param>
    /// <param name="data">The data context for template expressions.</param>
    /// <param name="cancellationToken">Cancellation token for async operation.</param>
    /// <returns>The rendered output. Caller is responsible for disposing.</returns>
    Task<TOutput> Render(
        Template template,
        ObjectValue data,
        CancellationToken cancellationToken = default);
}
