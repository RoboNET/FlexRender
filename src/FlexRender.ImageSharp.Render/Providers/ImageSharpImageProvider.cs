using FlexRender.Parsing.Ast;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

namespace FlexRender.ImageSharp.Providers;

/// <summary>
/// Loads and processes images using SixLabors.ImageSharp.
/// Supports file paths and base64 data URLs with fit modes (contain/cover/fill/none).
/// </summary>
/// <remarks>
/// <para>
/// This provider is the ImageSharp equivalent of <c>FlexRender.Providers.ImageProvider</c>.
/// It produces <see cref="Image{Rgba32}"/> instead of <c>SKBitmap</c>.
/// ImageSharp natively supports PNG, JPEG, BMP, GIF, WebP, and TIFF formats.
/// </para>
/// </remarks>
internal static class ImageSharpImageProvider
{
    private const string Base64Prefix = "data:";

    /// <summary>
    /// Maximum allowed size for base64 image data (10 MB).
    /// </summary>
    public const int MaxBase64DataSize = 10 * 1024 * 1024;

    /// <summary>
    /// Generates a processed image from the specified image element configuration.
    /// </summary>
    /// <param name="element">The image element configuration.</param>
    /// <param name="layoutWidth">Optional layout-computed width. Takes precedence over element.ImageWidth.</param>
    /// <param name="layoutHeight">Optional layout-computed height. Takes precedence over element.ImageHeight.</param>
    /// <returns>A new <see cref="Image{Rgba32}"/> containing the processed image. Caller owns disposal.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="element"/> is null.</exception>
    /// <exception cref="ArgumentException">Thrown when element source is empty or invalid.</exception>
    /// <exception cref="FileNotFoundException">Thrown when the source file does not exist.</exception>
    /// <exception cref="InvalidOperationException">Thrown when the image cannot be decoded.</exception>
    public static Image<Rgba32> Generate(
        ImageElement element,
        int? layoutWidth = null,
        int? layoutHeight = null)
    {
        return Generate(element, imageCache: null, layoutWidth, layoutHeight);
    }

    /// <summary>
    /// Generates a processed image from the specified image element configuration,
    /// optionally using a pre-loaded image cache for HTTP and other async sources.
    /// </summary>
    /// <param name="element">The image element configuration.</param>
    /// <param name="imageCache">
    /// Optional pre-loaded image cache. When provided, the cache is checked before
    /// falling back to inline loading (file and base64 only). This enables HTTP image
    /// support by pre-loading images asynchronously before synchronous rendering.
    /// </param>
    /// <param name="layoutWidth">Optional layout-computed width. Takes precedence over element.ImageWidth.</param>
    /// <param name="layoutHeight">Optional layout-computed height. Takes precedence over element.ImageHeight.</param>
    /// <returns>A new <see cref="Image{Rgba32}"/> containing the processed image. Caller owns disposal.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="element"/> is null.</exception>
    /// <exception cref="ArgumentException">Thrown when element source is empty or invalid.</exception>
    /// <exception cref="FileNotFoundException">Thrown when the source file does not exist.</exception>
    /// <exception cref="InvalidOperationException">Thrown when the image cannot be decoded.</exception>
    public static Image<Rgba32> Generate(
        ImageElement element,
        IReadOnlyDictionary<string, Image<Rgba32>>? imageCache,
        int? layoutWidth = null,
        int? layoutHeight = null)
    {
        ArgumentNullException.ThrowIfNull(element);

        if (string.IsNullOrEmpty(element.Src))
        {
            throw new ArgumentException("Image source cannot be empty.", nameof(element));
        }

        // Check pre-loaded cache first (used for HTTP and other async sources)
        if (imageCache is not null && imageCache.TryGetValue(element.Src, out var cached))
        {
            // Clone because ProcessImage caller disposes the source
            using var clone = cached.Clone();
            return ProcessImage(clone, element, layoutWidth, layoutHeight);
        }

        using var source = LoadImage(element.Src);
        return ProcessImage(source, element, layoutWidth, layoutHeight);
    }

    private static Image<Rgba32> LoadImage(string src)
    {
        if (src.StartsWith(Base64Prefix, StringComparison.OrdinalIgnoreCase))
        {
            return LoadFromBase64(src);
        }

        return LoadFromFile(src);
    }

    private static Image<Rgba32> LoadFromFile(string path)
    {
        var fullPath = Path.GetFullPath(path);

        if (!File.Exists(fullPath))
        {
            throw new FileNotFoundException($"Image file not found: {path}", path);
        }

        var fileInfo = new FileInfo(fullPath);
        if (fileInfo.Length > MaxBase64DataSize)
        {
            throw new ArgumentException(
                $"Image file exceeds maximum size of {MaxBase64DataSize / (1024 * 1024)} MB.", nameof(path));
        }

        return Image.Load<Rgba32>(fullPath);
    }

