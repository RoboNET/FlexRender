using FlexRender.Content.Ndc;
using Xunit;

namespace FlexRender.Tests.Content.Ndc;

public sealed class NdcEncodingsTests
{
    [Theory]
    [InlineData("ntcnjdsq ~fyr", "\u0442\u0435\u0441\u0442\u043e\u0432\u044b\u0439 \u0431\u0430\u043d\u043a")]
    [InlineData("ntk", "\u0442\u0435\u043b")]
    [InlineData("flhtc", "\u0430\u0434\u0440\u0435\u0441")]
    [InlineData("lfnf", "\u0434\u0430\u0442\u0430")]
    [InlineData("dhtvz", "\u0432\u0440\u0435\u043c\u044f")]
    [InlineData("rfhns", "\u043a\u0430\u0440\u0442\u044b")]
    [InlineData("jgthfwbz", "\u043e\u043f\u0435\u0440\u0430\u0446\u0438\u044f")]
    [InlineData("gjcktlybt", "\u043f\u043e\u0441\u043b\u0435\u0434\u043d\u0438\u0435")]
    [InlineData("jlj~htyj", "\u043e\u0434\u043e\u0431\u0440\u0435\u043d\u043e")]
    [InlineData("he~", "\u0440\u0443\u0431")]
    [InlineData("~fkfyc", "\u0431\u0430\u043b\u0430\u043d\u0441")]
    [InlineData("~fyrjvfnf", "\u0431\u0430\u043d\u043a\u043e\u043c\u0430\u0442\u0430")]
    [InlineData("xtr", "\u0447\u0435\u043a")]
    [InlineData("wbrk", "\u0446\u0438\u043a\u043b")]
    [InlineData("lbcgtycthf", "\u0434\u0438\u0441\u043f\u0435\u043d\u0441\u0435\u0440\u0430")]
    [InlineData("cevvs", "\u0441\u0443\u043c\u043c\u044b")]
    [InlineData("rfcctnfv", "\u043a\u0430\u0441\u0441\u0435\u0442\u0430\u043c")]
    [InlineData("dslfxb", "\u0432\u044b\u0434\u0430\u0447\u0438")]
    [InlineData("jcnfnjr", "\u043e\u0441\u0442\u0430\u0442\u043e\u043a")]
    [InlineData("bnjuj", "\u0438\u0442\u043e\u0433\u043e")]
    [InlineData("rjvbccbz", "\u043a\u043e\u043c\u0438\u0441\u0441\u0438\u044f")]
    [InlineData("gjlndth|ltybz", "\u043f\u043e\u0434\u0442\u0432\u0435\u0440\u0436\u0434\u0435\u043d\u0438\u044f")]
    public void QwertyJcuken_DecodesRussianText(string input, string expected)
    {
        var result = NdcEncodings.Decode(input, "qwerty-jcuken");
        Assert.Equal(expected, result);
    }

    [Fact]
    public void QwertyJcuken_UppercaseFlag_ConvertsToUppercase()
    {
        // With uppercase=true, all mapped chars become uppercase Cyrillic
        Assert.Equal("БАНК", NdcEncodings.Decode("~fyr", "qwerty-jcuken", uppercase: true));
    }

    [Fact]
    public void QwertyJcuken_WithoutUppercase_AllLowercase()
    {
        // Without uppercase flag, uppercase ASCII keys still map to lowercase Cyrillic
        Assert.Equal("банк", NdcEncodings.Decode("~fyr", "qwerty-jcuken"));
    }

    [Fact]
    public void QwertyJcuken_PreservesNonMappedChars()
    {
        // Digits, spaces, punctuation that don't map to Cyrillic stay as-is
        Assert.Equal("123 - 456", NdcEncodings.Decode("123 - 456", "qwerty-jcuken"));
    }

    [Theory]
    [InlineData(";", ";")]
    [InlineData(":", ":")]
    [InlineData(",", ",")]
    [InlineData(".", ".")]
    [InlineData("<", "<")]
    [InlineData(">", ">")]
    [InlineData("15:14:57", "15:14:57")]
    [InlineData("07.10.24", "07.10.24")]
    public void QwertyJcuken_PreservesPunctuation(string input, string expected)
    {
        // Punctuation must pass through unchanged — banks send digits/punctuation
        // in Cyrillic charset without switching to charset 1
        var result = NdcEncodings.Decode(input, "qwerty-jcuken");
        Assert.Equal(expected, result);
    }

    [Fact]
    public void NoneEncoding_ReturnsInputUnchanged()
    {
        Assert.Equal("hello", NdcEncodings.Decode("hello", "none"));
        Assert.Equal("hello", NdcEncodings.Decode("hello", "ascii"));
    }

    [Fact]
    public void UnknownEncoding_ReturnsInputUnchanged()
    {
        Assert.Equal("hello", NdcEncodings.Decode("hello", "unknown-encoding"));
    }

    [Fact]
    public void QwertyJcuken_BacktickMapsToHardSign()
    {
        // ` maps to э (U+044D) in the JCUKEN layout
        Assert.Equal("\u044d", NdcEncodings.Decode("`", "qwerty-jcuken"));
    }

    [Fact]
    public void QwertyJcuken_DelByteMapsToYu()
    {
        // DEL byte (0x7F) maps to ю (U+044E) in the JCUKEN layout
        Assert.Equal("\u044e", NdcEncodings.Decode("\x7f", "qwerty-jcuken"));
    }
}
