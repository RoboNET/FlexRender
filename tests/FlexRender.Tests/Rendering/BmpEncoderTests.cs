using FlexRender.Rendering;
using SkiaSharp;
using Xunit;

namespace FlexRender.Tests.Rendering;

public sealed class BmpEncoderTests
{
    [Fact]
    public void Encode_1x1White_ValidBmpHeader()
    {
        // Arrange
        using var bitmap = new SKBitmap(1, 1);
        bitmap.SetPixel(0, 0, SKColors.White);

        // Act
        var result = BmpEncoder.Encode(bitmap);

        // Assert
        Assert.Equal((byte)'B', result[0]);
        Assert.Equal((byte)'M', result[1]);
        Assert.Equal(58u, BitConverter.ToUInt32(result, 2)); // File size: 14 + 40 + 4
        Assert.Equal(54u, BitConverter.ToUInt32(result, 10)); // Data offset
    }

    [Fact]
    public void Encode_1x1Red_CorrectBgraOrder()
    {
        // Arrange
        using var bitmap = new SKBitmap(1, 1);
        bitmap.SetPixel(0, 0, new SKColor(255, 0, 0, 255)); // Red

        // Act
        var result = BmpEncoder.Encode(bitmap);

        // Assert - pixel data starts at offset 54, BGRA order
        Assert.Equal(0, result[54]);   // Blue
        Assert.Equal(0, result[55]);   // Green
        Assert.Equal(255, result[56]); // Red
        Assert.Equal(255, result[57]); // Alpha
    }

    [Fact]
    public void Encode_2x2_BottomUpRowOrder()
    {
        // Arrange
        using var bitmap = new SKBitmap(2, 2);
        bitmap.SetPixel(0, 0, new SKColor(255, 0, 0, 255));   // Top-left: Red
        bitmap.SetPixel(1, 0, new SKColor(0, 255, 0, 255));   // Top-right: Green
        bitmap.SetPixel(0, 1, new SKColor(0, 0, 255, 255));   // Bottom-left: Blue
        bitmap.SetPixel(1, 1, new SKColor(255, 255, 255, 255)); // Bottom-right: White

        // Act
        var result = BmpEncoder.Encode(bitmap);

        // Assert - BMP stores rows bottom-to-top
        // Bottom row first (y=1): Blue, White
        Assert.Equal(255, result[54]); // Blue pixel: B=255
        Assert.Equal(0, result[55]);   // Blue pixel: G=0
        Assert.Equal(0, result[56]);   // Blue pixel: R=0

        Assert.Equal(255, result[58]); // White pixel: B=255
        Assert.Equal(255, result[59]); // White pixel: G=255
        Assert.Equal(255, result[60]); // White pixel: R=255

        // Top row second (y=0): Red, Green
        Assert.Equal(0, result[62]);   // Red pixel: B=0
        Assert.Equal(0, result[63]);   // Red pixel: G=0
        Assert.Equal(255, result[64]); // Red pixel: R=255

        Assert.Equal(0, result[66]);   // Green pixel: B=0
        Assert.Equal(255, result[67]); // Green pixel: G=255
        Assert.Equal(0, result[68]);   // Green pixel: R=0
    }

    [Fact]
    public void Encode_SmallImage_RoundTrip()
    {
        // Arrange
        using var original = new SKBitmap(4, 4);
        var colors = new SKColor[]
        {
            SKColors.Red, SKColors.Green, SKColors.Blue, SKColors.White,
            SKColors.Yellow, SKColors.Cyan, SKColors.Magenta, SKColors.Black,
            SKColors.Gray, SKColors.Orange, SKColors.Purple, SKColors.Brown,
            SKColors.Pink, SKColors.Lime, SKColors.Navy, SKColors.Teal
        };

        for (var y = 0; y < 4; y++)
        {
            for (var x = 0; x < 4; x++)
            {
                original.SetPixel(x, y, colors[y * 4 + x]);
            }
        }

        // Act
        var bmpBytes = BmpEncoder.Encode(original);
        using var decoded = SKBitmap.Decode(bmpBytes);

        // Assert
        Assert.NotNull(decoded);
        Assert.Equal(4, decoded.Width);
        Assert.Equal(4, decoded.Height);

        for (var y = 0; y < 4; y++)
        {
            for (var x = 0; x < 4; x++)
            {
                var originalPixel = original.GetPixel(x, y);
                var decodedPixel = decoded.GetPixel(x, y);
                Assert.Equal(originalPixel.Red, decodedPixel.Red);
                Assert.Equal(originalPixel.Green, decodedPixel.Green);
                Assert.Equal(originalPixel.Blue, decodedPixel.Blue);
            }
        }
    }

