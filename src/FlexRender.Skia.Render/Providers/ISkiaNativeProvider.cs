using SkiaSharp;

namespace FlexRender.Providers;

/// <summary>
/// Internal optimization interface for Skia content providers.
/// Allows providers to return <see cref="SKBitmap"/> directly to the Skia rendering engine,
/// avoiding the PNG encode/decode overhead that <see cref="IContentProvider{TElement}"/> requires.
/// </summary>
/// <remarks>
/// <para>
/// The Skia rendering engine checks for this interface first. If a provider implements it,
/// <see cref="GenerateBitmap"/> is called instead of <see cref="IContentProvider{TElement}.Generate"/>.
/// This keeps the public API clean (all providers implement <see cref="IContentProvider{TElement}"/>)
/// while avoiding unnecessary serialization in the hot path.
/// </para>
/// </remarks>
/// <typeparam name="TElement">The type of template element this provider handles.</typeparam>
internal interface ISkiaNativeProvider<in TElement>
{
    /// <summary>
    /// Generates a bitmap representation of the element for direct Skia canvas drawing.
    /// </summary>
    /// <param name="element">The element to generate content for.</param>
    /// <param name="width">The allocated width in pixels.</param>
    /// <param name="height">The allocated height in pixels.</param>
    /// <returns>An <see cref="SKBitmap"/> containing the rendered content. Caller is responsible for disposal.</returns>
    SKBitmap GenerateBitmap(TElement element, int width, int height);
}
