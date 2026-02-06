using Xunit;

namespace FlexRender.Tests;

public sealed class RenderOptionsTests
{
    [Fact]
    public void Default_AntialiasingIsTrue()
    {
        var options = RenderOptions.Default;

        Assert.True(options.Antialiasing);
    }

    [Fact]
    public void Default_SubpixelTextIsTrue()
    {
        var options = RenderOptions.Default;

        Assert.True(options.SubpixelText);
    }

    [Fact]
    public void Default_FontHintingIsNormal()
    {
        var options = RenderOptions.Default;

        Assert.Equal(FontHinting.Normal, options.FontHinting);
    }

    [Fact]
    public void Default_TextRenderingIsSubpixelLcd()
    {
        var options = RenderOptions.Default;

        Assert.Equal(TextRendering.SubpixelLcd, options.TextRendering);
    }

    [Fact]
    public void Default_IsSingletonInstance()
    {
        var a = RenderOptions.Default;
        var b = RenderOptions.Default;

        Assert.Same(a, b);
    }

    [Fact]
    public void Deterministic_SubpixelTextIsFalse()
    {
        var options = RenderOptions.Deterministic;

        Assert.False(options.SubpixelText);
    }

    [Fact]
    public void Deterministic_FontHintingIsNone()
    {
        var options = RenderOptions.Deterministic;

        Assert.Equal(FontHinting.None, options.FontHinting);
    }

    [Fact]
    public void Deterministic_TextRenderingIsGrayscale()
    {
        var options = RenderOptions.Deterministic;

        Assert.Equal(TextRendering.Grayscale, options.TextRendering);
    }

    [Fact]
    public void Deterministic_AntialiasingIsTrue()
    {
        var options = RenderOptions.Deterministic;

        Assert.True(options.Antialiasing);
    }

    [Fact]
    public void Deterministic_IsSingletonInstance()
    {
        var a = RenderOptions.Deterministic;
        var b = RenderOptions.Deterministic;

        Assert.Same(a, b);
    }

    [Fact]
    public void Init_SetsAntialiasing()
    {
        var options = new RenderOptions { Antialiasing = false };

        Assert.False(options.Antialiasing);
    }

    [Fact]
    public void Init_SetsSubpixelText()
    {
        var options = new RenderOptions { SubpixelText = false };

        Assert.False(options.SubpixelText);
    }

    [Fact]
    public void Init_SetsFontHinting()
    {
        var options = new RenderOptions { FontHinting = FontHinting.Full };

        Assert.Equal(FontHinting.Full, options.FontHinting);
    }

    [Fact]
    public void Init_SetsTextRendering()
    {
        var options = new RenderOptions { TextRendering = TextRendering.Aliased };

        Assert.Equal(TextRendering.Aliased, options.TextRendering);
    }

    [Fact]
    public void ValueEquality_EqualInstances()
    {
        var a = new RenderOptions
        {
            Antialiasing = true,
            SubpixelText = false,
            FontHinting = FontHinting.Slight,
            TextRendering = TextRendering.Grayscale
        };
        var b = new RenderOptions
        {
            Antialiasing = true,
            SubpixelText = false,
            FontHinting = FontHinting.Slight,
            TextRendering = TextRendering.Grayscale
        };

        Assert.Equal(a, b);
    }

    [Fact]
    public void ValueEquality_DifferentAntialiasing()
    {
        var a = new RenderOptions { Antialiasing = true };
        var b = new RenderOptions { Antialiasing = false };

        Assert.NotEqual(a, b);
    }

    [Fact]
    public void ValueEquality_DifferentSubpixelText()
    {
        var a = new RenderOptions { SubpixelText = true };
        var b = new RenderOptions { SubpixelText = false };

        Assert.NotEqual(a, b);
    }

    [Fact]
    public void ValueEquality_DifferentFontHinting()
    {
        var a = new RenderOptions { FontHinting = FontHinting.None };
        var b = new RenderOptions { FontHinting = FontHinting.Full };

        Assert.NotEqual(a, b);
    }

    [Fact]
    public void ValueEquality_DifferentTextRendering()
    {
        var a = new RenderOptions { TextRendering = TextRendering.Grayscale };
        var b = new RenderOptions { TextRendering = TextRendering.SubpixelLcd };

        Assert.NotEqual(a, b);
    }

    [Fact]
    public void NewInstance_DefaultsMatchDefaultStatic()
    {
        var options = new RenderOptions();

        Assert.True(options.Antialiasing);
        Assert.True(options.SubpixelText);
        Assert.Equal(FontHinting.Normal, options.FontHinting);
        Assert.Equal(TextRendering.SubpixelLcd, options.TextRendering);
    }

    [Fact]
    public void WithExpression_CreatesModifiedCopy()
    {
        var original = RenderOptions.Default;

        var modified = original with { FontHinting = FontHinting.Full };

        Assert.Equal(FontHinting.Normal, original.FontHinting);
        Assert.Equal(FontHinting.Full, modified.FontHinting);
        Assert.True(modified.Antialiasing);
        Assert.True(modified.SubpixelText);
        Assert.Equal(TextRendering.SubpixelLcd, modified.TextRendering);
    }

    [Fact]
    public void WithExpression_MultipleProperties()
    {
        var options = RenderOptions.Default with
        {
            Antialiasing = false,
            SubpixelText = false,
            FontHinting = FontHinting.None,
            TextRendering = TextRendering.Aliased
        };

        Assert.False(options.Antialiasing);
        Assert.False(options.SubpixelText);
        Assert.Equal(FontHinting.None, options.FontHinting);
        Assert.Equal(TextRendering.Aliased, options.TextRendering);
    }
}
