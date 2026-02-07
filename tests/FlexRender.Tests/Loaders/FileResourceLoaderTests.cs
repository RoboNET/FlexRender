using AwesomeAssertions;
using FlexRender.Configuration;
using FlexRender.Loaders;
using Xunit;

namespace FlexRender.Tests.Loaders;

/// <summary>
/// Unit tests for <see cref="FileResourceLoader"/>.
/// </summary>
public class FileResourceLoaderTests : IDisposable
{
    private readonly List<string> _tempFiles = [];

    /// <summary>
    /// Creates a temporary test file with the specified content.
    /// </summary>
    /// <param name="content">The content to write to the file.</param>
    /// <returns>The absolute path to the temporary file.</returns>
    private string CreateTempFile(string content = "test content")
    {
        var tempPath = Path.GetTempFileName();
        File.WriteAllText(tempPath, content);
        _tempFiles.Add(tempPath);
        return tempPath;
    }

    /// <summary>
    /// Creates a <see cref="FileResourceLoader"/> with the specified options.
    /// </summary>
    /// <param name="basePath">The base path for resolving relative paths.</param>
    /// <returns>A configured <see cref="FileResourceLoader"/> instance.</returns>
    private static FileResourceLoader CreateLoader(string? basePath = null)
    {
        var options = new FlexRenderOptions
        {
            BasePath = basePath
        };
        return new FileResourceLoader(options);
    }

    /// <summary>
    /// Verifies that Load returns a valid stream when given a valid file path.
    /// </summary>
    [Fact]
    public async Task Load_WithValidFile_ReturnsStream()
    {
        // Arrange
        var expectedContent = "Hello, World!";
        var filePath = CreateTempFile(expectedContent);
        var loader = CreateLoader();

        // Act
        var stream = await loader.Load(filePath);

        // Assert
        stream.Should().NotBeNull();
        using var reader = new StreamReader(stream!);
        var actualContent = await reader.ReadToEndAsync();
        actualContent.Should().Be(expectedContent);
    }

    /// <summary>
    /// Verifies that Load throws FileNotFoundException when given a non-existent file path.
    /// </summary>
    [Fact]
    public async Task Load_WithNonExistentFile_ThrowsFileNotFoundException()
    {
        // Arrange
        var loader = CreateLoader();
        var nonExistentPath = Path.Combine(Path.GetTempPath(), "nonexistent_file_12345.txt");

        // Act
        var act = () => loader.Load(nonExistentPath);

        // Assert
        await act.Should().ThrowAsync<FileNotFoundException>();
    }

    /// <summary>
    /// Verifies that Load throws ArgumentException when path traversal is detected.
    /// </summary>
    [Fact]
    public async Task Load_WithPathTraversal_ThrowsArgumentException()
    {
        // Arrange
        var loader = CreateLoader();
        var pathWithTraversal = "/some/path/../../../etc/passwd";

        // Act
        var act = () => loader.Load(pathWithTraversal);

        // Assert
        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("*path traversal*");
    }

    /// <summary>
    /// Verifies that path traversal in relative paths is detected.
    /// </summary>
    [Fact]
    public async Task Load_WithRelativePathTraversal_ThrowsArgumentException()
    {
        // Arrange
        var loader = CreateLoader(Path.GetTempPath());
        var pathWithTraversal = "../../../etc/passwd";

        // Act
        var act = () => loader.Load(pathWithTraversal);

        // Assert
        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("*path traversal*");
    }

    /// <summary>
    /// Verifies that CanHandle returns true for file paths.
    /// </summary>
    [Theory]
    [InlineData("/path/to/file.txt")]
    [InlineData("file.txt")]
    [InlineData("relative/path/image.png")]
    [InlineData("C:\\Windows\\file.txt")]
    public void CanHandle_WithFilePath_ReturnsTrue(string filePath)
    {
        // Arrange
        var loader = CreateLoader();

        // Act
        var result = loader.CanHandle(filePath);

        // Assert
        result.Should().BeTrue();
    }

    /// <summary>
    /// Verifies that CanHandle returns false for HTTP URLs.
    /// </summary>
    [Theory]
    [InlineData("http://example.com/image.png")]
    [InlineData("https://example.com/image.png")]
    [InlineData("HTTP://EXAMPLE.COM/IMAGE.PNG")]
    [InlineData("HTTPS://EXAMPLE.COM/IMAGE.PNG")]
    public void CanHandle_WithHttpUrl_ReturnsFalse(string httpUrl)
    {
        // Arrange
        var loader = CreateLoader();

        // Act
        var result = loader.CanHandle(httpUrl);

        // Assert
        result.Should().BeFalse();
    }

    /// <summary>
    /// Verifies that CanHandle returns false for data URLs.
    /// </summary>
    [Theory]
    [InlineData("data:image/png;base64,ABC123")]
    [InlineData("DATA:image/png;base64,ABC123")]
    public void CanHandle_WithDataUrl_ReturnsFalse(string dataUrl)
    {
        // Arrange
        var loader = CreateLoader();

        // Act
        var result = loader.CanHandle(dataUrl);

        // Assert
        result.Should().BeFalse();
    }

    /// <summary>
    /// Verifies that CanHandle returns false for embedded resource URLs.
    /// </summary>
    [Theory]
    [InlineData("embedded://Assembly/Resource.png")]
    [InlineData("EMBEDDED://Assembly/Resource.png")]
    public void CanHandle_WithEmbeddedUrl_ReturnsFalse(string embeddedUrl)
    {
        // Arrange
        var loader = CreateLoader();

        // Act
        var result = loader.CanHandle(embeddedUrl);

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
    /// Verifies that Load resolves relative paths using the configured base path.
    /// </summary>
    [Fact]
    public async Task Load_WithRelativePath_ResolvesFromBasePath()
    {
        // Arrange
        var tempDir = Path.GetTempPath();
        var fileName = $"test_file_{Guid.NewGuid()}.txt";
        var fullPath = Path.Combine(tempDir, fileName);
        var expectedContent = "relative path content";
        File.WriteAllText(fullPath, expectedContent);
        _tempFiles.Add(fullPath);

        var loader = CreateLoader(tempDir);

        // Act
        var stream = await loader.Load(fileName);

        // Assert
        stream.Should().NotBeNull();
        using var reader = new StreamReader(stream!);
        var actualContent = await reader.ReadToEndAsync();
        actualContent.Should().Be(expectedContent);
    }

    /// <summary>
    /// Verifies that Load returns null for URLs it cannot handle.
    /// </summary>
    [Fact]
    public async Task Load_WithHttpUrl_ReturnsNull()
    {
        // Arrange
        var loader = CreateLoader();

        // Act
        var stream = await loader.Load("http://example.com/image.png");

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
        loader.Priority.Should().Be(100);
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
    }
}
