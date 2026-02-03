using FlexRender.Abstractions;
using FlexRender.Configuration;
using FlexRender.Parsing.Ast;
using FlexRender.Rendering;
using SkiaSharp;
using Xunit;

namespace FlexRender.Tests.Rendering;

/// <summary>
/// Tests for SkiaRenderer integration with IImageLoader.
/// </summary>
public sealed class SkiaRendererImageLoaderTests : IDisposable
{
    private readonly SkiaRenderer _renderer;

    public SkiaRendererImageLoaderTests()
    {
        _renderer = new SkiaRenderer(
            new ResourceLimits(),
            qrProvider: null,
            barcodeProvider: null,
            imageLoader: new FakeImageLoader());
    }

    /// <summary>
    /// Verifies that images are loaded via IImageLoader during render.
    /// </summary>
    [Fact]
    public async Task Render_WithImageLoader_LoadsImagesViaLoader()
    {
        var template = new Template
        {
            Canvas = new CanvasSettings { Width = 100, Height = 100, Fixed = FixedDimension.Both }
        };
        template.AddElement(new ImageElement
        {
            Src = "fake://red-50x50",
            ImageWidth = 50,
            ImageHeight = 50,
            Width = "50",
            Height = "50"
        });

        using var bitmap = await _renderer.Render(template, new ObjectValue());

        Assert.NotNull(bitmap);
        Assert.Equal(100, bitmap.Width);
        Assert.Equal(100, bitmap.Height);
    }

    /// <summary>
    /// Verifies that renderer works without IImageLoader (backward compatibility).
    /// </summary>
    [Fact]
    public async Task Render_WithoutImageLoader_UsesInlineLoading()
    {
        using var renderer = new SkiaRenderer(new ResourceLimits(), null, null, null);

        // Create a temp image for inline loading
        var tempPath = Path.GetTempFileName();
        var pngPath = Path.ChangeExtension(tempPath, ".png");
        try
        {
            using (var img = new SKBitmap(30, 20))
            {
                using var canvas = new SKCanvas(img);
                canvas.Clear(SKColors.Green);
                using var stream = File.OpenWrite(pngPath);
                img.Encode(stream, SKEncodedImageFormat.Png, 100);
            }

            var template = new Template
            {
                Canvas = new CanvasSettings { Width = 100, Height = 100, Fixed = FixedDimension.Both }
            };
            template.AddElement(new ImageElement
            {
                Src = pngPath,
                ImageWidth = 30,
                ImageHeight = 20,
                Width = "30",
                Height = "20"
            });

            using var bitmap = await renderer.Render(template, new ObjectValue());

            Assert.NotNull(bitmap);
            Assert.Equal(100, bitmap.Width);
        }
        finally
        {
            File.Delete(tempPath);
            File.Delete(pngPath);
        }
    }

    /// <summary>
    /// Disposes the renderer.
    /// </summary>
    public void Dispose()
    {
        _renderer.Dispose();
    }

    /// <summary>
    /// Fake image loader that returns colored bitmaps based on URI scheme.
    /// </summary>
    private sealed class FakeImageLoader : IImageLoader
    {
        /// <inheritdoc />
        public Task<SKBitmap?> Load(string uri, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(uri);

            if (!uri.StartsWith("fake://", StringComparison.Ordinal))
                return Task.FromResult<SKBitmap?>(null);

            var bitmap = new SKBitmap(50, 50);
            using var canvas = new SKCanvas(bitmap);
            canvas.Clear(SKColors.Red);

            return Task.FromResult<SKBitmap?>(bitmap);
        }
    }
}
