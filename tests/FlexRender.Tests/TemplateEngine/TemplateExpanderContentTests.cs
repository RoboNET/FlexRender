using FlexRender.Abstractions;
using FlexRender.Configuration;
using FlexRender.Parsing.Ast;
using FlexRender.TemplateEngine;
using Xunit;

namespace FlexRender.Tests.TemplateEngine;

public sealed class TemplateExpanderContentTests
{
    [Fact]
    public void Expand_ContentElement_ReplacesWithParsedSubtree()
    {
        var registry = new ContentParserRegistry();
        registry.Register(new StubContentParser("test", [
            new TextElement { Content = "Line 1" },
            new TextElement { Content = "Line 2" }
        ]));

        var expander = new TemplateExpander(new ResourceLimits(), contentParserRegistry: registry);

        var template = CreateTemplate(
            new ContentElement { Source = "some text", Format = "test" });

        var result = expander.Expand(template, new ObjectValue());

        Assert.Equal(2, result.Elements.Count);
        Assert.IsType<TextElement>(result.Elements[0]);
        Assert.IsType<TextElement>(result.Elements[1]);
        Assert.Equal("Line 1", ((TextElement)result.Elements[0]).Content.Value);
        Assert.Equal("Line 2", ((TextElement)result.Elements[1]).Content.Value);
    }

    [Fact]
    public void Expand_ContentElement_ResolvesSourceFromData()
    {
        var capturedText = "";
        var registry = new ContentParserRegistry();
        registry.Register(new CapturingContentParser("test", t =>
        {
            capturedText = t;
            return [new TextElement { Content = t }];
        }));

        var expander = new TemplateExpander(new ResourceLimits(), contentParserRegistry: registry);

        var template = CreateTemplate(
            new ContentElement { Source = "{{body}}", Format = "test" });

        var data = new ObjectValue { ["body"] = new StringValue("resolved body") };
        expander.Expand(template, data);

        Assert.Equal("resolved body", capturedText);
    }

    [Fact]
    public void Expand_ContentElement_ResolvesFormatFromData()
    {
        var registry = new ContentParserRegistry();
        registry.Register(new StubContentParser("markdown", [
            new TextElement { Content = "parsed" }
        ]));

        var expander = new TemplateExpander(new ResourceLimits(), contentParserRegistry: registry);

        var template = CreateTemplate(
            new ContentElement { Source = "text", Format = "{{fmt}}" });

        var data = new ObjectValue { ["fmt"] = new StringValue("markdown") };
        var result = expander.Expand(template, data);

        Assert.Single(result.Elements);
        Assert.Equal("parsed", ((TextElement)result.Elements[0]).Content.Value);
    }

    [Fact]
    public void Expand_ContentElement_UnknownFormat_ThrowsTemplateEngineException()
    {
        var registry = new ContentParserRegistry();
        var expander = new TemplateExpander(new ResourceLimits(), contentParserRegistry: registry);

        var template = CreateTemplate(
            new ContentElement { Source = "text", Format = "unknown" });

        Assert.Throws<TemplateEngineException>(() =>
            expander.Expand(template, new ObjectValue()));
    }

    [Fact]
    public void Expand_ContentElement_NoRegistry_ThrowsTemplateEngineException()
    {
        var expander = new TemplateExpander(new ResourceLimits());

        var template = CreateTemplate(
            new ContentElement { Source = "text", Format = "markdown" });

        Assert.Throws<TemplateEngineException>(() =>
            expander.Expand(template, new ObjectValue()));
    }

    [Fact]
    public void Expand_ContentElement_InsideFlex_PreservesStructure()
    {
        var registry = new ContentParserRegistry();
        registry.Register(new StubContentParser("test", [
            new TextElement { Content = "from content" }
        ]));

        var expander = new TemplateExpander(new ResourceLimits(), contentParserRegistry: registry);

        var flex = new FlexElement
        {
            Children =
            [
                new TextElement { Content = "before" },
                new ContentElement { Source = "text", Format = "test" },
                new TextElement { Content = "after" }
            ]
        };
        var template = CreateTemplate(flex);

        var result = expander.Expand(template, new ObjectValue());

        Assert.Single(result.Elements);
        var resultFlex = Assert.IsType<FlexElement>(result.Elements[0]);
        Assert.Equal(3, resultFlex.Children.Count);
        Assert.Equal("before", ((TextElement)resultFlex.Children[0]).Content.Value);
        Assert.Equal("from content", ((TextElement)resultFlex.Children[1]).Content.Value);
        Assert.Equal("after", ((TextElement)resultFlex.Children[2]).Content.Value);
    }

    private static Template CreateTemplate(params TemplateElement[] elements)
    {
        return new Template
        {
            Name = "test",
            Version = 1,
            Canvas = new CanvasSettings { Width = 200 },
            Elements = elements.ToList()
        };
    }

    private sealed class StubContentParser(string formatName, IReadOnlyList<TemplateElement> result) : IContentParser
    {
        public string FormatName => formatName;
        public IReadOnlyList<TemplateElement> Parse(string text) => result;
    }

    private sealed class CapturingContentParser(
        string formatName,
        Func<string, IReadOnlyList<TemplateElement>> parseFunc) : IContentParser
    {
        public string FormatName => formatName;
        public IReadOnlyList<TemplateElement> Parse(string text) => parseFunc(text);
    }
}
