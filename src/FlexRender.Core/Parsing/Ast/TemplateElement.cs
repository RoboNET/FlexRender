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
}
