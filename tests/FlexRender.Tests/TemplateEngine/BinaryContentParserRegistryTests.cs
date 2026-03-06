using FlexRender.Abstractions;
using FlexRender.Parsing.Ast;
using FlexRender.TemplateEngine;
using Xunit;

namespace FlexRender.Tests.TemplateEngine;

public sealed class BinaryContentParserRegistryTests
{
    [Fact]
    public void GetBinaryParser_RegisteredParser_ReturnsParser()
    {
        var registry = new ContentParserRegistry();
        var parser = new StubBinaryContentParser("ndc");
        registry.RegisterBinary(parser);

        var resolved = registry.GetBinaryParser("ndc");

        Assert.NotNull(resolved);
        Assert.Same(parser, resolved);
    }

    [Fact]
    public void GetBinaryParser_CaseInsensitive_ReturnsParser()
    {
        var registry = new ContentParserRegistry();
        registry.RegisterBinary(new StubBinaryContentParser("Ndc"));

        var resolved = registry.GetBinaryParser("NDC");

        Assert.NotNull(resolved);
    }

    [Fact]
    public void GetBinaryParser_UnknownFormat_ReturnsNull()
    {
        var registry = new ContentParserRegistry();

        var resolved = registry.GetBinaryParser("unknown");

        Assert.Null(resolved);
    }

    [Fact]
    public void RegisterBinary_DuplicateFormat_ThrowsInvalidOperation()
    {
        var registry = new ContentParserRegistry();
        registry.RegisterBinary(new StubBinaryContentParser("ndc"));

        Assert.Throws<InvalidOperationException>(() =>
            registry.RegisterBinary(new StubBinaryContentParser("ndc")));
    }

    [Fact]
    public void RegisterBinary_NullParser_ThrowsArgumentNull()
    {
        var registry = new ContentParserRegistry();

        Assert.Throws<ArgumentNullException>(() => registry.RegisterBinary(null!));
    }

    [Fact]
    public void RegisterBinary_DoesNotAffectStringParsers()
    {
        var registry = new ContentParserRegistry();
        registry.RegisterBinary(new StubBinaryContentParser("ndc"));

        var resolved = registry.GetParser("ndc");

        Assert.Null(resolved);
    }

    private sealed class StubBinaryContentParser(string formatName) : IBinaryContentParser
    {
        public string FormatName => formatName;
        public IReadOnlyList<TemplateElement> Parse(ReadOnlyMemory<byte> data, ContentParserContext context, IReadOnlyDictionary<string, object>? options = null) => [];
    }
}
