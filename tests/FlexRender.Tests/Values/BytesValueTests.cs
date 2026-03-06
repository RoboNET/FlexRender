using Xunit;

namespace FlexRender.Tests.Values;

public class BytesValueTests
{
    [Fact]
    public void Constructor_StoresValue()
    {
        var bytes = new byte[] { 1, 2, 3 };
        var bytesValue = new BytesValue(bytes);

        Assert.Equal(bytes, bytesValue.Value);
    }

    [Fact]
    public void Constructor_NullValue_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => new BytesValue(null!));
    }

    [Fact]
    public void Equals_SameBytes_ReturnsTrue()
    {
        var value1 = new BytesValue(new byte[] { 1, 2, 3 });
        var value2 = new BytesValue(new byte[] { 1, 2, 3 });

        Assert.Equal(value1, value2);
    }

    [Fact]
    public void Equals_DifferentBytes_ReturnsFalse()
    {
        var value1 = new BytesValue(new byte[] { 1, 2, 3 });
        var value2 = new BytesValue(new byte[] { 4, 5, 6 });

        Assert.NotEqual(value1, value2);
    }

    [Fact]
    public void Equals_DifferentType_ReturnsFalse()
    {
        var bytesValue = new BytesValue(new byte[] { 1, 2, 3 });
        var stringValue = new StringValue("test");

        Assert.False(bytesValue.Equals(stringValue));
    }

    [Fact]
    public void Equals_EmptyArrays_ReturnsTrue()
    {
        var value1 = new BytesValue([]);
        var value2 = new BytesValue([]);

        Assert.Equal(value1, value2);
    }

    [Fact]
    public void GetHashCode_SameBytes_ReturnsSameHash()
    {
        var value1 = new BytesValue(new byte[] { 1, 2, 3 });
        var value2 = new BytesValue(new byte[] { 1, 2, 3 });

        Assert.Equal(value1.GetHashCode(), value2.GetHashCode());
    }

    [Fact]
    public void ToString_ReturnsLengthDescription()
    {
        var bytesValue = new BytesValue(new byte[] { 1, 2, 3, 4, 5 });

        Assert.Equal("bytes[5]", bytesValue.ToString());
    }

    [Fact]
    public void ToString_EmptyArray_ReturnsZeroLength()
    {
        var bytesValue = new BytesValue([]);

        Assert.Equal("bytes[0]", bytesValue.ToString());
    }

    [Fact]
    public void Constructor_ReadOnlyMemory_StoresValue()
    {
        ReadOnlyMemory<byte> memory = new byte[] { 1, 2, 3 };
        var bytesValue = new BytesValue(memory);
        Assert.True(memory.Span.SequenceEqual(bytesValue.Memory.Span));
    }

    [Fact]
    public void Constructor_ReadOnlyMemory_WithMimeType()
    {
        var bytesValue = new BytesValue(new byte[] { 1, 2, 3 }, "image/png");
        Assert.Equal("image/png", bytesValue.MimeType);
    }

    [Fact]
    public void Constructor_ByteArray_MimeTypeDefaultsToNull()
    {
        var bytesValue = new BytesValue(new byte[] { 1, 2, 3 });
        Assert.Null(bytesValue.MimeType);
    }

    [Fact]
    public void AsSpan_ReturnsSameData()
    {
        var bytes = new byte[] { 10, 20, 30 };
        var bytesValue = new BytesValue(bytes);
        Assert.True(bytes.AsSpan().SequenceEqual(bytesValue.AsSpan()));
    }

    [Fact]
    public void AsStream_ReturnsReadableStream()
    {
        var bytes = new byte[] { 1, 2, 3 };
        var bytesValue = new BytesValue(bytes);
        using var stream = bytesValue.AsStream();
        var buffer = new byte[3];
        var read = stream.Read(buffer, 0, 3);
        Assert.Equal(3, read);
        Assert.Equal(bytes, buffer);
    }

    [Fact]
    public void AsStream_IsNotWritable()
    {
        var bytesValue = new BytesValue(new byte[] { 1 });
        using var stream = bytesValue.AsStream();
        Assert.False(stream.CanWrite);
    }

    [Fact]
    public void AsStream_MultipleCallsReturnIndependentStreams()
    {
        var bytesValue = new BytesValue(new byte[] { 1, 2, 3 });
        using var stream1 = bytesValue.AsStream();
        using var stream2 = bytesValue.AsStream();
        stream1.ReadByte();
        Assert.Equal(1, stream1.Position);
        Assert.Equal(0, stream2.Position);
    }

    [Fact]
    public void FromStream_ReadsStreamContents()
    {
        var bytes = new byte[] { 5, 6, 7, 8 };
        using var source = new MemoryStream(bytes);
        var bytesValue = BytesValue.FromStream(source);
        Assert.Equal(bytes, bytesValue.Value);
    }

    [Fact]
    public void FromStream_WithMimeType()
    {
        using var source = new MemoryStream(new byte[] { 1 });
        var bytesValue = BytesValue.FromStream(source, "application/octet-stream");
        Assert.Equal("application/octet-stream", bytesValue.MimeType);
    }

    [Fact]
    public void FromStream_NullStream_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => BytesValue.FromStream(null!));
    }

    [Fact]
    public void Value_ReturnsArrayFromMemory()
    {
        ReadOnlyMemory<byte> memory = new byte[] { 1, 2, 3 };
        var bytesValue = new BytesValue(memory);
        Assert.Equal(new byte[] { 1, 2, 3 }, bytesValue.Value);
    }

    [Fact]
    public void Memory_Property_ReturnsBackingMemory()
    {
        var bytes = new byte[] { 1, 2, 3 };
        var bytesValue = new BytesValue(bytes);
        Assert.True(bytes.AsSpan().SequenceEqual(bytesValue.Memory.Span));
    }

    [Fact]
    public void ImplicitConversion_FromByteArray_CreatesBytesValue()
    {
        TemplateValue value = new byte[] { 1, 2, 3 };

        Assert.IsType<BytesValue>(value);
        Assert.Equal(new byte[] { 1, 2, 3 }, ((BytesValue)value).Value);
    }

    [Fact]
    public void FromStream_ExceedingMaxSize_Throws()
    {
        var data = new byte[100];
        using var stream = new MemoryStream(data);
        var ex = Assert.Throws<InvalidOperationException>(() => BytesValue.FromStream(stream, maxSize: 50));
        Assert.Contains("exceeds the maximum allowed size", ex.Message);
    }

    [Fact]
    public void FromStream_WithinMaxSize_Succeeds()
    {
        var data = new byte[50];
        using var stream = new MemoryStream(data);
        var result = BytesValue.FromStream(stream, maxSize: 100);
        Assert.Equal(50, result.Memory.Length);
    }
}
