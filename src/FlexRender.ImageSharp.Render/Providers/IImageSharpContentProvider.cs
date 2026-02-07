using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace FlexRender.Providers;

/// <summary>
/// Provides ImageSharp-native content generation for template elements.
/// </summary>
/// <typeparam name="TElement">The type of template element this provider handles.</typeparam>
public interface IImageSharpContentProvider<in TElement>
{
    /// <summary>
    /// Generates an ImageSharp image for the specified element.
    /// </summary>
    /// <param name="element">The element to generate content for.</param>
    /// <param name="width">The allocated width in pixels.</param>
    /// <param name="height">The allocated height in pixels.</param>
    /// <returns>An <see cref="Image{TPixel}"/> containing rendered content.</returns>
    Image<Rgba32> GenerateImage(TElement element, int width, int height);
}
