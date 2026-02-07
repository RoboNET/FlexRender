using AwesomeAssertions;
using FlexRender.Abstractions;
using FlexRender.Configuration;
using FlexRender.Loaders;
using SkiaSharp;
using Xunit;

namespace FlexRender.Tests.Loaders;

/// <summary>
/// Unit tests for <see cref="ImageLoader"/>.
/// </summary>
public class ImageLoaderTests : IDisposable
{
    /// <summary>
    /// A minimal 1x1 red PNG image encoded in base64.
    /// </summary>
    private const string ValidBase64Png = "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mP8z8DwHwAFBQIAX8jx0gAAAABJRU5ErkJggg==";

    private readonly List<string> _tempFiles = [];

    /// <summary>
    /// Creates a temporary test image file.
    /// </summary>
    /// <param name="width">Image width.</param>
    /// <param name="height">Image height.</param>
    /// <param name="color">Fill color.</param>
    /// <returns>Path to the temporary file.</returns>
    private string CreateTestImage(int width, int height, SKColor color)
    {
        var tempPath = Path.GetTempFileName();
        var pngPath = Path.ChangeExtension(tempPath, ".png");

        using var bitmap = new SKBitmap(width, height);
        using var canvas = new SKCanvas(bitmap);
        canvas.Clear(color);

        using var stream = File.OpenWrite(pngPath);
        bitmap.Encode(stream, SKEncodedImageFormat.Png, 100);

        _tempFiles.Add(tempPath);
        _tempFiles.Add(pngPath);

        return pngPath;
    }

    /// <summary>
    /// Creates an <see cref="ImageLoader"/> with the specified resource loaders.
    /// </summary>
    /// <param name="loaders">The resource loaders to use.</param>
    /// <param name="options">Optional configuration options. If null, default options are used.</param>
    /// <returns>A configured <see cref="ImageLoader"/> instance.</returns>
    private static ImageLoader CreateImageLoader(FlexRenderOptions? options = null, params IResourceLoader[] loaders)
    {
        options ??= new FlexRenderOptions();
        return new ImageLoader(loaders, options);
    }

    /// <summary>
    /// Creates an <see cref="ImageLoader"/> with default file and base64 loaders.
    /// </summary>
    /// <param name="options">Optional configuration options. If null, default options are used.</param>
    /// <returns>A configured <see cref="ImageLoader"/> instance with default loaders.</returns>
    private static ImageLoader CreateDefaultImageLoader(FlexRenderOptions? options = null)
    {
        options ??= new FlexRenderOptions();
        var fileLoader = new FileResourceLoader(options);
        var base64Loader = new Base64ResourceLoader(options);
        return new ImageLoader([fileLoader, base64Loader], options);
    }

    /// <summary>
    /// Verifies that Load returns a valid bitmap when given a valid image file path.
    /// </summary>
    [Fact]
    public async Task Load_WithValidImage_ReturnsBitmap()
    {
        // Arrange
        var imagePath = CreateTestImage(100, 50, SKColors.Red);
        var loader = CreateDefaultImageLoader();

        // Act
        var bitmap = await loader.Load(imagePath);

        // Assert
        bitmap.Should().NotBeNull();
        bitmap!.Width.Should().Be(100);
        bitmap.Height.Should().Be(50);
        bitmap.Dispose();
    }

    /// <summary>
    /// Verifies that Load returns a valid bitmap when given a valid base64 data URL.
    /// </summary>
    [Fact]
    public async Task Load_WithValidBase64DataUrl_ReturnsBitmap()
    {
        // Arrange
        var dataUrl = $"data:image/png;base64,{ValidBase64Png}";
        var loader = CreateDefaultImageLoader();

        // Act
        var bitmap = await loader.Load(dataUrl);

        // Assert
        bitmap.Should().NotBeNull();
        bitmap!.Width.Should().Be(1);
        bitmap.Height.Should().Be(1);
        bitmap.Dispose();
    }

    /// <summary>
    /// Verifies that Load returns null when no loader can handle the URI.
    /// </summary>
    [Fact]
    public async Task Load_WithInvalidUri_ReturnsNull()
    {
        // Arrange - use loader with no registered resource loaders that can handle this
        var options = new FlexRenderOptions();
        var base64Loader = new Base64ResourceLoader(options); // Only base64 loader
        var loader = CreateImageLoader(options, base64Loader);

        // Act - this is not a data URL, so base64 loader cannot handle it
        var bitmap = await loader.Load("http://example.com/image.png");

        // Assert
        bitmap.Should().BeNull();
    }

    /// <summary>
    /// Verifies that Load throws InvalidOperationException when the image cannot be decoded.
    /// </summary>
    [Fact]
    public async Task Load_WithNonImageData_ThrowsInvalidOperationException()
    {
        // Arrange
        var loader = CreateDefaultImageLoader();
        var nonImageData = Convert.ToBase64String("not an image"u8.ToArray());
        var dataUrl = $"data:image/png;base64,{nonImageData}";

        // Act
        var act = () => loader.Load(dataUrl);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*Failed to decode image*");
    }

