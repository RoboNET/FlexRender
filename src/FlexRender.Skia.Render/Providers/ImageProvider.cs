using FlexRender.Parsing.Ast;
using SkiaSharp;

namespace FlexRender.Providers;

/// <summary>
/// Provides image loading and processing from files and base64 data URLs.
/// </summary>
public sealed class ImageProvider
{
    private const string Base64Prefix = "data:";

    /// <summary>
    /// Maximum allowed size for base64 image data (10 MB).
    /// </summary>
    public const int MaxBase64DataSize = 10 * 1024 * 1024;

    /// <summary>
    /// Generates a bitmap from the specified image element configuration.
    /// Satisfies the <see cref="IContentProvider{TElement}"/> interface contract.
    /// </summary>
    /// <param name="element">The image element configuration.</param>
    /// <returns>A bitmap containing the loaded and processed image.</returns>
    /// <exception cref="ArgumentNullException">Thrown when element is null.</exception>
    /// <exception cref="ArgumentException">Thrown when element source is empty.</exception>
    /// <exception cref="FileNotFoundException">Thrown when the source file does not exist.</exception>
    /// <exception cref="InvalidOperationException">Thrown when the image cannot be decoded.</exception>
    public static SKBitmap Generate(ImageElement element) => Generate(element, imageCache: null);

    /// <summary>
    /// Generates a bitmap from the specified image element configuration,
    /// optionally using a pre-loaded image cache for thread-safe rendering.
    /// </summary>
    /// <param name="element">The image element configuration.</param>
    /// <param name="imageCache">
    /// Optional pre-loaded image cache. When provided, the cache is checked before falling back to inline loading.
    /// This parameter is passed per-call to ensure thread safety when the renderer is shared across concurrent renders.
    /// </param>
    /// <param name="antialiasing">Whether to enable antialiasing for image scaling.</param>
    /// <param name="layoutWidth">
    /// Optional width computed by the layout engine. Used as fallback when ImageWidth is not specified.
    /// Priority: layoutWidth > ImageWidth > intrinsic width.
    /// </param>
    /// <param name="layoutHeight">
    /// Optional height computed by the layout engine. Used as fallback when ImageHeight is not specified.
    /// Priority: layoutHeight > ImageHeight > intrinsic height.
    /// </param>
    /// <returns>A bitmap containing the loaded and processed image.</returns>
    /// <exception cref="ArgumentNullException">Thrown when element is null.</exception>
    /// <exception cref="ArgumentException">Thrown when element source is empty.</exception>
    /// <exception cref="FileNotFoundException">Thrown when the source file does not exist.</exception>
    /// <exception cref="InvalidOperationException">Thrown when the image cannot be decoded.</exception>
    public static SKBitmap Generate(ImageElement element, IReadOnlyDictionary<string, SKBitmap>? imageCache, bool antialiasing = true, int? layoutWidth = null, int? layoutHeight = null)
    {
        ArgumentNullException.ThrowIfNull(element);

        if (string.IsNullOrEmpty(element.Src.Value))
        {
            throw new ArgumentException("Image source cannot be empty.", nameof(element));
        }

        // Check pre-loaded cache first (used when IImageLoader is available)
        if (imageCache is not null && imageCache.TryGetValue(element.Src.Value, out var cached))
        {
            // Clone because ProcessImage caller disposes the source
            var clone = cached.Copy();
            try
            {
                return ProcessImage(clone, element, antialiasing, layoutWidth, layoutHeight);
            }
            finally
            {
                clone.Dispose();
            }
        }

        var sourceBitmap = LoadImage(element.Src.Value);

        try
        {
            return ProcessImage(sourceBitmap, element, antialiasing, layoutWidth, layoutHeight);
        }
        finally
        {
            if (!ReferenceEquals(sourceBitmap, null))
            {
                sourceBitmap.Dispose();
            }
        }
    }

    /// <summary>
    /// Loads an image from a file path or base64 data URL.
    /// </summary>
    /// <param name="src">The image source (file path or data URL).</param>
    /// <returns>The loaded bitmap.</returns>
    private static SKBitmap LoadImage(string src)
    {
        if (src.StartsWith(Base64Prefix, StringComparison.OrdinalIgnoreCase))
        {
            return LoadFromBase64(src);
        }

        return LoadFromFile(src);
    }

    /// <summary>
    /// Loads an image from a file path.
    /// </summary>
    /// <param name="path">The file path.</param>
    /// <returns>The loaded bitmap.</returns>
    /// <exception cref="ArgumentException">Thrown when path traversal is detected.</exception>
    private static SKBitmap LoadFromFile(string path)
    {
        // Validate path to prevent path traversal attacks
        if (path.Contains(".."))
        {
            throw new ArgumentException($"Invalid image path (path traversal detected): {path}", nameof(path));
        }

        var fullPath = Path.GetFullPath(path);

        if (!File.Exists(fullPath))
        {
            throw new FileNotFoundException($"Image file not found: {path}", path);
        }

        using var stream = File.OpenRead(fullPath);
        var bitmap = SKBitmap.Decode(stream);

        if (bitmap == null)
        {
            throw new InvalidOperationException($"Failed to decode image from file: {path}");
        }

        return bitmap;
    }

