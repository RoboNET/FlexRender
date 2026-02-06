using FlexRender.HarfBuzz;
using FlexRender.Layout;
using SkiaSharp;
using Xunit;

namespace FlexRender.Tests.HarfBuzz;

/// <summary>
/// Tests for HarfBuzz text shaping integration.
/// </summary>
public class HarfBuzzTextShaperTests
{
    [Fact]
    public void MeasureShapedText_LtrText_ReturnsPositiveWidth()
    {
        using var typeface = SKTypeface.Default;
        using var font = new SKFont(typeface, 16f);

        var width = HarfBuzzTextShaper.MeasureShapedText("Hello World", font, TextDirection.Ltr);

        Assert.True(width > 0, $"Expected positive width, got {width}");
    }

    [Fact]
    public void MeasureShapedText_RtlText_ReturnsPositiveWidth()
    {
        using var typeface = SKTypeface.Default;
        using var font = new SKFont(typeface, 16f);

        var width = HarfBuzzTextShaper.MeasureShapedText("Hello", font, TextDirection.Rtl);

        Assert.True(width > 0, $"Expected positive width for RTL, got {width}");
    }

    [Fact]
    public void MeasureShapedText_EmptyString_ReturnsZero()
    {
        using var typeface = SKTypeface.Default;
        using var font = new SKFont(typeface, 16f);

        var width = HarfBuzzTextShaper.MeasureShapedText("", font, TextDirection.Ltr);

        Assert.Equal(0f, width);
    }

    [Fact]
    public void MeasureShapedText_NullString_ReturnsZero()
    {
        using var typeface = SKTypeface.Default;
        using var font = new SKFont(typeface, 16f);

        var width = HarfBuzzTextShaper.MeasureShapedText(null!, font, TextDirection.Ltr);

        Assert.Equal(0f, width);
    }

    [Fact]
    public void MeasureShapedText_NullFont_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() =>
            HarfBuzzTextShaper.MeasureShapedText("Hello", null!, TextDirection.Ltr));
    }

    [Fact]
    public void DrawShapedText_LtrText_DoesNotThrow()
    {
        using var bitmap = new SKBitmap(200, 50);
        using var canvas = new SKCanvas(bitmap);
        using var typeface = SKTypeface.Default;
        using var font = new SKFont(typeface, 16f);
        using var paint = new SKPaint { Color = SKColors.Black };

        HarfBuzzTextShaper.DrawShapedText(canvas, "Hello World", 0, 20, font, paint, TextDirection.Ltr);
    }

    [Fact]
    public void DrawShapedText_RtlText_DoesNotThrow()
    {
        using var bitmap = new SKBitmap(200, 50);
        using var canvas = new SKCanvas(bitmap);
        using var typeface = SKTypeface.Default;
        using var font = new SKFont(typeface, 16f);
        using var paint = new SKPaint { Color = SKColors.Black };

        HarfBuzzTextShaper.DrawShapedText(canvas, "Hello", 0, 20, font, paint, TextDirection.Rtl);
    }

    [Fact]
    public void DrawShapedText_EmptyString_DoesNotThrow()
    {
        using var bitmap = new SKBitmap(200, 50);
        using var canvas = new SKCanvas(bitmap);
        using var typeface = SKTypeface.Default;
        using var font = new SKFont(typeface, 16f);
        using var paint = new SKPaint { Color = SKColors.Black };

        HarfBuzzTextShaper.DrawShapedText(canvas, "", 0, 20, font, paint, TextDirection.Ltr);
    }

    [Fact]
    public void DrawShapedText_NullCanvas_ThrowsArgumentNullException()
    {
        using var typeface = SKTypeface.Default;
        using var font = new SKFont(typeface, 16f);
        using var paint = new SKPaint { Color = SKColors.Black };

        Assert.Throws<ArgumentNullException>(() =>
            HarfBuzzTextShaper.DrawShapedText(null!, "Hello", 0, 20, font, paint, TextDirection.Ltr));
    }

    [Fact]
    public void DrawShapedText_NullFont_ThrowsArgumentNullException()
    {
        using var bitmap = new SKBitmap(200, 50);
        using var canvas = new SKCanvas(bitmap);
        using var paint = new SKPaint { Color = SKColors.Black };

        Assert.Throws<ArgumentNullException>(() =>
            HarfBuzzTextShaper.DrawShapedText(canvas, "Hello", 0, 20, null!, paint, TextDirection.Ltr));
    }

    [Fact]
    public void DrawShapedText_NullPaint_ThrowsArgumentNullException()
    {
        using var bitmap = new SKBitmap(200, 50);
        using var canvas = new SKCanvas(bitmap);
        using var typeface = SKTypeface.Default;
        using var font = new SKFont(typeface, 16f);

        Assert.Throws<ArgumentNullException>(() =>
            HarfBuzzTextShaper.DrawShapedText(canvas, "Hello", 0, 20, font, null!, TextDirection.Ltr));
    }
}
