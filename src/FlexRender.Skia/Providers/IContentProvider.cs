using SkiaSharp;

namespace FlexRender.Providers;

/// <summary>
/// Provides content generation for template elements.
/// </summary>
/// <typeparam name="TElement">The type of template element this provider handles.</typeparam>
public interface IContentProvider<in TElement>
{
    /// <summary>
    /// Generates a bitmap representation of the element.
    /// </summary>
    /// <param name="element">The element to generate content for.</param>
    /// <returns>A bitmap containing the generated content.</returns>
    SKBitmap Generate(TElement element);
}