    private static Image<Rgba32> LoadFromBase64(string dataUrl)
    {
        var commaIndex = dataUrl.IndexOf(',');
        if (commaIndex == -1)
        {
            throw new ArgumentException("Invalid data URL format. Expected 'data:...,base64data'.", nameof(dataUrl));
        }

        var base64Data = dataUrl[(commaIndex + 1)..];

        var estimatedSize = (base64Data.Length * 3) / 4;
        if (estimatedSize > MaxBase64DataSize)
        {
            throw new ArgumentException(
                $"Base64 image data exceeds maximum allowed size of {MaxBase64DataSize} bytes.", nameof(dataUrl));
        }

        byte[] bytes;
        try
        {
            bytes = Convert.FromBase64String(base64Data);
        }
        catch (FormatException ex)
        {
            throw new ArgumentException("Invalid base64 data in data URL.", nameof(dataUrl), ex);
        }

        return Image.Load<Rgba32>(bytes);
    }

    private static Image<Rgba32> ProcessImage(
        Image<Rgba32> source,
        ImageElement element,
        int? layoutWidth,
        int? layoutHeight)
    {
        var targetWidth = layoutWidth ?? element.ImageWidth ?? source.Width;
        var targetHeight = layoutHeight ?? element.ImageHeight ?? source.Height;

        if (element.Fit == ImageFit.None)
        {
            return CreateCenteredCopy(source, targetWidth, targetHeight);
        }

        return CreateFittedImage(source, targetWidth, targetHeight, element.Fit);
    }

    private static Image<Rgba32> CreateCenteredCopy(Image<Rgba32> source, int targetWidth, int targetHeight)
    {
        var result = new Image<Rgba32>(targetWidth, targetHeight, new Rgba32(0, 0, 0, 0));

        var offsetX = (targetWidth - source.Width) / 2;
        var offsetY = (targetHeight - source.Height) / 2;

        // Copy source pixels into centered position
        for (var y = 0; y < source.Height; y++)
        {
            var destY = y + offsetY;
            if (destY < 0 || destY >= targetHeight) continue;

            for (var x = 0; x < source.Width; x++)
            {
                var destX = x + offsetX;
                if (destX < 0 || destX >= targetWidth) continue;
                result[destX, destY] = source[x, y];
            }
        }

        return result;
    }

    private static Image<Rgba32> CreateFittedImage(
        Image<Rgba32> source,
        int targetWidth,
        int targetHeight,
        ImageFit fit)
    {
        var result = new Image<Rgba32>(targetWidth, targetHeight, new Rgba32(0, 0, 0, 0));

        switch (fit)
        {
            case ImageFit.Fill:
            {
                // Stretch source to fill entire target
                using var resized = source.Clone(ctx => ctx.Resize(new ResizeOptions
                {
                    Size = new Size(targetWidth, targetHeight),
                    Mode = ResizeMode.Stretch
                }));
                CopyPixels(resized, result, 0, 0);
                break;
            }
            case ImageFit.Contain:
            {
                // Scale to fit within bounds, preserving aspect ratio
                var sourceAspect = source.Width / (float)source.Height;
                var targetAspect = targetWidth / (float)targetHeight;

                int destWidth, destHeight;
                if (sourceAspect > targetAspect)
                {
                    destWidth = targetWidth;
                    destHeight = (int)(targetWidth / sourceAspect);
                }
                else
                {
                    destHeight = targetHeight;
                    destWidth = (int)(targetHeight * sourceAspect);
                }

                using var resized = source.Clone(ctx => ctx.Resize(new ResizeOptions
                {
                    Size = new Size(destWidth, destHeight),
                    Mode = ResizeMode.Stretch
                }));

                var offsetX = (targetWidth - destWidth) / 2;
                var offsetY = (targetHeight - destHeight) / 2;
                CopyPixels(resized, result, offsetX, offsetY);
                break;
            }
            case ImageFit.Cover:
            {
                // Scale to cover bounds, center and crop
                var sourceAspect = source.Width / (float)source.Height;
                var targetAspect = targetWidth / (float)targetHeight;

                int cropWidth, cropHeight;
                if (sourceAspect > targetAspect)
                {
                    cropHeight = source.Height;
                    cropWidth = (int)(source.Height * targetAspect);
                }
                else
                {
                    cropWidth = source.Width;
                    cropHeight = (int)(source.Width / targetAspect);
                }

                var cropX = (source.Width - cropWidth) / 2;
                var cropY = (source.Height - cropHeight) / 2;

                using var cropped = source.Clone(ctx => ctx
                    .Crop(new Rectangle(cropX, cropY, cropWidth, cropHeight))
                    .Resize(new ResizeOptions
                    {
                        Size = new Size(targetWidth, targetHeight),
                        Mode = ResizeMode.Stretch
                    }));

                CopyPixels(cropped, result, 0, 0);
                break;
            }
        }

        return result;
    }

    private static void CopyPixels(Image<Rgba32> source, Image<Rgba32> dest, int offsetX, int offsetY)
    {
        for (var y = 0; y < source.Height; y++)
        {
            var destY = y + offsetY;
            if (destY < 0 || destY >= dest.Height) continue;

            for (var x = 0; x < source.Width; x++)
            {
                var destX = x + offsetX;
                if (destX < 0 || destX >= dest.Width) continue;
                dest[destX, destY] = source[x, y];
            }
        }
    }
}
