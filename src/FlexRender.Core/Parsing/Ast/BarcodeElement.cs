using FlexRender.Layout;

namespace FlexRender.Parsing.Ast;

/// <summary>
/// Supported barcode formats.
/// </summary>
public enum BarcodeFormat
{
    /// <summary>
    /// Code 128 barcode (alphanumeric, high density).
    /// </summary>
    Code128,

    /// <summary>
    /// Code 39 barcode (alphanumeric, widely supported).
    /// </summary>
    Code39,

    /// <summary>
    /// EAN-13 barcode (13 digits, retail).
    /// </summary>
    Ean13,

    /// <summary>
    /// EAN-8 barcode (8 digits, compact retail).
    /// </summary>
    Ean8,

    /// <summary>
    /// UPC-A barcode (12 digits, North American retail).
    /// </summary>
    Upc
}

/// <summary>
/// A barcode element in the template.
/// </summary>
public sealed class BarcodeElement : TemplateElement
{
    /// <inheritdoc />
    public override ElementType Type => ElementType.Barcode;

    /// <summary>
    /// The data to encode in the barcode.
    /// </summary>
    public string Data { get; set; } = "";

    /// <summary>
    /// The barcode format to use.
    /// </summary>
    public BarcodeFormat Format { get; set; } = BarcodeFormat.Code128;

    /// <summary>
    /// The width of the barcode in pixels.
    /// </summary>
    public int BarcodeWidth { get; set; } = 200;

    /// <summary>
    /// The height of the barcode in pixels.
    /// </summary>
    public int BarcodeHeight { get; set; } = 80;

    /// <summary>
    /// Whether to display the encoded text below the barcode.
    /// </summary>
    public bool ShowText { get; set; } = true;

    /// <summary>
    /// The foreground color (bars) in hex format.
    /// </summary>
    public string Foreground { get; set; } = "#000000";

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
