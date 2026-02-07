using System.Buffers.Binary;

namespace FlexRender.Rendering;

/// <summary>
/// Reads the font family name from TrueType (.ttf), OpenType (.otf), and TrueType Collection (.ttc) font files
/// by parsing the OpenType <c>name</c> table directly from raw bytes.
/// </summary>
/// <remarks>
/// This reader is AOT-safe, allocation-minimal, and does not depend on any native libraries.
/// All reads are bounds-checked; any parsing failure returns <see langword="null"/>.
/// </remarks>
public static class FontNameReader
{
    private const uint NameTag = 0x6E616D65; // 'name' in big-endian ASCII
    private const uint TtcfTag = 0x74746366; // 'ttcf' in big-endian ASCII
    private const ushort PlatformWindows = 3;
    private const ushort PlatformMacintosh = 1;
    private const ushort EncodingWindowsUnicodeBmp = 1;
    private const ushort EncodingMacintoshRoman = 0;
    private const ushort NameIdFontFamily = 1;
    private const int OffsetTableSize = 12;
    private const int TableDirectoryEntrySize = 16;
    private const int NameRecordSize = 12;
    private const int TtcHeaderMinSize = 12;

    /// <summary>
    /// Reads the font family name from the raw bytes of a TrueType, OpenType, or TrueType Collection font file.
    /// </summary>
    /// <param name="fontBytes">The raw font file bytes.</param>
    /// <returns>
    /// The font family name if successfully parsed; otherwise, <see langword="null"/>.
    /// Returns <see langword="null"/> for empty, truncated, or malformed font data.
    /// </returns>
    public static string? ReadFamilyName(ReadOnlySpan<byte> fontBytes)
    {
        if (fontBytes.Length < OffsetTableSize)
        {
            return null;
        }

        var offset = 0;

        // Check for TrueType Collection (.ttc)
        var sfVersion = BinaryPrimitives.ReadUInt32BigEndian(fontBytes);
        if (sfVersion == TtcfTag)
        {
            offset = GetFirstFontOffsetFromTtc(fontBytes);
            if (offset < 0 || offset + OffsetTableSize > fontBytes.Length)
            {
                return null;
            }
        }

        return ReadFamilyNameFromOffset(fontBytes, offset);
    }

    /// <summary>
    /// Reads the font family name from the raw bytes of a TrueType, OpenType, or TrueType Collection font file.
    /// </summary>
    /// <param name="fontBytes">The raw font file bytes.</param>
    /// <returns>
    /// The font family name if successfully parsed; otherwise, <see langword="null"/>.
    /// Returns <see langword="null"/> for <see langword="null"/>, empty, truncated, or malformed font data.
    /// </returns>
    public static string? ReadFamilyName(byte[]? fontBytes)
    {
        if (fontBytes is null || fontBytes.Length == 0)
        {
            return null;
        }

        return ReadFamilyName(fontBytes.AsSpan());
    }

    /// <summary>
    /// Extracts the byte offset of the first font in a TrueType Collection header.
    /// </summary>
    /// <param name="data">The raw font file bytes starting with the TTC header.</param>
    /// <returns>The byte offset of the first font, or -1 if the header is malformed.</returns>
    private static int GetFirstFontOffsetFromTtc(ReadOnlySpan<byte> data)
    {
        // TTC Header:
        //   uint32 ttcTag        (4 bytes) - 'ttcf'
        //   uint16 majorVersion  (2 bytes)
        //   uint16 minorVersion  (2 bytes)
        //   uint32 numFonts      (4 bytes)
        //   uint32[] offsets     (numFonts * 4 bytes)
        if (data.Length < TtcHeaderMinSize)
        {
            return -1;
        }

        var numFonts = BinaryPrimitives.ReadUInt32BigEndian(data.Slice(8, 4));
        if (numFonts == 0)
        {
            return -1;
        }

        // Read the first font offset (at byte 12)
        if (data.Length < TtcHeaderMinSize + 4)
        {
            return -1;
        }

        var firstOffset = BinaryPrimitives.ReadUInt32BigEndian(data.Slice(12, 4));
        if (firstOffset > int.MaxValue)
        {
            return -1;
        }

        return (int)firstOffset;
    }

    /// <summary>
    /// Reads the font family name starting at the given byte offset within the font data.
    /// </summary>
    /// <param name="data">The raw font file bytes.</param>
    /// <param name="fontOffset">The byte offset where the font's offset table begins.</param>
    /// <returns>The font family name, or <see langword="null"/> if parsing fails.</returns>
    private static string? ReadFamilyNameFromOffset(ReadOnlySpan<byte> data, int fontOffset)
    {
        // Read the offset table
        if (fontOffset + OffsetTableSize > data.Length)
        {
            return null;
        }

        var numTables = BinaryPrimitives.ReadUInt16BigEndian(data.Slice(fontOffset + 4, 2));

        // Find the 'name' table in the table directory
        var tableDirectoryStart = fontOffset + OffsetTableSize;
        var tableDirectoryEnd = tableDirectoryStart + (numTables * TableDirectoryEntrySize);

        if (tableDirectoryEnd > data.Length)
        {
            return null;
        }

        for (var i = 0; i < numTables; i++)
        {
            var entryOffset = tableDirectoryStart + (i * TableDirectoryEntrySize);
            var tag = BinaryPrimitives.ReadUInt32BigEndian(data.Slice(entryOffset, 4));

            if (tag == NameTag)
            {
                var nameTableOffset = BinaryPrimitives.ReadUInt32BigEndian(data.Slice(entryOffset + 8, 4));
                var nameTableLength = BinaryPrimitives.ReadUInt32BigEndian(data.Slice(entryOffset + 12, 4));

                if (nameTableOffset > int.MaxValue || nameTableLength > int.MaxValue)
                {
                    return null;
                }

                return ReadFamilyNameFromNameTable(data, (int)nameTableOffset, (int)nameTableLength);
            }
        }

        return null;
    }

