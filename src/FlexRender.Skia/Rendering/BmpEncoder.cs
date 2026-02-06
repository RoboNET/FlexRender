using System.Buffers;
using SkiaSharp;

namespace FlexRender.Rendering;

/// <summary>
/// Encodes an <see cref="SKBitmap"/> into BMP format with configurable color depth.
/// SkiaSharp does not support BMP encoding natively, so this class provides
/// a manual implementation using the BITMAPINFOHEADER format.
/// </summary>
public static class BmpEncoder
{
    private const int FileHeaderSize = 14;
    private const int InfoHeaderSize = 40;
    private const int BaseDataOffset = FileHeaderSize + InfoHeaderSize;
    private const int GrayscaleColorTableSize = 256 * 4; // 256 entries * 4 bytes (BGRA)
    private const int Grayscale4ColorTableSize = 16 * 4; // 16 entries * 4 bytes (BGRA)
    private const int MonochromeColorTableSize = 2 * 4;  // 2 entries * 4 bytes (BGRA)
    private const ushort BmpSignature = 0x4D42; // "BM" in little-endian
    private const ushort Planes = 1;
    private const int StackAllocThreshold = 1024;

    /// <summary>
    /// Encodes the specified bitmap into BMP format and returns the result as a byte array.
    /// </summary>
    /// <param name="bitmap">The bitmap to encode.</param>
    /// <param name="colorMode">The color depth mode to use for encoding.</param>
    /// <returns>A byte array containing the BMP file data.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="bitmap"/> is null.</exception>
    /// <exception cref="ArgumentException">Thrown when bitmap dimensions are too large for BMP encoding.</exception>
    public static byte[] Encode(SKBitmap bitmap, BmpColorMode colorMode = BmpColorMode.Bgra32)
    {
        ArgumentNullException.ThrowIfNull(bitmap);

        var width = bitmap.Width;
        var height = bitmap.Height;
        var bitsPerPixel = GetBitsPerPixel(colorMode);
        var rowBytes = CalculateRowBytes(width, bitsPerPixel);
        var colorTableSize = GetColorTableSize(colorMode);
        var dataOffset = BaseDataOffset + colorTableSize;
        var imageDataSize = (long)rowBytes * height;

        if (imageDataSize > int.MaxValue - dataOffset)
        {
            throw new ArgumentException(
                $"Bitmap dimensions ({width}x{height}) are too large for BMP encoding.", nameof(bitmap));
        }
        var fileSize = dataOffset + (int)imageDataSize;

        var result = new byte[fileSize];
        using var stream = new MemoryStream(result);
        using var writer = new BinaryWriter(stream);

        WriteFileHeader(writer, fileSize, dataOffset);
        WriteInfoHeader(writer, width, height, bitsPerPixel, (int)imageDataSize, colorMode);
        WriteColorTable(writer, colorMode);
        WritePixelData(writer, bitmap, width, height, colorMode);

        return result;
    }

    /// <summary>
    /// Encodes the specified bitmap into BMP format and writes the result to the given stream.
    /// </summary>
    /// <param name="bitmap">The bitmap to encode.</param>
    /// <param name="output">The stream to write the BMP data to.</param>
    /// <param name="colorMode">The color depth mode to use for encoding.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="bitmap"/> or <paramref name="output"/> is null.</exception>
    /// <exception cref="ArgumentException">Thrown when bitmap dimensions are too large for BMP encoding.</exception>
    public static void Encode(SKBitmap bitmap, Stream output, BmpColorMode colorMode = BmpColorMode.Bgra32)
    {
        ArgumentNullException.ThrowIfNull(bitmap);
        ArgumentNullException.ThrowIfNull(output);

        var width = bitmap.Width;
        var height = bitmap.Height;
        var bitsPerPixel = GetBitsPerPixel(colorMode);
        var rowBytes = CalculateRowBytes(width, bitsPerPixel);
        var colorTableSize = GetColorTableSize(colorMode);
        var dataOffset = BaseDataOffset + colorTableSize;
        var imageDataSize = (long)rowBytes * height;

        if (imageDataSize > int.MaxValue - dataOffset)
        {
            throw new ArgumentException(
                $"Bitmap dimensions ({width}x{height}) are too large for BMP encoding.", nameof(bitmap));
        }
        var fileSize = dataOffset + (int)imageDataSize;

        using var writer = new BinaryWriter(output, System.Text.Encoding.UTF8, leaveOpen: true);

        WriteFileHeader(writer, fileSize, dataOffset);
        WriteInfoHeader(writer, width, height, bitsPerPixel, (int)imageDataSize, colorMode);
        WriteColorTable(writer, colorMode);
        WritePixelData(writer, bitmap, width, height, colorMode);
    }

