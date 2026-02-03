namespace FlexRender.Abstractions;

/// <summary>
/// Loads raw resource data from various sources using Chain of Responsibility pattern.
/// </summary>
/// <remarks>
/// Implementations should handle specific URI schemes (file://, http://, data:, embedded://).
/// The loader chain is ordered by <see cref="Priority"/> (lower values = higher priority).
/// </remarks>
public interface IResourceLoader
{
    /// <summary>
    /// Attempts to load resource data from the specified URI.
    /// </summary>
    /// <param name="uri">Resource URI (file path, http://, data:, embedded://).</param>
    /// <param name="cancellationToken">Cancellation token for async operation.</param>
    /// <returns>Stream with resource data or null if this loader cannot handle the URI.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="uri"/> is null.</exception>
    Task<Stream?> Load(string uri, CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks if this loader can handle the specified URI scheme.
    /// </summary>
    /// <param name="uri">The URI to check.</param>
    /// <returns>True if this loader can handle the URI; otherwise, false.</returns>
    bool CanHandle(string uri);

    /// <summary>
    /// Gets the priority for chain ordering (lower = higher priority).
    /// </summary>
    /// <remarks>
    /// Default priorities:
    /// <list type="bullet">
    /// <item><description>0-99: High priority (embedded resources, base64)</description></item>
    /// <item><description>100-199: Normal priority (file system)</description></item>
    /// <item><description>200+: Low priority (HTTP, external sources)</description></item>
    /// </list>
    /// </remarks>
    int Priority { get; }
}
