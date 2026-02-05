namespace FlexRender.Http;

/// <summary>
/// Configuration options for <see cref="HttpResourceLoader"/>.
/// </summary>
public sealed class HttpResourceLoaderOptions
{
    private TimeSpan _timeout = TimeSpan.FromSeconds(30);
    private int _maxResourceSize = 10 * 1024 * 1024;

    /// <summary>
    /// Gets or sets the timeout for HTTP requests.
    /// </summary>
    /// <value>Default: 30 seconds.</value>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when value is zero or negative.</exception>
    public TimeSpan Timeout
    {
        get => _timeout;
        set
        {
            if (value <= TimeSpan.Zero)
            {
                throw new ArgumentOutOfRangeException(nameof(value), value,
                    "Timeout must be a positive duration.");
            }

            _timeout = value;
        }
    }

    /// <summary>
    /// Gets or sets the maximum allowed resource size in bytes.
    /// </summary>
    /// <value>Default: 10 MB (10,485,760 bytes).</value>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when value is zero or negative.</exception>
    public int MaxResourceSize
    {
        get => _maxResourceSize;
        set
        {
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(value);
            _maxResourceSize = value;
        }
    }
}
