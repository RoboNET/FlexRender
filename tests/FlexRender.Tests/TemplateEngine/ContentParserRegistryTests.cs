using FlexRender.Abstractions;
using FlexRender.Parsing.Ast;
using FlexRender.TemplateEngine;
using Xunit;

namespace FlexRender.Tests.TemplateEngine;

public sealed class ContentParserRegistryTests
{
    [Fact]
    public void Register_AndResolve_ReturnsParser()
    {
        var registry = new ContentParserRegistry();
        var parser = new StubContentParser("markdown");
        registry.Register(parser);

        var resolved = registry.GetParser("markdown");

        Assert.NotNull(resolved);
        Assert.Same(parser, resolved);
    }

    [Fact]
    public void GetParser_CaseInsensitive_ReturnsParser()
    {
        var registry = new ContentParserRegistry();
        registry.Register(new StubContentParser("Markdown"));

        var resolved = registry.GetParser("MARKDOWN");

        Assert.NotNull(resolved);
    }

    [Fact]
    public void GetParser_UnknownFormat_ReturnsNull()
    {
        var registry = new ContentParserRegistry();

        var resolved = registry.GetParser("unknown");

        Assert.Null(resolved);
    }

    [Fact]
    public void Register_DuplicateFormat_ThrowsInvalidOperation()
    {
        var registry = new ContentParserRegistry();
        registry.Register(new StubContentParser("markdown"));

        Assert.Throws<InvalidOperationException>(() =>
            registry.Register(new StubContentParser("markdown")));
    }

    [Fact]
    public void Register_NullParser_ThrowsArgumentNull()
    {
        var registry = new ContentParserRegistry();

        Assert.Throws<ArgumentNullException>(() => registry.Register(null!));
    }

    private sealed class StubContentParser(string formatName) : IContentParser
    {
        public string FormatName => formatName;
        public IReadOnlyList<TemplateElement> Parse(string text) => [];
    }
}
