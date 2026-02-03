using FluentAssertions;
using FlexRender.Abstractions;
using FlexRender.Configuration;
using FlexRender.Loaders;
using SkiaSharp;
using Xunit;

namespace FlexRender.Tests.Loaders;

/// <summary>
/// Unit tests for <see cref="FontLoader"/>.
/// </summary>
public class FontLoaderTests
{
    /// <summary>
    /// Creates a <see cref="FontLoader"/> with the specified resource loaders.
    /// </summary>
    /// <param name="loaders">The resource loaders to use.</param>
    /// <returns>A configured <see cref="FontLoader"/> instance.</returns>
    private static FontLoader CreateFontLoader(params IResourceLoader[] loaders)
    {
        return new FontLoader(loaders);
    }

    /// <summary>
    /// Creates a <see cref="FontLoader"/> with default file and base64 loaders.
    /// </summary>
    /// <returns>A configured <see cref="FontLoader"/> instance with default loaders.</returns>
    private static FontLoader CreateDefaultFontLoader()
    {
        var options = new FlexRenderOptions();
        var fileLoader = new FileResourceLoader(options);
        var base64Loader = new Base64ResourceLoader(options);
        return new FontLoader([fileLoader, base64Loader]);
    }

    /// <summary>
    /// Verifies that Load returns a typeface when given a system font family name.
    /// </summary>
    [Fact]
    public async Task Load_WithSystemFont_ReturnsTypeface()
    {
        // Arrange
        var loader = CreateDefaultFontLoader();
        // Use a font that should be available on all systems
        var systemFont = GetAvailableSystemFont();

        // Act
        var typeface = await loader.Load(systemFont);

        // Assert
        typeface.Should().NotBeNull();
        typeface!.Dispose();
    }

    /// <summary>
    /// Verifies that Load falls back to system fonts for unknown font names.
    /// </summary>
    [Fact]
    public async Task Load_WithInvalidFont_ReturnsDefaultTypeface()
    {
        // Arrange
        var loader = CreateDefaultFontLoader();
        var invalidFontName = "NonExistentFontFamily_12345";

        // Act
        var typeface = await loader.Load(invalidFontName);

        // Assert
        // SKTypeface.FromFamilyName returns a default typeface for unknown fonts
        // It should not return null, but a fallback typeface
        typeface.Should().NotBeNull();
        typeface!.Dispose();
    }

    /// <summary>
    /// Verifies that Load throws ArgumentNullException for null input.
    /// </summary>
    [Fact]
    public async Task Load_WithNullUri_ThrowsArgumentNullException()
    {
        // Arrange
        var loader = CreateDefaultFontLoader();

        // Act
        var act = () => loader.Load(null!);

        // Assert
        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    /// <summary>
    /// Verifies that the constructor throws for null loaders.
    /// </summary>
    [Fact]
    public void Constructor_WithNullLoaders_ThrowsArgumentNullException()
    {
        // Act
        var act = () => new FontLoader(null!);

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }

    /// <summary>
    /// Verifies that Load correctly identifies font file extensions.
    /// </summary>
    [Theory]
    [InlineData("font.ttf")]
    [InlineData("font.otf")]
    [InlineData("font.ttc")]
    [InlineData("FONT.TTF")]
    [InlineData("font.OTF")]
    public async Task Load_WithFontFileExtension_AttemptsResourceLoading(string fontFileName)
    {
        // Arrange
        var loader = CreateDefaultFontLoader();

        // Act - This will fail because the file doesn't exist, but it verifies
        // the loader recognizes the extension and attempts to load
        var act = () => loader.Load(fontFileName);

        // Assert - Should throw FileNotFoundException because it tries file loading
        await act.Should().ThrowAsync<FileNotFoundException>();
    }

    /// <summary>
    /// Verifies that Load handles system fonts with different styles.
    /// </summary>
    [Theory]
    [InlineData("Arial")]
    [InlineData("Helvetica")]
    [InlineData("Times New Roman")]
    [InlineData("Courier New")]
    public async Task Load_WithCommonSystemFont_ReturnsTypeface(string fontName)
    {
        // Arrange
        var loader = CreateDefaultFontLoader();

        // Act
        var typeface = await loader.Load(fontName);

        // Assert
        // All font names should return a typeface (fallback if not available)
        typeface.Should().NotBeNull();
        typeface!.Dispose();
    }

    /// <summary>
    /// Verifies that Load correctly handles data URLs with font data.
    /// </summary>
    [Fact]
    public async Task Load_WithDataUrlContainingInvalidFontData_ThrowsInvalidOperationException()
    {
        // Arrange
        var loader = CreateDefaultFontLoader();
        var invalidFontData = Convert.ToBase64String("not a font"u8.ToArray());
        var dataUrl = $"data:font/ttf;base64,{invalidFontData}";

        // Act
        var act = () => loader.Load(dataUrl);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*Failed to load font*");
    }

    /// <summary>
    /// Verifies that Load works with empty loaders collection and falls back to system fonts.
    /// </summary>
    [Fact]
    public async Task Load_WithEmptyLoaders_FallsBackToSystemFont()
    {
        // Arrange
        var loader = CreateFontLoader(); // No loaders
        var systemFont = GetAvailableSystemFont();

        // Act
        var typeface = await loader.Load(systemFont);

        // Assert
        typeface.Should().NotBeNull();
        typeface!.Dispose();
    }

    /// <summary>
    /// Verifies that Load handles embedded:// URI scheme correctly.
    /// </summary>
    [Fact]
    public async Task Load_WithEmbeddedUri_AttemptsResourceLoading()
    {
        // Arrange - file loader cannot handle embedded:// URIs
        var options = new FlexRenderOptions();
        var fileLoader = new FileResourceLoader(options);
        var loader = CreateFontLoader(fileLoader);

        // Act
        // embedded:// is not handled by file loader, so it should fall back to system font
        var typeface = await loader.Load("embedded://SomeAssembly/font.ttf");

        // Assert - falls back to system font lookup which returns fallback typeface
        typeface.Should().NotBeNull();
        typeface!.Dispose();
    }

    /// <summary>
    /// Verifies that Load handles HTTP URI schemes correctly.
    /// </summary>
    [Fact]
    public async Task Load_WithHttpUri_AttemptsResourceLoading()
    {
        // Arrange - without HttpResourceLoader, this falls back to system font
        var options = new FlexRenderOptions();
        var fileLoader = new FileResourceLoader(options);
        var loader = CreateFontLoader(fileLoader);

        // Act
        // http:// is not handled by file loader, should fall back to system font
        var typeface = await loader.Load("http://example.com/font.ttf");

        // Assert - returns fallback typeface
        typeface.Should().NotBeNull();
        typeface!.Dispose();
    }

    /// <summary>
    /// Gets an available system font name for testing.
    /// </summary>
    /// <returns>A font family name that should be available on the system.</returns>
    private static string GetAvailableSystemFont()
    {
        // Try to find a common font that exists on most systems
        var commonFonts = new[] { "Arial", "Helvetica", "DejaVu Sans", "Liberation Sans", "sans-serif" };

        foreach (var fontName in commonFonts)
        {
            var typeface = SKTypeface.FromFamilyName(fontName);
            if (typeface != null && typeface.FamilyName.Contains(fontName.Split(' ')[0], StringComparison.OrdinalIgnoreCase))
            {
                typeface.Dispose();
                return fontName;
            }
            typeface?.Dispose();
        }

        // Fallback - will use system default
        return "Arial";
    }
}
