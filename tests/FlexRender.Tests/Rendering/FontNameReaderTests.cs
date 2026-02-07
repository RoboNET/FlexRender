using FlexRender.Rendering;
using Xunit;

namespace FlexRender.Tests.Rendering;

public class FontNameReaderTests
{
    [Fact]
    public void ReadFamilyName_NullByteArray_ReturnsNull()
    {
        var result = FontNameReader.ReadFamilyName((byte[]?)null);

        Assert.Null(result);
    }

    [Fact]
    public void ReadFamilyName_EmptyByteArray_ReturnsNull()
    {
        var result = FontNameReader.ReadFamilyName(Array.Empty<byte>());

        Assert.Null(result);
    }

    [Fact]
    public void ReadFamilyName_EmptySpan_ReturnsNull()
    {
        var result = FontNameReader.ReadFamilyName(ReadOnlySpan<byte>.Empty);

        Assert.Null(result);
    }

    [Fact]
    public void ReadFamilyName_TruncatedHeader_ReturnsNull()
    {
        // Only 4 bytes -- not enough for offset table (needs 12)
        var result = FontNameReader.ReadFamilyName(new byte[] { 0x00, 0x01, 0x00, 0x00 });

        Assert.Null(result);
    }

    [Fact]
    public void ReadFamilyName_TruncatedOffsetTable_ReturnsNull()
    {
        // 11 bytes -- one short of the minimum 12-byte offset table
        var result = FontNameReader.ReadFamilyName(new byte[11]);

        Assert.Null(result);
    }

    [Fact]
    public void ReadFamilyName_InvalidTableCount_ReturnsNull()
    {
        // Valid sfVersion (0x00010000) but numTables pointing beyond data
        var data = new byte[12];
        // sfVersion = 0x00010000 (TrueType)
        data[0] = 0x00;
        data[1] = 0x01;
        data[2] = 0x00;
        data[3] = 0x00;
        // numTables = 100 (would need 12 + 100*16 = 1612 bytes)
        data[4] = 0x00;
        data[5] = 100;

        var result = FontNameReader.ReadFamilyName(data);

        Assert.Null(result);
    }

    [Fact]
    public void ReadFamilyName_NoNameTable_ReturnsNull()
    {
        // Valid header with 1 table, but the table tag is not 'name'
        var data = new byte[12 + 16]; // offset table + 1 table record
        // sfVersion = 0x00010000
        data[0] = 0x00;
        data[1] = 0x01;
        data[2] = 0x00;
        data[3] = 0x00;
        // numTables = 1
        data[4] = 0x00;
        data[5] = 0x01;
        // Table record: tag = 'head' (not 'name')
        data[12] = (byte)'h';
        data[13] = (byte)'e';
        data[14] = (byte)'a';
        data[15] = (byte)'d';

        var result = FontNameReader.ReadFamilyName(data);

        Assert.Null(result);
    }

    [Fact]
    public void ReadFamilyName_TtcfTag_TruncatedHeader_ReturnsNull()
    {
        // Starts with 'ttcf' but too short for TTC header
        var data = new byte[12];
        data[0] = (byte)'t';
        data[1] = (byte)'t';
        data[2] = (byte)'c';
        data[3] = (byte)'f';
        // numFonts = 0
        data[8] = 0x00;
        data[9] = 0x00;
        data[10] = 0x00;
        data[11] = 0x00;

        var result = FontNameReader.ReadFamilyName(data);

        Assert.Null(result);
    }

    [Fact]
    public void ReadFamilyName_MinimalValidFont_WindowsPlatform_ReadsName()
    {
        // Build a minimal font with just the offset table, one table directory entry ('name'),
        // and a name table with one Windows platform record for nameID=1.
        var familyName = "TestFont";
        var fontBytes = BuildMinimalFont(familyName, platformId: 3, encodingId: 1);

        var result = FontNameReader.ReadFamilyName(fontBytes);

        Assert.Equal(familyName, result);
    }

    [Fact]
    public void ReadFamilyName_MinimalValidFont_MacPlatform_ReadsName()
    {
        // Build a minimal font with Macintosh platform record
        var familyName = "MacFont";
        var fontBytes = BuildMinimalFontMac(familyName);

        var result = FontNameReader.ReadFamilyName(fontBytes);

        Assert.Equal(familyName, result);
    }

