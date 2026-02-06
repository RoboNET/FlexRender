namespace FlexRender.Parsing.Ast;

/// <summary>
/// Error correction levels for QR codes.
/// Higher levels allow more damage tolerance but reduce data capacity.
/// </summary>
public enum ErrorCorrectionLevel
{
    /// <summary>
    /// Low error correction (~7% recovery).
    /// </summary>
    L,

    /// <summary>
    /// Medium error correction (~15% recovery).
    /// </summary>
    M,

    /// <summary>
    /// Quartile error correction (~25% recovery).
    /// </summary>
    Q,

    /// <summary>
    /// High error correction (~30% recovery).
    /// </summary>
    H
}

/// <summary>
/// A QR code element in the template.
/// </summary>
public sealed class QrElement : TemplateElement
{
    /// <inheritdoc />
    public override ElementType Type => ElementType.Qr;

    /// <summary>
    /// The data to encode in the QR code.
    /// </summary>
    public string Data { get; set; } = "";

    /// <summary>
    /// The size of the QR code in pixels (width and height are equal).
    /// </summary>
    public int Size { get; set; } = 100;

    /// <summary>
    /// The error correction level for the QR code.
    /// </summary>
    public ErrorCorrectionLevel ErrorCorrection { get; set; } = ErrorCorrectionLevel.M;

    /// <summary>
    /// The foreground color (QR code modules) in hex format.
    /// </summary>
    public string Foreground { get; set; } = "#000000";
}