    [Fact]
    public void Encode_NullBitmap_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => BmpEncoder.Encode(null!));

        using var stream = new MemoryStream();
        Assert.Throws<ArgumentNullException>(() => BmpEncoder.Encode(null!, stream));
    }

    [Fact]
    public void Encode_ToStream_WritesValidBmp()
    {
        // Arrange
        using var bitmap = new SKBitmap(3, 2);
        bitmap.SetPixel(0, 0, SKColors.Red);
        bitmap.SetPixel(1, 0, SKColors.Green);
        bitmap.SetPixel(2, 0, SKColors.Blue);
        bitmap.SetPixel(0, 1, SKColors.Yellow);
        bitmap.SetPixel(1, 1, SKColors.Cyan);
        bitmap.SetPixel(2, 1, SKColors.Magenta);

        using var stream = new MemoryStream();

        // Act
        BmpEncoder.Encode(bitmap, stream);

        // Assert
        var result = stream.ToArray();
        Assert.Equal((byte)'B', result[0]);
        Assert.Equal((byte)'M', result[1]);
        Assert.Equal(78u, BitConverter.ToUInt32(result, 2)); // File size: 14 + 40 + 24

        // Verify round-trip
        using var decoded = SKBitmap.Decode(result);
        Assert.NotNull(decoded);
        Assert.Equal(3, decoded.Width);
        Assert.Equal(2, decoded.Height);
    }

    [Fact]
    public void Encode_DefaultMode_IsBgra32()
    {
        // Arrange
        using var bitmap = new SKBitmap(1, 1);
        bitmap.SetPixel(0, 0, SKColors.Red);

        // Act - call without colorMode (should default to Bgra32)
        var withDefault = BmpEncoder.Encode(bitmap);
        var withExplicit = BmpEncoder.Encode(bitmap, BmpColorMode.Bgra32);

        // Assert - both should produce identical output
        Assert.Equal(withDefault, withExplicit);
        // BitCount at offset 28 should be 32
        Assert.Equal(32, BitConverter.ToUInt16(withDefault, 28));
    }

    [Fact]
    public void Encode_Rgb24_CorrectHeaderBitCount()
    {
        // Arrange
        using var bitmap = new SKBitmap(2, 2);
        bitmap.SetPixel(0, 0, SKColors.Red);
        bitmap.SetPixel(1, 0, SKColors.Green);
        bitmap.SetPixel(0, 1, SKColors.Blue);
        bitmap.SetPixel(1, 1, SKColors.White);

        // Act
        var result = BmpEncoder.Encode(bitmap, BmpColorMode.Rgb24);

        // Assert - BitCount at offset 28 should be 24
        Assert.Equal(24, BitConverter.ToUInt16(result, 28));
        // Data offset should remain 54 (no color table)
        Assert.Equal(54u, BitConverter.ToUInt32(result, 10));
    }

    [Fact]
    public void Encode_Rgb24_NoAlphaInOutput()
    {
        // Arrange - 3px wide to test row padding (3*3=9, padded to 12)
        using var bitmap = new SKBitmap(3, 1);
        bitmap.SetPixel(0, 0, SKColors.Red);
        bitmap.SetPixel(1, 0, SKColors.Green);
        bitmap.SetPixel(2, 0, SKColors.Blue);

        // Act
        var result = BmpEncoder.Encode(bitmap, BmpColorMode.Rgb24);

        // Assert - row bytes: 3 pixels * 3 bytes = 9, padded to 12
        var rowBytes = 12;
        var expectedSize = 54 + 1 * rowBytes; // 1 row
        Assert.Equal(expectedSize, result.Length);

        // First pixel (Red): B=0, G=0, R=255
        Assert.Equal(0, result[54]);     // B
        Assert.Equal(0, result[55]);     // G
        Assert.Equal(255, result[56]);   // R
    }

    [Fact]
    public void Encode_Rgb24_RoundTrip()
    {
        // Arrange
        using var original = new SKBitmap(4, 4);
        var colors = new SKColor[]
        {
            SKColors.Red, SKColors.Green, SKColors.Blue, SKColors.White,
            SKColors.Yellow, SKColors.Cyan, SKColors.Magenta, SKColors.Black,
            SKColors.Gray, SKColors.Orange, SKColors.Purple, SKColors.Brown,
            SKColors.Pink, SKColors.Lime, SKColors.Navy, SKColors.Teal
        };

        for (var y = 0; y < 4; y++)
            for (var x = 0; x < 4; x++)
                original.SetPixel(x, y, colors[y * 4 + x]);

        // Act
        var bmpBytes = BmpEncoder.Encode(original, BmpColorMode.Rgb24);
        using var decoded = SKBitmap.Decode(bmpBytes);

        // Assert
        Assert.NotNull(decoded);
        Assert.Equal(4, decoded.Width);
        Assert.Equal(4, decoded.Height);

        for (var y = 0; y < 4; y++)
        {
            for (var x = 0; x < 4; x++)
            {
                var orig = original.GetPixel(x, y);
                var dec = decoded.GetPixel(x, y);
                Assert.Equal(orig.Red, dec.Red);
                Assert.Equal(orig.Green, dec.Green);
                Assert.Equal(orig.Blue, dec.Blue);
            }
        }
    }

    [Fact]
    public void Encode_Rgb565_CorrectHeaderBitCount()
    {
        // Arrange
        using var bitmap = new SKBitmap(2, 2);
        bitmap.SetPixel(0, 0, SKColors.Red);
        bitmap.SetPixel(1, 0, SKColors.Green);
        bitmap.SetPixel(0, 1, SKColors.Blue);
        bitmap.SetPixel(1, 1, SKColors.White);

        // Act
        var result = BmpEncoder.Encode(bitmap, BmpColorMode.Rgb565);

        // Assert - BitCount at offset 28 should be 16
        Assert.Equal(16, BitConverter.ToUInt16(result, 28));
    }

    [Fact]
    public void Encode_Rgb565_CorrectFileSize()
    {
        // Arrange - 3px wide: 3*2=6, no padding needed (6 % 4 == 2, so padding = 2)
        using var bitmap = new SKBitmap(3, 2);
        for (var x = 0; x < 3; x++)
            for (var y = 0; y < 2; y++)
                bitmap.SetPixel(x, y, SKColors.Red);

        // Act
        var result = BmpEncoder.Encode(bitmap, BmpColorMode.Rgb565);

        // Assert - row bytes: 3*2=6, padding=(4-6%4)%4=2, rowBytes=8; fileSize=54+2*8=70
        var rowBytes = 8;
        var expectedSize = 54 + 2 * rowBytes;
        Assert.Equal(expectedSize, result.Length);
    }

    [Fact]
    public void Encode_Grayscale8_CorrectHeaderBitCount()
    {
        // Arrange
        using var bitmap = new SKBitmap(2, 2);
        bitmap.SetPixel(0, 0, SKColors.White);
        bitmap.SetPixel(1, 0, SKColors.Black);
        bitmap.SetPixel(0, 1, SKColors.Gray);
        bitmap.SetPixel(1, 1, SKColors.Red);

        // Act
        var result = BmpEncoder.Encode(bitmap, BmpColorMode.Grayscale8);

        // Assert - BitCount at offset 28 should be 8
        Assert.Equal(8, BitConverter.ToUInt16(result, 28));
    }

    [Fact]
    public void Encode_Grayscale8_HasColorTable()
    {
        // Arrange
        using var bitmap = new SKBitmap(1, 1);
        bitmap.SetPixel(0, 0, SKColors.White);

        // Act
        var result = BmpEncoder.Encode(bitmap, BmpColorMode.Grayscale8);

        // Assert - DataOffset should be 54 + 1024 (256 * 4 bytes color table)
        Assert.Equal(54u + 1024u, BitConverter.ToUInt32(result, 10));
        // ColorsUsed at offset 46 should be 256
        Assert.Equal(256u, BitConverter.ToUInt32(result, 46));
    }

    [Fact]
    public void Encode_Grayscale8_CorrectFileSize()
    {
        // Arrange - 3px wide: padding = (4 - 3%4) % 4 = 1, rowBytes = 4
        using var bitmap = new SKBitmap(3, 2);
        for (var x = 0; x < 3; x++)
            for (var y = 0; y < 2; y++)
                bitmap.SetPixel(x, y, SKColors.Gray);

        // Act
        var result = BmpEncoder.Encode(bitmap, BmpColorMode.Grayscale8);

        // Assert - fileSize = 54 + 1024 + 2 * 4 = 1086
        var colorTableSize = 1024;
        var rowBytes = 4; // 3 + 1 padding
        var expectedSize = 54 + colorTableSize + 2 * rowBytes;
        Assert.Equal(expectedSize, result.Length);
    }

    [Fact]
    public void Encode_Monochrome1_CorrectHeaderBitCount()
    {
        // Arrange
        using var bitmap = new SKBitmap(8, 2);
        for (var x = 0; x < 8; x++)
            for (var y = 0; y < 2; y++)
                bitmap.SetPixel(x, y, SKColors.White);

        // Act
        var result = BmpEncoder.Encode(bitmap, BmpColorMode.Monochrome1);

        // Assert - BitCount at offset 28 should be 1
        Assert.Equal(1, BitConverter.ToUInt16(result, 28));
    }

    [Fact]
    public void Encode_Monochrome1_HasColorTable()
    {
        // Arrange
        using var bitmap = new SKBitmap(1, 1);
        bitmap.SetPixel(0, 0, SKColors.White);

        // Act
        var result = BmpEncoder.Encode(bitmap, BmpColorMode.Monochrome1);

        // Assert - DataOffset should be 54 + 8 (2 entries * 4 bytes color table) = 62
        Assert.Equal(62u, BitConverter.ToUInt32(result, 10));
        // ColorsUsed at offset 46 should be 2
        Assert.Equal(2u, BitConverter.ToUInt32(result, 46));
    }

    [Fact]
    public void Encode_Monochrome1_BlackAndWhitePixels()
    {
        // Arrange - 8px wide (fits in 1 byte), 1 row
        using var bitmap = new SKBitmap(8, 1);
        bitmap.SetPixel(0, 0, SKColors.White);                       // lum=255 > 128 -> white (1)
        bitmap.SetPixel(1, 0, SKColors.Black);                       // lum=0 <= 128 -> black (0)
        bitmap.SetPixel(2, 0, SKColors.White);                       // white (1)
        bitmap.SetPixel(3, 0, SKColors.Red);                         // lum~76 <= 128 -> black (0)
        bitmap.SetPixel(4, 0, new SKColor(200, 200, 200, 255));      // lum=200 > 128 -> white (1)
        bitmap.SetPixel(5, 0, new SKColor(50, 50, 50, 255));         // lum=50 <= 128 -> black (0)
        bitmap.SetPixel(6, 0, SKColors.Yellow);                      // lum~226 > 128 -> white (1)
        bitmap.SetPixel(7, 0, new SKColor(0, 0, 128, 255));          // lum~15 <= 128 -> black (0)

        // Act
        var result = BmpEncoder.Encode(bitmap, BmpColorMode.Monochrome1);

        // Assert - data offset = 62, pixel byte should be: 10101010 = 0xAA
        // bit7=white(1), bit6=black(0), bit5=white(1), bit4=black(0),
        // bit3=white(1), bit2=black(0), bit1=white(1), bit0=black(0)
        var dataOffset = 62;
        Assert.Equal(0xAA, result[dataOffset]); // 10101010 in binary
    }

    [Fact]
    public void Encode_Grayscale4_CorrectHeaderBitCount()
    {
        // Arrange
        using var bitmap = new SKBitmap(2, 2);
        bitmap.SetPixel(0, 0, SKColors.White);
        bitmap.SetPixel(1, 0, SKColors.Black);
        bitmap.SetPixel(0, 1, SKColors.Gray);
        bitmap.SetPixel(1, 1, SKColors.Red);

        // Act
        var result = BmpEncoder.Encode(bitmap, BmpColorMode.Grayscale4);

        // Assert - BitCount at offset 28 should be 4
        Assert.Equal(4, BitConverter.ToUInt16(result, 28));
    }

    [Fact]
    public void Encode_Grayscale4_HasColorTable()
    {
        // Arrange
        using var bitmap = new SKBitmap(1, 1);
        bitmap.SetPixel(0, 0, SKColors.White);

        // Act
        var result = BmpEncoder.Encode(bitmap, BmpColorMode.Grayscale4);

        // Assert - DataOffset should be 54 + 64 (16 * 4 bytes color table) = 118
        Assert.Equal(118u, BitConverter.ToUInt32(result, 10));
        // ColorsUsed at offset 46 should be 16
        Assert.Equal(16u, BitConverter.ToUInt32(result, 46));
    }

    [Fact]
    public void Encode_Grayscale4_CorrectFileSize()
    {
        // Arrange - 3px wide: bytesPerRow=(3+1)/2=2, padded=(2+3)&~3=4
        using var bitmap = new SKBitmap(3, 2);
        for (var x = 0; x < 3; x++)
            for (var y = 0; y < 2; y++)
                bitmap.SetPixel(x, y, SKColors.Gray);

        // Act
        var result = BmpEncoder.Encode(bitmap, BmpColorMode.Grayscale4);

        // Assert - fileSize = 54 + 64 + 2 * 4 = 126
        var colorTableSize = 64;
        var rowBytes = 4; // 2 bytes data + 2 padding
        var expectedSize = 54 + colorTableSize + 2 * rowBytes;
        Assert.Equal(expectedSize, result.Length);
    }
}