    /// <summary>
    /// Loads an image from a base64 data URL.
    /// </summary>
    /// <param name="dataUrl">The data URL (e.g., "data:image/png;base64,...").</param>
    /// <returns>The loaded bitmap.</returns>
    private static SKBitmap LoadFromBase64(string dataUrl)
    {
        // Find the base64 data portion
        var commaIndex = dataUrl.IndexOf(',');
        if (commaIndex == -1)
        {
            throw new ArgumentException("Invalid data URL format. Expected 'data:...,base64data'.", nameof(dataUrl));
        }

        var base64Data = dataUrl[(commaIndex + 1)..];

        // Validate size before decoding to prevent memory exhaustion attacks
        // Base64 encoding is approximately 33% larger than the decoded data
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

        var bitmap = SKBitmap.Decode(bytes);

        if (bitmap == null)
        {
            throw new InvalidOperationException("Failed to decode image from base64 data.");
        }

        return bitmap;
    }

    /// <summary>
    /// Processes the source image according to the element's fit mode and dimensions.
    /// </summary>
    /// <param name="source">The source bitmap.</param>
    /// <param name="element">The image element configuration.</param>
    /// <param name="antialiasing">Whether to enable antialiasing for image scaling.</param>
    /// <param name="layoutWidth">Optional width from layout engine.</param>
    /// <param name="layoutHeight">Optional height from layout engine.</param>
    /// <returns>The processed bitmap.</returns>
    private static SKBitmap ProcessImage(SKBitmap source, ImageElement element, bool antialiasing, int? layoutWidth = null, int? layoutHeight = null)
    {
        var targetWidth = layoutWidth ?? element.ImageWidth.Value ?? source.Width;
        var targetHeight = layoutHeight ?? element.ImageHeight.Value ?? source.Height;

        if (element.Fit.Value == ImageFit.None)
        {
            // Return a copy at the target size, centered
            return CreateCenteredCopy(source, targetWidth, targetHeight);
        }

        var (sourceRect, destRect) = CalculateFitRects(source, targetWidth, targetHeight, element.Fit.Value);

        var result = new SKBitmap(targetWidth, targetHeight);
        using var canvas = new SKCanvas(result);
        canvas.Clear(SKColors.Transparent);

        using var paint = new SKPaint
        {
            IsAntialias = antialiasing
        };

        using var image = SKImage.FromBitmap(source);
        canvas.DrawImage(image, sourceRect, destRect, new SKSamplingOptions(SKCubicResampler.Mitchell), paint);

        return result;
    }

    /// <summary>
    /// Creates a copy of the source image centered in the target dimensions.
    /// </summary>
    /// <param name="source">The source bitmap.</param>
    /// <param name="targetWidth">The target width.</param>
    /// <param name="targetHeight">The target height.</param>
    /// <returns>The centered copy.</returns>
    private static SKBitmap CreateCenteredCopy(SKBitmap source, int targetWidth, int targetHeight)
    {
        var result = new SKBitmap(targetWidth, targetHeight);
        using var canvas = new SKCanvas(result);
        canvas.Clear(SKColors.Transparent);

        var x = (targetWidth - source.Width) / 2f;
        var y = (targetHeight - source.Height) / 2f;

        canvas.DrawBitmap(source, x, y);

        return result;
    }

    /// <summary>
    /// Calculates source and destination rectangles for the specified fit mode.
    /// </summary>
    /// <param name="source">The source bitmap.</param>
    /// <param name="targetWidth">The target width.</param>
    /// <param name="targetHeight">The target height.</param>
    /// <param name="fit">The fit mode.</param>
    /// <returns>A tuple of (source rect, destination rect).</returns>
    private static (SKRect Source, SKRect Dest) CalculateFitRects(
        SKBitmap source,
        int targetWidth,
        int targetHeight,
        ImageFit fit)
    {
        var sourceRect = new SKRect(0, 0, source.Width, source.Height);

        if (fit == ImageFit.Fill)
        {
            // Stretch to fill entire target
            var destRect = new SKRect(0, 0, targetWidth, targetHeight);
            return (sourceRect, destRect);
        }

        var sourceAspect = source.Width / (float)source.Height;
        var targetAspect = targetWidth / (float)targetHeight;

        if (fit == ImageFit.Contain)
        {
            // Scale to fit within bounds, preserving aspect ratio
            float destWidth, destHeight;

            if (sourceAspect > targetAspect)
            {
                // Source is wider - fit to width
                destWidth = targetWidth;
                destHeight = targetWidth / sourceAspect;
            }
            else
            {
                // Source is taller - fit to height
                destHeight = targetHeight;
                destWidth = targetHeight * sourceAspect;
            }

            var x = (targetWidth - destWidth) / 2;
            var y = (targetHeight - destHeight) / 2;
            var destRect = new SKRect(x, y, x + destWidth, y + destHeight);

            return (sourceRect, destRect);
        }

        if (fit == ImageFit.Cover)
        {
            // Scale to cover bounds, may crop
            var destRect = new SKRect(0, 0, targetWidth, targetHeight);

            float cropWidth, cropHeight;

            if (sourceAspect > targetAspect)
            {
                // Source is wider - crop horizontally
                cropHeight = source.Height;
                cropWidth = source.Height * targetAspect;
            }
            else
            {
                // Source is taller - crop vertically
                cropWidth = source.Width;
                cropHeight = source.Width / targetAspect;
            }

            var x = (source.Width - cropWidth) / 2;
            var y = (source.Height - cropHeight) / 2;
            sourceRect = new SKRect(x, y, x + cropWidth, y + cropHeight);

            return (sourceRect, destRect);
        }

        // Default: no transformation
        return (sourceRect, new SKRect(0, 0, targetWidth, targetHeight));
    }
}
