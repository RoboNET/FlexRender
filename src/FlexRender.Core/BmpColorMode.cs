namespace FlexRender;

/// <summary>
/// Color depth mode for BMP image encoding.
/// </summary>
public enum BmpColorMode
{
    /// <summary>32-bit BGRA with alpha channel (default, current behavior).</summary>
    Bgra32 = 0,

    /// <summary>24-bit RGB without alpha. Most common BMP format, 25% smaller.</summary>
    Rgb24 = 1,

    /// <summary>16-bit RGB565. 50% smaller, suitable for thermal/receipt printers.</summary>
    Rgb565 = 2,

    /// <summary>8-bit grayscale with 256-entry color table. 75% smaller.</summary>
    Grayscale8 = 3,

    /// <summary>4-bit grayscale with 16-entry color table. ~87% smaller than Bgra32.</summary>
    Grayscale4 = 4,

    /// <summary>1-bit monochrome (black and white). 96% smaller, ideal for thermal printers.</summary>
    Monochrome1 = 5
}
