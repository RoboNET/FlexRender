using FlexRender.Configuration;
using FlexRender.Parsing.Ast;

namespace FlexRender.Rendering;

/// <summary>
/// Handles font registration for Skia rendering.
/// Expression resolution and element processing are handled by <see cref="FlexRender.TemplateEngine.TemplatePipeline"/>.
/// </summary>
internal sealed class TemplatePreprocessor
{
    private readonly FontManager _fontManager;
    private readonly FlexRenderOptions? _options;

    /// <summary>
    /// Initializes a new instance of the <see cref="TemplatePreprocessor"/> class.
    /// </summary>
    /// <param name="fontManager">The font manager for font registration.</param>
    /// <param name="options">Optional configuration options for path resolution.</param>
    internal TemplatePreprocessor(
        FontManager fontManager,
        FlexRenderOptions? options)
    {
        ArgumentNullException.ThrowIfNull(fontManager);
        _fontManager = fontManager;
        _options = options;
    }

    /// <summary>
    /// Registers all fonts defined in the template with the font manager.
    /// Tries file system first, then falls back to resource loaders (async) for WASM support.
    /// </summary>
    /// <param name="template">The processed template containing font definitions.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    internal async Task RegisterFontsAsync(Template template, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(template);
        await RegisterTemplateFontsAsync(template, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Registers all fonts defined in the template with the font manager.
    /// If a font named "default" is defined, it is also registered as "main"
    /// to serve as the default font for elements without an explicit font specification.
    /// Falls back to resource loaders when font file is not found on disk.
    /// </summary>
    /// <param name="template">The template containing font definitions.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    private async Task RegisterTemplateFontsAsync(Template template, CancellationToken cancellationToken)
    {
        foreach (var (fontName, fontDef) in template.Fonts)
        {
            var resolvedPath = ResolveFontPath(fontDef.Path);
            var registered = _fontManager.RegisterFont(fontName, resolvedPath, fontDef.Fallback);

            // If file not found on disk, try resource loaders (e.g. MemoryResourceLoader for WASM)
            if (!registered)
            {
                if (!await _fontManager.PreloadFontFromResourcesAsync(fontName, resolvedPath, cancellationToken).ConfigureAwait(false))
                {
                    await _fontManager.PreloadFontFromResourcesAsync(fontName, fontDef.Path, cancellationToken).ConfigureAwait(false);
                }
            }

            // Register "default" font also as "main" for elements without explicit font
            if (string.Equals(fontName, "default", StringComparison.OrdinalIgnoreCase))
            {
                var mainRegistered = _fontManager.RegisterFont("main", resolvedPath, fontDef.Fallback);
                if (!mainRegistered)
                {
                    if (!await _fontManager.PreloadFontFromResourcesAsync("main", resolvedPath, cancellationToken).ConfigureAwait(false))
                    {
                        await _fontManager.PreloadFontFromResourcesAsync("main", fontDef.Path, cancellationToken).ConfigureAwait(false);
                    }
                }
            }
        }
    }

    /// <summary>
    /// Resolves a font path, applying the base path if the path is relative.
    /// </summary>
    /// <param name="path">The font path from the template.</param>
    /// <returns>The resolved absolute path.</returns>
    private string ResolveFontPath(string path)
    {
        if (Path.IsPathRooted(path))
        {
            return Path.GetFullPath(path);
        }

        if (_options?.BasePath is not null)
        {
            return Path.GetFullPath(Path.Combine(_options.BasePath, path));
        }

        return Path.GetFullPath(path);
    }
}