    /// <summary>
    /// Verifies that Load throws ArgumentNullException for null URI.
    /// </summary>
    [Fact]
    public async Task Load_WithNullUri_ThrowsArgumentNullException()
    {
        // Arrange
        var loader = CreateDefaultImageLoader();

        // Act
        var act = () => loader.Load(null!);

        // Assert
        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    /// <summary>
    /// Verifies that Load uses loaders in priority order.
    /// </summary>
    [Fact]
    public async Task Load_UsesLoadersInPriorityOrder()
    {
        // Arrange
        var imagePath = CreateTestImage(50, 50, SKColors.Blue);
        var options = new FlexRenderOptions();

        // Base64 loader has priority 50, File loader has priority 100
        var base64Loader = new Base64ResourceLoader(options);
        var fileLoader = new FileResourceLoader(options);

        // Add in reverse order to verify sorting by priority
        var loader = CreateImageLoader(options, fileLoader, base64Loader);

        // Act
        var bitmap = await loader.Load(imagePath);

        // Assert - file loader should handle the file path since base64 cannot
        bitmap.Should().NotBeNull();
        bitmap!.Width.Should().Be(50);
        bitmap.Height.Should().Be(50);
        bitmap.Dispose();
    }

    /// <summary>
    /// Verifies that Load handles larger images correctly.
    /// </summary>
    [Fact]
    public async Task Load_WithLargerImage_ReturnsBitmap()
    {
        // Arrange
        var imagePath = CreateTestImage(800, 600, SKColors.Green);
        var loader = CreateDefaultImageLoader();

        // Act
        var bitmap = await loader.Load(imagePath);

        // Assert
        bitmap.Should().NotBeNull();
        bitmap!.Width.Should().Be(800);
        bitmap.Height.Should().Be(600);
        bitmap.Dispose();
    }

    /// <summary>
    /// Verifies that the constructor throws for null loaders.
    /// </summary>
    [Fact]
    public void Constructor_WithNullLoaders_ThrowsArgumentNullException()
    {
        // Arrange
        var options = new FlexRenderOptions();

        // Act
        var act = () => new ImageLoader(null!, options);

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }

    /// <summary>
    /// Verifies that the constructor throws for null options.
    /// </summary>
    [Fact]
    public void Constructor_WithNullOptions_ThrowsArgumentNullException()
    {
        // Act
        var act = () => new ImageLoader([], null!);

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }

    /// <summary>
    /// Verifies that Load works with an empty loaders collection (returns null).
    /// </summary>
    [Fact]
    public async Task Load_WithEmptyLoaders_ReturnsNull()
    {
        // Arrange
        var loader = CreateImageLoader(); // No loaders

        // Act
        var bitmap = await loader.Load("/path/to/image.png");

        // Assert
        bitmap.Should().BeNull();
    }

    /// <summary>
    /// Verifies that Load returns null for empty URI.
    /// </summary>
    [Fact]
    public async Task Load_WithEmptyUri_ReturnsNull()
    {
        // Arrange
        var loader = CreateDefaultImageLoader();

        // Act
        var bitmap = await loader.Load("");

        // Assert
        bitmap.Should().BeNull();
    }

    /// <summary>
    /// Verifies that Load returns null for whitespace URI.
    /// </summary>
    [Fact]
    public async Task Load_WithWhitespaceUri_ReturnsNull()
    {
        // Arrange
        var loader = CreateDefaultImageLoader();

        // Act
        var bitmap = await loader.Load("   ");

        // Assert
        bitmap.Should().BeNull();
    }

    /// <summary>
    /// Verifies that Load throws InvalidOperationException when image exceeds MaxImageSize.
    /// </summary>
    [Fact]
    public async Task Load_WithImageExceedingMaxSize_ThrowsInvalidOperationException()
    {
        // Arrange - create options with a very small max size
        var options = new FlexRenderOptions { MaxImageSize = 100 };
        var imagePath = CreateTestImage(100, 100, SKColors.Red); // This will exceed 100 bytes
        var loader = CreateDefaultImageLoader(options);

        // Act
        var act = () => loader.Load(imagePath);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*exceeds maximum allowed size*");
    }

    /// <summary>
    /// Verifies that Load respects CancellationToken.
    /// </summary>
    [Fact]
    public async Task Load_WithCancelledToken_ThrowsOperationCanceledException()
    {
        // Arrange
        var loader = CreateDefaultImageLoader();
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        // Act
        var act = () => loader.Load("/path/to/image.png", cts.Token);

        // Assert
        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    /// <summary>
    /// Cleans up temporary files created during tests.
    /// </summary>
    public void Dispose()
    {
        foreach (var file in _tempFiles)
        {
            try
            {
                if (File.Exists(file))
                {
                    File.Delete(file);
                }
            }
            catch
            {
                // Ignore cleanup errors
            }
        }

        GC.SuppressFinalize(this);
    }
}
