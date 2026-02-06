namespace FlexRender;

/// <summary>
/// Controls how text edges are rendered (antialiasing mode for glyphs).
/// </summary>
/// <remarks>
/// <para>
/// <see cref="SubpixelLcd"/> uses the RGB subpixel layout of LCD displays
/// for sharper text but produces platform-dependent output.
/// </para>
/// <para>
/// <see cref="Grayscale"/> uses grayscale antialiasing that looks identical
/// across macOS, Linux, and Windows — use for snapshot tests.
/// </para>
/// </remarks>
public enum TextRendering
{
    /// <summary>
    /// No antialiasing on text edges. Produces aliased (jagged) text.
    /// Suitable for monochrome or very low resolution targets.
    /// </summary>
    Aliased = 0,

    /// <summary>
    /// Grayscale antialiasing. Platform-independent smooth text.
    /// Produces identical output across macOS, Linux, and Windows.
    /// </summary>
    Grayscale = 1,

    /// <summary>
    /// Subpixel LCD antialiasing. Sharpest text on LCD displays.
    /// Output varies by platform and display — not suitable for snapshot tests.
    /// </summary>
    SubpixelLcd = 2
}
