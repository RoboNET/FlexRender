using AwesomeAssertions;
using FlexRender.Loaders;
using FlexRender.Providers;
using FlexRender.SvgElement.Svg.Providers;
using Xunit;
using SvgAstElement = FlexRender.Parsing.Ast.SvgElement;

namespace FlexRender.Tests.SvgElement;

/// <summary>
/// Unit tests for <see cref="SvgElementSvgProvider"/>.
/// </summary>
public sealed class SvgElementSvgProviderTests : IDisposable
{
    /// <summary>
    /// A minimal valid SVG document with a viewBox for testing.
    /// </summary>
    private const string SvgWithViewBox =
        """<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 100 100"><rect width="100" height="100" fill="red"/></svg>""";

    /// <summary>
    /// A minimal SVG without a viewBox attribute.
    /// </summary>
    private const string SvgWithoutViewBox =
        """<svg xmlns="http://www.w3.org/2000/svg" width="100" height="100"><rect width="100" height="100" fill="blue"/></svg>""";

    private readonly List<string> _tempFiles = [];

    /// <summary>
    /// Creates a temporary SVG file with the specified content.
    /// </summary>
    private string CreateTempSvgFile(string svgContent)
    {
        var tempPath = Path.GetTempFileName();
        var svgPath = Path.ChangeExtension(tempPath, ".svg");
        File.WriteAllText(svgPath, svgContent);
        _tempFiles.Add(tempPath);
        _tempFiles.Add(svgPath);
        return svgPath;
    }

    // ========================================================================
    // Interface Tests
    // ========================================================================

    [Fact]
    public void Provider_ImplementsISvgContentProvider()
    {
        // Arrange & Act
        var provider = new SvgElementSvgProvider();

        // Assert
        provider.Should().BeAssignableTo<ISvgContentProvider<SvgAstElement>>();
    }

    [Fact]
    public void Provider_ImplementsIResourceLoaderAware()
    {
        // Arrange & Act
        var provider = new SvgElementSvgProvider();

        // Assert
        Assert.IsAssignableFrom<IResourceLoaderAware>(provider);
    }

    // ========================================================================
    // Inline Content Tests
    // ========================================================================

    [Fact]
    public void GenerateSvgContent_WithInlineContent_ReturnsContent()
    {
        // Arrange
        var provider = new SvgElementSvgProvider();
        var element = new SvgAstElement { Content = SvgWithViewBox };

        // Act
        var result = provider.GenerateSvgContent(element, 200, 200);

        // Assert
        Assert.False(string.IsNullOrEmpty(result));
        Assert.Contains("""fill="red""", result);
    }

    [Fact]
    public void GenerateSvgContent_WithInlineContentNoViewBox_ReturnsContent()
    {
        // Arrange
        var provider = new SvgElementSvgProvider();
        var element = new SvgAstElement { Content = SvgWithoutViewBox };

        // Act
        var result = provider.GenerateSvgContent(element, 100, 100);

        // Assert
        Assert.False(string.IsNullOrEmpty(result));
        Assert.Contains("""fill="blue""", result);
    }

    // ========================================================================
    // File Loading Tests
    // ========================================================================

    [Fact]
    public void GenerateSvgContent_WithSrcAndNoLoaders_ThrowsInvalidOperation()
    {
        // Arrange -- no resource loaders configured, so file cannot be loaded
        var provider = new SvgElementSvgProvider();
        var element = new SvgAstElement { Src = "/nonexistent/file.svg" };

        // Act & Assert
        Assert.Throws<InvalidOperationException>(() => provider.GenerateSvgContent(element, 100, 100));
    }

    [Fact]
    public void GenerateSvgContent_WithSrcAndLoaders_LoadsAndReturns()
    {
        // Arrange
        var svgPath = CreateTempSvgFile(SvgWithViewBox);
        var provider = new SvgElementSvgProvider();

        var loader = new TestResourceLoader(svgPath, SvgWithViewBox);
        provider.SetResourceLoaders([loader]);

        var element = new SvgAstElement { Src = svgPath };

        // Act
        var result = provider.GenerateSvgContent(element, 200, 200);

        // Assert
        Assert.False(string.IsNullOrEmpty(result));
        Assert.Contains("""fill="red""", result);
    }

    // ========================================================================
    // Validation Tests
    // ========================================================================

    [Fact]
    public void GenerateSvgContent_WithNullElement_ThrowsArgumentNullException()
    {
        // Arrange
        var provider = new SvgElementSvgProvider();

        // Act
        var act = () => provider.GenerateSvgContent(null!, 100, 100);

        // Assert
        Assert.Throws<ArgumentNullException>(act);
    }

    [Fact]
    public void GenerateSvgContent_WithNoSrcOrContent_ThrowsArgumentException()
    {
        // Arrange
        var provider = new SvgElementSvgProvider();
        var element = new SvgAstElement();

        // Act
        var act = () => provider.GenerateSvgContent(element, 100, 100);

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithMessage("*src*content*");
    }

    [Fact]
    public void SetResourceLoaders_WithNull_ThrowsArgumentNullException()
    {
        // Arrange
        var provider = new SvgElementSvgProvider();

        // Act
        var act = () => provider.SetResourceLoaders(null!);

        // Assert
        Assert.Throws<ArgumentNullException>(act);
    }

    // ========================================================================
    // MaxSvgContentSize Tests (now on SvgContentLoader)
    // ========================================================================

    [Fact]
    public void MaxSvgContentSize_Is10MB()
    {
        // Assert
        Assert.Equal(10 * 1024 * 1024, SvgContentLoader.MaxSvgContentSize);
    }

    // ========================================================================
    // Cleanup
    // ========================================================================

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

    // ========================================================================
    // Test Helpers
    // ========================================================================

    /// <summary>
    /// Simple test resource loader that returns content for a specific URI.
    /// </summary>
    private sealed class TestResourceLoader : Abstractions.IResourceLoader
    {
        private readonly string _uri;
        private readonly string _content;

        public TestResourceLoader(string uri, string content)
        {
            _uri = uri;
            _content = content;
        }

        public int Priority => 100;

        public bool CanHandle(string uri) => uri == _uri;

        public Task<Stream?> Load(string uri, CancellationToken cancellationToken = default)
        {
            if (uri == _uri)
            {
                var stream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(_content));
                return Task.FromResult<Stream?>(stream);
            }

            return Task.FromResult<Stream?>(null);
        }
    }
}
