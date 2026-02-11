using FlexRender.Abstractions;
using FlexRender.Loaders;
using FlexRender.Parsing.Ast;
using FlexRender.Providers;
using SkiaSharp;
using Svg.Skia;

namespace FlexRender.SvgElement.Providers;

/// <summary>
/// Provides SVG rendering using the Svg.Skia library.
/// Converts SVG content (inline or file-based) to SkiaSharp bitmaps
/// for use in FlexRender templates.
/// </summary>
/// <remarks>
/// <para>
/// This provider supports two modes of SVG input:
/// <list type="bullet">
/// <item><description>Inline SVG via <see cref="Parsing.Ast.SvgElement.Content"/> -- SVG markup string.</description></item>
/// <item><description>File-based SVG via <see cref="Parsing.Ast.SvgElement.Src"/> -- path to an SVG file.</description></item>
/// </list>
/// </para>
/// <para>
/// When resource loaders are available (injected via <see cref="IResourceLoaderAware"/>),
/// the <c>Src</c> property supports file paths, HTTP URLs, base64 data URIs, and embedded
/// resources. Without resource loaders, only local file paths are supported via direct loading.
/// </para>
/// <para>
/// The SVG is parsed using Svg.Skia and rasterized to an <see cref="SKBitmap"/> at the
/// dimensions specified by the element's <c>SvgWidth</c>/<c>SvgHeight</c> properties,
/// or the SVG's intrinsic dimensions if not specified.
/// </para>
/// </remarks>
public sealed class SvgElementProvider : IContentProvider<Parsing.Ast.SvgElement>, IResourceLoaderAware, ISkiaNativeProvider<Parsing.Ast.SvgElement>
{
    /// <summary>
    /// Default rendering size when neither the element nor the SVG specifies dimensions.
    /// </summary>
    private const int DefaultSize = 100;

    /// <summary>
    /// Maximum allowed rendering dimension to prevent excessive memory allocation.
    /// </summary>
    private const int MaxDimension = 4096;

    /// <summary>
    /// The resource loaders for loading SVG content from various sources.
    /// Null when no loaders have been injected.
    /// </summary>
    private IReadOnlyList<IResourceLoader>? _loaders;

    /// <summary>
    /// Sets the resource loaders for loading SVG content from URIs.
    /// </summary>
    /// <param name="loaders">The ordered collection of resource loaders.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="loaders"/> is null.</exception>
    public void SetResourceLoaders(IReadOnlyList<IResourceLoader> loaders)
    {
        ArgumentNullException.ThrowIfNull(loaders);
        _loaders = loaders;
    }

    /// <summary>
    /// Generates a PNG-encoded bitmap from the specified SVG element at the given dimensions.
    /// </summary>
    /// <param name="element">The SVG element containing either inline content or a file source.</param>
    /// <param name="width">The allocated width in pixels.</param>
    /// <param name="height">The allocated height in pixels.</param>
    /// <returns>A <see cref="ContentResult"/> containing PNG bytes and dimensions.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="element"/> is null.</exception>
    /// <exception cref="ArgumentException">
    /// Thrown when neither <see cref="Parsing.Ast.SvgElement.Src"/> nor
    /// <see cref="Parsing.Ast.SvgElement.Content"/> is specified.
    /// </exception>
    /// <exception cref="InvalidOperationException">
    /// Thrown when the SVG content cannot be parsed or exceeds <see cref="SvgContentLoader.MaxSvgContentSize"/>.
    /// </exception>
    public ContentResult Generate(Parsing.Ast.SvgElement element, int width, int height)
    {
        using var bitmap = GenerateBitmap(element, width, height);
        using var image = SKImage.FromBitmap(bitmap);
        using var data = image.Encode(SKEncodedImageFormat.Png, 100);
        return new ContentResult(data.ToArray(), bitmap.Width, bitmap.Height);
    }

    /// <summary>
    /// Generates an SVG element bitmap for direct Skia canvas drawing,
    /// avoiding PNG encode/decode overhead.
    /// </summary>
    /// <param name="element">The SVG element containing either inline content or a file source.</param>
    /// <param name="width">The allocated width in pixels.</param>
    /// <param name="height">The allocated height in pixels.</param>
    /// <returns>An <see cref="SKBitmap"/> containing the rasterized SVG. Caller is responsible for disposal.</returns>
    SKBitmap ISkiaNativeProvider<Parsing.Ast.SvgElement>.GenerateBitmap(Parsing.Ast.SvgElement element, int width, int height)
    {
        return GenerateBitmap(element, width, height);
    }

