using FlexRender.Layout;

namespace FlexRender.Parsing.Ast;

/// <summary>
/// Text alignment options.
/// </summary>
public enum TextAlign
{
    /// <summary>
    /// Align text to the left.
    /// </summary>
    Left,

    /// <summary>
    /// Center text horizontally.
    /// </summary>
    Center,

    /// <summary>
    /// Align text to the right.
    /// </summary>
    Right
}

/// <summary>
/// Text overflow handling options.
/// </summary>
public enum TextOverflow
{
    /// <summary>
    /// Truncate text with an ellipsis (...).
    /// </summary>
    Ellipsis,

    /// <summary>
    /// Clip text at the boundary.
    /// </summary>
    Clip,

    /// <summary>
    /// Allow text to overflow visibly.
    /// </summary>
    Visible
}

/// <summary>
/// A text element in the template.
/// </summary>
public sealed class TextElement : TemplateElement
{
    /// <inheritdoc />
    public override ElementType Type => ElementType.Text;

    /// <summary>
    /// Text content, may contain template expressions like {{variable}}.
    /// </summary>
    public string Content { get; set; } = "";

    /// <summary>
    /// Font name reference.
    /// </summary>
    public string Font { get; set; } = "main";

    /// <summary>
    /// Font size (pixels, em, or percentage).
    /// </summary>
    public string Size { get; set; } = "1em";

    /// <summary>
    /// Text color in hex format.
    /// </summary>
    public string Color { get; set; } = "#000000";

    /// <summary>
    /// Text alignment within the element.
    /// </summary>
    public TextAlign Align { get; set; } = TextAlign.Left;

    /// <summary>
    /// Whether to wrap text to multiple lines.
    /// </summary>
    public bool Wrap { get; set; } = true;

    /// <summary>
    /// How to handle text overflow.
    /// </summary>
    public TextOverflow Overflow { get; set; } = TextOverflow.Ellipsis;

    /// <summary>
    /// Maximum number of lines (null for unlimited).
    /// </summary>
    public int? MaxLines { get; set; }

    /// <summary>
    /// Line height for multi-line text.
    /// Plain number (e.g. "1.8") is a multiplier of fontSize.
    /// With unit (e.g. "24px", "2em") is an absolute or relative value.
    /// Empty string means default (font-defined spacing).
    /// </summary>
    public string LineHeight { get; set; } = "";

    // Flex item properties

    /// <summary>Flex grow factor.</summary>
    public float Grow { get; set; } = 0f;

    /// <summary>Flex shrink factor.</summary>
    public float Shrink { get; set; } = 1f;

    /// <summary>Flex basis (px, %, em, auto).</summary>
    public string Basis { get; set; } = "auto";

    /// <summary>Self alignment override.</summary>
    public AlignSelf AlignSelf { get; set; } = AlignSelf.Auto;

    /// <summary>Display order.</summary>
    public int Order { get; set; } = 0;

    /// <summary>Width (px, %, em, auto).</summary>
    public string? Width { get; set; }

    /// <summary>Height (px, %, em, auto).</summary>
    public string? Height { get; set; }
}
