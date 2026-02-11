using FlexRender.Parsing.Ast;
using FlexRender.Providers;
using FlexRender.QrCode.Svg.Providers;
using Xunit;

namespace FlexRender.Tests.Providers;

/// <summary>
/// Tests for QrSvgProvider SVG content generation.
/// </summary>
public sealed class QrSvgProviderTests
{
    private readonly QrSvgProvider _provider = new();

    /// <summary>
    /// Verifies QrSvgProvider implements ISvgContentProvider.
    /// </summary>
    [Fact]
    public void QrSvgProvider_ImplementsISvgContentProvider()
    {
        Assert.IsAssignableFrom<ISvgContentProvider<QrElement>>(_provider);
    }

    /// <summary>
    /// Verifies SVG output contains a path element with the foreground color.
    /// </summary>
    [Fact]
    public void GenerateSvgContent_DefaultSettings_ContainsPathWithForeground()
    {
        var element = new QrElement
        {
            Data = "Hello",
            Foreground = "#000000"
        };

        var svg = _provider.GenerateSvgContent(element, 200f, 200f);

        Assert.Contains("<path", svg);
        Assert.Contains("fill=\"#000000\"", svg);
        Assert.Contains(" d=\"", svg);
    }

    /// <summary>
    /// Verifies SVG output contains a background rect when background is set.
    /// </summary>
    [Fact]
    public void GenerateSvgContent_WithBackground_ContainsBackgroundRect()
    {
        var element = new QrElement
        {
            Data = "Hello",
            Background = "#ffffff"
        };

        var svg = _provider.GenerateSvgContent(element, 200f, 200f);

        Assert.Contains("<rect", svg);
        Assert.Contains("fill=\"#ffffff\"", svg);
        Assert.Contains("width=\"200\"", svg);
        Assert.Contains("height=\"200\"", svg);
    }

    /// <summary>
    /// Verifies SVG output has no background rect when background is null.
    /// </summary>
    [Fact]
    public void GenerateSvgContent_NoBackground_NoBackgroundRect()
    {
        var element = new QrElement
        {
            Data = "Hello",
            Background = null!
        };

        var svg = _provider.GenerateSvgContent(element, 200f, 200f);

        Assert.DoesNotContain("<rect", svg);
    }

    /// <summary>
    /// Verifies SVG output uses custom foreground color.
    /// </summary>
    [Fact]
    public void GenerateSvgContent_CustomForeground_UsesColor()
    {
        var element = new QrElement
        {
            Data = "Color Test",
            Foreground = "#ff0000"
        };

        var svg = _provider.GenerateSvgContent(element, 100f, 100f);

        Assert.Contains("fill=\"#ff0000\"", svg);
    }

    /// <summary>
    /// Verifies path data uses horizontal run-length encoding (h commands).
    /// </summary>
    [Fact]
    public void GenerateSvgContent_PathData_UsesRunLengthEncoding()
    {
        var element = new QrElement
        {
            Data = "Test",
            Size = 100
        };

        var svg = _provider.GenerateSvgContent(element, 100f, 100f);

        // Path data should contain M (move) and h (horizontal line) commands
        Assert.Contains("M", svg);
        Assert.Contains("h", svg);
        Assert.Contains("v", svg);
    }

    /// <summary>
    /// Verifies all error correction levels produce valid SVG.
    /// </summary>
    [Theory]
    [InlineData(ErrorCorrectionLevel.L)]
    [InlineData(ErrorCorrectionLevel.M)]
    [InlineData(ErrorCorrectionLevel.Q)]
    [InlineData(ErrorCorrectionLevel.H)]
    public void GenerateSvgContent_AllErrorCorrectionLevels_ProducesValidSvg(ErrorCorrectionLevel level)
    {
        var element = new QrElement
        {
            Data = "ECC test",
            ErrorCorrection = level
        };

        var svg = _provider.GenerateSvgContent(element, 200f, 200f);

        Assert.Contains("<path", svg);
        Assert.Contains("d=\"", svg);
    }

    /// <summary>
    /// Verifies exception is thrown for null element.
    /// </summary>
    [Fact]
    public void GenerateSvgContent_NullElement_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => _provider.GenerateSvgContent(null!, 100f, 100f));
    }

    /// <summary>
    /// Verifies exception is thrown for empty data.
    /// </summary>
    [Fact]
    public void GenerateSvgContent_EmptyData_ThrowsArgumentException()
    {
        var element = new QrElement { Data = "" };

        Assert.Throws<ArgumentException>(() => _provider.GenerateSvgContent(element, 100f, 100f));
    }

    /// <summary>
    /// Verifies SVG uses invariant culture for float formatting (no comma decimals).
    /// </summary>
    [Fact]
    public void GenerateSvgContent_FloatFormatting_UsesInvariantCulture()
    {
        var element = new QrElement
        {
            Data = "Format test"
        };

        // Use a non-square size that will produce decimal module sizes
        var svg = _provider.GenerateSvgContent(element, 201f, 201f);

        // Verify no commas appear in numeric values (which would happen with
        // cultures that use comma as decimal separator)
        // The path data should use dots for decimals
        Assert.DoesNotContain(",", svg.Replace("fill=\"", "").Replace("\"", ""));
    }

    /// <summary>
    /// Verifies SVG output wraps content in a group element.
    /// </summary>
    [Fact]
    public void GenerateSvgContent_Output_WrappedInGroup()
    {
        var element = new QrElement
        {
            Data = "Group test"
        };

        var svg = _provider.GenerateSvgContent(element, 100f, 100f);

        Assert.StartsWith("<g>", svg);
        Assert.EndsWith("</g>", svg);
    }

    /// <summary>
    /// Verifies path data ends with z (closepath) for each sub-path.
    /// </summary>
    [Fact]
    public void GenerateSvgContent_PathData_ContainsClosePath()
    {
        var element = new QrElement
        {
            Data = "Close path test"
        };

        var svg = _provider.GenerateSvgContent(element, 100f, 100f);

        // Each horizontal run becomes a closed rectangle sub-path
        Assert.Contains("z", svg);
    }
}