    /// <summary>
    /// Parses the OpenType <c>name</c> table to extract the font family name (nameID = 1).
    /// Prefers the Windows platform (platformID = 3, encodingID = 1, UTF-16 BE) and falls back
    /// to the Macintosh platform (platformID = 1, encodingID = 0, ASCII).
    /// </summary>
    /// <param name="data">The raw font file bytes.</param>
    /// <param name="tableOffset">The byte offset of the name table within <paramref name="data"/>.</param>
    /// <param name="tableLength">The length of the name table in bytes.</param>
    /// <returns>The font family name, or <see langword="null"/> if not found or parsing fails.</returns>
    private static string? ReadFamilyNameFromNameTable(ReadOnlySpan<byte> data, int tableOffset, int tableLength)
    {
        // Name table header:
        //   uint16 format         (2 bytes)
        //   uint16 count          (2 bytes)
        //   uint16 stringOffset   (2 bytes) - offset from start of name table to string storage
        const int nameTableHeaderSize = 6;

        if (tableOffset + nameTableHeaderSize > data.Length)
        {
            return null;
        }

        var nameTableEnd = tableOffset + tableLength;
        if (nameTableEnd > data.Length)
        {
            // Clamp to actual data length
            nameTableEnd = data.Length;
        }

        var count = BinaryPrimitives.ReadUInt16BigEndian(data.Slice(tableOffset + 2, 2));
        var stringOffset = BinaryPrimitives.ReadUInt16BigEndian(data.Slice(tableOffset + 4, 2));
        var stringStorageStart = tableOffset + stringOffset;

        var recordsStart = tableOffset + nameTableHeaderSize;
        var recordsEnd = recordsStart + (count * NameRecordSize);

        if (recordsEnd > data.Length)
        {
            return null;
        }

        // First pass: look for Windows platform (3) with Unicode BMP encoding (1)
        string? windowsName = null;
        string? macName = null;

        for (var i = 0; i < count; i++)
        {
            var recordOffset = recordsStart + (i * NameRecordSize);

            var platformId = BinaryPrimitives.ReadUInt16BigEndian(data.Slice(recordOffset, 2));
            var encodingId = BinaryPrimitives.ReadUInt16BigEndian(data.Slice(recordOffset + 2, 2));
            // languageId at recordOffset + 4 (not needed for family name matching)
            var nameId = BinaryPrimitives.ReadUInt16BigEndian(data.Slice(recordOffset + 6, 2));
            var length = BinaryPrimitives.ReadUInt16BigEndian(data.Slice(recordOffset + 8, 2));
            var strOffset = BinaryPrimitives.ReadUInt16BigEndian(data.Slice(recordOffset + 10, 2));

            if (nameId != NameIdFontFamily)
            {
                continue;
            }

            var absoluteStringOffset = stringStorageStart + strOffset;

            if (absoluteStringOffset + length > data.Length || length == 0)
            {
                continue;
            }

            var stringSpan = data.Slice(absoluteStringOffset, length);

            if (platformId == PlatformWindows && encodingId == EncodingWindowsUnicodeBmp)
            {
                windowsName = DecodeUtf16BigEndian(stringSpan);
                // Windows platform is preferred, return immediately
                if (!string.IsNullOrEmpty(windowsName))
                {
                    return windowsName;
                }
            }
            else if (platformId == PlatformMacintosh && encodingId == EncodingMacintoshRoman && macName is null)
            {
                macName = DecodeAscii(stringSpan);
            }
        }

        // Fall back to Macintosh name if Windows name was not found
        return !string.IsNullOrEmpty(macName) ? macName : null;
    }

    /// <summary>
    /// Decodes a UTF-16 Big Endian encoded string from a byte span.
    /// </summary>
    /// <param name="data">The raw UTF-16 BE bytes.</param>
    /// <returns>The decoded string, or <see langword="null"/> if the span length is odd.</returns>
    private static string? DecodeUtf16BigEndian(ReadOnlySpan<byte> data)
    {
        if (data.Length < 2 || data.Length % 2 != 0)
        {
            return null;
        }

        var charCount = data.Length / 2;
        var chars = charCount <= 128
            ? stackalloc char[charCount]
            : new char[charCount];

        for (var i = 0; i < charCount; i++)
        {
            chars[i] = (char)BinaryPrimitives.ReadUInt16BigEndian(data.Slice(i * 2, 2));
        }

        return new string(chars);
    }

    /// <summary>
    /// Decodes an ASCII-encoded string from a byte span (Macintosh Roman, encoding 0).
    /// Non-ASCII bytes (> 127) are replaced with '?'.
    /// </summary>
    /// <param name="data">The raw ASCII bytes.</param>
    /// <returns>The decoded string.</returns>
    private static string DecodeAscii(ReadOnlySpan<byte> data)
    {
        var chars = data.Length <= 128
            ? stackalloc char[data.Length]
            : new char[data.Length];

        for (var i = 0; i < data.Length; i++)
        {
            chars[i] = data[i] <= 127 ? (char)data[i] : '?';
        }

        return new string(chars);
    }
}