    /// <summary>
    /// Generates a bitmap from the specified SVG element at the given dimensions.
    /// </summary>
    /// <param name="element">The SVG element containing either inline content or a file source.</param>
    /// <param name="width">The target width in pixels.</param>
    /// <param name="height">The target height in pixels.</param>
    /// <returns>A bitmap containing the rasterized SVG content.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="element"/> is null.</exception>
    /// <exception cref="ArgumentException">
    /// Thrown when neither <see cref="Parsing.Ast.SvgElement.Src"/> nor
    /// <see cref="Parsing.Ast.SvgElement.Content"/> is specified.
    /// </exception>
    /// <exception cref="InvalidOperationException">
    /// Thrown when the SVG content cannot be parsed or exceeds <see cref="SvgContentLoader.MaxSvgContentSize"/>.
    /// </exception>
    private SKBitmap GenerateBitmap(Parsing.Ast.SvgElement element, int width, int height)
    {
        ArgumentNullException.ThrowIfNull(element);

        if (string.IsNullOrEmpty(element.Src.Value) && string.IsNullOrEmpty(element.Content.Value))
        {
            throw new ArgumentException(
                "SVG element must have either 'src' or 'content' specified.",
                nameof(element));
        }

        using var svg = new SKSvg();

        SKPicture? picture;
        if (!string.IsNullOrEmpty(element.Content.Value))
        {
            picture = svg.FromSvg(element.Content.Value);
        }
        else
        {
            // Try resource loaders first (supports HTTP, base64, embedded)
            var svgContent = SvgContentLoader.LoadFromLoaders(_loaders, element.Src.Value!);
            if (svgContent is not null)
            {
                picture = svg.FromSvg(svgContent);
            }
            else
            {
                // Fallback to direct file loading
                picture = svg.Load(element.Src.Value!);
            }
        }

        if (picture is null)
        {
            throw new InvalidOperationException(
                $"Failed to parse SVG content. Source: {(element.Src.Value ?? "inline content")}");
        }

        // Determine target dimensions
        var svgBounds = picture.CullRect;
        var intrinsicWidth = svgBounds.Width > 0 ? svgBounds.Width : DefaultSize;
        var intrinsicHeight = svgBounds.Height > 0 ? svgBounds.Height : DefaultSize;

        var targetWidth = Math.Clamp(width, 1, MaxDimension);
        var targetHeight = Math.Clamp(height, 1, MaxDimension);

        // Calculate scale factors
        var scaleX = targetWidth / intrinsicWidth;
        var scaleY = targetHeight / intrinsicHeight;

        // Apply fit mode
        switch (element.Fit.Value)
        {
            case ImageFit.Contain:
            {
                var scale = Math.Min(scaleX, scaleY);
                scaleX = scale;
                scaleY = scale;
                break;
            }
            case ImageFit.Cover:
            {
                var scale = Math.Max(scaleX, scaleY);
                scaleX = scale;
                scaleY = scale;
                break;
            }
            case ImageFit.Fill:
                // scaleX and scaleY are already set for fill
                break;
        }

        var bitmap = new SKBitmap(targetWidth, targetHeight);
        using var canvas = new SKCanvas(bitmap);
        canvas.Clear(SKColors.Transparent);

        // Center the scaled SVG within the target bounds
        var scaledWidth = intrinsicWidth * scaleX;
        var scaledHeight = intrinsicHeight * scaleY;
        var offsetX = (targetWidth - scaledWidth) / 2f;
        var offsetY = (targetHeight - scaledHeight) / 2f;

        canvas.Save();

        // Clip to target bounds for Cover mode
        if (element.Fit.Value == ImageFit.Cover)
        {
            canvas.ClipRect(new SKRect(0, 0, targetWidth, targetHeight));
        }

        canvas.Translate(offsetX, offsetY);
        canvas.Scale(scaleX, scaleY);

        // Offset to account for SVG viewBox origin
        if (svgBounds.Left != 0 || svgBounds.Top != 0)
        {
            canvas.Translate(-svgBounds.Left, -svgBounds.Top);
        }

        canvas.DrawPicture(picture);
        canvas.Restore();

        return bitmap;
    }

}
