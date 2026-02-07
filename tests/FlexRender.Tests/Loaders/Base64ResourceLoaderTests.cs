using AwesomeAssertions;
using FlexRender.Configuration;
using FlexRender.Loaders;
using Xunit;

namespace FlexRender.Tests.Loaders;

/// <summary>
/// Unit tests for <see cref="Base64ResourceLoader"/>.
/// </summary>
public class Base64ResourceLoaderTests
{
    /// <summary>
    /// A minimal 1x1 red PNG image encoded in base64.
    /// </summary>
    private const string ValidBase64Png = "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mP8z8DwHwAFBQIAX8jx0gAAAABJRU5ErkJggg==";

    /// <summary>
    /// Creates a <see cref="Base64ResourceLoader"/> with the specified options.
    /// </summary>
    /// <param name="maxImageSize">The maximum image size in bytes.</param>
    /// <returns>A configured <see cref="Base64ResourceLoader"/> instance.</returns>
    private static Base64ResourceLoader CreateLoader(int maxImageSize = 10 * 1024 * 1024)
    {
        var options = new FlexRenderOptions
        {
            MaxImageSize = maxImageSize
        };
        return new Base64ResourceLoader(options);
    }

    /// <summary>
    /// Verifies that Load returns a valid stream when given a valid data URL with PNG image.
    /// </summary>
    [Fact]
    public async Task Load_WithValidDataUrl_ReturnsStream()
    {
        // Arrange
        var loader = CreateLoader();
        var dataUrl = $"data:image/png;base64,{ValidBase64Png}";

        // Act
        var stream = await loader.Load(dataUrl);

        // Assert
        stream.Should().NotBeNull();
        stream!.Length.Should().BeGreaterThan(0);

        // Verify PNG signature
        var buffer = new byte[8];
        await stream.ReadExactlyAsync(buffer.AsMemory(0, 8));
        buffer[0].Should().Be(0x89); // PNG signature starts with 0x89
        buffer[1].Should().Be(0x50); // 'P'
        buffer[2].Should().Be(0x4E); // 'N'
        buffer[3].Should().Be(0x47); // 'G'
    }

    /// <summary>
    /// Verifies that Load returns a valid stream for different media types.
    /// </summary>
    [Theory]
    [InlineData("data:image/png;base64,")]
    [InlineData("data:image/jpeg;base64,")]
    [InlineData("data:application/octet-stream;base64,")]
    [InlineData("data:text/plain;base64,")]
    public async Task Load_WithDifferentMediaTypes_ReturnsStream(string prefix)
    {
        // Arrange
        var loader = CreateLoader();
        var testData = Convert.ToBase64String("test data"u8.ToArray());
        var dataUrl = prefix + testData;

        // Act
        var stream = await loader.Load(dataUrl);

        // Assert
        stream.Should().NotBeNull();
    }

    /// <summary>
    /// Verifies that Load throws ArgumentException when the data URL is missing the comma separator.
    /// </summary>
    [Fact]
    public async Task Load_WithInvalidDataUrl_MissingComma_ThrowsArgumentException()
    {
        // Arrange
        var loader = CreateLoader();
        var invalidDataUrl = "data:image/png;base64ABC123"; // Missing comma

        // Act
        var act = () => loader.Load(invalidDataUrl);

        // Assert
        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("*Invalid data URL format*");
    }

    /// <summary>
    /// Verifies that Load throws ArgumentException when the base64 data is invalid.
    /// </summary>
    [Fact]
    public async Task Load_WithInvalidBase64Data_ThrowsArgumentException()
    {
        // Arrange
        var loader = CreateLoader();
        var invalidDataUrl = "data:image/png;base64,not-valid-base64!!!";

        // Act
        var act = () => loader.Load(invalidDataUrl);

        // Assert
        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("*Invalid base64 data*");
    }

