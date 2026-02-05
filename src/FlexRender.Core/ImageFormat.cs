namespace FlexRender;

/// <summary>
/// Supported output image formats.
/// </summary>
public enum ImageFormat
{
    /// <summary>PNG format (lossless, supports transparency).</summary>
    Png,

    /// <summary>JPEG format (lossy compression).</summary>
    Jpeg,

    /// <summary>BMP format (uncompressed).</summary>
    Bmp,

    /// <summary>
    /// Raw pixel data in BGRA8888 format (4 bytes per pixel: Blue, Green, Red, Alpha).
    /// Pixels are stored in row-major order, top-to-bottom, left-to-right.
    /// </summary>
    Raw
}
