using FlexRender.Parsing.Ast;
using SkiaSharp;

namespace FlexRender.Rendering;

/// <summary>
/// Extension methods for convenient rendering output.
/// </summary>
public static class SkiaRendererExtensions
{
    /// <summary>
    /// Renders the template to a new bitmap.
    /// </summary>
    /// <param name="renderer">The renderer.</param>
    /// <param name="template">The template to render.</param>
    /// <param name="data">The data for variable substitution.</param>
    /// <returns>A new bitmap with the rendered content.</returns>
    public static SKBitmap RenderToBitmap(this SkiaRenderer renderer, Template template, ObjectValue data)
    {
        ArgumentNullException.ThrowIfNull(renderer);
        ArgumentNullException.ThrowIfNull(template);
        ArgumentNullException.ThrowIfNull(data);

        var size = renderer.Measure(template, data);
        var bitmap = new SKBitmap((int)Math.Ceiling(size.Width), (int)Math.Ceiling(size.Height));

        renderer.Render(bitmap, template, data, default(SKPoint));
        return bitmap;
    }

    /// <summary>
    /// Renders the template to a new bitmap using typed data.
    /// </summary>
    /// <typeparam name="T">The data type implementing ITemplateData.</typeparam>
    /// <param name="renderer">The renderer.</param>
    /// <param name="template">The template to render.</param>
    /// <param name="data">The typed data.</param>
    /// <returns>A new bitmap with the rendered content.</returns>
    public static SKBitmap RenderToBitmap<T>(this SkiaRenderer renderer, Template template, T data)
        where T : ITemplateData
    {
        return RenderToBitmap(renderer, template, data.ToTemplateValue());
    }

    /// <summary>
    /// Renders the template to PNG bytes.
    /// </summary>
    /// <param name="renderer">The renderer.</param>
    /// <param name="template">The template to render.</param>
    /// <param name="data">The data for variable substitution.</param>
    /// <returns>PNG-encoded bytes.</returns>
    public static byte[] RenderToPng(this SkiaRenderer renderer, Template template, ObjectValue data)
    {
        using var bitmap = RenderToBitmap(renderer, template, data);
        using var image = SKImage.FromBitmap(bitmap);
        using var encoded = image.Encode(SKEncodedImageFormat.Png, 100);
        return encoded.ToArray();
    }

    /// <summary>
    /// Renders the template to PNG bytes using typed data.
    /// </summary>
    /// <typeparam name="T">The data type implementing ITemplateData.</typeparam>
    /// <param name="renderer">The renderer.</param>
    /// <param name="template">The template to render.</param>
    /// <param name="data">The typed data.</param>
    /// <returns>PNG-encoded bytes.</returns>
    public static byte[] RenderToPng<T>(this SkiaRenderer renderer, Template template, T data)
        where T : ITemplateData
    {
        return RenderToPng(renderer, template, data.ToTemplateValue());
    }

    /// <summary>
    /// Renders the template to JPEG bytes.
    /// </summary>
    /// <param name="renderer">The renderer.</param>
    /// <param name="template">The template to render.</param>
    /// <param name="data">The data for variable substitution.</param>
    /// <param name="quality">JPEG quality (0-100, default 90).</param>
    /// <returns>JPEG-encoded bytes.</returns>
    public static byte[] RenderToJpeg(this SkiaRenderer renderer, Template template, ObjectValue data, int quality = 90)
    {
        quality = Math.Clamp(quality, 0, 100);

        using var bitmap = RenderToBitmap(renderer, template, data);
        using var image = SKImage.FromBitmap(bitmap);
        using var encoded = image.Encode(SKEncodedImageFormat.Jpeg, quality);
        return encoded.ToArray();
    }

