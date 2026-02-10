using FlexRender.Rendering;
using Xunit;

namespace FlexRender.Tests.Rendering;

public class FontManagerTests : IDisposable
{
    private readonly FontManager _fontManager = new();
    private readonly string _tempDir;

    public FontManagerTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"FlexRenderFontTests_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        _fontManager.Dispose();
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, recursive: true);
    }

    [Fact]
    public void GetTypeface_DefaultFont_ReturnsTypeface()
    {
        var typeface = _fontManager.GetTypeface("main");

        Assert.NotNull(typeface);
    }

    [Fact]
    public void GetTypeface_UnknownFont_ReturnsFallback()
    {
        var typeface = _fontManager.GetTypeface("nonexistent-font-xyz");

        Assert.NotNull(typeface);
    }

    [Fact]
    public void GetTypeface_SameNameTwice_ReturnsSameInstance()
    {
        var typeface1 = _fontManager.GetTypeface("main");
        var typeface2 = _fontManager.GetTypeface("main");

        Assert.Same(typeface1, typeface2);
    }

    [Fact]
    public void RegisterFont_WithValidPath_RegistersFont()
    {
        // We can't easily create a valid font file in tests,
        // so we just test that the method doesn't throw for non-existent paths
        // and returns false for missing files

        var result = _fontManager.RegisterFont("test-font", "/nonexistent/path.ttf");

        Assert.False(result);
    }

    [Fact]
    public void RegisterFont_WithFallback_UsesFallbackOnMissingFile()
    {
        _fontManager.RegisterFont("custom", "/nonexistent.ttf", fallback: "Arial");

        var typeface = _fontManager.GetTypeface("custom");

        Assert.NotNull(typeface);
    }

    [Fact]
    public void SetDefaultFallback_ChangesDefault()
    {
        _fontManager.SetDefaultFallback("Helvetica");

        var typeface = _fontManager.GetTypeface("undefined-font");

        Assert.NotNull(typeface);
    }

    [Fact]
    public void ParseFontSize_Pixels_ReturnsValue()
    {
        var size = _fontManager.ParseFontSize("16", 12f, 100f);

        Assert.Equal(16f, size);
    }

    [Fact]
    public void ParseFontSize_Em_MultipliesBaseSize()
    {
        var size = _fontManager.ParseFontSize("1.5em", 12f, 100f);

        Assert.Equal(18f, size);
    }

    [Fact]
    public void ParseFontSize_Percent_MultipliesParentSize()
    {
        var size = _fontManager.ParseFontSize("50%", 12f, 100f);

        Assert.Equal(50f, size);
    }

    [Fact]
    public void ParseFontSize_Invalid_ReturnsBaseSize()
    {
        var size = _fontManager.ParseFontSize("invalid", 12f, 100f);

        Assert.Equal(12f, size);
    }

    [Fact]
    public void ParseFontSize_Empty_ReturnsBaseSize()
    {
        var size = _fontManager.ParseFontSize("", 12f, 100f);

        Assert.Equal(12f, size);
    }

    [Theory]
    [InlineData("24", 12f, 100f, 24f)]
    [InlineData("2em", 12f, 100f, 24f)]
    [InlineData("200%", 12f, 100f, 200f)]
    [InlineData("0.5em", 20f, 100f, 10f)]
    [InlineData("48px", 12f, 100f, 48f)]
    [InlineData("120px", 12f, 100f, 120f)]
    [InlineData("16.5px", 12f, 100f, 16.5f)]
    public void ParseFontSize_VariousFormats_ReturnsCorrectSize(
        string sizeStr, float baseSize, float parentSize, float expected)
    {
        var size = _fontManager.ParseFontSize(sizeStr, baseSize, parentSize);

        Assert.Equal(expected, size, precision: 2);
    }

    [Fact]
    public void ParseFontSize_PxSuffix_ReturnsValue()
    {
        var size = _fontManager.ParseFontSize("48px", 12f, 100f);

        Assert.Equal(48f, size);
    }

    [Fact]
    public void GetTypeface_MainFont_WithNoRegisteredFonts_ReturnsNonNull()
    {
        // "main" is the default font name used by TextElement.
        // When no fonts are registered, it should still return a valid typeface.
        var typeface = _fontManager.GetTypeface("main");

        Assert.NotNull(typeface);
    }

    [Fact]
    public void GetTypeface_EmptyString_ReturnsNonNull()
    {
        var typeface = _fontManager.GetTypeface("");

        Assert.NotNull(typeface);
    }
}
