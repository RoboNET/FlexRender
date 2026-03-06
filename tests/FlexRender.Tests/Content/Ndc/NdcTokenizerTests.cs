using FlexRender.Content.Ndc;
using Xunit;

namespace FlexRender.Tests.Content.Ndc;

public sealed class NdcTokenizerTests
{
    [Fact]
    public void Tokenize_PlainText_ReturnsSingleTextToken()
    {
        var tokens = NdcTokenizer.Tokenize("Hello World").ToList();

        Assert.Single(tokens);
        Assert.Equal(NdcTokenType.Text, tokens[0].Type);
        Assert.Equal("Hello World", tokens[0].Value);
    }

    [Fact]
    public void Tokenize_LineFeed_ReturnsLineFeedToken()
    {
        var tokens = NdcTokenizer.Tokenize("Line1\nLine2").ToList();

        Assert.Equal(3, tokens.Count);
        Assert.Equal(NdcTokenType.Text, tokens[0].Type);
        Assert.Equal("Line1", tokens[0].Value);
        Assert.Equal(NdcTokenType.LineFeed, tokens[1].Type);
        Assert.Equal(NdcTokenType.Text, tokens[2].Type);
        Assert.Equal("Line2", tokens[2].Value);
    }

    [Fact]
    public void Tokenize_CrLf_ReturnsSingleLineFeedToken()
    {
        var tokens = NdcTokenizer.Tokenize("A\r\nB").ToList();

        Assert.Equal(3, tokens.Count);
        Assert.Equal(NdcTokenType.LineFeed, tokens[1].Type);
    }

    [Fact]
    public void Tokenize_FormFeed_ReturnsFormFeedToken()
    {
        var tokens = NdcTokenizer.Tokenize("Page1\x0CPage2").ToList();

        Assert.Equal(3, tokens.Count);
        Assert.Equal(NdcTokenType.FormFeed, tokens[1].Type);
    }

    [Fact]
    public void Tokenize_EscCharsetSwitch_ReturnsSwitchToken()
    {
        // ESC ( I = 0x1B 0x28 0x49
        var tokens = NdcTokenizer.Tokenize("before\x1B(Iafter\x1B(1end").ToList();

        Assert.Equal(5, tokens.Count);
        Assert.Equal(NdcTokenType.Text, tokens[0].Type);
        Assert.Equal("before", tokens[0].Value);
        Assert.Equal(NdcTokenType.CharsetSwitch, tokens[1].Type);
        Assert.Equal("I", tokens[1].Value);
        Assert.Equal(NdcTokenType.Text, tokens[2].Type);
        Assert.Equal("after", tokens[2].Value);
        Assert.Equal(NdcTokenType.CharsetSwitch, tokens[3].Type);
        Assert.Equal("1", tokens[3].Value);
        Assert.Equal(NdcTokenType.Text, tokens[4].Type);
        Assert.Equal("end", tokens[4].Value);
    }

    [Fact]
    public void Tokenize_ShiftOutSpaces_ReturnsSpacesToken()
    {
        // SO 4 = 0x0E 0x34 -> 4 spaces
        var tokens = NdcTokenizer.Tokenize("A\x0E" + "4B").ToList();

        Assert.Equal(3, tokens.Count);
        Assert.Equal(NdcTokenType.Spaces, tokens[1].Type);
        Assert.Equal("4", tokens[1].Value); // 4 spaces
    }

    [Fact]
    public void Tokenize_ShiftOutColon_Returns10Spaces()
    {
        // SO : = 0x0E 0x3A -> 10 spaces
        var tokens = NdcTokenizer.Tokenize("\x0E:").ToList();

        Assert.Single(tokens);
        Assert.Equal(NdcTokenType.Spaces, tokens[0].Type);
        Assert.Equal("10", tokens[0].Value);
    }

    [Fact]
    public void Tokenize_GroupSeparator_ReturnsFieldSeparatorToken()
    {
        // GS 3 = 0x1D 0x33
        var tokens = NdcTokenizer.Tokenize("A\x1D" + "3B").ToList();

        Assert.Equal(3, tokens.Count);
        Assert.Equal(NdcTokenType.FieldSeparator, tokens[1].Type);
        Assert.Equal("3", tokens[1].Value);
    }

    [Fact]
    public void Tokenize_Barcode_ReturnsBarcodeToken()
    {
        // ESC k 4 CODE39DATA ESC \ = 0x1B 0x6B 0x34 ... 0x1B 0x5C
        var tokens = NdcTokenizer.Tokenize("\x1Bk4CODE39DATA\x1B\\").ToList();

        Assert.Single(tokens);
        Assert.Equal(NdcTokenType.Barcode, tokens[0].Type);
        Assert.Equal("4:CODE39DATA", tokens[0].Value); // type:data format
    }

    [Fact]
    public void Tokenize_SecondaryCharsetSwitch_ReturnsSwitchToken()
    {
        // ESC ) 2 = 0x1B 0x29 0x32
        var tokens = NdcTokenizer.Tokenize("\x1B)2text").ToList();

        Assert.Equal(2, tokens.Count);
        Assert.Equal(NdcTokenType.CharsetSwitch, tokens[0].Type);
        Assert.Equal("2", tokens[0].Value);
    }

    [Fact]
    public void Tokenize_UnknownEscSequence_SkipsGracefully()
    {
        // ESC Z (unknown) should be skipped, not crash
        var tokens = NdcTokenizer.Tokenize("\x1BZtext").ToList();

        // Should get at least the "text" part
        Assert.Contains(tokens, t => t.Type == NdcTokenType.Text && t.Value == "text");
    }

    [Fact]
    public void Tokenize_EmptyString_ReturnsEmpty()
    {
        var tokens = NdcTokenizer.Tokenize("").ToList();
        Assert.Empty(tokens);
    }

    [Fact]
    public void Tokenize_PrinterFlag_SkippedAtStart()
    {
        // ":02" at start of receipt -- printer flag, should be ignored
        // It's just text that happens to be ":02", parser will handle as text
        var tokens = NdcTokenizer.Tokenize(":02\x1B(Itext").ToList();

        Assert.Equal(3, tokens.Count);
        Assert.Equal(NdcTokenType.Text, tokens[0].Type);
        Assert.Equal(":02", tokens[0].Value);
    }
}
