using FlexRender.ImageSharp.Rendering;
using SixLabors.Fonts;
using Xunit;
using AstFontWeight = FlexRender.Parsing.Ast.FontWeight;
using AstFontStyle = FlexRender.Parsing.Ast.FontStyle;

namespace FlexRender.ImageSharp.Tests.Rendering;

public sealed class ImageSharpFontManagerTests : IDisposable
{
    private readonly ImageSharpFontManager _manager = new();
    private readonly string _fontPath;
    private readonly string _boldFontPath;

    public ImageSharpFontManagerTests()
    {
        var assemblyDir = Path.GetDirectoryName(typeof(ImageSharpFontManagerTests).Assembly.Location)!;
        _fontPath = Path.Combine(assemblyDir, "Fonts", "Inter-Regular.ttf");
        _boldFontPath = Path.Combine(assemblyDir, "Fonts", "Inter-Bold.ttf");
    }

    [Fact]
    public void RegisterFont_ValidPath_ReturnsTrue()
    {
        var result = _manager.RegisterFont("test", _fontPath);
        Assert.True(result);
    }

    [Fact]
    public void RegisterFont_InvalidPath_ReturnsFalse()
    {
        var result = _manager.RegisterFont("test", "/nonexistent/path/font.ttf");
        Assert.False(result);
    }

    [Fact]
    public void GetFont_RegisteredFont_ReturnsFont()
    {
        _manager.RegisterFont("test", _fontPath);
        var font = _manager.GetFont("test", 16f);
        Assert.NotNull(font);
        Assert.Equal(16f, font.Size);
    }

    [Fact]
    public void GetFont_UnregisteredFont_ReturnsFallback()
    {
        _manager.RegisterFont("default", _fontPath);
        var font = _manager.GetFont("nonexistent", 14f);
        Assert.NotNull(font);
    }

    [Fact]
    public void GetFont_CaseInsensitive()
    {
        _manager.RegisterFont("MyFont", _fontPath);
        var font = _manager.GetFont("myfont", 12f);
        Assert.NotNull(font);
    }

    [Fact]
    public void GetFontFamily_RegisteredFont_ReturnsFamily()
    {
        _manager.RegisterFont("test", _fontPath);
        var family = _manager.GetFontFamily("test");
        Assert.NotEqual(default, family);
    }

    [Fact]
    public void GetFont_WithBoldWeight_ReturnsBoldVariantFromSiblingFile()
    {
        // Register only the Regular font — Bold sibling is in the same directory
        _manager.RegisterFont("default", _fontPath);

        var font = _manager.GetFont("default", 16f, AstFontWeight.Bold, AstFontStyle.Normal);

        Assert.NotNull(font);
        Assert.Equal(16f, font.Size);
        // The font should be created with Bold style from the shared collection
        Assert.True(font.IsBold, "Expected Bold font from sibling Inter-Bold.ttf");
    }

    [Fact]
    public void GetFont_WithNormalWeight_DoesNotUseSiblingScanning()
    {
        _manager.RegisterFont("default", _fontPath);

        var font = _manager.GetFont("default", 14f, AstFontWeight.Normal, AstFontStyle.Normal);

        Assert.NotNull(font);
        Assert.Equal(14f, font.Size);
        // Should use the isolated collection, returning Regular
        Assert.False(font.IsBold);
    }

    [Fact]
    public void GetFont_WithBoldWeight_NoSiblingFiles_FallsBackToIsolatedCollection()
    {
        // Create a temp directory with only the Regular font (no Bold sibling)
        var tempDir = Path.Combine(Path.GetTempPath(), $"FontTest_{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);
        try
        {
            var tempFontPath = Path.Combine(tempDir, "Inter-Regular.ttf");
            File.Copy(_fontPath, tempFontPath);

            _manager.RegisterFont("isolated", tempFontPath);

            // Should still return a font (fallback), not throw
            var font = _manager.GetFont("isolated", 16f, AstFontWeight.Bold, AstFontStyle.Normal);
            Assert.NotNull(font);
            Assert.Equal(16f, font.Size);
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public void GetFont_WithBoldWeight_CalledTwice_UsesCachedSharedCollection()
    {
        _manager.RegisterFont("default", _fontPath);

        var font1 = _manager.GetFont("default", 16f, AstFontWeight.Bold, AstFontStyle.Normal);
        var font2 = _manager.GetFont("default", 20f, AstFontWeight.Bold, AstFontStyle.Normal);

        Assert.NotNull(font1);
        Assert.NotNull(font2);
        Assert.True(font1.IsBold);
        Assert.True(font2.IsBold);
        Assert.Equal(16f, font1.Size);
        Assert.Equal(20f, font2.Size);
    }

    public void Dispose()
    {
        _manager.Dispose();
    }
}
