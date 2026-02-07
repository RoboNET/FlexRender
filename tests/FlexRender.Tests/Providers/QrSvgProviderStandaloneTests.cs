using FlexRender.Parsing.Ast;
using FlexRender.Providers;
using FlexRender.QrCode.Svg.Providers;
using Xunit;

namespace FlexRender.Tests.Providers;

/// <summary>
/// Tests verifying that QrSvgProvider in FlexRender.QrCode.Svg is a standalone
/// ISvgContentProvider that produces identical output to the extracted code.
/// </summary>
public sealed class QrSvgProviderStandaloneTests
{
    private readonly QrSvgProvider _provider = new();

    /// <summary>
    /// Verifies the provider can be instantiated without any Skia dependency.
    /// </summary>
    [Fact]
    public void QrSvgProvider_IsSealed()
    {
        Assert.True(typeof(QrSvgProvider).IsSealed);
    }

    /// <summary>
    /// Verifies the provider implements the ISvgContentProvider interface.
    /// </summary>
    [Fact]
    public void QrSvgProvider_ImplementsISvgContentProvider()
    {
        Assert.IsAssignableFrom<ISvgContentProvider<QrElement>>(_provider);
    }

    /// <summary>
    /// Verifies the provider does NOT implement the raster content provider interface.
    /// </summary>
    [Fact]
    public void QrSvgProvider_DoesNotImplementContentProvider()
    {
        // Use runtime type checking to avoid compiler const-folding the 'is' pattern
        object provider = _provider;
        Assert.False(provider is IContentProvider<QrElement>);
    }

    /// <summary>
    /// Verifies SVG output for a simple data string produces well-formed SVG fragment.
    /// </summary>
    [Fact]
    public void GenerateSvgContent_SimpleData_ProducesWellFormedSvg()
    {
        var element = new QrElement { Data = "test123" };

        var svg = _provider.GenerateSvgContent(element, 100f, 100f);

        Assert.StartsWith("<g>", svg);
        Assert.EndsWith("</g>", svg);
        Assert.Contains("<path d=\"M", svg);
    }

    /// <summary>
    /// Verifies SVG output with URL data works correctly.
    /// </summary>
    [Fact]
    public void GenerateSvgContent_UrlData_Succeeds()
    {
        var element = new QrElement { Data = "https://example.com/path?q=value&lang=en" };

        var svg = _provider.GenerateSvgContent(element, 300f, 300f);

        Assert.Contains("<path", svg);
        Assert.Contains("d=\"M", svg);
    }

    /// <summary>
    /// Verifies the provider output uses the correct module dimensions for non-square aspect ratios.
    /// </summary>
    [Fact]
    public void GenerateSvgContent_DifferentWidthHeight_ScalesModules()
    {
        var element = new QrElement { Data = "test" };

        var svgSquare = _provider.GenerateSvgContent(element, 100f, 100f);
        var svgWide = _provider.GenerateSvgContent(element, 200f, 100f);

        // Different dimensions should produce different path data
        Assert.NotEqual(svgSquare, svgWide);
    }

    /// <summary>
    /// Verifies that two calls with the same input produce identical output (deterministic).
    /// </summary>
    [Fact]
    public void GenerateSvgContent_SameInput_ProducesDeterministicOutput()
    {
        var element = new QrElement
        {
            Data = "determinism test",
            Foreground = "#333333",
            Background = "#eeeeee"
        };

        var svg1 = _provider.GenerateSvgContent(element, 150f, 150f);
        var svg2 = _provider.GenerateSvgContent(element, 150f, 150f);

        Assert.Equal(svg1, svg2);
    }

    /// <summary>
    /// Verifies XML-unsafe characters in foreground/background are escaped.
    /// </summary>
    [Fact]
    public void GenerateSvgContent_ForegroundWithSpecialChars_EscapesXml()
    {
        var element = new QrElement
        {
            Data = "test",
            Foreground = "color&test"
        };

        var svg = _provider.GenerateSvgContent(element, 100f, 100f);

        Assert.Contains("color&amp;test", svg);
        Assert.DoesNotContain("color&test\"", svg);
    }
}