    /// <summary>
    /// Verifies that Load throws ArgumentException when the data exceeds the maximum allowed size.
    /// </summary>
    [Fact]
    public async Task Load_WithTooLargeData_ThrowsArgumentException()
    {
        // Arrange
        var loader = CreateLoader(maxImageSize: 100); // Set max to 100 bytes
        var largeData = new byte[200];
        var largeBase64 = Convert.ToBase64String(largeData);
        var dataUrl = $"data:image/png;base64,{largeBase64}";

        // Act
        var act = () => loader.Load(dataUrl);

        // Assert
        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("*exceeds maximum allowed size*");
    }

    /// <summary>
    /// Verifies that CanHandle returns true for data URLs.
    /// </summary>
    [Theory]
    [InlineData("data:image/png;base64,ABC123")]
    [InlineData("data:text/plain,Hello")]
    [InlineData("DATA:image/png;base64,ABC123")]
    [InlineData("Data:Application/JSON,{}")]
    public void CanHandle_WithDataUrl_ReturnsTrue(string dataUrl)
    {
        // Arrange
        var loader = CreateLoader();

        // Act
        var result = loader.CanHandle(dataUrl);

        // Assert
        result.Should().BeTrue();
    }

    /// <summary>
    /// Verifies that CanHandle returns false for non-data URLs.
    /// </summary>
    [Theory]
    [InlineData("http://example.com/image.png")]
    [InlineData("https://example.com/image.png")]
    [InlineData("/path/to/file.png")]
    [InlineData("embedded://Assembly/Resource.png")]
    public void CanHandle_WithNonDataUrl_ReturnsFalse(string nonDataUrl)
    {
        // Arrange
        var loader = CreateLoader();

        // Act
        var result = loader.CanHandle(nonDataUrl);

        // Assert
        result.Should().BeFalse();
    }

    /// <summary>
    /// Verifies that CanHandle returns false for null or empty strings.
    /// </summary>
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void CanHandle_WithNullOrEmptyString_ReturnsFalse(string? uri)
    {
        // Arrange
        var loader = CreateLoader();

        // Act
        var result = loader.CanHandle(uri!);

        // Assert
        result.Should().BeFalse();
    }

    /// <summary>
    /// Verifies that Load returns null for URLs it cannot handle.
    /// </summary>
    [Fact]
    public async Task Load_WithNonDataUrl_ReturnsNull()
    {
        // Arrange
        var loader = CreateLoader();

        // Act
        var stream = await loader.Load("/path/to/file.png");

        // Assert
        stream.Should().BeNull();
    }

    /// <summary>
    /// Verifies that Load throws ArgumentNullException for null URI.
    /// </summary>
    [Fact]
    public async Task Load_WithNullUri_ThrowsArgumentNullException()
    {
        // Arrange
        var loader = CreateLoader();

        // Act
        var act = () => loader.Load(null!);

        // Assert
        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    /// <summary>
    /// Verifies that the Priority property returns the expected value.
    /// </summary>
    [Fact]
    public void Priority_ReturnsExpectedValue()
    {
        // Arrange
        var loader = CreateLoader();

        // Act & Assert
        loader.Priority.Should().Be(50);
    }

    /// <summary>
    /// Verifies that Load correctly handles data URLs with minimal content.
    /// </summary>
    [Fact]
    public async Task Load_WithMinimalValidDataUrl_ReturnsStream()
    {
        // Arrange
        var loader = CreateLoader();
        var minimalBase64 = Convert.ToBase64String("x"u8.ToArray()); // Single character
        var dataUrl = $"data:,{minimalBase64}";

        // Act
        var stream = await loader.Load(dataUrl);

        // Assert
        stream.Should().NotBeNull();
    }

    /// <summary>
    /// Verifies that Load respects cancellation token.
    /// </summary>
    [Fact]
    public async Task Load_WithCancelledToken_ThrowsOperationCanceledException()
    {
        // Arrange
        var loader = CreateLoader();
        var dataUrl = $"data:image/png;base64,{ValidBase64Png}";
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        // Act
        var act = () => loader.Load(dataUrl, cts.Token);

        // Assert
        await act.Should().ThrowAsync<OperationCanceledException>();
    }
}
