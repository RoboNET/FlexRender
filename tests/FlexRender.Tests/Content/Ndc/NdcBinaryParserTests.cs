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
    [InlineData("unknown")]
    public void ResolveEncoding_ReturnsValidEncoding(string name)
    {
        var encoding = NdcContentParser.ResolveEncoding(name);

        Assert.NotNull(encoding);
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
    public void ResolveEncoding_Unknown_DefaultsToLatin1()
    {
        Assert.Same(global::System.Text.Encoding.Latin1, NdcContentParser.ResolveEncoding("something-else"));
    }
}
