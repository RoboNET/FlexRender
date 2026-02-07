using AwesomeAssertions;
using FlexRender.Configuration;
using FlexRender.Loaders;
using FlexRender.Providers;
using FlexRender.SvgElement.Providers;
using Xunit;
using SvgAstElement = FlexRender.Parsing.Ast.SvgElement;

namespace FlexRender.Tests.Providers;

/// <summary>
/// Unit tests for <see cref="SvgElementProvider"/>.
/// </summary>
public class SvgElementProviderTests : IDisposable
{
    /// <summary>
    /// A minimal valid SVG document for testing.
    /// </summary>
    private const string MinimalSvg =
        """<svg xmlns="http://www.w3.org/2000/svg" width="100" height="100"><rect width="100" height="100" fill="red"/></svg>""";

    private readonly List<string> _tempFiles = [];

    /// <summary>
    /// Creates a temporary SVG file with the specified content.
    /// </summary>
    /// <param name="svgContent">The SVG content to write.</param>
    /// <returns>The path to the temporary SVG file.</returns>
    private string CreateTempSvgFile(string svgContent)
    {
        var tempPath = Path.GetTempFileName();
        var svgPath = Path.ChangeExtension(tempPath, ".svg");
        File.WriteAllText(svgPath, svgContent);
        _tempFiles.Add(tempPath);
        _tempFiles.Add(svgPath);
        return svgPath;
    }

    /// <summary>
    /// Creates a provider with default file and base64 resource loaders.
    /// </summary>
    /// <returns>A configured <see cref="SvgElementProvider"/> with resource loaders.</returns>
    private static SvgElementProvider CreateProviderWithLoaders()
    {
        var options = new FlexRenderOptions();
        var fileLoader = new FileResourceLoader(options);
        var base64Loader = new Base64ResourceLoader(options);
        var provider = new SvgElementProvider();
        provider.SetResourceLoaders([base64Loader, fileLoader]);
        return provider;
    }

    /// <summary>
    /// Creates a provider without resource loaders (fallback mode).
    /// </summary>
    /// <returns>A <see cref="SvgElementProvider"/> without resource loaders.</returns>
    private static SvgElementProvider CreateProviderWithoutLoaders()
    {
        return new SvgElementProvider();
    }

    // ========================================================================
    // Inline Content Tests
    // ========================================================================

    /// <summary>
    /// Verifies that Generate creates a bitmap from inline SVG content.
    /// </summary>
    [Fact]
    public void Generate_WithInlineContent_CreatesBitmap()
    {
        // Arrange
        var provider = CreateProviderWithoutLoaders();
        var element = new SvgAstElement
        {
            Content = MinimalSvg,
            SvgWidth = 50,
            SvgHeight = 50
        };

        // Act
        var result = provider.Generate(element, 50, 50);

        // Assert
        result.PngBytes.Should().NotBeEmpty();
        result.Width.Should().Be(50);
        result.Height.Should().Be(50);
    }

    /// <summary>
    /// Verifies that inline content works identically with or without resource loaders.
    /// </summary>
    [Fact]
    public void Generate_WithInlineContent_WorksWithLoaders()
    {
        // Arrange
        var provider = CreateProviderWithLoaders();
        var element = new SvgAstElement
        {
            Content = MinimalSvg,
            SvgWidth = 80,
            SvgHeight = 80
        };

        // Act
        var result = provider.Generate(element, 80, 80);

        // Assert
        result.PngBytes.Should().NotBeEmpty();
        result.Width.Should().Be(80);
        result.Height.Should().Be(80);
    }

    // ========================================================================
    // File Loading via Resource Loaders
    // ========================================================================

    /// <summary>
    /// Verifies that Generate loads SVG from a file path through resource loaders.
    /// </summary>
    [Fact]
    public void Generate_WithFileSrc_LoadsThroughResourceLoaders()
    {
        // Arrange
        var svgPath = CreateTempSvgFile(MinimalSvg);
        var provider = CreateProviderWithLoaders();
        var element = new SvgAstElement
        {
            Src = svgPath,
            SvgWidth = 60,
            SvgHeight = 60
        };

        // Act
        var result = provider.Generate(element, 60, 60);

        // Assert
        result.PngBytes.Should().NotBeEmpty();
        result.Width.Should().Be(60);
        result.Height.Should().Be(60);
    }

    // ========================================================================
    // Base64 Data URI Loading
    // ========================================================================

