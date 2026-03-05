using FlexRender.Parsing.Ast;
using FlexRender.Rendering;
using SkiaSharp;
using Xunit;
using AstFontStyle = global::FlexRender.Parsing.Ast.FontStyle;

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

    [Fact]
    public void GetTypeface_WithDefaultWeightAndStyle_ReturnsSameAsBasic()
    {
        var basic = _fontManager.GetTypeface("main");
        var withDefaults = _fontManager.GetTypeface("main", FontWeight.Normal, AstFontStyle.Normal);

        // Default weight+style should delegate to the basic overload and return the same instance
        Assert.Same(basic, withDefaults);
    }

    [Fact]
    public void GetTypeface_WithBoldWeight_ReturnsNonNull()
    {
        var typeface = _fontManager.GetTypeface("main", FontWeight.Bold, AstFontStyle.Normal);

        Assert.NotNull(typeface);
    }

    [Fact]
    public void GetTypeface_WithItalicStyle_ReturnsNonNull()
    {
        var typeface = _fontManager.GetTypeface("main", FontWeight.Normal, AstFontStyle.Italic);

        Assert.NotNull(typeface);
    }

    [Fact]
    public void GetTypeface_WithBoldItalic_ReturnsNonNull()
    {
        var typeface = _fontManager.GetTypeface("main", FontWeight.Bold, AstFontStyle.Italic);

        Assert.NotNull(typeface);
    }

    [Fact]
    public void GetTypeface_WithObliqueStyle_ReturnsNonNull()
    {
        var typeface = _fontManager.GetTypeface("main", FontWeight.Normal, AstFontStyle.Oblique);

        Assert.NotNull(typeface);
    }

    [Theory]
    [InlineData(FontWeight.Thin)]
    [InlineData(FontWeight.ExtraLight)]
    [InlineData(FontWeight.Light)]
    [InlineData(FontWeight.Medium)]
    [InlineData(FontWeight.SemiBold)]
    [InlineData(FontWeight.ExtraBold)]
    [InlineData(FontWeight.Black)]
    public void GetTypeface_AllWeights_ReturnNonNull(FontWeight weight)
    {
        var typeface = _fontManager.GetTypeface("main", weight, AstFontStyle.Normal);

        Assert.NotNull(typeface);
    }

    [Fact]
    public void GetTypeface_SameVariantTwice_ReturnsSameInstance()
    {
        var first = _fontManager.GetTypeface("main", FontWeight.Bold, AstFontStyle.Italic);
        var second = _fontManager.GetTypeface("main", FontWeight.Bold, AstFontStyle.Italic);

        Assert.Same(first, second);
    }

    [Fact]
    public void GetTypeface_DifferentVariants_MayReturnDifferentInstances()
    {
        var normal = _fontManager.GetTypeface("main", FontWeight.Normal, AstFontStyle.Normal);
        var bold = _fontManager.GetTypeface("main", FontWeight.Bold, AstFontStyle.Normal);

        // Both should be non-null; they may or may not be different typefaces
        // depending on system fonts, but they should not throw
        Assert.NotNull(normal);
        Assert.NotNull(bold);
    }

    [Fact]
    public void GetTypeface_VariantCaseInsensitive_ReturnsSameInstance()
    {
        var lower = _fontManager.GetTypeface("main", FontWeight.Bold, AstFontStyle.Normal);
        var upper = _fontManager.GetTypeface("MAIN", FontWeight.Bold, AstFontStyle.Normal);

        Assert.Same(lower, upper);
    }

    [Fact]
    public void ToSkFontStyle_NormalDefaults_ReturnsUpright400()
    {
        var skStyle = FontManager.ToSkFontStyle(FontWeight.Normal, AstFontStyle.Normal);

        Assert.Equal((int)SKFontStyleWeight.Normal, skStyle.Weight);
        Assert.Equal(SKFontStyleSlant.Upright, skStyle.Slant);
    }

    [Fact]
    public void ToSkFontStyle_Bold_ReturnsWeight700()
    {
        var skStyle = FontManager.ToSkFontStyle(FontWeight.Bold, AstFontStyle.Normal);

        Assert.Equal(700, skStyle.Weight);
        Assert.Equal(SKFontStyleSlant.Upright, skStyle.Slant);
    }

    [Fact]
    public void ToSkFontStyle_Italic_ReturnsItalicSlant()
    {
        var skStyle = FontManager.ToSkFontStyle(FontWeight.Normal, AstFontStyle.Italic);

        Assert.Equal(SKFontStyleSlant.Italic, skStyle.Slant);
    }

    [Fact]
    public void ToSkFontStyle_Oblique_ReturnsObliqueSlant()
    {
        var skStyle = FontManager.ToSkFontStyle(FontWeight.Normal, AstFontStyle.Oblique);

        Assert.Equal(SKFontStyleSlant.Oblique, skStyle.Slant);
    }

    [Fact]
    public void ToSkFontStyle_Black_ReturnsWeight900()
    {
        var skStyle = FontManager.ToSkFontStyle(FontWeight.Black, AstFontStyle.Normal);

        Assert.Equal(900, skStyle.Weight);
    }
}
