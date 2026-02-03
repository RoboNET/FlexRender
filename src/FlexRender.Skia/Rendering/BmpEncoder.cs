using SkiaSharp;

namespace FlexRender.Rendering;

/// <summary>
/// Encodes an <see cref="SKBitmap"/> into 32-bit BMP format.
/// SkiaSharp does not support BMP encoding natively, so this class provides
/// a manual implementation using the BITMAPINFOHEADER format with 32-bit BGRA pixels.
/// </summary>
public static class BmpEncoder
{
    private const int FileHeaderSize = 14;
    private const int InfoHeaderSize = 40;
    private const int DataOffset = FileHeaderSize + InfoHeaderSize;
    private const ushort BmpSignature = 0x4D42; // "BM" in little-endian
    private const ushort Planes = 1;
    private const ushort BitCount = 32;

    /// <summary>
    /// Encodes the specified bitmap into 32-bit BMP format and returns the result as a byte array.
    /// </summary>
    /// <param name="bitmap">The bitmap to encode.</param>
    /// <returns>A byte array containing the BMP file data.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="bitmap"/> is null.</exception>
    /// <exception cref="ArgumentException">Thrown when bitmap dimensions are too large for BMP encoding.</exception>
    public static byte[] Encode(SKBitmap bitmap)
    {
        ArgumentNullException.ThrowIfNull(bitmap);

        var width = bitmap.Width;
        var height = bitmap.Height;
        var imageDataSize = (long)width * height * 4; // 32-bit = 4 bytes per pixel, no padding needed for 32-bit
        if (imageDataSize > int.MaxValue - DataOffset)
        {
            throw new ArgumentException(
                $"Bitmap dimensions ({width}x{height}) are too large for BMP encoding.", nameof(bitmap));
        }
        var fileSize = DataOffset + (int)imageDataSize;

        var result = new byte[fileSize];
        using var stream = new MemoryStream(result);
        using var writer = new BinaryWriter(stream);

        WriteFileHeader(writer, fileSize);
        WriteInfoHeader(writer, width, height);
        WritePixelData(writer, bitmap, width, height);

        return result;
    }

    /// <summary>
    /// Encodes the specified bitmap into 32-bit BMP format and writes the result to the given stream.
    /// </summary>
    /// <param name="bitmap">The bitmap to encode.</param>
    /// <param name="output">The stream to write the BMP data to.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="bitmap"/> or <paramref name="output"/> is null.</exception>
    /// <exception cref="ArgumentException">Thrown when bitmap dimensions are too large for BMP encoding.</exception>
    public static void Encode(SKBitmap bitmap, Stream output)
    {
        ArgumentNullException.ThrowIfNull(bitmap);
        ArgumentNullException.ThrowIfNull(output);

        var width = bitmap.Width;
        var height = bitmap.Height;
        var imageDataSize = (long)width * height * 4; // 32-bit = 4 bytes per pixel, no padding needed for 32-bit
        if (imageDataSize > int.MaxValue - DataOffset)
        {
            throw new ArgumentException(
                $"Bitmap dimensions ({width}x{height}) are too large for BMP encoding.", nameof(bitmap));
        }
        var fileSize = DataOffset + (int)imageDataSize;

        using var writer = new BinaryWriter(output, System.Text.Encoding.UTF8, leaveOpen: true);

        WriteFileHeader(writer, fileSize);
        WriteInfoHeader(writer, width, height);
        WritePixelData(writer, bitmap, width, height);
    }

    /// <summary>
    /// Writes the 14-byte BMP file header.
    /// </summary>
    private static void WriteFileHeader(BinaryWriter writer, int fileSize)
    {
        writer.Write(BmpSignature);       // Signature "BM"
        writer.Write((uint)fileSize);     // File size
        writer.Write((ushort)0);          // Reserved1
        writer.Write((ushort)0);          // Reserved2
        writer.Write((uint)DataOffset);   // Offset to pixel data
    }

    /// <summary>
    /// Writes the 40-byte BITMAPINFOHEADER.
    /// </summary>
    private static void WriteInfoHeader(BinaryWriter writer, int width, int height)
    {
        writer.Write((uint)InfoHeaderSize); // Header size
        writer.Write(width);                // Width (signed int32)
        writer.Write(height);               // Height (positive = bottom-up)
        writer.Write(Planes);               // Color planes
        writer.Write(BitCount);             // Bits per pixel
        writer.Write((uint)0);              // Compression (BI_RGB = 0)
        writer.Write((uint)0);              // Image size (can be 0 for BI_RGB)
        writer.Write(0);                    // X pixels per meter
        writer.Write(0);                    // Y pixels per meter
        writer.Write((uint)0);              // Colors used
        writer.Write((uint)0);              // Important colors
    }

    /// <summary>
    /// Writes pixel data in bottom-up row order with BGRA byte ordering.
    /// Uses a fast span-based path for BGRA8888 bitmaps and falls back
    /// to per-pixel access for other color types.
    /// </summary>
    private static void WritePixelData(BinaryWriter writer, SKBitmap bitmap, int width, int height)
    {
        var rowBytes = width * 4;

        if (bitmap.ColorType == SKColorType.Bgra8888)
        {
            // Fast path: BGRA8888 pixels are already in BMP byte order (B, G, R, A)
            var span = bitmap.GetPixelSpan();
            for (var y = height - 1; y >= 0; y--)
            {
                var row = span.Slice(y * rowBytes, rowBytes);
                writer.Write(row);
            }
        }
        else
        {
            // Slow path: read each pixel individually for correct color space conversion
            for (var y = height - 1; y >= 0; y--)
            {
                for (var x = 0; x < width; x++)
                {
                    var pixel = bitmap.GetPixel(x, y);
                    writer.Write(pixel.Blue);
                    writer.Write(pixel.Green);
                    writer.Write(pixel.Red);
                    writer.Write(pixel.Alpha);
                }
            }
        }
    }
}
