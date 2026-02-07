using FlexRender.Parsing.Ast;

namespace FlexRender.QrCode;

/// <summary>
/// Validates QR code data capacity against error correction level limits.
/// </summary>
public static class QrDataValidator
{
    private static readonly Dictionary<ErrorCorrectionLevel, int> MaxDataCapacity = new()
    {
        { ErrorCorrectionLevel.L, 2953 },
        { ErrorCorrectionLevel.M, 2331 },
        { ErrorCorrectionLevel.Q, 1663 },
        { ErrorCorrectionLevel.H, 1273 }
    };

    /// <summary>
    /// Validates that the QR element's data does not exceed the maximum capacity
    /// for its configured error correction level.
    /// </summary>
    /// <param name="element">The QR element to validate.</param>
    /// <exception cref="ArgumentException">
    /// Thrown when the error correction level is unsupported or data exceeds capacity.
    /// </exception>
    public static void ValidateDataCapacity(QrElement element)
    {
        var dataBytes = System.Text.Encoding.UTF8.GetByteCount(element.Data);
        if (!MaxDataCapacity.TryGetValue(element.ErrorCorrection, out var maxCapacity))
        {
            throw new ArgumentException(
                $"Unsupported error correction level: {element.ErrorCorrection}.",
                nameof(element));
        }
        if (dataBytes > maxCapacity)
        {
            throw new ArgumentException(
                $"QR code data ({dataBytes} bytes) exceeds maximum capacity for error correction level " +
                $"{element.ErrorCorrection} ({maxCapacity} bytes).",
                nameof(element));
        }
    }
}
