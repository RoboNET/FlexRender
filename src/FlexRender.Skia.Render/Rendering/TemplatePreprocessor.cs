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
    /// </summary>
    /// <param name="template">The processed template containing font definitions.</param>
    internal void RegisterFonts(Template template)
    {
        ArgumentNullException.ThrowIfNull(template);
        RegisterTemplateFonts(template);
    }

    /// <summary>
    /// Registers all fonts defined in the template with the font manager.
    /// If a font named "default" is defined, it is also registered as "main"
    /// to serve as the default font for elements without an explicit font specification.
    /// </summary>
    /// <param name="template">The template containing font definitions.</param>
    private void RegisterTemplateFonts(Template template)
    {
        foreach (var (fontName, fontDef) in template.Fonts)
        {
            var resolvedPath = ResolveFontPath(fontDef.Path);
            _fontManager.RegisterFont(fontName, resolvedPath, fontDef.Fallback);

            // Register "default" font also as "main" for elements without explicit font
            if (string.Equals(fontName, "default", StringComparison.OrdinalIgnoreCase))
            {
                _fontManager.RegisterFont("main", resolvedPath, fontDef.Fallback);
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