    /// <summary>
    /// Renders the template to JPEG bytes using typed data.
    /// </summary>
    /// <typeparam name="T">The data type implementing ITemplateData.</typeparam>
    /// <param name="renderer">The renderer.</param>
    /// <param name="template">The template to render.</param>
    /// <param name="data">The typed data.</param>
    /// <param name="quality">JPEG quality (0-100, default 90).</param>
    /// <returns>JPEG-encoded bytes.</returns>
    public static byte[] RenderToJpeg<T>(this SkiaRenderer renderer, Template template, T data, int quality = 90)
        where T : ITemplateData
    {
        return RenderToJpeg(renderer, template, data.ToTemplateValue(), quality);
    }

    /// <summary>
    /// Renders the template to BMP bytes.
    /// </summary>
    /// <param name="renderer">The renderer.</param>
    /// <param name="template">The template to render.</param>
    /// <param name="data">The data for variable substitution.</param>
    /// <returns>BMP-encoded bytes.</returns>
    public static byte[] RenderToBmp(this SkiaRenderer renderer, Template template, ObjectValue data)
    {
        using var bitmap = RenderToBitmap(renderer, template, data);
        return BmpEncoder.Encode(bitmap);
    }

    /// <summary>
    /// Renders the template to BMP bytes using typed data.
    /// </summary>
    /// <typeparam name="T">The data type implementing ITemplateData.</typeparam>
    /// <param name="renderer">The renderer.</param>
    /// <param name="template">The template to render.</param>
    /// <param name="data">The typed data.</param>
    /// <returns>BMP-encoded bytes.</returns>
    public static byte[] RenderToBmp<T>(this SkiaRenderer renderer, Template template, T data)
        where T : ITemplateData
    {
        return RenderToBmp(renderer, template, data.ToTemplateValue());
    }

    /// <summary>
    /// Renders the template directly to a file.
    /// Format is determined by file extension (.png, .jpg, .jpeg, .bmp).
    /// </summary>
    /// <param name="renderer">The renderer.</param>
    /// <param name="template">The template to render.</param>
    /// <param name="data">The data for variable substitution.</param>
    /// <param name="path">The output file path.</param>
    /// <param name="quality">JPEG quality if applicable (0-100, default 90).</param>
    public static void RenderToFile(this SkiaRenderer renderer, Template template, ObjectValue data, string path, int quality = 90)
    {
        ArgumentNullException.ThrowIfNull(path);

        var extension = Path.GetExtension(path).ToLowerInvariant();
        var format = extension switch
        {
            ".jpg" or ".jpeg" => SKEncodedImageFormat.Jpeg,
            ".bmp" => SKEncodedImageFormat.Bmp,
            ".webp" => SKEncodedImageFormat.Webp,
            ".gif" => SKEncodedImageFormat.Gif,
            _ => SKEncodedImageFormat.Png
        };

        using var bitmap = RenderToBitmap(renderer, template, data);

        if (format == SKEncodedImageFormat.Bmp)
        {
            using var stream = File.Create(path);
            BmpEncoder.Encode(bitmap, stream);
            return;
        }

        using var image = SKImage.FromBitmap(bitmap);

        // Some formats may not be supported, fall back to PNG
        var encoded = image.Encode(format, quality);
        if (encoded == null)
        {
            encoded = image.Encode(SKEncodedImageFormat.Png, 100);
        }

        using (encoded)
        using (var stream = File.OpenWrite(path))
        {
            encoded!.SaveTo(stream);
        }
    }

    /// <summary>
    /// Renders the template directly to a file using typed data.
    /// Format is determined by file extension (.png, .jpg, .jpeg, .bmp).
    /// </summary>
    /// <typeparam name="T">The data type implementing ITemplateData.</typeparam>
    /// <param name="renderer">The renderer.</param>
    /// <param name="template">The template to render.</param>
    /// <param name="data">The typed data.</param>
    /// <param name="path">The output file path.</param>
    /// <param name="quality">JPEG quality if applicable (0-100, default 90).</param>
    public static void RenderToFile<T>(this SkiaRenderer renderer, Template template, T data, string path, int quality = 90)
        where T : ITemplateData
    {
        RenderToFile(renderer, template, data.ToTemplateValue(), path, quality);
    }
}
