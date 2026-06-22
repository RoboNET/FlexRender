using FlexRender.Abstractions;
using FlexRender.Configuration;
using FlexRender.Content.Ndc;
using FlexRender.Parsing.Ast;
using Xunit;

namespace FlexRender.Tests.Content.Ndc;

public sealed class NdcBinaryParserTests
{
    private static readonly ContentParserContext EmptyContext = new();

    [Fact]
    public void NdcContentParser_ImplementsIBinaryContentParser()
    {
        var parser = new NdcContentParser();

        Assert.IsAssignableFrom<IBinaryContentParser>(parser);
        Assert.IsAssignableFrom<IContentParser>(parser);
    }

    [Fact]
    public void ParseBytes_EmptyData_ReturnsEmpty()
    {
        var parser = new NdcContentParser();

        var result = parser.Parse(ReadOnlyMemory<byte>.Empty, EmptyContext);

        Assert.Empty(result);
    }

    [Fact]
    public void ParseBytes_Latin1Data_ParsesCorrectly()
    {
        var parser = new NdcContentParser();
        var text = "Hello World";
        var data = global::System.Text.Encoding.Latin1.GetBytes(text);

        var result = parser.Parse(data, EmptyContext);

        var root = Assert.IsType<FlexElement>(Assert.Single(result));
        Assert.True(root.Children.Count > 0);
    }

    [Fact]
    public void ParseBytes_WithInputEncoding_UsesSpecifiedEncoding()
    {
        var parser = new NdcContentParser();
        // Use a UTF-8 string with a non-ASCII character
        var text = "Caf\u00e9";
        var data = global::System.Text.Encoding.UTF8.GetBytes(text);
        var options = new Dictionary<string, object>
        {
            ["input_encoding"] = "utf-8"
        };

        var result = parser.Parse(data, EmptyContext, options);

        var root = Assert.IsType<FlexElement>(Assert.Single(result));
        var row = Assert.IsType<FlexElement>(root.Children[0]);
        var textElement = Assert.IsType<TextElement>(row.Children[0]);
        Assert.Equal("Caf\u00e9", textElement.Content);
    }

    [Fact]
    public void ParseBytes_RealBankAData_ProducesNonEmptyAst()
    {
        var parser = new NdcContentParser();
        var data = File.ReadAllBytes("Content/Ndc/TestData/bank-a-mini-statement.bin");

        var result = parser.Parse(data, EmptyContext);

        Assert.NotEmpty(result);
        var root = Assert.IsType<FlexElement>(Assert.Single(result));
        Assert.True(root.Children.Count > 0);
    }

    [Fact]
    public void WithNdc_RegistersBothStringAndBinaryParser()
    {
        var builder = new FlexRenderBuilder();

        builder.WithNdc();

        Assert.NotNull(builder.ContentParserRegistry);
        Assert.NotNull(builder.ContentParserRegistry!.GetParser("ndc"));
        Assert.NotNull(builder.ContentParserRegistry!.GetBinaryParser("ndc"));
    }

    [Theory]
    [InlineData("latin1")]
    [InlineData("iso-8859-1")]
    [InlineData("utf-8")]
    [InlineData("utf8")]
    [InlineData("ascii")]
    [InlineData("iso-8859-5")]
    [InlineData("28595")]
    public void ResolveEncoding_ReturnsValidEncoding(string name)
    {
        var encoding = NdcContentParser.ResolveEncoding(name);

        Assert.NotNull(encoding);
    }

    [Theory]
    [InlineData("iso-8859-5")]
    [InlineData("28595")]
    public void ResolveEncoding_Iso88595_ByNameOrCodePage_ResolvesToCodePage28595(string name)
    {
        var encoding = NdcContentParser.ResolveEncoding(name);

        Assert.Equal(28595, encoding.CodePage);
    }

    [Fact]
    public void ResolveEncoding_Latin1_ReturnsLatin1()
    {
        Assert.Same(global::System.Text.Encoding.Latin1, NdcContentParser.ResolveEncoding("latin1"));
        Assert.Same(global::System.Text.Encoding.Latin1, NdcContentParser.ResolveEncoding("iso-8859-1"));
    }

    [Fact]
    public void ResolveEncoding_Utf8_ReturnsUtf8()
    {
        Assert.Same(global::System.Text.Encoding.UTF8, NdcContentParser.ResolveEncoding("utf-8"));
        Assert.Same(global::System.Text.Encoding.UTF8, NdcContentParser.ResolveEncoding("utf8"));
    }

    [Fact]
    public void ResolveEncoding_Ascii_ReturnsAscii()
    {
        Assert.Same(global::System.Text.Encoding.ASCII, NdcContentParser.ResolveEncoding("ascii"));
    }

    [Fact]
    public void ResolveEncoding_Unknown_Throws()
    {
        var ex = Assert.Throws<NotSupportedException>(
            () => NdcContentParser.ResolveEncoding("something-else"));

        Assert.Contains("something-else", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ResolveEncoding_UnknownCodePage_Throws()
    {
        var ex = Assert.Throws<NotSupportedException>(
            () => NdcContentParser.ResolveEncoding("999999"));

        Assert.Contains("999999", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ParseBytes_WithIso88595Encoding_DecodesCyrillic()
    {
        var parser = new NdcContentParser();
        // Cyrillic text encoded with ISO-8859-5 (code page 28595). The default
        // NDC charset uses "none" encoding, so the decoded Unicode survives unchanged.
        // GetEncoding(28595) succeeds here because the parser's static constructor
        // already registered the code-pages provider.
        var text = "Привет";
        var iso88595 = global::System.Text.Encoding.GetEncoding(28595);
        var data = iso88595.GetBytes(text);
        var options = new Dictionary<string, object>
        {
            ["input_encoding"] = "iso-8859-5"
        };

        var result = parser.Parse(data, EmptyContext, options);

        var root = Assert.IsType<FlexElement>(Assert.Single(result));
        var row = Assert.IsType<FlexElement>(root.Children[0]);
        var textElement = Assert.IsType<TextElement>(row.Children[0]);
        Assert.Equal("Привет", textElement.Content);
    }

    [Fact]
    public void ParseBytes_WithNumericInputEncoding_DecodesCyrillic()
    {
        var parser = new NdcContentParser();
        // input_encoding arrives as a boxed int (as YAML numeric scalars do), not a string.
        // The binary path must coerce it to its string form so the numeric code page resolves.
        var text = "Привет";
        var iso88595 = global::System.Text.Encoding.GetEncoding(28595);
        var data = iso88595.GetBytes(text);
        var options = new Dictionary<string, object>
        {
            ["input_encoding"] = 28595
        };

        var result = parser.Parse(data, EmptyContext, options);

        var root = Assert.IsType<FlexElement>(Assert.Single(result));
        var row = Assert.IsType<FlexElement>(root.Children[0]);
        var textElement = Assert.IsType<TextElement>(row.Children[0]);
        Assert.Equal("Привет", textElement.Content);
    }
}
