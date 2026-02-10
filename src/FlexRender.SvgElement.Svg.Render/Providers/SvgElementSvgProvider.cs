using FlexRender.Abstractions;
using FlexRender.Loaders;
using FlexRender.Providers;
using FlexRender.Rendering;

namespace FlexRender.SvgElement.Svg.Providers;

/// <summary>
/// Provides SVG-native rendering for SVG elements.
/// Loads inline SVG content or resolves SVG sources via resource loaders and returns markup.
/// </summary>
public sealed class SvgElementSvgProvider : ISvgContentProvider<Parsing.Ast.SvgElement>, IResourceLoaderAware
{
    private IReadOnlyList<IResourceLoader>? _loaders;

    /// <summary>
    /// Sets the resource loaders for loading SVG content from URIs.
    /// </summary>
    /// <param name="loaders">The ordered collection of resource loaders.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="loaders"/> is null.</exception>
    public void SetResourceLoaders(IReadOnlyList<IResourceLoader> loaders)
    {
        ArgumentNullException.ThrowIfNull(loaders);
        _loaders = loaders;
    }

    /// <summary>
    /// Generates SVG markup for the specified SVG element.
    /// </summary>
    /// <param name="element">The SVG element containing inline content or a source URI.</param>
    /// <param name="width">The allocated width in SVG user units.</param>
    /// <param name="height">The allocated height in SVG user units.</param>
    /// <returns>SVG markup to embed in the output document.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="element"/> is null.</exception>
    /// <exception cref="ArgumentException">
    /// Thrown when neither <see cref="Parsing.Ast.SvgElement.Src"/> nor
    /// <see cref="Parsing.Ast.SvgElement.Content"/> is specified.
    /// </exception>
    /// <exception cref="InvalidOperationException">
    /// Thrown when SVG content exceeds <see cref="SvgContentLoader.MaxSvgContentSize"/> or cannot be loaded.
    /// </exception>
    public string GenerateSvgContent(Parsing.Ast.SvgElement element, float width, float height)
    {
        ArgumentNullException.ThrowIfNull(element);
        _ = width;
        _ = height;

        if (!string.IsNullOrEmpty(element.Content))
        {
            return SvgFormatting.SanitizeSvgContent(element.Content);
        }

        if (string.IsNullOrEmpty(element.Src))
        {
            throw new ArgumentException(
                "SVG element must have either 'src' or 'content' specified.",
                nameof(element));
        }

        var svgContent = SvgContentLoader.LoadFromLoaders(_loaders, element.Src!);
        if (svgContent is not null)
        {
            return SvgFormatting.SanitizeSvgContent(svgContent);
        }

        // Fallback to direct file loading if resource loaders were not available.
        var path = element.Src!;
        if (!File.Exists(path))
        {
            throw new InvalidOperationException($"Failed to load SVG content from '{path}'.");
        }

        return SvgFormatting.SanitizeSvgContent(SvgContentLoader.ReadFileWithLimit(path));
    }
}
