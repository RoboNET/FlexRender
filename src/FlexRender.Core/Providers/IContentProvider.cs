namespace FlexRender.Providers;

/// <summary>
/// Provides raster content generation for template elements.
/// Returns PNG-encoded bytes for cross-backend compatibility.
/// </summary>
/// <typeparam name="TElement">The type of template element this provider handles.</typeparam>
public interface IContentProvider<in TElement>
{
    /// <summary>
    /// Generates a PNG-encoded bitmap representation of the element.
    /// </summary>
    /// <param name="element">The element to generate content for.</param>
    /// <param name="width">The allocated width in pixels.</param>
    /// <param name="height">The allocated height in pixels.</param>
    /// <returns>A <see cref="ContentResult"/> containing PNG bytes and dimensions.</returns>
    ContentResult Generate(TElement element, int width, int height);
}