    /// <summary>
    /// Returns the bits per pixel for the given color mode.
    /// </summary>
    private static ushort GetBitsPerPixel(BmpColorMode colorMode) => colorMode switch
    {
        BmpColorMode.Bgra32 => 32,
        BmpColorMode.Rgb24 => 24,
        BmpColorMode.Rgb565 => 16,
        BmpColorMode.Grayscale8 => 8,
        BmpColorMode.Grayscale4 => 4,
        BmpColorMode.Monochrome1 => 1,
        _ => throw new ArgumentOutOfRangeException(nameof(colorMode), colorMode, "Unsupported BMP color mode.")
    };

    /// <summary>
    /// Calculates BMP row size in bytes (padded to 4-byte boundary).
    /// </summary>
    private static int CalculateRowBytes(int width, int bitsPerPixel)
    {
        var rawBytes = (width * bitsPerPixel + 7) / 8; // Ceiling division for sub-byte pixels
        return (rawBytes + 3) & ~3; // Round up to nearest multiple of 4
    }

    /// <summary>
    /// Writes the 14-byte BMP file header.
    /// </summary>
    private static void WriteFileHeader(BinaryWriter writer, int fileSize, int dataOffset)
    {
        writer.Write(BmpSignature);       // Signature "BM"
        writer.Write((uint)fileSize);     // File size
        writer.Write((ushort)0);          // Reserved1
        writer.Write((ushort)0);          // Reserved2
        writer.Write((uint)dataOffset);   // Offset to pixel data
    }

    /// <summary>
    /// Writes the 40-byte BITMAPINFOHEADER.
    /// </summary>
    private static void WriteInfoHeader(BinaryWriter writer, int width, int height,
        ushort bitsPerPixel, int imageDataSize, BmpColorMode colorMode)
    {
        var colorsUsed = colorMode switch
        {
            BmpColorMode.Grayscale8 => 256u,
            BmpColorMode.Grayscale4 => 16u,
            BmpColorMode.Monochrome1 => 2u,
            _ => 0u
        };

        writer.Write((uint)InfoHeaderSize); // Header size
        writer.Write(width);                // Width (signed int32)
        writer.Write(height);               // Height (positive = bottom-up)
        writer.Write(Planes);               // Color planes
        writer.Write(bitsPerPixel);         // Bits per pixel
        writer.Write((uint)0);              // Compression (BI_RGB = 0)
        writer.Write((uint)imageDataSize);  // Image size
        writer.Write(0);                    // X pixels per meter
        writer.Write(0);                    // Y pixels per meter
        writer.Write(colorsUsed);           // Colors used
        writer.Write((uint)0);              // Important colors
    }

    /// <summary>
    /// Returns the color table size in bytes for the given color mode.
    /// </summary>
    private static int GetColorTableSize(BmpColorMode colorMode) => colorMode switch
    {
        BmpColorMode.Bgra32 => 0,
        BmpColorMode.Rgb24 => 0,
        BmpColorMode.Rgb565 => 0,
        BmpColorMode.Grayscale8 => GrayscaleColorTableSize,
        BmpColorMode.Grayscale4 => Grayscale4ColorTableSize,
        BmpColorMode.Monochrome1 => MonochromeColorTableSize,
        _ => throw new ArgumentOutOfRangeException(nameof(colorMode), colorMode, "Unsupported BMP color mode.")
    };

