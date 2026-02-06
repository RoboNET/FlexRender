namespace FlexRender;

/// <summary>
/// Options for BMP image encoding.
/// </summary>
/// <remarks>
/// BMP supports multiple color depth modes, from full 32-bit BGRA down to
/// 1-bit monochrome. Lower color depths produce significantly smaller files
/// and are commonly used for thermal and receipt printers.
/// </remarks>
public sealed record BmpOptions
{
    /// <summary>
    /// Default BMP options. Color mode Bgra32.
    /// </summary>
    public static readonly BmpOptions Default = new();

    /// <summary>
    /// Gets the BMP color depth mode.
    /// </summary>
    /// <value>The default value is <see cref="BmpColorMode.Bgra32"/>.</value>
    public BmpColorMode ColorMode { get; init; } = BmpColorMode.Bgra32;
}
