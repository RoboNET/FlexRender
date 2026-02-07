namespace FlexRender.Parsing.Ast;

/// <summary>
/// Parsed template representation.
/// </summary>
public sealed class Template
{
    private List<TemplateElement> _elements = new();

    /// <summary>
    /// Template name for identification.
    /// </summary>
    public string? Name { get; set; }

    /// <summary>
    /// Template version number.
    /// </summary>
    public int Version { get; set; } = 1;

    /// <summary>
    /// The culture identifier for culture-aware formatting in filter expressions.
    /// Must be a valid BCP 47 language tag (e.g., "ru-RU", "en-US", "de-DE").
    /// When set, filters like <c>currency</c>, <c>number</c>, <c>upper</c>, and <c>lower</c>
    /// will use this culture for formatting. Can be overridden at render time by
    /// <c>RenderOptions.Culture</c>.
    /// </summary>
    public string? Culture { get; set; }

    /// <summary>
    /// Font definitions for use in the template.
    /// Maps font names to their definitions (path and optional fallback).
    /// </summary>
    /// <remarks>
    /// <para>
    /// The special font name "default" is treated as the default font for all text elements
    /// that do not specify an explicit font. When a font named "default" is defined,
    /// it is automatically registered as "main", which is the fallback font name used
    /// by <see cref="TextElement"/> when no font is specified.
    /// </para>
    /// <para>
    /// Example YAML:
    /// <code>
    /// fonts:
    ///   default: "assets/fonts/Roboto-Regular.ttf"
    ///   bold: "assets/fonts/Roboto-Bold.ttf"
    /// </code>
    /// </para>
    /// </remarks>
    public Dictionary<string, FontDefinition> Fonts { get; set; } = new();

    /// <summary>
    /// Canvas settings for rendering.
    /// </summary>
    public CanvasSettings Canvas { get; set; } = new();

    /// <summary>
    /// Gets or sets the list of elements to render.
    /// The getter returns a read-only view; use setter for bulk assignment.
    /// </summary>
    public IReadOnlyList<TemplateElement> Elements
    {
        get => _elements;
        set
        {
            ArgumentNullException.ThrowIfNull(value);
            _elements = value.ToList();
        }
    }

    /// <summary>
    /// Adds an element to the template.
    /// </summary>
    /// <param name="element">The element to add.</param>
    /// <exception cref="ArgumentNullException">Thrown when element is null.</exception>
    public void AddElement(TemplateElement element)
    {
        ArgumentNullException.ThrowIfNull(element);
        _elements.Add(element);
    }
}