    /// <summary>
    /// Writes the color table for modes that require one.
    /// </summary>
    private static void WriteColorTable(BinaryWriter writer, BmpColorMode colorMode)
    {
        switch (colorMode)
        {
            case BmpColorMode.Bgra32:
            case BmpColorMode.Rgb24:
            case BmpColorMode.Rgb565:
                // No color table for these modes
                break;
            case BmpColorMode.Grayscale8:
                // 256-entry grayscale: each entry (B, G, R, Reserved) with same value
                for (var i = 0; i < 256; i++)
                {
                    var b = (byte)i;
                    writer.Write(b);
                    writer.Write(b);
                    writer.Write(b);
                    writer.Write((byte)0);
                }
                break;
            case BmpColorMode.Grayscale4:
                // 16-entry grayscale: values 0, 17, 34, ..., 255
                for (var i = 0; i < 16; i++)
                {
                    var b = (byte)(i * 17); // Maps 0-15 to 0-255 evenly
                    writer.Write(b);
                    writer.Write(b);
                    writer.Write(b);
                    writer.Write((byte)0);
                }
                break;
            case BmpColorMode.Monochrome1:
                // Entry 0 = black
                writer.Write((byte)0);
                writer.Write((byte)0);
                writer.Write((byte)0);
                writer.Write((byte)0);
                // Entry 1 = white
                writer.Write((byte)255);
                writer.Write((byte)255);
                writer.Write((byte)255);
                writer.Write((byte)0);
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(colorMode), colorMode, "Unsupported BMP color mode.");
        }
    }

    /// <summary>
    /// Writes pixel data in bottom-up row order in the specified color mode.
    /// </summary>
    private static void WritePixelData(BinaryWriter writer, SKBitmap bitmap, int width, int height,
        BmpColorMode colorMode)
    {
        switch (colorMode)
        {
            case BmpColorMode.Bgra32:
                WritePixelDataBgra32(writer, bitmap, width, height);
                break;
            case BmpColorMode.Rgb24:
                WritePixelDataRgb24(writer, bitmap, width, height);
                break;
            case BmpColorMode.Rgb565:
                WritePixelDataRgb565(writer, bitmap, width, height);
                break;
            case BmpColorMode.Grayscale8:
                WritePixelDataGrayscale8(writer, bitmap, width, height);
                break;
            case BmpColorMode.Grayscale4:
                WritePixelDataGrayscale4(writer, bitmap, width, height);
                break;
            case BmpColorMode.Monochrome1:
                WritePixelDataMonochrome1(writer, bitmap, width, height);
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(colorMode), colorMode, "Unsupported BMP color mode.");
        }
    }

    /// <summary>
    /// Writes 32-bit BGRA pixel data (original behavior).
    /// </summary>
    private static void WritePixelDataBgra32(BinaryWriter writer, SKBitmap bitmap, int width, int height)
    {
        var bmpRowBytes = width * 4;
        var bitmapRowBytes = bitmap.RowBytes;

        if (bitmap.ColorType == SKColorType.Bgra8888)
        {
            // Fast path: BGRA8888 pixels are already in BMP byte order
            var span = bitmap.GetPixelSpan();
            for (var y = height - 1; y >= 0; y--)
            {
                var row = span.Slice(y * bitmapRowBytes, bmpRowBytes);
                writer.Write(row);
            }
        }
        else
        {
            byte[]? rentedBuffer = bmpRowBytes <= StackAllocThreshold ? null : ArrayPool<byte>.Shared.Rent(bmpRowBytes);
            try
            {
                Span<byte> rowBuffer = rentedBuffer is not null
                    ? rentedBuffer.AsSpan(0, bmpRowBytes)
                    : stackalloc byte[bmpRowBytes];

                for (var y = height - 1; y >= 0; y--)
                {
                    for (var x = 0; x < width; x++)
                    {
                        var pixel = bitmap.GetPixel(x, y);
                        var offset = x * 4;
                        rowBuffer[offset] = pixel.Blue;
                        rowBuffer[offset + 1] = pixel.Green;
                        rowBuffer[offset + 2] = pixel.Red;
                        rowBuffer[offset + 3] = pixel.Alpha;
                    }
                    writer.Write(rowBuffer);
                }
            }
            finally
            {
                if (rentedBuffer is not null)
                    ArrayPool<byte>.Shared.Return(rentedBuffer);
            }
        }
    }

    /// <summary>
    /// Writes 24-bit RGB pixel data (no alpha, with row padding).
    /// </summary>
    private static void WritePixelDataRgb24(BinaryWriter writer, SKBitmap bitmap, int width, int height)
    {
        var rawRowBytes = width * 3;
        var paddedRowBytes = (rawRowBytes + 3) & ~3;
        byte[]? rentedBuffer = paddedRowBytes <= StackAllocThreshold ? null : ArrayPool<byte>.Shared.Rent(paddedRowBytes);
        try
        {
            Span<byte> rowBuffer = rentedBuffer is not null
                ? rentedBuffer.AsSpan(0, paddedRowBytes)
                : stackalloc byte[paddedRowBytes];

            for (var y = height - 1; y >= 0; y--)
            {
                rowBuffer.Clear();

                for (var x = 0; x < width; x++)
                {
                    var pixel = bitmap.GetPixel(x, y);
                    var offset = x * 3;
                    rowBuffer[offset] = pixel.Blue;
                    rowBuffer[offset + 1] = pixel.Green;
                    rowBuffer[offset + 2] = pixel.Red;
                }
                writer.Write(rowBuffer);
            }
        }
        finally
        {
            if (rentedBuffer is not null)
                ArrayPool<byte>.Shared.Return(rentedBuffer);
        }
    }

    /// <summary>
    /// Writes 16-bit RGB565 pixel data (with row padding).
    /// </summary>
    private static void WritePixelDataRgb565(BinaryWriter writer, SKBitmap bitmap, int width, int height)
    {
        var rawRowBytes = width * 2;
        var paddedRowBytes = (rawRowBytes + 3) & ~3;
        byte[]? rentedBuffer = paddedRowBytes <= StackAllocThreshold ? null : ArrayPool<byte>.Shared.Rent(paddedRowBytes);
        try
        {
            Span<byte> rowBuffer = rentedBuffer is not null
                ? rentedBuffer.AsSpan(0, paddedRowBytes)
                : stackalloc byte[paddedRowBytes];

            for (var y = height - 1; y >= 0; y--)
            {
                rowBuffer.Clear();

                for (var x = 0; x < width; x++)
                {
                    var pixel = bitmap.GetPixel(x, y);
                    var rgb565 = (ushort)((pixel.Red >> 3) << 11 | (pixel.Green >> 2) << 5 | (pixel.Blue >> 3));
                    var offset = x * 2;
                    // Little-endian
                    rowBuffer[offset] = (byte)(rgb565 & 0xFF);
                    rowBuffer[offset + 1] = (byte)(rgb565 >> 8);
                }
                writer.Write(rowBuffer);
            }
        }
        finally
        {
            if (rentedBuffer is not null)
                ArrayPool<byte>.Shared.Return(rentedBuffer);
        }
    }

    /// <summary>
    /// Writes 8-bit grayscale pixel data (with row padding).
    /// Uses ITU-R BT.601 luminance formula: 0.299*R + 0.587*G + 0.114*B.
    /// </summary>
    private static void WritePixelDataGrayscale8(BinaryWriter writer, SKBitmap bitmap, int width, int height)
    {
        var paddedRowBytes = (width + 3) & ~3;
        byte[]? rentedBuffer = paddedRowBytes <= StackAllocThreshold ? null : ArrayPool<byte>.Shared.Rent(paddedRowBytes);
        try
        {
            Span<byte> rowBuffer = rentedBuffer is not null
                ? rentedBuffer.AsSpan(0, paddedRowBytes)
                : stackalloc byte[paddedRowBytes];

            for (var y = height - 1; y >= 0; y--)
            {
                rowBuffer.Clear();

                for (var x = 0; x < width; x++)
                {
                    var pixel = bitmap.GetPixel(x, y);
                    rowBuffer[x] = (byte)Math.Min(0.299f * pixel.Red + 0.587f * pixel.Green + 0.114f * pixel.Blue, 255f);
                }
                writer.Write(rowBuffer);
            }
        }
        finally
        {
            if (rentedBuffer is not null)
                ArrayPool<byte>.Shared.Return(rentedBuffer);
        }
    }

    /// <summary>
    /// Writes 4-bit grayscale pixel data (with row padding).
    /// Two pixels per byte: high nibble = left pixel, low nibble = right pixel.
    /// Uses ITU-R BT.601 luminance formula quantized to 16 levels.
    /// </summary>
    private static void WritePixelDataGrayscale4(BinaryWriter writer, SKBitmap bitmap, int width, int height)
    {
        var bytesPerRow = (width + 1) / 2;
        var paddedRowBytes = (bytesPerRow + 3) & ~3;
        byte[]? rentedBuffer = paddedRowBytes <= StackAllocThreshold ? null : ArrayPool<byte>.Shared.Rent(paddedRowBytes);
        try
        {
            Span<byte> rowBuffer = rentedBuffer is not null
                ? rentedBuffer.AsSpan(0, paddedRowBytes)
                : stackalloc byte[paddedRowBytes];

            for (var y = height - 1; y >= 0; y--)
            {
                rowBuffer.Clear();

                for (var x = 0; x < width; x++)
                {
                    var pixel = bitmap.GetPixel(x, y);
                    var lum = 0.299f * pixel.Red + 0.587f * pixel.Green + 0.114f * pixel.Blue;
                    var index = (int)MathF.Round(lum / 255f * 15f); // Quantize 0-255 to 0-15
                    if (index > 15) index = 15;

                    if (x % 2 == 0)
                        rowBuffer[x / 2] |= (byte)(index << 4); // High nibble
                    else
                        rowBuffer[x / 2] |= (byte)index; // Low nibble
                }
                writer.Write(rowBuffer);
            }
        }
        finally
        {
            if (rentedBuffer is not null)
                ArrayPool<byte>.Shared.Return(rentedBuffer);
        }
    }

    /// <summary>
    /// Writes 1-bit monochrome pixel data (with row padding).
    /// Uses ITU-R BT.601 luminance formula with threshold at 128.
    /// Bits are packed MSB-first: bit 7 is the leftmost pixel.
    /// </summary>
    private static void WritePixelDataMonochrome1(BinaryWriter writer, SKBitmap bitmap, int width, int height)
    {
        var bytesPerRow = (width + 7) / 8;
        var paddedRowBytes = (bytesPerRow + 3) & ~3;
        byte[]? rentedBuffer = paddedRowBytes <= StackAllocThreshold ? null : ArrayPool<byte>.Shared.Rent(paddedRowBytes);
        try
        {
            Span<byte> rowBuffer = rentedBuffer is not null
                ? rentedBuffer.AsSpan(0, paddedRowBytes)
                : stackalloc byte[paddedRowBytes];

            for (var y = height - 1; y >= 0; y--)
            {
                rowBuffer.Clear();

                for (var x = 0; x < width; x++)
                {
                    var pixel = bitmap.GetPixel(x, y);
                    var lum = 0.299f * pixel.Red + 0.587f * pixel.Green + 0.114f * pixel.Blue;
                    if (lum > 128f)
                        rowBuffer[x / 8] |= (byte)(0x80 >> (x % 8)); // set bit = white
                }
                writer.Write(rowBuffer);
            }
        }
        finally
        {
            if (rentedBuffer is not null)
                ArrayPool<byte>.Shared.Return(rentedBuffer);
        }
    }
}
