using FlexRender.Barcode.Svg.Providers;
using FlexRender.Parsing.Ast;
using FlexRender.Providers;
using Xunit;

namespace FlexRender.Tests.Providers;

/// <summary>
/// Tests for BarcodeSvgProvider SVG content generation.
/// </summary>
public sealed class BarcodeSvgProviderTests
{
    private readonly BarcodeSvgProvider _provider = new();

    [Fact]
    public void BarcodeSvgProvider_ImplementsISvgContentProvider()
    {
        Assert.IsAssignableFrom<ISvgContentProvider<BarcodeElement>>(_provider);
    }

    [Fact]
    public void GenerateSvgContent_DefaultSettings_ContainsBarsWithForeground()
    {
        var element = new BarcodeElement
        {
            Data = "Hello",
            Foreground = "#000000"
        };

        var svg = _provider.GenerateSvgContent(element, 200f, 80f);

        Assert.Contains("<path", svg);
        Assert.Contains("fill=\"#000000\"", svg);
    }

    [Fact]
    public void GenerateSvgContent_WithBackground_ContainsBackgroundRect()
    {
        var element = new BarcodeElement
        {
            Data = "Hello",
            Background = "#ffffff"
        };

        var svg = _provider.GenerateSvgContent(element, 200f, 80f);

        Assert.Contains("fill=\"#ffffff\"", svg);
        Assert.Contains("width=\"200\"", svg);
        Assert.Contains("height=\"80\"", svg);
    }

    [Fact]
    public void GenerateSvgContent_NoBackground_NoBackgroundRect()
    {
        var element = new BarcodeElement
        {
            Data = "Hello",
            Background = null
        };

        var svg = _provider.GenerateSvgContent(element, 200f, 80f);

        // Should not contain a full-size background rect
        // (there will be bar rects but no rect matching full width+height with a non-foreground fill)
        Assert.DoesNotContain("fill=\"#ffffff\"", svg);
    }

    [Fact]
    public void GenerateSvgContent_CustomForeground_UsesColor()
    {
        var element = new BarcodeElement
        {
            Data = "Color Test",
            Foreground = "#ff0000"
        };

        var svg = _provider.GenerateSvgContent(element, 200f, 80f);

        Assert.Contains("fill=\"#ff0000\"", svg);
    }

    [Fact]
    public void GenerateSvgContent_ShowTextTrue_RendersBarPathWithoutText()
    {
        var element = new BarcodeElement
        {
            Data = "ABC123",
            ShowText = true,
            Foreground = "#000000"
        };

        var svg = _provider.GenerateSvgContent(element, 200f, 80f);

        // SVG provider does not render text -- ShowText has no effect on SVG output
        Assert.DoesNotContain("<text", svg);
        // Barcode bars are rendered as a single <path> element within a <g> wrapper
        Assert.Contains("<g>", svg);
        Assert.Contains("<path", svg);
        Assert.Contains("fill=\"#000000\"", svg);
    }

    [Fact]
    public void GenerateSvgContent_ShowTextFalse_NoTextElement()
    {
        var element = new BarcodeElement
        {
            Data = "ABC123",
            ShowText = false
        };

        var svg = _provider.GenerateSvgContent(element, 200f, 80f);

        Assert.DoesNotContain("<text", svg);
    }

    [Fact]
    public void GenerateSvgContent_Output_WrappedInGroup()
    {
        var element = new BarcodeElement
        {
            Data = "Group test"
        };

        var svg = _provider.GenerateSvgContent(element, 200f, 80f);

        Assert.StartsWith("<g>", svg);
        Assert.EndsWith("</g>", svg);
    }

    [Fact]
    public void GenerateSvgContent_NullElement_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => _provider.GenerateSvgContent(null!, 100f, 100f));
    }

    [Fact]
    public void GenerateSvgContent_EmptyData_ThrowsArgumentException()
    {
        var element = new BarcodeElement { Data = "" };

        Assert.Throws<ArgumentException>(() => _provider.GenerateSvgContent(element, 100f, 100f));
    }

    [Fact]
    public void GenerateSvgContent_UnsupportedFormat_ThrowsNotSupportedException()
    {
        var element = new BarcodeElement { Data = "1234567890123", Format = BarcodeFormat.Ean13 };

        Assert.Throws<NotSupportedException>(() => _provider.GenerateSvgContent(element, 200f, 80f));
    }

    [Fact]
    public void GenerateSvgContent_FloatFormatting_UsesInvariantCulture()
    {
        var element = new BarcodeElement
        {
            Data = "Format",
            ShowText = false
        };

        // Use dimensions that produce fractional bar widths
        var svg = _provider.GenerateSvgContent(element, 201f, 79f);

        // Bar rects should not use comma as decimal separator
        // This would fail for locales that use comma as decimal separator if not using invariant culture
        Assert.DoesNotContain("width=\",", svg);
        Assert.DoesNotContain("height=\",", svg);
    }

    [Fact]
    public void GenerateSvgContent_MergesAdjacentBars()
    {
        var element = new BarcodeElement
        {
            Data = "A",
            ShowText = false
        };

        var svg = _provider.GenerateSvgContent(element, 200f, 80f);

        // The entire barcode is rendered as a single <path> element with run-length encoded
        // subpaths (M...h...v...h...z) -- adjacent '1' bits are merged into one subpath
        var pathCount = CountSubstring(svg, "<path ");
        Assert.Equal(1, pathCount);
        // Path data should contain at least one subpath with M (moveto) commands
        Assert.Contains("d=\"M", svg);
    }

    [Fact]
    public void GenerateSvgContent_ColorContainsSpecialChars_EscapedProperly()
    {
        // The SVG provider never renders data as visible text.
        // Test that XML special characters in color attributes are properly escaped.
        var element = new BarcodeElement
        {
            Data = "ABC",
            Background = "url(&test)",
            Foreground = "<color>"
        };

        var svg = _provider.GenerateSvgContent(element, 200f, 80f);

        Assert.Contains("fill=\"url(&amp;test)\"", svg);
        Assert.Contains("fill=\"&lt;color&gt;\"", svg);
    }

    private static int CountSubstring(string source, string substring)
    {
        var count = 0;
        var idx = 0;
        while ((idx = source.IndexOf(substring, idx, StringComparison.Ordinal)) >= 0)
        {
            count++;
            idx += substring.Length;
        }
        return count;
    }
}
