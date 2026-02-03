namespace FlexRender.Parsing.Ast;

/// <summary>
/// Defines a font registration for use in templates.
/// </summary>
public sealed class FontDefinition
{
    /// <summary>
    /// Gets or sets the path to the font file (.ttf, .otf).
    /// </summary>
    public string Path { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the optional fallback system font family name.
    /// </summary>
    public string? Fallback { get; set; }

    /// <summary>
    /// Initializes a new instance of the <see cref="FontDefinition"/> class.
    /// </summary>
    public FontDefinition()
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="FontDefinition"/> class with the specified path.
    /// </summary>
    /// <param name="path">The path to the font file.</param>
    public FontDefinition(string path)
    {
        Path = path;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="FontDefinition"/> class with the specified path and fallback.
    /// </summary>
    /// <param name="path">The path to the font file.</param>
    /// <param name="fallback">The fallback system font family name.</param>
    public FontDefinition(string path, string? fallback)
    {
        Path = path;
        Fallback = fallback;
    }
}