    [Fact]
    public void ReadFamilyName_WindowsPlatformPreferredOverMac()
    {
        // Build a font with both Windows and Mac records; Windows should be preferred
        var fontBytes = BuildMinimalFontBothPlatforms("WinName", "MacName");

        var result = FontNameReader.ReadFamilyName(fontBytes);

        Assert.Equal("WinName", result);
    }

    [Fact]
    public void ReadFamilyName_RealInterFont_ReadsFamilyName()
    {
        var fontPath = Path.Combine(
            AppContext.BaseDirectory,
            "Snapshots", "Fonts", "Inter-Regular.ttf");

        if (!File.Exists(fontPath))
        {
            // Skip if the font file is not available in the test output
            return;
        }

        var fontBytes = File.ReadAllBytes(fontPath);

        var result = FontNameReader.ReadFamilyName(fontBytes);

        Assert.NotNull(result);
        Assert.Contains("Inter", result);
    }

    [Fact]
    public void ReadFamilyName_RandomGarbage_ReturnsNull()
    {
        // 256 bytes of non-font data
        var random = new Random(42);
        var data = new byte[256];
        random.NextBytes(data);
        // Make sure it does not accidentally start with 'ttcf' or valid sfVersion
        data[0] = 0xFF;
        data[1] = 0xFF;

        var result = FontNameReader.ReadFamilyName(data);

        Assert.Null(result);
    }

    [Fact]
    public void ReadFamilyName_NameTableWithZeroCount_ReturnsNull()
    {
        // Build a font where the name table has count=0 (no name records)
        var fontBytes = BuildMinimalFontEmptyNameTable();

        var result = FontNameReader.ReadFamilyName(fontBytes);

        Assert.Null(result);
    }

    [Fact]
    public void ReadFamilyName_NameRecordPointsBeyondData_ReturnsNull()
    {
        // Build a font where the name record's string offset points beyond the data
        var fontBytes = BuildMinimalFontBadStringOffset();

        var result = FontNameReader.ReadFamilyName(fontBytes);

        Assert.Null(result);
    }

    [Fact]
    public void ReadFamilyName_SpanOverload_MatchesByteArrayOverload()
    {
        var familyName = "SpanTest";
        var fontBytes = BuildMinimalFont(familyName, platformId: 3, encodingId: 1);

        var arrayResult = FontNameReader.ReadFamilyName(fontBytes);
        var spanResult = FontNameReader.ReadFamilyName(fontBytes.AsSpan());

        Assert.Equal(arrayResult, spanResult);
    }

    /// <summary>
    /// Builds a minimal valid TTF with a single Windows platform name record (UTF-16 BE).
    /// </summary>
    private static byte[] BuildMinimalFont(string familyName, ushort platformId, ushort encodingId)
    {
        // Encode family name as UTF-16 BE
        var nameBytes = new byte[familyName.Length * 2];
        for (var i = 0; i < familyName.Length; i++)
        {
            nameBytes[i * 2] = (byte)(familyName[i] >> 8);
            nameBytes[i * 2 + 1] = (byte)(familyName[i] & 0xFF);
        }

        return BuildFontWithNameRecords(
            new NameRecord(platformId, encodingId, 0x0409, 1, nameBytes));
    }

    /// <summary>
    /// Builds a minimal valid TTF with a single Macintosh platform name record (ASCII).
    /// </summary>
    private static byte[] BuildMinimalFontMac(string familyName)
    {
        var nameBytes = System.Text.Encoding.ASCII.GetBytes(familyName);

        return BuildFontWithNameRecords(
            new NameRecord(1, 0, 0, 1, nameBytes));
    }

    /// <summary>
    /// Builds a minimal valid TTF with both Windows and Macintosh platform name records.
    /// </summary>
    private static byte[] BuildMinimalFontBothPlatforms(string windowsName, string macName)
    {
        var winBytes = new byte[windowsName.Length * 2];
        for (var i = 0; i < windowsName.Length; i++)
        {
            winBytes[i * 2] = (byte)(windowsName[i] >> 8);
            winBytes[i * 2 + 1] = (byte)(windowsName[i] & 0xFF);
        }

        var macBytes = System.Text.Encoding.ASCII.GetBytes(macName);

        // Mac record first, Windows second -- should still prefer Windows
        return BuildFontWithNameRecords(
            new NameRecord(1, 0, 0, 1, macBytes),
            new NameRecord(3, 1, 0x0409, 1, winBytes));
    }

