namespace FlexRender;

/// <summary>
/// Represents a binary byte array value in template data.
/// Backed by <see cref="ReadOnlyMemory{T}"/> for efficient access.
/// </summary>
public sealed class BytesValue : TemplateValue
{
    /// <summary>
    /// Gets the binary data as <see cref="ReadOnlyMemory{T}"/>.
    /// </summary>
    public ReadOnlyMemory<byte> Memory { get; }

    /// <summary>
    /// Gets the optional MIME type of the binary data (e.g., "image/png", "application/octet-stream").
    /// </summary>
    public string? MimeType { get; }

    /// <summary>
    /// Gets the byte array value. Creates a copy from <see cref="Memory"/>.
    /// </summary>
    public byte[] Value => Memory.ToArray();

    /// <summary>
    /// Initializes a new instance from a byte array.
    /// </summary>
    /// <param name="value">The byte array value. Cannot be null.</param>
    /// <param name="mimeType">Optional MIME type of the data.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="value"/> is null.</exception>
    public BytesValue(byte[] value, string? mimeType = null)
    {
        ArgumentNullException.ThrowIfNull(value);
        Memory = value;
        MimeType = mimeType;
    }

    /// <summary>
    /// Initializes a new instance from a <see cref="ReadOnlyMemory{T}"/>.
    /// </summary>
    /// <param name="memory">The memory containing binary data.</param>
    /// <param name="mimeType">Optional MIME type of the data.</param>
    public BytesValue(ReadOnlyMemory<byte> memory, string? mimeType = null)
    {
        Memory = memory;
        MimeType = mimeType;
    }

    /// <summary>
    /// Creates a <see cref="BytesValue"/> by reading the entire stream into memory.
    /// </summary>
    /// <param name="stream">The stream to read. Cannot be null.</param>
    /// <param name="mimeType">Optional MIME type of the data.</param>
    /// <param name="maxSize">Maximum allowed size in bytes. Default: 10 MB.</param>
    /// <returns>A new <see cref="BytesValue"/> containing the stream data.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="stream"/> is null.</exception>
    /// <exception cref="InvalidOperationException">Thrown when the stream exceeds <paramref name="maxSize"/>.</exception>
    public static BytesValue FromStream(Stream stream, string? mimeType = null, int maxSize = 10 * 1024 * 1024)
    {
        ArgumentNullException.ThrowIfNull(stream);

        if (stream.CanSeek && stream.Length > maxSize)
        {
            throw new InvalidOperationException(
                $"Content source stream size ({stream.Length} bytes) exceeds the maximum allowed size ({maxSize} bytes).");
        }

        using var ms = new MemoryStream();
        var buffer = new byte[8192];
        int totalRead = 0;
        int bytesRead;
        while ((bytesRead = stream.Read(buffer, 0, buffer.Length)) > 0)
        {
            totalRead += bytesRead;
            if (totalRead > maxSize)
            {
                throw new InvalidOperationException(
                    $"Content source stream exceeds the maximum allowed size ({maxSize} bytes).");
            }
            ms.Write(buffer, 0, bytesRead);
        }
        return new BytesValue(ms.ToArray(), mimeType);
    }

    /// <summary>
    /// Returns the data as a <see cref="ReadOnlySpan{T}"/> for synchronous hot-path access.
    /// </summary>
    public ReadOnlySpan<byte> AsSpan() => Memory.Span;

    /// <summary>
    /// Creates a new read-only <see cref="Stream"/> over the data.
    /// The caller owns the returned stream and must dispose it.
    /// Each call returns an independent stream at position 0.
    /// </summary>
    public Stream AsStream() => new MemoryStream(Memory.ToArray(), writable: false);

    /// <inheritdoc />
    public override bool Equals(TemplateValue? other)
    {
        return other is BytesValue bytesValue && Memory.Span.SequenceEqual(bytesValue.Memory.Span);
    }

    /// <inheritdoc />
    public override int GetHashCode()
    {
        var hash = new HashCode();
        hash.AddBytes(Memory.Span);
        return hash.ToHashCode();
    }

    /// <inheritdoc />
    public override string ToString() => $"bytes[{Memory.Length}]";
}
