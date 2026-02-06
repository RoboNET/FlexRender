namespace FlexRender;

/// <summary>
/// Controls how font glyphs are adjusted to fit the pixel grid.
/// </summary>
/// <remarks>
/// Higher hinting levels produce crisper text at small sizes but may
/// alter glyph shapes. <see cref="None"/> produces platform-consistent
/// output suitable for snapshot testing.
/// </remarks>
public enum FontHinting
{
    /// <summary>
    /// No hinting. Glyph shapes are rendered exactly as designed.
    /// Produces identical output across platforms — use for snapshot tests.
    /// </summary>
    None = 0,

    /// <summary>
    /// Slight hinting. Minimal grid-fitting that preserves glyph shape.
    /// </summary>
    Slight = 1,

    /// <summary>
    /// Normal hinting. Default platform behavior.
    /// </summary>
    Normal = 2,

    /// <summary>
    /// Full hinting. Maximum grid-fitting for crisp edges at small sizes.
    /// May alter glyph shapes noticeably.
    /// </summary>
    Full = 3
}
