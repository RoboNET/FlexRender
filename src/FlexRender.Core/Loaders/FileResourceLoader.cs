using FlexRender.Abstractions;
using FlexRender.Configuration;

namespace FlexRender.Loaders;

/// <summary>
/// Loads resources from the local file system.
/// </summary>
/// <remarks>
/// This loader handles local file paths that are not URLs.
/// It supports both absolute and relative paths, with relative paths
/// resolved against the <see cref="FlexRenderOptions.BasePath"/> setting.
/// </remarks>
public sealed class FileResourceLoader : IResourceLoader
{
    private readonly FlexRenderOptions _options;

    /// <summary>
    /// Initializes a new instance of the <see cref="FileResourceLoader"/> class.
    /// </summary>
    /// <param name="options">The FlexRender configuration options.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="options"/> is null.</exception>
    public FileResourceLoader(FlexRenderOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        _options = options;
    }

    /// <inheritdoc />
    /// <remarks>
    /// File loader has normal priority (100) as it handles local resources
    /// which are typically faster than remote sources.
    /// </remarks>
    public int Priority => 100;

    /// <inheritdoc />
    /// <remarks>
    /// Returns <c>true</c> for URIs that look like file paths or use the "file:" / "file://" scheme.
    /// Rejects anything with other URI schemes (e.g., "http://", "data:", "base64:", "embedded://").
    /// </remarks>
    public bool CanHandle(string uri)
    {
        if (string.IsNullOrWhiteSpace(uri))
        {
            return false;
        }

        // Allow "file:" and "file://" scheme — this loader handles local files
        if (uri.StartsWith("file:", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        // Reject any URI with a "://" scheme (e.g., "http://", "data://", "custom://")
        if (uri.Contains("://", StringComparison.Ordinal))
        {
            return false;
        }

        // Reject scheme-like prefixes without "://" (e.g., "data:", "base64:")
        // A URI scheme is [a-zA-Z][a-zA-Z0-9+.-]*: (min 2 chars to exclude Windows drive letters like "C:")
        var colonIndex = uri.IndexOf(':');
        if (colonIndex > 1 && colonIndex < 20)
        {
            var scheme = uri.AsSpan(0, colonIndex);
            if (char.IsLetter(scheme[0]) && IsValidScheme(scheme))
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// Checks whether a span represents a valid URI scheme (letters, digits, +, ., -).
    /// </summary>
    private static bool IsValidScheme(ReadOnlySpan<char> scheme)
    {
        foreach (var c in scheme)
        {
            if (!char.IsLetterOrDigit(c) && c != '+' && c != '.' && c != '-')
            {
                return false;
            }
        }

        return true;
    }

    /// <inheritdoc />
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="uri"/> is null.</exception>
    /// <exception cref="ArgumentException">Thrown when path traversal is detected (contains "..").</exception>
    /// <exception cref="FileNotFoundException">Thrown when the file does not exist.</exception>
    public Task<Stream?> Load(string uri, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(uri);

        if (!CanHandle(uri))
        {
            return Task.FromResult<Stream?>(null);
        }

        // Strip file: scheme prefix if present
        // Supports: "file:///path" (RFC 8089), "file://path", "file:path"
        var path = uri;
        if (path.StartsWith("file:///", StringComparison.OrdinalIgnoreCase))
            path = path["file:///".Length..];
        else if (path.StartsWith("file://", StringComparison.OrdinalIgnoreCase))
            path = path["file://".Length..];
        else if (path.StartsWith("file:", StringComparison.OrdinalIgnoreCase))
            path = path["file:".Length..];

        ValidatePathSecurity(path);

        var fullPath = ResolvePath(path);

        if (!File.Exists(fullPath))
        {
            throw new FileNotFoundException($"File not found: {uri}", fullPath);
        }

        cancellationToken.ThrowIfCancellationRequested();

        Stream stream = File.OpenRead(fullPath);
        return Task.FromResult<Stream?>(stream);
    }

    /// <summary>
    /// Validates the path for security issues such as path traversal attacks.
    /// </summary>
    /// <param name="path">The path to validate.</param>
    /// <exception cref="ArgumentException">Thrown when path traversal is detected.</exception>
    private static void ValidatePathSecurity(string path)
    {
        if (path.Contains(".."))
        {
            throw new ArgumentException(
                $"Invalid path (path traversal detected): {path}",
                nameof(path));
        }
    }

    /// <summary>
    /// Resolves a relative or absolute path to a full file system path.
    /// </summary>
    /// <param name="path">The path to resolve.</param>
    /// <returns>The fully resolved absolute path.</returns>
    private string ResolvePath(string path)
    {
        if (Path.IsPathRooted(path))
        {
            return Path.GetFullPath(path);
        }

        if (!string.IsNullOrEmpty(_options.BasePath))
        {
            return Path.GetFullPath(Path.Combine(_options.BasePath, path));
        }

        return Path.GetFullPath(path);
    }
}
