using FlexRender.Abstractions;
using FlexRender.Parsing.Ast;

namespace FlexRender.TemplateEngine;

/// <summary>
/// Resolves a content element source into either text or binary data.
/// Handles variable expressions, base64-encoded payloads, and URI-based resource loading.
/// </summary>
public sealed class ContentSourceResolver
{
    /// <summary>
    /// Asynchronously resolves a content source expression into a <see cref="ContentSource"/>.
    /// Uses <c>await</c> for resource loader calls instead of blocking synchronously.
    /// </summary>
    /// <param name="source">The source expression to resolve.</param>
    /// <param name="context">The template context for variable resolution.</param>
    /// <param name="loaders">Optional resource loaders for URI-based content.</param>
    /// <param name="substituteVariables">Optional function to substitute template variables in strings.</param>
    /// <returns>
    /// A <see cref="BinaryContent"/> if the source resolves to binary data (bytes variable, base64, or loaded resource),
    /// or a <see cref="TextContent"/> for plain text sources.
    /// </returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="context"/> is null.</exception>
    /// <exception cref="TemplateEngineException">Thrown when base64 data is invalid.</exception>
    public static async ValueTask<ContentSource> ResolveAsync(
        ExprValue<string> source,
        TemplateContext context,
        IReadOnlyList<IResourceLoader>? loaders,
        Func<string?, TemplateContext, string?>? substituteVariables = null)
    {
        ArgumentNullException.ThrowIfNull(context);

        // Step 1: Check if pure {{variable}} resolves to BytesValue
        var bytesValue = TryResolveBytes(source, context);
        if (bytesValue is not null)
        {
            return new BinaryContent(bytesValue.Memory, bytesValue.MimeType);
        }

        // Step 2: Resolve source as string
        var rawText = source.RawValue ?? source.Value;
        var resolvedSource = substituteVariables?.Invoke(rawText, context) ?? rawText;

        if (resolvedSource is null)
        {
            return new TextContent(string.Empty);
        }

        // Step 3: Check "base64:" prefix
        if (resolvedSource.StartsWith("base64:", StringComparison.Ordinal))
        {
            var base64Payload = resolvedSource["base64:".Length..];
            byte[] decodedBytes;
            try
            {
                decodedBytes = Convert.FromBase64String(base64Payload);
            }
            catch (FormatException ex)
            {
                throw new TemplateEngineException(
                    $"Invalid base64 data in content source: {ex.Message}", ex);
            }

            return new BinaryContent(decodedBytes);
        }

        // Step 4: Explicit "text:" prefix — always treat as text, skip file heuristic
        if (resolvedSource.StartsWith("text:", StringComparison.Ordinal))
        {
            return new TextContent(resolvedSource["text:".Length..]);
        }

        // Step 5: Explicit "file:" scheme — strict, throws on failure
        if (resolvedSource.StartsWith("file:", StringComparison.OrdinalIgnoreCase))
        {
            var filePath = resolvedSource.StartsWith("file:///", StringComparison.OrdinalIgnoreCase)
                ? resolvedSource["file:///".Length..]
                : resolvedSource["file:".Length..];

            ValidateFilePath(filePath);
            return await LoadFromLoadersAsync(filePath, loaders).ConfigureAwait(false);
        }

        // Step 6: Try resource loaders opportunistically (no scheme — best effort)
        if (loaders is not null)
        {
            var loaded = await TryLoadFromLoadersAsync(resolvedSource, loaders).ConfigureAwait(false);
            if (loaded is not null)
            {
                return loaded;
            }
        }

        // Step 7: Fall back to text
        return new TextContent(resolvedSource);
    }

    /// <summary>
    /// Asynchronously loads binary content from resource loaders in strict mode.
    /// Throws <see cref="TemplateEngineException"/> if no loader can handle the path or the file is not found.
    /// </summary>
    private static async Task<BinaryContent> LoadFromLoadersAsync(string path, IReadOnlyList<IResourceLoader>? loaders)
    {
        if (loaders is not null)
        {
            foreach (var loader in loaders)
            {
                if (loader.CanHandle(path))
                {
                    var stream = await loader.Load(path).ConfigureAwait(false);
                    if (stream is not null)
                    {
                        using (stream)
                        {
                            var loaded = BytesValue.FromStream(stream);
                            return new BinaryContent(loaded.Memory, loaded.MimeType);
                        }
                    }
                }
            }
        }

        throw new TemplateEngineException(
            $"Cannot load content from 'file:{path}': file not found or no suitable resource loader registered.");
    }

