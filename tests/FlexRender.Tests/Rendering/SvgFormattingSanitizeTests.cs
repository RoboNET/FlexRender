using FlexRender.Rendering;
using Xunit;

namespace FlexRender.Tests.Rendering;

/// <summary>
/// Unit tests for <see cref="SvgFormatting.SanitizeSvgContent"/>.
/// </summary>
public sealed class SvgFormattingSanitizeTests
{
    // ========================================================================
    // Null / Empty / Safe passthrough
    // ========================================================================

    [Fact]
    public void SanitizeSvgContent_NullInput_ReturnsNull()
    {
        var result = SvgFormatting.SanitizeSvgContent(null!);
        Assert.Null(result);
    }

    [Fact]
    public void SanitizeSvgContent_EmptyString_ReturnsEmpty()
    {
        var result = SvgFormatting.SanitizeSvgContent(string.Empty);
        Assert.Equal(string.Empty, result);
    }

    [Fact]
    public void SanitizeSvgContent_SafeSvg_ReturnsUnchanged()
    {
        const string safe = """<rect width="100" height="100" fill="red"/>""";
        var result = SvgFormatting.SanitizeSvgContent(safe);
        Assert.Equal(safe, result);
    }

    [Fact]
    public void SanitizeSvgContent_ComplexSafeSvg_ReturnsUnchanged()
    {
        const string safe =
            """<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 100 100"><circle cx="50" cy="50" r="40" fill="blue"/><text x="10" y="50">Hello</text></svg>""";
        var result = SvgFormatting.SanitizeSvgContent(safe);
        Assert.Equal(safe, result);
    }

    // ========================================================================
    // Script tag removal
    // ========================================================================

