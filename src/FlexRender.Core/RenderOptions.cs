using System.Globalization;

namespace FlexRender;

/// <summary>
/// Per-call rendering options that apply to all output formats.
/// These options can differ between consecutive render calls on the same
/// <see cref="Abstractions.IFlexRender"/> instance.
/// </summary>
/// <remarks>
/// <para>
/// Use this to control rendering behavior on a per-call basis.
/// Engine-level configuration (fonts, resource limits, base paths)
/// is set through <see cref="Configuration.FlexRenderOptions"/> at build time.
/// </para>
/// <para>
/// All properties have sensible defaults. Pass <c>null</c> or
/// <see cref="Default"/> to use defaults everywhere.
/// </para>
/// <para>
/// For cross-platform deterministic output (e.g., snapshot testing),
/// use <see cref="Deterministic"/>.
/// </para>
/// </remarks>
public sealed record RenderOptions
{
    /// <summary>
    /// Default render options. Antialiasing and subpixel positioning enabled,
    /// normal font hinting, subpixel LCD text rendering.
    /// </summary>
    public static readonly RenderOptions Default = new();

    /// <summary>
    /// Deterministic render options for cross-platform identical output.
    /// Disables subpixel positioning and hinting, uses grayscale text rendering.
    /// Suitable for snapshot testing and CI environments.
    /// </summary>
    /// <remarks>
    /// Equivalent to:
    /// <code>
    /// new RenderOptions
    /// {
    ///     SubpixelText = false,
    ///     FontHinting = FontHinting.None,
    ///     TextRendering = TextRendering.Grayscale
    /// }
    /// </code>
    /// </remarks>
    public static readonly RenderOptions Deterministic = new()
    {
        SubpixelText = false,
        FontHinting = FontHinting.None,
        TextRendering = TextRendering.Grayscale
    };

    /// <summary>
    /// Gets whether antialiasing is enabled for shapes, separators, and image scaling.
    /// </summary>
    /// <value>The default value is <c>true</c>.</value>
    /// <remarks>
    /// Controls <c>SKPaint.IsAntialias</c> for non-text elements (separators,
    /// image scaling, background shapes). Text antialiasing is controlled separately
    /// by <see cref="TextRendering"/>.
    /// Set to <c>false</c> for crisp pixel-perfect output, such as thermal printer
    /// rendering or monochrome BMP targets where antialiased edges create unwanted
    /// gray pixels.
    /// </remarks>
    public bool Antialiasing { get; init; } = true;

    /// <summary>
    /// Gets whether subpixel text positioning is enabled.
    /// </summary>
    /// <value>The default value is <c>true</c>.</value>
    /// <remarks>
    /// When enabled, glyph positions are calculated at sub-pixel precision,
    /// producing smoother text spacing. Disable for deterministic cross-platform
    /// output where glyphs must snap to whole pixel boundaries.
    /// </remarks>
    public bool SubpixelText { get; init; } = true;

    /// <summary>
    /// Gets the font hinting level.
    /// </summary>
    /// <value>The default value is <see cref="FlexRender.FontHinting.Normal"/>.</value>
    /// <remarks>
    /// Font hinting adjusts glyph outlines to align with the pixel grid.
    /// Use <see cref="FlexRender.FontHinting.None"/> for platform-independent output.
    /// </remarks>
    public FontHinting FontHinting { get; init; } = FontHinting.Normal;

    /// <summary>
    /// Gets the text edge rendering mode.
    /// </summary>
    /// <value>The default value is <see cref="FlexRender.TextRendering.SubpixelLcd"/>.</value>
    /// <remarks>
    /// <para>
    /// <see cref="FlexRender.TextRendering.SubpixelLcd"/> produces the sharpest text
    /// on LCD displays but varies across platforms.
    /// </para>
    /// <para>
    /// <see cref="FlexRender.TextRendering.Grayscale"/> produces consistent output
    /// across all platforms — use for snapshot tests and CI.
    /// </para>
    /// <para>
    /// <see cref="FlexRender.TextRendering.Aliased"/> produces jagged text with no
    /// smoothing — suitable for very low resolution or monochrome targets.
    /// </para>
    /// </remarks>
    public TextRendering TextRendering { get; init; } = TextRendering.SubpixelLcd;

    /// <summary>
    /// Gets the culture to use for culture-aware filter formatting.
    /// </summary>
    /// <value>The default value is <c>null</c>, which falls back to
    /// <c>Template.Culture</c> or <see cref="CultureInfo.InvariantCulture"/>.</value>
    /// <remarks>
    /// <para>
    /// When set, this takes highest priority in the culture resolution chain:
    /// <c>RenderOptions.Culture</c> &gt; <c>Template.Culture</c> &gt; <see cref="CultureInfo.InvariantCulture"/>.
    /// </para>
    /// <para>
    /// Affects filters that perform culture-sensitive operations: <c>currency</c>, <c>number</c>,
    /// <c>format</c>, <c>upper</c>, <c>lower</c>.
    /// </para>
    /// </remarks>
    public CultureInfo? Culture { get; init; }
}