    /// <summary>
    /// Asynchronously attempts to load binary content from resource loaders opportunistically.
    /// Only tries loading if the source looks like a file path (contains path separators or a file extension).
    /// Returns <c>null</c> for plain text sources.
    /// </summary>
    private static async ValueTask<BinaryContent?> TryLoadFromLoadersAsync(string source, IReadOnlyList<IResourceLoader> loaders)
    {
        if (!LooksLikeFilePath(source))
        {
            return null;
        }

        foreach (var loader in loaders)
        {
            if (loader.CanHandle(source))
            {
                var stream = await loader.Load(source).ConfigureAwait(false);
                if (stream is not null)
                {
                    using (stream)
                    {
                        var loaded = BytesValue.FromStream(stream);
                        return new BinaryContent(loaded.Memory, loaded.MimeType);
                    }
                }
            }
        }

        return null;
    }

    /// <summary>
    /// Heuristic: determines if a string looks like a file path rather than plain text content.
    /// Returns <c>true</c> if the source contains path separators or ends with a file extension.
    /// </summary>
    private static bool LooksLikeFilePath(string source)
    {
        // Multiline content is never a file path
        if (source.Contains('\n', StringComparison.Ordinal) || source.Contains('\r', StringComparison.Ordinal))
        {
            return false;
        }

        // Very long strings are unlikely to be file paths
        if (source.Length > 260)
        {
            return false;
        }

        // Contains whitespace (other than in file names with spaces) — likely text content
        // File paths may have spaces, but combined with other indicators we skip them
        if (source.Contains("  ", StringComparison.Ordinal))
        {
            return false;
        }

        // Contains path separator
        if (source.Contains('/', StringComparison.Ordinal) || source.Contains('\\', StringComparison.Ordinal))
        {
            return true;
        }

        // Ends with a file extension (e.g., ".txt", ".bin", ".dat")
        var lastDot = source.LastIndexOf('.');
        if (lastDot > 0 && lastDot < source.Length - 1)
        {
            var ext = source[lastDot..];
            // Extension is short (1-5 chars) and contains no spaces
            if (ext.Length is >= 2 and <= 6 && !ext.Contains(' ', StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Validates that a file path does not contain path traversal sequences.
    /// </summary>
    /// <exception cref="TemplateEngineException">Thrown when the path contains directory traversal.</exception>
    private static void ValidateFilePath(string path)
    {
        if (path.Contains("..", StringComparison.Ordinal))
        {
            throw new TemplateEngineException(
                $"Path traversal detected in content source: '{path}'. Directory traversal ('..') is not allowed.");
        }
    }

    /// <summary>
    /// Attempts to resolve a source expression directly to a <see cref="BytesValue"/>.
    /// Only matches pure <c>{{variable}}</c> expressions (no mixed text or nested expressions).
    /// </summary>
    /// <param name="source">The source expression value.</param>
    /// <param name="context">The template context for variable resolution.</param>
    /// <returns>The <see cref="BytesValue"/> if the expression resolves to one; otherwise, <c>null</c>.</returns>
    private static BytesValue? TryResolveBytes(ExprValue<string> source, TemplateContext context)
    {
        var raw = source.RawValue ?? source.Value;
        if (raw is null)
        {
            return null;
        }

        if (!raw.StartsWith("{{", StringComparison.Ordinal) || !raw.EndsWith("}}", StringComparison.Ordinal))
        {
            return null;
        }

        var inner = raw[2..^2].Trim();
        if (inner.Contains("{{", StringComparison.Ordinal))
        {
            return null;
        }

        var resolved = ExpressionEvaluator.Resolve(inner, context);
        return resolved as BytesValue;
    }
}
