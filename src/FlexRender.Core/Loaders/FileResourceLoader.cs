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
    /// Validates the path for security issues: path traversal and absolute paths.
    /// </summary>
    /// <param name="path">The path to validate.</param>
    /// <exception cref="ArgumentException">Thrown when the path is absolute or contains traversal sequences.</exception>
    private static void ValidatePathSecurity(string path)
    {
        // Reject URL-encoded characters that could hide traversal sequences
        if (path.Contains('%'))
        {
            throw new ArgumentException(
                $"URL-encoded characters are not allowed in file paths: {path}",
                nameof(path));
        }

        if (path.Contains(".."))
        {
            throw new ArgumentException(
                $"Invalid path (path traversal detected): {path}",
                nameof(path));
        }

        if (Path.IsPathRooted(path))
        {
            throw new ArgumentException(
                $"Absolute paths are not allowed for security reasons: {path}. Use relative paths resolved against BasePath.",
                nameof(path));
        }
    }

    /// <summary>
    /// Resolves a relative path against BasePath and validates the result stays within bounds.
    /// </summary>
    /// <param name="path">The relative path to resolve.</param>
    /// <returns>The fully resolved absolute path.</returns>
    /// <exception cref="ArgumentException">Thrown when the resolved path escapes the base directory.</exception>
    private string ResolvePath(string path)
    {
        var basePath = !string.IsNullOrEmpty(_options.BasePath)
            ? Path.GetFullPath(_options.BasePath)
            : Path.GetFullPath(".");

        var fullPath = Path.GetFullPath(Path.Combine(basePath, path));

        // Ensure the resolved path is still within the base directory
        if (!fullPath.StartsWith(basePath, StringComparison.Ordinal))
        {
            throw new ArgumentException(
                $"Path '{path}' resolves outside the base directory.",
                nameof(path));
        }

        return fullPath;
    }
}
