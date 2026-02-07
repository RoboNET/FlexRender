using FlexRender.ImageSharp.Rendering;
using Xunit;

namespace FlexRender.ImageSharp.Tests.Rendering;

public sealed class ImageSharpFontManagerTests : IDisposable
{
    private readonly ImageSharpFontManager _manager = new();
    private readonly string _fontPath;

    public ImageSharpFontManagerTests()
    {
        var assemblyDir = Path.GetDirectoryName(typeof(ImageSharpFontManagerTests).Assembly.Location)!;
        _fontPath = Path.Combine(assemblyDir, "Fonts", "Inter-Regular.ttf");
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

    public void Dispose()
    {
        _manager.Dispose();
    }
}
