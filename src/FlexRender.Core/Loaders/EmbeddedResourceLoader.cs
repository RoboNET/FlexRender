using System.Reflection;
using FlexRender.Abstractions;
using FlexRender.Configuration;

namespace FlexRender.Loaders;

/// <summary>
/// Loads resources from embedded resources in .NET assemblies.
/// </summary>
/// <remarks>
/// This loader handles URIs in the format: embedded://AssemblyName/Resource.Path.Name
/// For example: embedded://MyApp.Resources/Images.Logo.png
///
/// The loader searches through assemblies registered in
/// <see cref="FlexRenderOptions.EmbeddedResourceAssemblies"/>.
/// </remarks>
public sealed class EmbeddedResourceLoader : IResourceLoader
{
    private const string EmbeddedPrefix = "embedded://";

    private readonly FlexRenderOptions _options;

    /// <summary>
    /// Initializes a new instance of the <see cref="EmbeddedResourceLoader"/> class.
    /// </summary>
    /// <param name="options">The FlexRender configuration options.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="options"/> is null.</exception>
    public EmbeddedResourceLoader(FlexRenderOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        _options = options;
    }

    /// <inheritdoc />
    /// <remarks>
    /// Embedded resource loader has priority 75, placing it between base64 (50)
    /// and file system (100) loaders, as embedded resources are fast to access
    /// but require assembly lookup.
    /// </remarks>
    public int Priority => 75;

    /// <inheritdoc />
    /// <remarks>
    /// Returns <c>true</c> for URIs that start with "embedded://".
    /// </remarks>
    public bool CanHandle(string uri)
    {
        if (string.IsNullOrWhiteSpace(uri))
        {
            return false;
        }

        return uri.StartsWith(EmbeddedPrefix, StringComparison.OrdinalIgnoreCase);
    }

    /// <inheritdoc />
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="uri"/> is null.</exception>
    /// <exception cref="ArgumentException">Thrown when the URI format is invalid.</exception>
    /// <exception cref="FileNotFoundException">Thrown when the embedded resource cannot be found.</exception>
    public Task<Stream?> Load(string uri, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(uri);

        if (!CanHandle(uri))
        {
            return Task.FromResult<Stream?>(null);
        }

        cancellationToken.ThrowIfCancellationRequested();

        var (assemblyName, resourcePath) = ParseEmbeddedUri(uri);
        var stream = FindResourceStream(assemblyName, resourcePath);

        if (stream is null)
        {
            throw new FileNotFoundException(
                $"Embedded resource not found: {uri}. " +
                $"Assembly: '{assemblyName}', Resource: '{resourcePath}'.",
                uri);
        }

        return Task.FromResult<Stream?>(stream);
    }

    /// <summary>
    /// Parses an embedded resource URI into assembly name and resource path components.
    /// </summary>
    /// <param name="uri">The embedded URI in format: embedded://AssemblyName/Resource.Path</param>
    /// <returns>A tuple containing the assembly name and resource path.</returns>
    /// <exception cref="ArgumentException">Thrown when the URI format is invalid.</exception>
    private static (string AssemblyName, string ResourcePath) ParseEmbeddedUri(string uri)
    {
        // Remove the "embedded://" prefix
        var pathPart = uri[EmbeddedPrefix.Length..];

        if (string.IsNullOrWhiteSpace(pathPart))
        {
            throw new ArgumentException(
                $"Invalid embedded URI format. Expected 'embedded://AssemblyName/Resource.Path', got: {uri}",
                nameof(uri));
        }

        var separatorIndex = pathPart.IndexOf('/');

        if (separatorIndex == -1 || separatorIndex == 0 || separatorIndex == pathPart.Length - 1)
        {
            throw new ArgumentException(
                $"Invalid embedded URI format. Expected 'embedded://AssemblyName/Resource.Path', got: {uri}",
                nameof(uri));
        }

        var assemblyName = pathPart[..separatorIndex];
        var resourcePath = pathPart[(separatorIndex + 1)..];

        return (assemblyName, resourcePath);
    }

    /// <summary>
    /// Finds an embedded resource stream by searching through registered assemblies.
    /// </summary>
    /// <param name="assemblyName">The name of the assembly to search in.</param>
    /// <param name="resourcePath">The resource path to find.</param>
    /// <returns>The resource stream if found; otherwise, <c>null</c>.</returns>
    private Stream? FindResourceStream(string assemblyName, string resourcePath)
    {
        // First, try to find the assembly by exact name match
        var targetAssembly = FindAssemblyByName(assemblyName);

        if (targetAssembly is not null)
        {
            var stream = TryGetResourceStream(targetAssembly, resourcePath);
            if (stream is not null)
            {
                return stream;
            }
        }

        // If not found, search all registered assemblies for the resource
        foreach (var assembly in _options.EmbeddedResourceAssemblies)
        {
            var stream = TryGetResourceStream(assembly, resourcePath, assemblyName);
            if (stream is not null)
            {
                return stream;
            }
        }

        return null;
    }

    /// <summary>
    /// Finds an assembly from the registered assemblies by name.
    /// </summary>
    /// <param name="assemblyName">The assembly name to search for.</param>
    /// <returns>The matching assembly if found; otherwise, <c>null</c>.</returns>
    private Assembly? FindAssemblyByName(string assemblyName)
    {
        foreach (var assembly in _options.EmbeddedResourceAssemblies)
        {
            var name = assembly.GetName().Name;
            if (string.Equals(name, assemblyName, StringComparison.OrdinalIgnoreCase))
            {
                return assembly;
            }
        }

        return null;
    }

    /// <summary>
    /// Attempts to get a resource stream from an assembly.
    /// </summary>
    /// <param name="assembly">The assembly to search in.</param>
    /// <param name="resourcePath">The resource path to find.</param>
    /// <param name="namespacePrefix">Optional namespace prefix to prepend to the resource path.</param>
    /// <returns>The resource stream if found; otherwise, <c>null</c>.</returns>
    private static Stream? TryGetResourceStream(Assembly assembly, string resourcePath, string? namespacePrefix = null)
    {
        // Try direct resource path
        var stream = assembly.GetManifestResourceStream(resourcePath);
        if (stream is not null)
        {
            return stream;
        }

        // Try with namespace prefix
        if (!string.IsNullOrEmpty(namespacePrefix))
        {
            var fullPath = $"{namespacePrefix}.{resourcePath}";
            stream = assembly.GetManifestResourceStream(fullPath);
            if (stream is not null)
            {
                return stream;
            }
        }

        // Try with assembly name as namespace
        var assemblyName = assembly.GetName().Name;
        if (!string.IsNullOrEmpty(assemblyName))
        {
            var fullPath = $"{assemblyName}.{resourcePath}";
            stream = assembly.GetManifestResourceStream(fullPath);
            if (stream is not null)
            {
                return stream;
            }
        }

        return null;
    }
}
