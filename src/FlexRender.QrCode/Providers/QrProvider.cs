using QRCoder;
using FlexRender.Parsing.Ast;
using FlexRender.Rendering;
using SkiaSharp;

namespace FlexRender.Providers;

/// <summary>
/// Provides QR code generation using SkiaSharp.QrCode.
/// </summary>
public sealed class QrProvider : IContentProvider<QrElement>
{
    /// <summary>
    /// Maximum data capacity in bytes for each error correction level.
    /// </summary>
    private static readonly Dictionary<ErrorCorrectionLevel, int> MaxDataCapacity = new()
    {
        { ErrorCorrectionLevel.L, 2953 },
        { ErrorCorrectionLevel.M, 2331 },
        { ErrorCorrectionLevel.Q, 1663 },
        { ErrorCorrectionLevel.H, 1273 }
    };
    /// <summary>
    /// Generates a QR code bitmap from the specified element configuration.
    /// </summary>
    /// <param name="element">The QR code element configuration.</param>
    /// <returns>A bitmap containing the rendered QR code.</returns>
    /// <exception cref="ArgumentNullException">Thrown when element is null.</exception>
    /// <exception cref="ArgumentException">Thrown when element data is empty or size is invalid.</exception>
    public SKBitmap Generate(QrElement element)
    {
        ArgumentNullException.ThrowIfNull(element);

        if (string.IsNullOrEmpty(element.Data))
        {
            throw new ArgumentException("QR code data cannot be empty.", nameof(element));
        }

        if (element.Size <= 0)
        {
            throw new ArgumentException("QR code size must be positive.", nameof(element));
        }

        var eccLevel = element.ErrorCorrection switch
        {
            ErrorCorrectionLevel.L => QRCodeGenerator.ECCLevel.L,
            ErrorCorrectionLevel.M => QRCodeGenerator.ECCLevel.M,
            ErrorCorrectionLevel.Q => QRCodeGenerator.ECCLevel.Q,
            ErrorCorrectionLevel.H => QRCodeGenerator.ECCLevel.H,
            _ => QRCodeGenerator.ECCLevel.M
        };

        // Validate data length against QR code capacity
        var dataBytes = System.Text.Encoding.UTF8.GetByteCount(element.Data);
        var maxCapacity = MaxDataCapacity[element.ErrorCorrection];
        if (dataBytes > maxCapacity)
        {
            throw new ArgumentException(
                $"QR code data ({dataBytes} bytes) exceeds maximum capacity for error correction level " +
                $"{element.ErrorCorrection} ({maxCapacity} bytes).",
                nameof(element));
        }

        using var qrGenerator = new QRCodeGenerator();
        using var qrCodeData = qrGenerator.CreateQrCode(element.Data, eccLevel);

        var moduleCount = qrCodeData.ModuleMatrix.Count;
        var moduleSize = element.Size / (float)moduleCount;

        var bitmap = new SKBitmap(element.Size, element.Size);
        using var canvas = new SKCanvas(bitmap);

        var foreground = ColorParser.Parse(element.Foreground);
        var background = element.Background is not null
            ? ColorParser.Parse(element.Background)
            : SKColors.White;

        // Fill background
        canvas.Clear(background);

        using var paint = new SKPaint
        {
            Color = foreground,
            IsAntialias = false,
            Style = SKPaintStyle.Fill
        };

        // Draw QR code modules
        for (var y = 0; y < moduleCount; y++)
        {
            for (var x = 0; x < moduleCount; x++)
            {
                if (qrCodeData.ModuleMatrix[y][x])
                {
                    var rect = new SKRect(
                        x * moduleSize,
                        y * moduleSize,
                        (x + 1) * moduleSize,
                        (y + 1) * moduleSize);
                    canvas.DrawRect(rect, paint);
                }
            }
        }

        return bitmap;
    }
}
