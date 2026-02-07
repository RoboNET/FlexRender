using FlexRender.Abstractions;

namespace FlexRender.Providers;

/// <summary>
/// Allows content providers to receive resource loaders for loading external assets.
/// </summary>
/// <remarks>
/// Content providers that need to load resources from URIs (files, HTTP, base64, embedded)
/// can implement this interface to receive the configured resource loader chain.
/// The loaders are injected after construction by the rendering infrastructure.
/// </remarks>
public interface IResourceLoaderAware
{
    /// <summary>
    /// Sets the resource loaders for this provider.
    /// </summary>
    /// <param name="loaders">The ordered collection of resource loaders.</param>
    void SetResourceLoaders(IReadOnlyList<IResourceLoader> loaders);
}
