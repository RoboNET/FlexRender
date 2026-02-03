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
}