    [Fact]
    public void SanitizeSvgContent_ScriptTagWithContent_StripsEntireTag()
    {
        const string input = """<rect fill="red"/><script>alert('xss')</script><circle r="5"/>""";
        var result = SvgFormatting.SanitizeSvgContent(input);
        Assert.DoesNotContain("script", result, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("""<rect fill="red"/>""", result);
        Assert.Contains("""<circle r="5"/>""", result);
    }

    [Fact]
    public void SanitizeSvgContent_ScriptTagCaseInsensitive_StripsTag()
    {
        const string input = """<SCRIPT>alert('xss')</SCRIPT>""";
        var result = SvgFormatting.SanitizeSvgContent(input);
        Assert.DoesNotContain("script", result, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void SanitizeSvgContent_ScriptTagMixedCase_StripsTag()
    {
        const string input = """<ScRiPt>alert('xss')</sCrIpT>""";
        var result = SvgFormatting.SanitizeSvgContent(input);
        Assert.DoesNotContain("script", result, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void SanitizeSvgContent_ScriptTagWithAttributes_StripsTag()
    {
        const string input = """<script type="text/javascript">alert('xss')</script>""";
        var result = SvgFormatting.SanitizeSvgContent(input);
        Assert.DoesNotContain("script", result, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void SanitizeSvgContent_SelfClosingScriptTag_StripsTag()
    {
        const string input = """<rect/><script src="evil.js" /><circle/>""";
        var result = SvgFormatting.SanitizeSvgContent(input);
        Assert.DoesNotContain("script", result, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("<rect/>", result);
        Assert.Contains("<circle/>", result);
    }

    [Fact]
    public void SanitizeSvgContent_MultipleScriptTags_StripsAll()
    {
        const string input = """<script>one</script><rect/><script>two</script>""";
        var result = SvgFormatting.SanitizeSvgContent(input);
        Assert.DoesNotContain("script", result, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("<rect/>", result);
    }

    // ========================================================================
    // ForeignObject tag removal
    // ========================================================================

    [Fact]
    public void SanitizeSvgContent_ForeignObjectTag_StripsEntireTag()
    {
        const string input = """<rect/><foreignObject><body xmlns="http://www.w3.org/1999/xhtml"><p>HTML</p></body></foreignObject><circle/>""";
        var result = SvgFormatting.SanitizeSvgContent(input);
        Assert.DoesNotContain("foreignObject", result, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("<rect/>", result);
        Assert.Contains("<circle/>", result);
    }

    [Fact]
    public void SanitizeSvgContent_ForeignObjectCaseInsensitive_StripsTag()
    {
        const string input = """<FOREIGNOBJECT>content</FOREIGNOBJECT>""";
        var result = SvgFormatting.SanitizeSvgContent(input);
        Assert.DoesNotContain("foreignObject", result, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void SanitizeSvgContent_SelfClosingForeignObject_StripsTag()
    {
        const string input = """<rect/><foreignObject width="100" height="100" /><circle/>""";
        var result = SvgFormatting.SanitizeSvgContent(input);
        Assert.DoesNotContain("foreignObject", result, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("<rect/>", result);
        Assert.Contains("<circle/>", result);
    }

    // ========================================================================
    // Event handler attribute removal
    // ========================================================================

    [Fact]
    public void SanitizeSvgContent_OnloadAttribute_Stripped()
    {
        const string input = """<svg onload="alert('xss')"><rect/></svg>""";
        var result = SvgFormatting.SanitizeSvgContent(input);
        Assert.DoesNotContain("onload", result, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("<rect/>", result);
    }

    [Fact]
    public void SanitizeSvgContent_OnclickAttribute_Stripped()
    {
        const string input = """<rect onclick="alert('xss')" fill="red"/>""";
        var result = SvgFormatting.SanitizeSvgContent(input);
        Assert.DoesNotContain("onclick", result, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("""fill="red""", result);
    }

    [Fact]
    public void SanitizeSvgContent_OnerrorAttribute_Stripped()
    {
        const string input = """<image onerror="alert('xss')" href="img.png"/>""";
        var result = SvgFormatting.SanitizeSvgContent(input);
        Assert.DoesNotContain("onerror", result, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("""href="img.png""", result);
    }

    [Fact]
    public void SanitizeSvgContent_OnmouseoverAttribute_Stripped()
    {
        const string input = """<rect onmouseover="alert('xss')" fill="blue"/>""";
        var result = SvgFormatting.SanitizeSvgContent(input);
        Assert.DoesNotContain("onmouseover", result, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("""fill="blue""", result);
    }

    [Fact]
    public void SanitizeSvgContent_EventHandlerCaseInsensitive_Stripped()
    {
        const string input = """<rect ONCLICK="alert('xss')" fill="red"/>""";
        var result = SvgFormatting.SanitizeSvgContent(input);
        Assert.DoesNotContain("onclick", result, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void SanitizeSvgContent_EventHandlerSingleQuotedValue_Stripped()
    {
        const string input = """<rect onclick='alert("xss")' fill="red"/>""";
        var result = SvgFormatting.SanitizeSvgContent(input);
        Assert.DoesNotContain("onclick", result, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("""fill="red""", result);
    }

    [Fact]
    public void SanitizeSvgContent_MultipleEventHandlers_AllStripped()
    {
        const string input = """<rect onclick="a()" onmouseover="b()" onload="c()" fill="red"/>""";
        var result = SvgFormatting.SanitizeSvgContent(input);
        Assert.DoesNotContain("onclick", result, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("onmouseover", result, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("onload", result, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("""fill="red""", result);
    }

    [Fact]
    public void SanitizeSvgContent_EventHandlerWithSpacesAroundEquals_Stripped()
    {
        const string input = """<rect onclick = "alert('xss')" fill="red"/>""";
        var result = SvgFormatting.SanitizeSvgContent(input);
        Assert.DoesNotContain("onclick", result, StringComparison.OrdinalIgnoreCase);
    }

    // ========================================================================
    // javascript: protocol removal
    // ========================================================================

    [Fact]
    public void SanitizeSvgContent_JavascriptHref_Stripped()
    {
        const string input = """<a href="javascript:alert('xss')"><text>Click</text></a>""";
        var result = SvgFormatting.SanitizeSvgContent(input);
        Assert.DoesNotContain("javascript:", result, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("href=", result);
        Assert.Contains("<text>Click</text>", result);
    }

    [Fact]
    public void SanitizeSvgContent_JavascriptXlinkHref_Stripped()
    {
        const string input = """<a xlink:href="javascript:alert('xss')"><text>Click</text></a>""";
        var result = SvgFormatting.SanitizeSvgContent(input);
        Assert.DoesNotContain("javascript:", result, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void SanitizeSvgContent_JavascriptHrefCaseInsensitive_Stripped()
    {
        const string input = """<a href="JAVASCRIPT:alert('xss')"><text>Click</text></a>""";
        var result = SvgFormatting.SanitizeSvgContent(input);
        Assert.DoesNotContain("javascript:", result, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void SanitizeSvgContent_JavascriptHrefSingleQuoted_Stripped()
    {
        const string input = """<a href='javascript:alert("xss")'><text>Click</text></a>""";
        var result = SvgFormatting.SanitizeSvgContent(input);
        Assert.DoesNotContain("javascript:", result, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void SanitizeSvgContent_NormalHref_Preserved()
    {
        const string input = """<a href="https://example.com"><text>Link</text></a>""";
        var result = SvgFormatting.SanitizeSvgContent(input);
        Assert.Contains("""href="https://example.com""", result);
    }

    // ========================================================================
    // Combined attack vectors
    // ========================================================================

    [Fact]
    public void SanitizeSvgContent_CombinedAttackVector_AllDangerousContentStripped()
    {
        const string input =
            """<svg onload="alert('xss')"><script>evil()</script><foreignObject><body>html</body></foreignObject><a href="javascript:void(0)"><rect fill="red"/></a></svg>""";
        var result = SvgFormatting.SanitizeSvgContent(input);

        Assert.DoesNotContain("script", result, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("foreignObject", result, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("onload", result, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("javascript:", result, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("""fill="red""", result);
    }

    [Fact]
    public void SanitizeSvgContent_ScriptInsideForeignObject_BothStripped()
    {
        const string input =
            """<foreignObject><script>alert('xss')</script></foreignObject>""";
        var result = SvgFormatting.SanitizeSvgContent(input);

        Assert.DoesNotContain("script", result, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("foreignObject", result, StringComparison.OrdinalIgnoreCase);
    }

    // ========================================================================
    // Preservation of legitimate content
    // ========================================================================

    [Fact]
    public void SanitizeSvgContent_WordContainingOn_NotStripped()
    {
        // "font" contains "on" -- should not be stripped
        const string input = """<text font-family="Onyx" fill="red">Content</text>""";
        var result = SvgFormatting.SanitizeSvgContent(input);
        Assert.Equal(input, result);
    }

    [Fact]
    public void SanitizeSvgContent_DataUri_NotStripped()
    {
        const string input = """<image href="data:image/png;base64,iVBORw0KGgo="/>""";
        var result = SvgFormatting.SanitizeSvgContent(input);
        Assert.Equal(input, result);
    }

    [Fact]
    public void SanitizeSvgContent_TextContentWithScriptWord_NotStripped()
    {
        const string input = """<text>This describes a script element in SVG</text>""";
        var result = SvgFormatting.SanitizeSvgContent(input);
        Assert.Equal(input, result);
    }

    [Fact]
    public void SanitizeSvgContent_StyleElement_NotStripped()
    {
        const string input = """<defs><style>rect { fill: red; }</style></defs>""";
        var result = SvgFormatting.SanitizeSvgContent(input);
        Assert.Equal(input, result);
    }
}
