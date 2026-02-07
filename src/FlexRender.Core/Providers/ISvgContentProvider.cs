namespace FlexRender.Providers;

/// <summary>
/// Provides SVG-native content generation for template elements.
/// </summary>
/// <remarks>
/// <para>
/// Content providers that implement this interface can generate native SVG markup
/// instead of rasterized bitmaps. The SVG rendering engine checks for this interface
/// and uses it when available, falling back to bitmap rasterization via
/// <see cref="IContentProvider{TElement}"/> otherwise.
/// </para>
/// <para>
/// The returned SVG markup is inserted directly into the SVG document at the
/// specified position and dimensions. It should not include an outer wrapping element
/// -- the rendering engine handles positioning via a nested <c>&lt;svg&gt;</c> element.
/// </para>
/// </remarks>
/// <typeparam name="TElement">The type of template element this provider handles.</typeparam>
public interface ISvgContentProvider<in TElement>
{
    /// <summary>
    /// Generates SVG markup for the specified element.
    /// </summary>
    /// <param name="element">The element to generate SVG content for.</param>
    /// <param name="width">The allocated width in SVG user units.</param>
    /// <param name="height">The allocated height in SVG user units.</param>
    /// <returns>A string containing SVG markup (e.g., path, rect, or group elements).</returns>
    string GenerateSvgContent(TElement element, float width, float height);
}
