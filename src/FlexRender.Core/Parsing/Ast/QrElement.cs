using FlexRender.TemplateEngine;

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
    public ExprValue<string> Data { get; set; } = "";

    /// <summary>
    /// The size of the QR code in pixels (width and height are equal).
    /// If not specified, inherits from container dimensions or flex Width/Height properties.
    /// </summary>
    public ExprValue<int?> Size { get; set; }

    /// <summary>
    /// The error correction level for the QR code.
    /// </summary>
    public ExprValue<ErrorCorrectionLevel> ErrorCorrection { get; set; } = ErrorCorrectionLevel.M;

    /// <summary>
    /// The foreground color (QR code modules) in hex format.
    /// </summary>
    public ExprValue<string> Foreground { get; set; } = "#000000";

    /// <inheritdoc />
    public override void ResolveExpressions(Func<string, ObjectValue, string> resolver, ObjectValue data)
    {
        base.ResolveExpressions(resolver, data);
        Data = Data.Resolve(resolver, data);
        Size = Size.Resolve(resolver, data);
        ErrorCorrection = ErrorCorrection.Resolve(resolver, data);
        Foreground = Foreground.Resolve(resolver, data);
    }

    /// <inheritdoc />
    public override void Materialize()
    {
        base.Materialize();
        Data = Data.Materialize(nameof(Data));
        Size = Size.Materialize(nameof(Size));
        ErrorCorrection = ErrorCorrection.Materialize(nameof(ErrorCorrection));
        Foreground = Foreground.Materialize(nameof(Foreground), ValueKind.Color);
    }
}
