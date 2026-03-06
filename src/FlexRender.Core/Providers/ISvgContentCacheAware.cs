namespace FlexRender.Providers;

/// <summary>
/// Allows SVG content providers to receive a pre-loaded SVG content cache.
/// </summary>
/// <remarks>
/// Providers that load SVG content from URIs during the sync rendering phase
/// implement this interface to receive a cache of pre-loaded content from the
/// async phase, eliminating the need for synchronous blocking on async loaders.
/// </remarks>
public interface ISvgContentCacheAware
{
    /// <summary>
    /// Sets the pre-loaded SVG content cache for the current render pass.
    /// Pass null to clear the cache after rendering completes.
    /// </summary>
    /// <param name="cache">The SVG content cache mapping URIs to sanitized content, or null to clear.</param>
    void SetSvgContentCache(IReadOnlyDictionary<string, string>? cache);
}
