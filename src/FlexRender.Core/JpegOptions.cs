namespace FlexRender;

/// <summary>
/// Options for JPEG image encoding.
/// </summary>
/// <remarks>
/// JPEG uses lossy compression. Higher <see cref="Quality"/> values produce
/// better visual quality but larger files.
/// </remarks>
public sealed record JpegOptions
{
    /// <summary>
    /// Default JPEG options. Quality 90.
    /// </summary>
    public static readonly JpegOptions Default = new();

    /// <summary>
    /// Gets the JPEG encoding quality (1-100).
    /// </summary>
    /// <value>
    /// 1 = lowest quality (smallest file).
    /// 100 = highest quality (largest file).
    /// The default value is <c>90</c>.
    /// </value>
    public int Quality
    {
        get => _quality;
        init
        {
            ArgumentOutOfRangeException.ThrowIfLessThan(value, 1);
            ArgumentOutOfRangeException.ThrowIfGreaterThan(value, 100);
            _quality = value;
        }
    }

    private int _quality = 90;
}
