using FlexRender.Layout;

namespace FlexRender.Parsing.Ast;

/// <summary>
/// Types of template elements.
/// </summary>
public enum ElementType
{
    /// <summary>
    /// A text element for displaying text content.
    /// </summary>
    Text,

    /// <summary>
    /// An image element for displaying images.
    /// </summary>
    Image,

    /// <summary>
    /// A QR code element.
    /// </summary>
    Qr,

    /// <summary>
    /// A barcode element.
    /// </summary>
    Barcode,

    /// <summary>
    /// A flex container element for layout.
    /// </summary>
    Flex,

    /// <summary>
    /// A separator element for drawing lines.
    /// </summary>
    Separator,

    /// <summary>
    /// An iteration element for looping over arrays.
    /// </summary>
    Each,

    /// <summary>
    /// A conditional element for conditional rendering.
    /// </summary>
    If
}

/// <summary>
/// Base class for all template elements.
/// </summary>
public abstract class TemplateElement
{
    /// <summary>
    /// The type of this element.
    /// </summary>
    public abstract ElementType Type { get; }

    /// <summary>
    /// Rotation of the element.
    /// </summary>
    public string Rotate { get; set; } = "none";

    /// <summary>
    /// Background color in hex format (e.g., "#000000"). Null means transparent.
    /// </summary>
    public string? Background { get; set; }

    /// <summary>
    /// Padding inside the element (px, %, em). Default is "0".
    /// </summary>
    public string Padding { get; set; } = "0";

    /// <summary>
    /// Margin outside the element (px, %, em). Default is "0".
    /// </summary>
    public string Margin { get; set; } = "0";

    /// <summary>
    /// Display mode. None removes the element from layout flow.
    /// </summary>
    public Display Display { get; set; } = Display.Flex;

    // Flex item properties (when this element is inside a flex container)

    /// <summary>Flex grow factor.</summary>
    public float Grow { get; set; }

    /// <summary>Flex shrink factor.</summary>
    public float Shrink { get; set; } = 1f;

    /// <summary>Flex basis (px, %, em, auto).</summary>
    public string Basis { get; set; } = "auto";

    /// <summary>Self alignment override.</summary>
    public AlignSelf AlignSelf { get; set; } = AlignSelf.Auto;

    /// <summary>Display order.</summary>
    public int Order { get; set; }

    /// <summary>Width (px, %, em, auto).</summary>
    public string? Width { get; set; }

    /// <summary>Height (px, %, em, auto).</summary>
    public string? Height { get; set; }

    /// <summary>Minimum width constraint (px, %, em).</summary>
    public string? MinWidth { get; set; }

    /// <summary>Maximum width constraint (px, %, em).</summary>
    public string? MaxWidth { get; set; }

    /// <summary>Minimum height constraint (px, %, em).</summary>
    public string? MinHeight { get; set; }

    /// <summary>Maximum height constraint (px, %, em).</summary>
    public string? MaxHeight { get; set; }

    /// <summary>Positioning mode.</summary>
    public Position Position { get; set; } = Position.Static;

    /// <summary>Top inset for positioned elements.</summary>
    public string? Top { get; set; }

    /// <summary>Right inset for positioned elements.</summary>
    public string? Right { get; set; }

    /// <summary>Bottom inset for positioned elements.</summary>
    public string? Bottom { get; set; }

    /// <summary>Left inset for positioned elements.</summary>
    public string? Left { get; set; }

    /// <summary>Aspect ratio (width / height). When one dimension is known, the other is computed.</summary>
    public float? AspectRatio { get; set; }

    // Border properties

    /// <summary>Border shorthand: "width style color" (e.g., "2 solid #333"). Applies to all sides.</summary>
    public string? Border { get; set; }

    /// <summary>Border width (px, em). Overrides shorthand width on all sides.</summary>
    public string? BorderWidth { get; set; }

    /// <summary>Border color in hex format. Overrides shorthand color on all sides.</summary>
    public string? BorderColor { get; set; }

    /// <summary>Border style: solid, dashed, dotted, none. Overrides shorthand style on all sides.</summary>
    public string? BorderStyle { get; set; }

    /// <summary>Per-side border shorthand for the top side: "width style color".</summary>
    public string? BorderTop { get; set; }

    /// <summary>Per-side border shorthand for the right side: "width style color".</summary>
    public string? BorderRight { get; set; }

    /// <summary>Per-side border shorthand for the bottom side: "width style color".</summary>
    public string? BorderBottom { get; set; }

    /// <summary>Per-side border shorthand for the left side: "width style color".</summary>
    public string? BorderLeft { get; set; }

    /// <summary>Border radius for corner rounding (px, em, %).</summary>
    public string? BorderRadius { get; set; }

    /// <summary>
    /// Text direction override. Null means inherit from parent/canvas.
    /// </summary>
    public TextDirection? TextDirection { get; set; }
}
