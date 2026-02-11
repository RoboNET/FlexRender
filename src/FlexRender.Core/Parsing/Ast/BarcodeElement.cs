using FlexRender.TemplateEngine;

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
    public ExprValue<string> Data { get; set; } = "";

    /// <summary>
    /// The barcode format to use.
    /// </summary>
    public ExprValue<BarcodeFormat> Format { get; set; } = BarcodeFormat.Code128;

    /// <summary>
    /// The width of the barcode in pixels.
    /// If not specified, inherits from container width or flex Width property.
    /// </summary>
    public ExprValue<int?> BarcodeWidth { get; set; }

    /// <summary>
    /// The height of the barcode in pixels.
    /// If not specified, inherits from container height or flex Height property.
    /// </summary>
    public ExprValue<int?> BarcodeHeight { get; set; }

    /// <summary>
    /// Whether to display the encoded text below the barcode.
    /// </summary>
    public ExprValue<bool> ShowText { get; set; } = true;

    /// <summary>
    /// The foreground color (bars) in hex format.
    /// </summary>
    public ExprValue<string> Foreground { get; set; } = "#000000";

    /// <inheritdoc />
    public override void ResolveExpressions(Func<string, ObjectValue, string> resolver, ObjectValue data)
    {
        base.ResolveExpressions(resolver, data);
        Data = Data.Resolve(resolver, data);
        Format = Format.Resolve(resolver, data);
        BarcodeWidth = BarcodeWidth.Resolve(resolver, data);
        BarcodeHeight = BarcodeHeight.Resolve(resolver, data);
        ShowText = ShowText.Resolve(resolver, data);
        Foreground = Foreground.Resolve(resolver, data);
    }

    /// <inheritdoc />
    public override void Materialize()
    {
        base.Materialize();
        Data = Data.Materialize(nameof(Data));
        Format = Format.Materialize(nameof(Format));
        BarcodeWidth = BarcodeWidth.Materialize(nameof(BarcodeWidth));
        BarcodeHeight = BarcodeHeight.Materialize(nameof(BarcodeHeight));
        ShowText = ShowText.Materialize(nameof(ShowText));
        Foreground = Foreground.Materialize(nameof(Foreground), ValueKind.Color);
    }
}
