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
/// The SVG is parsed using Svg.Skia and rasterized to an <see cref="SKBitmap"/> at the
/// dimensions specified by the element's <c>SvgWidth</c>/<c>SvgHeight</c> properties,
/// or the SVG's intrinsic dimensions if not specified.
/// </para>
/// </remarks>
public sealed class SvgElementProvider : IContentProvider<Parsing.Ast.SvgElement>
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
    /// Generates a bitmap from the specified SVG element.
    /// </summary>
    /// <param name="element">The SVG element containing either inline content or a file source.</param>
    /// <returns>A bitmap containing the rasterized SVG content.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="element"/> is null.</exception>
    /// <exception cref="ArgumentException">
    /// Thrown when neither <see cref="Parsing.Ast.SvgElement.Src"/> nor
    /// <see cref="Parsing.Ast.SvgElement.Content"/> is specified.
    /// </exception>
    /// <exception cref="InvalidOperationException">Thrown when the SVG content cannot be parsed.</exception>
    public SKBitmap Generate(Parsing.Ast.SvgElement element)
    {
        ArgumentNullException.ThrowIfNull(element);

        if (string.IsNullOrEmpty(element.Src) && string.IsNullOrEmpty(element.Content))
        {
            throw new ArgumentException(
                "SVG element must have either 'src' or 'content' specified.",
                nameof(element));
        }

        using var svg = new SKSvg();

        SKPicture? picture;
        if (!string.IsNullOrEmpty(element.Content))
        {
            picture = svg.FromSvg(element.Content);
        }
        else
        {
            picture = svg.Load(element.Src!);
        }

        if (picture is null)
        {
            throw new InvalidOperationException(
                $"Failed to parse SVG content. Source: {(element.Src ?? "inline content")}");
        }

        // Determine target dimensions
        var svgBounds = picture.CullRect;
        var intrinsicWidth = svgBounds.Width > 0 ? svgBounds.Width : DefaultSize;
        var intrinsicHeight = svgBounds.Height > 0 ? svgBounds.Height : DefaultSize;

        var targetWidth = element.SvgWidth ?? (int)intrinsicWidth;
        var targetHeight = element.SvgHeight ?? (int)intrinsicHeight;

        // Clamp to maximum dimension
        targetWidth = Math.Clamp(targetWidth, 1, MaxDimension);
        targetHeight = Math.Clamp(targetHeight, 1, MaxDimension);

        // Calculate scale factors
        var scaleX = targetWidth / intrinsicWidth;
        var scaleY = targetHeight / intrinsicHeight;

        // Apply fit mode
        switch (element.Fit)
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
        if (element.Fit == ImageFit.Cover)
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