    /// <summary>
    /// Builds a minimal font with an empty name table (count=0).
    /// </summary>
    private static byte[] BuildMinimalFontEmptyNameTable()
    {
        return BuildFontWithNameRecords();
    }

    /// <summary>
    /// Builds a minimal font where the name record string offset points beyond available data.
    /// </summary>
    private static byte[] BuildMinimalFontBadStringOffset()
    {
        // Create a single name record but truncate the string data
        var nameBytes = new byte[20]; // 20 bytes of string data
        var record = new NameRecord(3, 1, 0x0409, 1, nameBytes);

        var font = BuildFontWithNameRecords(record);

        // Truncate the font to cut off the string storage area
        var truncated = new byte[font.Length - 15];
        Array.Copy(font, truncated, truncated.Length);
        return truncated;
    }

    private static byte[] BuildFontWithNameRecords(params NameRecord[] records)
    {
        // Offset table: 12 bytes
        // Table directory: 1 entry * 16 bytes = 16 bytes
        // Name table starts at offset 28
        const int offsetTableSize = 12;
        const int tableDirectorySize = 16; // 1 table entry
        const int nameTableStart = offsetTableSize + tableDirectorySize;
        const int nameTableHeaderSize = 6;
        var nameRecordsSize = records.Length * 12;
        var stringStorageOffset = nameTableHeaderSize + nameRecordsSize;

        // Calculate total string storage size
        var totalStringSize = 0;
        foreach (var r in records)
        {
            totalStringSize += r.StringData.Length;
        }

        var nameTableSize = stringStorageOffset + totalStringSize;
        var totalSize = nameTableStart + nameTableSize;

        var data = new byte[totalSize];
        var ms = new MemoryStream(data);
        using var w = new BinaryWriter(ms);

        // Offset table
        WriteUInt32BE(w, 0x00010000); // sfVersion (TrueType)
        WriteUInt16BE(w, 1);          // numTables
        WriteUInt16BE(w, 16);         // searchRange
        WriteUInt16BE(w, 0);          // entrySelector
        WriteUInt16BE(w, 0);          // rangeShift

        // Table directory entry for 'name'
        w.Write((byte)'n');
        w.Write((byte)'a');
        w.Write((byte)'m');
        w.Write((byte)'e');
        WriteUInt32BE(w, 0);                          // checksum (not validated)
        WriteUInt32BE(w, (uint)nameTableStart);       // offset
        WriteUInt32BE(w, (uint)nameTableSize);        // length

        // Name table header
        WriteUInt16BE(w, 0);                              // format
        WriteUInt16BE(w, (ushort)records.Length);          // count
        WriteUInt16BE(w, (ushort)stringStorageOffset);    // stringOffset (from table start)

        // Name records
        var currentStringOffset = 0;
        foreach (var r in records)
        {
            WriteUInt16BE(w, r.PlatformId);
            WriteUInt16BE(w, r.EncodingId);
            WriteUInt16BE(w, r.LanguageId);
            WriteUInt16BE(w, r.NameId);
            WriteUInt16BE(w, (ushort)r.StringData.Length);
            WriteUInt16BE(w, (ushort)currentStringOffset);
            currentStringOffset += r.StringData.Length;
        }

        // String storage
        foreach (var r in records)
        {
            w.Write(r.StringData);
        }

        return data;
    }

    private static void WriteUInt16BE(BinaryWriter w, ushort value)
    {
        w.Write((byte)(value >> 8));
        w.Write((byte)(value & 0xFF));
    }

    private static void WriteUInt32BE(BinaryWriter w, uint value)
    {
        w.Write((byte)((value >> 24) & 0xFF));
        w.Write((byte)((value >> 16) & 0xFF));
        w.Write((byte)((value >> 8) & 0xFF));
        w.Write((byte)(value & 0xFF));
    }

    private sealed record NameRecord(
        ushort PlatformId,
        ushort EncodingId,
        ushort LanguageId,
        ushort NameId,
        byte[] StringData);
}
