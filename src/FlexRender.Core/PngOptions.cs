namespace FlexRender;

/// <summary>
/// Options for PNG image encoding.
/// </summary>
/// <remarks>
/// <para>
/// PNG uses lossless compression. The <see cref="CompressionLevel"/> controls
/// the trade-off between encoding speed and file size. Higher values produce
/// smaller files but take longer to encode.
/// </para>
/// <para>
/// SkiaSharp maps the quality parameter (0-100) inversely to zlib compression
/// level. A quality of 100 means maximum compression (smallest file, slowest),
/// while 0 means no compression (largest file, fastest).
/// </para>
/// </remarks>
public sealed record PngOptions
{
    /// <summary>
    /// Default PNG options. Compression level 100 (maximum compression).
    /// </summary>
    public static readonly PngOptions Default = new();

    /// <summary>
    /// Gets the PNG compression level (0-100).
    /// </summary>
    /// <value>
    /// 0 = no compression (fastest, largest file).
    /// 100 = maximum compression (slowest, smallest file).
    /// The default value is <c>100</c>.
    /// </value>
    public int CompressionLevel
    {
        get => _compressionLevel;
        init
        {
            ArgumentOutOfRangeException.ThrowIfLessThan(value, 0);
            ArgumentOutOfRangeException.ThrowIfGreaterThan(value, 100);
            _compressionLevel = value;
        }
    }

    private int _compressionLevel = 100;
}
