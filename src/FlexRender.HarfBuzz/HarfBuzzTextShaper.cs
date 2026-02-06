using FlexRender.Layout;
using SkiaSharp;
using SkiaSharp.HarfBuzz;

namespace FlexRender.HarfBuzz;

/// <summary>
/// Provides text shaping using HarfBuzz for correct rendering of complex scripts
/// including Arabic, Hebrew, and other RTL languages with proper glyph forms and ligatures.
/// </summary>
/// <remarks>
/// <para>
/// HarfBuzz text shaping handles:
/// <list type="bullet">
/// <item><description>RTL glyph reordering (Arabic, Hebrew characters)</description></item>
/// <item><description>Arabic contextual forms (initial, medial, final shapes)</description></item>
/// <item><description>Ligatures and kerning</description></item>
/// </list>
/// </para>
/// <para>
/// Without HarfBuzz, Arabic text renders as disconnected, reversed characters.
/// This class uses <see cref="SKShaper"/> from the SkiaSharp.HarfBuzz package
/// for native text shaping via P/Invoke (AOT-safe, no reflection).
/// </para>
/// </remarks>
public sealed class HarfBuzzTextShaper
{
    /// <summary>
    /// Measures the width of shaped text. Shaped text may have different width
    /// than unshaped due to ligatures, kerning, and contextual glyph forms.
    /// </summary>
    /// <param name="text">The text to measure.</param>
    /// <param name="font">The font to use for shaping.</param>
    /// <param name="direction">The text direction for shaping.</param>
    /// <returns>The width of the shaped text in pixels.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="font"/> is null.</exception>
    public static float MeasureShapedText(string text, SKFont font, TextDirection direction)
    {
        ArgumentNullException.ThrowIfNull(font);

        if (string.IsNullOrEmpty(text))
            return 0f;

        using var shaper = new SKShaper(font.Typeface);
        var result = shaper.Shape(text, 0, 0, font);
        return result.Width;
    }

    /// <summary>
    /// Draws shaped text to the canvas with proper glyph ordering and ligatures.
    /// </summary>
    /// <param name="canvas">The canvas to draw on.</param>
    /// <param name="text">The text to draw.</param>
    /// <param name="x">The X position.</param>
    /// <param name="y">The Y position (baseline).</param>
    /// <param name="font">The font to use.</param>
    /// <param name="paint">The paint for color and style.</param>
    /// <param name="direction">The text direction for shaping.</param>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="canvas"/>, <paramref name="font"/>,
    /// or <paramref name="paint"/> is null.
    /// </exception>
    public static void DrawShapedText(SKCanvas canvas, string text, float x, float y,
        SKFont font, SKPaint paint, TextDirection direction)
    {
        ArgumentNullException.ThrowIfNull(canvas);
        ArgumentNullException.ThrowIfNull(font);
        ArgumentNullException.ThrowIfNull(paint);

        if (string.IsNullOrEmpty(text))
            return;

        using var shaper = new SKShaper(font.Typeface);
        canvas.DrawShapedText(shaper, text, x, y, SKTextAlign.Left, font, paint);
    }
}
