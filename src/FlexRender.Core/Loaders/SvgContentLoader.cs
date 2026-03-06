using System.Text;
using FlexRender.Abstractions;
using FlexRender.Parsing.Ast;
using FlexRender.Rendering;

namespace FlexRender.Loaders;

/// <summary>
/// Loads SVG content from resource loaders or files with size limits.
/// </summary>
public static class SvgContentLoader
{
    /// <summary>
    /// Maximum allowed SVG content size in bytes (10 MB).
    /// </summary>
    public const int MaxSvgContentSize = 10 * 1024 * 1024;

    /// <summary>
    /// Asynchronously loads SVG content using the configured resource loaders.
    /// Returns null when no loader can handle the URI.
    /// </summary>
    /// <param name="loaders">The resource loader chain.</param>
    /// <param name="uri">The URI to load.</param>
    /// <returns>SVG content string or null if not handled.</returns>
    /// <exception cref="InvalidOperationException">Thrown when content exceeds size limit.</exception>
    public static async ValueTask<string?> LoadFromLoaders(IReadOnlyList<IResourceLoader>? loaders, string uri)
    {
        if (loaders is null || loaders.Count == 0)
        {
            return null;
        }

        foreach (var loader in loaders)
        {
            if (!loader.CanHandle(uri))
            {
                continue;
            }

            var stream = await loader.Load(uri).ConfigureAwait(false);
            if (stream is null)
            {
                continue;
            }

            using (stream)
            {
                var content = ReadStreamWithLimit(stream, uri);
                return SvgFormatting.SanitizeSvgContent(content);
            }
        }

        return null;
    }

    /// <summary>
    /// Collects all unique SVG source URIs from the processed template element tree.
    /// Only collects URIs where <see cref="SvgElement.Src"/> has a value and
    /// <see cref="SvgElement.Content"/> is empty (content-based SVGs don't need loading).
    /// </summary>
    /// <param name="template">The template to collect URIs from.</param>
    /// <returns>A set of unique SVG source URIs.</returns>
    public static HashSet<string> CollectSvgUris(Template template)
    {
        ArgumentNullException.ThrowIfNull(template);
        var uris = new HashSet<string>(StringComparer.Ordinal);
        CollectSvgUrisFromElements(template.Elements, uris);
        return uris;
    }

    /// <summary>
    /// Recursively collects SVG source URIs from a list of elements.
    /// </summary>
    private static void CollectSvgUrisFromElements(IReadOnlyList<TemplateElement> elements, HashSet<string> uris)
    {
        foreach (var element in elements)
        {
            if (element is SvgElement svg &&
                !string.IsNullOrEmpty(svg.Src.Value) &&
                string.IsNullOrEmpty(svg.Content.Value))
            {
                uris.Add(svg.Src.Value);
            }
            else if (element is FlexElement flex)
            {
                CollectSvgUrisFromElements(flex.Children, uris);
            }
        }
    }

    /// <summary>
    /// Reads a file from disk with size validation.
    /// </summary>
    /// <param name="path">The file path.</param>
    /// <returns>The file contents as UTF-8 text.</returns>
    /// <exception cref="InvalidOperationException">Thrown when content exceeds size limit.</exception>
    public static string ReadFileWithLimit(string path)
    {
        var fileInfo = new FileInfo(path);
        if (fileInfo.Length > MaxSvgContentSize)
        {
            throw new InvalidOperationException(
                $"SVG content at '{path}' exceeds maximum allowed size of {MaxSvgContentSize} bytes " +
                $"(actual size: {fileInfo.Length} bytes).");
        }

        return SvgFormatting.SanitizeSvgContent(File.ReadAllText(path, Encoding.UTF8));
    }

    private static string ReadStreamWithLimit(Stream stream, string uri)
    {
        if (stream.CanSeek && stream.Length > MaxSvgContentSize)
        {
            throw new InvalidOperationException(
                $"SVG content at '{uri}' exceeds maximum allowed size of {MaxSvgContentSize} bytes " +
                $"(actual size: {stream.Length} bytes).");
        }

        const int bufferSize = 16 * 1024;
        var totalBytes = 0;
        using var ms = new MemoryStream();
        var buffer = new byte[bufferSize];
        int bytesRead;
        while ((bytesRead = stream.Read(buffer, 0, buffer.Length)) > 0)
        {
            totalBytes += bytesRead;
            if (totalBytes > MaxSvgContentSize)
            {
                throw new InvalidOperationException(
                    $"SVG content at '{uri}' exceeds maximum allowed size of {MaxSvgContentSize} bytes.");
            }

            ms.Write(buffer, 0, bytesRead);
        }

        ms.Position = 0;
        using var reader = new StreamReader(ms, Encoding.UTF8, detectEncodingFromByteOrderMarks: true);
        return reader.ReadToEnd();
    }
}
