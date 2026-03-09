using FlexRender.Abstractions;

namespace FlexRender.Playground;

/// <summary>
/// In-memory resource loader for browser-uploaded files (fonts, images, NDC content).
/// Uploaded resources take highest priority so they override built-in loaders.
/// </summary>
internal sealed class MemoryResourceLoader : IResourceLoader
{
    private readonly Dictionary<string, byte[]> _resources = new(StringComparer.OrdinalIgnoreCase);

    /// <inheritdoc />
    /// <remarks>Priority 10 ensures uploaded files override all other loaders.</remarks>
    public int Priority => 10;

    /// <inheritdoc />
    public bool CanHandle(string uri)
    {
        if (string.IsNullOrWhiteSpace(uri))
        {
            return false;
        }

        return _resources.ContainsKey(NormalizePath(uri))
            || _resources.ContainsKey(Path.GetFileName(uri));
    }

    /// <inheritdoc />
    public Task<Stream?> Load(string uri, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(uri);

        var normalized = NormalizePath(uri);
        if (_resources.TryGetValue(normalized, out var data))
        {
            Stream stream = new MemoryStream(data, writable: false);
            return Task.FromResult<Stream?>(stream);
        }

        // Also try by filename only (handles absolute paths from ResolveFontPath)
        var fileName = Path.GetFileName(uri);
        if (_resources.TryGetValue(fileName, out data))
        {
            Stream stream = new MemoryStream(data, writable: false);
            return Task.FromResult<Stream?>(stream);
        }

        return Task.FromResult<Stream?>(null);
    }

    /// <summary>
    /// Stores a resource in memory, keyed by its normalized name.
    /// </summary>
    /// <param name="name">The resource name or path.</param>
    /// <param name="data">The raw bytes of the resource.</param>
    public void AddResource(string name, byte[] data)
    {
        ArgumentNullException.ThrowIfNull(name);
        ArgumentNullException.ThrowIfNull(data);

        _resources[NormalizePath(name)] = data;
    }

    /// <summary>
    /// Removes a previously stored resource.
    /// </summary>
    /// <param name="name">The resource name or path.</param>
    public void RemoveResource(string name)
    {
        ArgumentNullException.ThrowIfNull(name);

        _resources.Remove(NormalizePath(name));
    }

    /// <summary>
    /// Removes all stored resources.
    /// </summary>
    public void Clear()
    {
        _resources.Clear();
    }

    /// <summary>
    /// Strips leading "./" or "/" from a path to produce a canonical lookup key.
    /// </summary>
    private static string NormalizePath(string path)
    {
        if (path.StartsWith("./", StringComparison.Ordinal))
        {
            path = path[2..];
        }
        else if (path.StartsWith('/'))
        {
            path = path[1..];
        }

        return path;
    }
}