    /// <summary>
    /// Verifies that Generate loads SVG from a base64 data URI through resource loaders.
    /// </summary>
    [Fact]
    public void Generate_WithBase64DataUri_LoadsThroughResourceLoaders()
    {
        // Arrange
        var svgBytes = System.Text.Encoding.UTF8.GetBytes(MinimalSvg);
        var base64 = Convert.ToBase64String(svgBytes);
        var dataUri = $"data:image/svg+xml;base64,{base64}";

        var provider = CreateProviderWithLoaders();
        var element = new SvgAstElement
        {
            Src = dataUri,
            SvgWidth = 40,
            SvgHeight = 40
        };

        // Act
        var result = provider.Generate(element, 40, 40);

        // Assert
        result.PngBytes.Should().NotBeEmpty();
        result.Width.Should().Be(40);
        result.Height.Should().Be(40);
    }

    // ========================================================================
    // Fallback to Direct File Loading
    // ========================================================================

    /// <summary>
    /// Verifies that Generate falls back to direct file loading when no resource loaders are configured.
    /// </summary>
    [Fact]
    public void Generate_WithFileSrc_FallsBackToDirectLoadingWithoutLoaders()
    {
        // Arrange
        var svgPath = CreateTempSvgFile(MinimalSvg);
        var provider = CreateProviderWithoutLoaders();
        var element = new SvgAstElement
        {
            Src = svgPath,
            SvgWidth = 70,
            SvgHeight = 70
        };

        // Act
        var result = provider.Generate(element, 70, 70);

        // Assert
        result.PngBytes.Should().NotBeEmpty();
        result.Width.Should().Be(70);
        result.Height.Should().Be(70);
    }

    /// <summary>
    /// Verifies that Generate falls back to direct file loading when loaders list is empty.
    /// </summary>
    [Fact]
    public void Generate_WithFileSrc_FallsBackWhenEmptyLoadersList()
    {
        // Arrange
        var svgPath = CreateTempSvgFile(MinimalSvg);
        var provider = new SvgElementProvider();
        provider.SetResourceLoaders([]);
        var element = new SvgAstElement
        {
            Src = svgPath,
            SvgWidth = 50,
            SvgHeight = 50
        };

        // Act
        var result = provider.Generate(element, 50, 50);

        // Assert
        result.PngBytes.Should().NotBeEmpty();
        result.Width.Should().Be(50);
        result.Height.Should().Be(50);
    }

    // ========================================================================
    // IResourceLoaderAware Tests
    // ========================================================================

    /// <summary>
    /// Verifies that the provider implements <see cref="IResourceLoaderAware"/>.
    /// </summary>
    [Fact]
    public void Provider_ImplementsIResourceLoaderAware()
    {
        // Arrange & Act
        var provider = new SvgElementProvider();

        // Assert
        provider.Should().BeAssignableTo<IResourceLoaderAware>();
    }

    /// <summary>
    /// Verifies that SetResourceLoaders throws for null argument.
    /// </summary>
    [Fact]
    public void SetResourceLoaders_WithNull_ThrowsArgumentNullException()
    {
        // Arrange
        var provider = new SvgElementProvider();

        // Act
        var act = () => provider.SetResourceLoaders(null!);

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }

    // ========================================================================
    // Validation Tests
    // ========================================================================

    /// <summary>
    /// Verifies that Generate throws for null element.
    /// </summary>
    [Fact]
    public void Generate_WithNullElement_ThrowsArgumentNullException()
    {
        // Arrange
        var provider = CreateProviderWithoutLoaders();

        // Act
        var act = () => provider.Generate(null!, 100, 100);

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }

    /// <summary>
    /// Verifies that Generate throws when neither Src nor Content is specified.
    /// </summary>
    [Fact]
    public void Generate_WithNoSrcOrContent_ThrowsArgumentException()
    {
        // Arrange
        var provider = CreateProviderWithoutLoaders();
        var element = new SvgAstElement();

        // Act
        var act = () => provider.Generate(element, 100, 100);

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithMessage("*src*content*");
    }

    /// <summary>
    /// Verifies that Generate throws for invalid SVG content.
    /// The Svg.Skia library throws <see cref="System.Xml.XmlException"/> for non-XML input.
    /// </summary>
    [Fact]
    public void Generate_WithInvalidSvgContent_ThrowsException()
    {
        // Arrange
        var provider = CreateProviderWithoutLoaders();
        var element = new SvgAstElement
        {
            Content = "this is not valid SVG"
        };

        // Act
        var act = () => provider.Generate(element, 100, 100);

        // Assert -- Svg.Skia throws XmlException for non-XML input
        act.Should().Throw<Exception>();
    }

    /// <summary>
    /// Verifies that the SVG content size limit has the expected value.
    /// </summary>
    [Fact]
    public void MaxSvgContentSize_Is10MB()
    {
        // Assert
        FlexRender.Loaders.SvgContentLoader.MaxSvgContentSize.Should().Be(10 * 1024 * 1024);
    }

    // ========================================================================
    // Cleanup
    // ========================================================================

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
