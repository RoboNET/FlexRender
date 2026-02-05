namespace FlexRender.Configuration;

/// <summary>
/// Configurable security limits for the FlexRender rendering pipeline.
/// All defaults are safe-by-default values that prevent resource exhaustion.
/// </summary>
public sealed class ResourceLimits
{
    private long _maxTemplateFileSize = 1024 * 1024;
    private long _maxDataFileSize = 10L * 1024 * 1024;
    private int _maxTemplateNestingDepth = 100;
    private int _maxRenderDepth = 100;
    private int _maxImageSize = 10 * 1024 * 1024;
    /// <summary>
    /// Maximum allowed YAML template file size in bytes.
    /// </summary>
    /// <value>Default: 1 MB (1,048,576 bytes).</value>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when value is zero or negative.</exception>
    public long MaxTemplateFileSize
    {
        get => _maxTemplateFileSize;
        set
        {
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(value);
            _maxTemplateFileSize = value;
        }
    }

    /// <summary>
    /// Maximum allowed JSON data file size in bytes.
    /// This value is intended for callers that pass it to <c>DataLoader.LoadFromFile(path, limits.MaxDataFileSize)</c>.
    /// </summary>
    /// <value>Default: 10 MB (10,485,760 bytes).</value>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when value is zero or negative.</exception>
    public long MaxDataFileSize
    {
        get => _maxDataFileSize;
        set
        {
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(value);
            _maxDataFileSize = value;
        }
    }

    /// <summary>
    /// Maximum nesting depth for template engine control flow blocks.
    /// </summary>
    /// <value>Default: 100.</value>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when value is zero or negative.</exception>
    public int MaxTemplateNestingDepth
    {
        get => _maxTemplateNestingDepth;
        set
        {
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(value);
            _maxTemplateNestingDepth = value;
        }
    }

    /// <summary>
    /// Maximum recursion depth when rendering the layout tree.
    /// </summary>
    /// <value>Default: 100.</value>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when value is zero or negative.</exception>
    public int MaxRenderDepth
    {
        get => _maxRenderDepth;
        set
        {
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(value);
            _maxRenderDepth = value;
        }
    }

    /// <summary>
    /// Maximum allowed image size in bytes for image loading.
    /// </summary>
    /// <value>Default: 10 MB (10,485,760 bytes).</value>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when value is zero or negative.</exception>
    public int MaxImageSize
    {
        get => _maxImageSize;
        set
        {
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(value);
            _maxImageSize = value;
        }
    }
}
