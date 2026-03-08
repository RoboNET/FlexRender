using FlexRender.Abstractions;
using FlexRender.Parsing.Ast;

namespace FlexRender.TemplateEngine;

/// <summary>
/// Resolves a content element source into either text or binary data.
/// Delegates all URI-based resolution to the <see cref="IResourceLoader"/> chain.
/// </summary>
public sealed class ContentSourceResolver
{
    /// <summary>
    /// Asynchronously resolves a content source expression into a <see cref="ContentSource"/>.
    /// </summary>
    /// <param name="source">The source expression to resolve.</param>
    /// <param name="context">The template context for variable resolution.</param>
    /// <param name="loaders">Resource loaders for URI-based content (data:, file, http, embedded).</param>
    /// <param name="substituteVariables">Optional function to substitute template variables in strings.</param>
    /// <returns>
    /// A <see cref="BinaryContent"/> if the source resolves to binary data,
    /// or a <see cref="TextContent"/> for plain text sources.
    /// </returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="context"/> is null.</exception>
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

        // Step 3: "text:" prefix — always treat as text, bypass loaders
        if (resolvedSource.StartsWith("text:", StringComparison.Ordinal))
        {
            return new TextContent(resolvedSource["text:".Length..]);
        }

        // Step 4: Delegate everything to IResourceLoader chain
        // Handles data: URIs, base64: shorthand, file: URIs, http(s):// URLs,
        // embedded:// resources, and relative file paths.
        // This handles data: URIs, file: URIs, http(s):// URLs, embedded:// resources,
        // and relative file paths — all through the same loader pipeline as images.
        if (loaders is not null)
        {
            var loaded = await TryLoadFromLoadersAsync(resolvedSource, loaders).ConfigureAwait(false);
            if (loaded is not null)
            {
                return loaded;
            }
        }

        // Step 6: No loader handled it — treat as plain text content
        return new TextContent(resolvedSource);
    }

    /// <summary>
    /// Attempts to load binary content by delegating to the resource loader chain.
    /// Returns <c>null</c> if no loader can handle the source.
    /// </summary>
    private static async ValueTask<BinaryContent?> TryLoadFromLoadersAsync(
        string source,
        IReadOnlyList<IResourceLoader> loaders)
    {
        foreach (var loader in loaders)
        {
            if (loader.CanHandle(source))
            {
                try
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
                catch (Exception) when (loader is not Loaders.Base64ResourceLoader)
                {
                    // Loader claimed it can handle this URI but failed —
                    // continue to next loader or fall through to text.
                }
            }
        }

        return null;
    }

    /// <summary>
    /// Attempts to resolve a source expression directly to a <see cref="BytesValue"/>.
    /// Only matches pure <c>{{variable}}</c> expressions (no mixed text or nested expressions).
    /// </summary>
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
