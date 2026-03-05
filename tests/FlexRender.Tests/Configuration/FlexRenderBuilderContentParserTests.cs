using FlexRender.Abstractions;
using FlexRender.Configuration;
using FlexRender.Parsing.Ast;
using Xunit;

namespace FlexRender.Tests.Configuration;

public sealed class FlexRenderBuilderContentParserTests
{
    [Fact]
    public void WithContentParser_RegistersParser()
    {
        var builder = new FlexRenderBuilder();

        builder.WithContentParser(new StubContentParser("markdown"));

        Assert.NotNull(builder.ContentParserRegistry);
        Assert.NotNull(builder.ContentParserRegistry!.GetParser("markdown"));
    }

    [Fact]
    public void WithContentParser_MultipleParsers_RegistersAll()
    {
        var builder = new FlexRenderBuilder();

        builder.WithContentParser(new StubContentParser("markdown"));
        builder.WithContentParser(new StubContentParser("escpos"));

        Assert.NotNull(builder.ContentParserRegistry!.GetParser("markdown"));
        Assert.NotNull(builder.ContentParserRegistry!.GetParser("escpos"));
    }

    [Fact]
    public void WithContentParser_Null_ThrowsArgumentNull()
    {
        var builder = new FlexRenderBuilder();

        Assert.Throws<ArgumentNullException>(() => builder.WithContentParser(null!));
    }

    [Fact]
    public void ContentParserRegistry_DefaultsToNull()
    {
        var builder = new FlexRenderBuilder();

        Assert.Null(builder.ContentParserRegistry);
    }

    private sealed class StubContentParser(string formatName) : IContentParser
    {
        public string FormatName => formatName;
        public IReadOnlyList<TemplateElement> Parse(string text) => [];
    }
}
