using FlexRender.Abstractions;
using FlexRender.Configuration;
using FlexRender.Parsing.Ast;
using FlexRender.TemplateEngine;
using Xunit;

namespace FlexRender.Tests.TemplateEngine;

public sealed class TemplateExpanderContentTests
{
    [Fact]
    public async Task Expand_ContentElement_ReplacesWithParsedSubtree()
    {
        var registry = new ContentParserRegistry();
        registry.Register(new StubContentParser("test", [
            new TextElement { Content = "Line 1" },
            new TextElement { Content = "Line 2" }
        ]));

        var expander = new TemplateExpander(new ResourceLimits(), contentParserRegistry: registry);

        var template = CreateTemplate(
            new ContentElement { Source = "some text", Format = "test" });

        var result = await expander.ExpandAsync(template, new ObjectValue());

        Assert.Equal(2, result.Elements.Count);
        Assert.IsType<TextElement>(result.Elements[0]);
        Assert.IsType<TextElement>(result.Elements[1]);
        Assert.Equal("Line 1", ((TextElement)result.Elements[0]).Content.Value);
        Assert.Equal("Line 2", ((TextElement)result.Elements[1]).Content.Value);
    }

    [Fact]
    public async Task Expand_ContentElement_ResolvesSourceFromData()
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
        await expander.ExpandAsync(template, data);

        Assert.Equal("resolved body", capturedText);
    }

    [Fact]
    public async Task Expand_ContentElement_ResolvesFormatFromData()
    {
        var registry = new ContentParserRegistry();
        registry.Register(new StubContentParser("markdown", [
            new TextElement { Content = "parsed" }
        ]));

        var expander = new TemplateExpander(new ResourceLimits(), contentParserRegistry: registry);

        var template = CreateTemplate(
            new ContentElement { Source = "text", Format = "{{fmt}}" });

        var data = new ObjectValue { ["fmt"] = new StringValue("markdown") };
        var result = await expander.ExpandAsync(template, data);

        Assert.Single(result.Elements);
        Assert.Equal("parsed", ((TextElement)result.Elements[0]).Content.Value);
    }

    [Fact]
    public async Task Expand_ContentElement_UnknownFormat_ThrowsTemplateEngineException()
    {
        var registry = new ContentParserRegistry();
        var expander = new TemplateExpander(new ResourceLimits(), contentParserRegistry: registry);

        var template = CreateTemplate(
            new ContentElement { Source = "text", Format = "unknown" });

        await Assert.ThrowsAsync<TemplateEngineException>(async () =>
            await expander.ExpandAsync(template, new ObjectValue()));
    }

    [Fact]
    public async Task Expand_ContentElement_NoRegistry_ThrowsTemplateEngineException()
    {
        var expander = new TemplateExpander(new ResourceLimits());

        var template = CreateTemplate(
            new ContentElement { Source = "text", Format = "markdown" });

        await Assert.ThrowsAsync<TemplateEngineException>(async () =>
            await expander.ExpandAsync(template, new ObjectValue()));
    }

    [Fact]
    public async Task Expand_ContentElement_InsideFlex_PreservesStructure()
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

        var result = await expander.ExpandAsync(template, new ObjectValue());

        Assert.Single(result.Elements);
        var resultFlex = Assert.IsType<FlexElement>(result.Elements[0]);
        Assert.Equal(3, resultFlex.Children.Count);
        Assert.Equal("before", ((TextElement)resultFlex.Children[0]).Content.Value);
        Assert.Equal("from content", ((TextElement)resultFlex.Children[1]).Content.Value);
        Assert.Equal("after", ((TextElement)resultFlex.Children[2]).Content.Value);
    }

    [Fact]
    public async Task Expand_ContentElement_PassesCanvasViaContext()
    {
        var parser = new OptionsCapturingContentParser("test");
        var registry = new ContentParserRegistry();
        registry.Register(parser);
        var expander = new TemplateExpander(new ResourceLimits(), contentParserRegistry: registry);

        var template = new Template
        {
            Canvas = new CanvasSettings { Width = 576 },
            Elements = [new ContentElement
            {
                Source = "test data",
                Format = "test",
                Options = new Dictionary<string, object> { ["columns"] = "40" }
            }]
        };

        await expander.ExpandAsync(template, new ObjectValue());

        Assert.NotNull(parser.LastContext);
        Assert.NotNull(parser.LastContext!.Canvas);
        Assert.Equal(576, parser.LastContext.Canvas!.Width);
        // Original options preserved without _canvas_width injection
        Assert.NotNull(parser.LastOptions);
        Assert.Equal("40", parser.LastOptions!["columns"]);
    }

    [Fact]
    public async Task Expand_ContentElement_PassesTemplateViaContext()
    {
        var parser = new OptionsCapturingContentParser("test");
        var registry = new ContentParserRegistry();
        registry.Register(parser);
        var expander = new TemplateExpander(new ResourceLimits(), contentParserRegistry: registry);

        var template = new Template
        {
            Canvas = new CanvasSettings { Width = 400 },
            Elements = [new ContentElement
            {
                Source = "test data",
                Format = "test"
                // Options is null
            }]
        };

        await expander.ExpandAsync(template, new ObjectValue());

        Assert.NotNull(parser.LastContext);
        Assert.Same(template, parser.LastContext!.Template);
        Assert.NotNull(parser.LastContext.Canvas);
        Assert.Equal(400, parser.LastContext.Canvas!.Width);
        // Options should be null when not specified
        Assert.Null(parser.LastOptions);
    }

    [Fact]
    public async Task Expand_ContentElement_InsideFlexWithWidth_PassesParentWidth()
    {
        var parser = new OptionsCapturingContentParser("test");
        var registry = new ContentParserRegistry();
        registry.Register(parser);
        var expander = new TemplateExpander(new ResourceLimits(), contentParserRegistry: registry);

        var template = new Template
        {
            Canvas = new CanvasSettings { Width = 576 },
            Elements =
            [
                new FlexElement
                {
                    Width = "400",
                    Children =
                    [
                        new ContentElement { Source = "data", Format = "test" }
                    ]
                }
            ]
        };

        await expander.ExpandAsync(template, new ObjectValue());

        Assert.NotNull(parser.LastContext);
        Assert.Equal(400, parser.LastContext!.ParentWidth);
        Assert.Equal(576, parser.LastContext.Canvas!.Width);
    }

    [Fact]
    public async Task Expand_ContentElement_InsideFlexWithoutWidth_ParentWidthIsNull()
    {
        var parser = new OptionsCapturingContentParser("test");
        var registry = new ContentParserRegistry();
        registry.Register(parser);
        var expander = new TemplateExpander(new ResourceLimits(), contentParserRegistry: registry);

        var template = new Template
        {
            Canvas = new CanvasSettings { Width = 576 },
            Elements =
            [
                new FlexElement
                {
                    Children =
                    [
                        new ContentElement { Source = "data", Format = "test" }
                    ]
                }
            ]
        };

        await expander.ExpandAsync(template, new ObjectValue());

        Assert.NotNull(parser.LastContext);
        Assert.Null(parser.LastContext!.ParentWidth);
        Assert.Equal(576, parser.LastContext.Canvas!.Width);
    }

    [Fact]
    public async Task Expand_ContentElement_InsideFlexWithPercentWidth_ParentWidthIsNull()
    {
        var parser = new OptionsCapturingContentParser("test");
        var registry = new ContentParserRegistry();
        registry.Register(parser);
        var expander = new TemplateExpander(new ResourceLimits(), contentParserRegistry: registry);

        var template = new Template
        {
            Canvas = new CanvasSettings { Width = 576 },
            Elements =
            [
                new FlexElement
                {
                    Width = "50%",
                    Children =
                    [
                        new ContentElement { Source = "data", Format = "test" }
                    ]
                }
            ]
        };

        await expander.ExpandAsync(template, new ObjectValue());

        Assert.NotNull(parser.LastContext);
        Assert.Null(parser.LastContext!.ParentWidth);
    }

    [Fact]
    public async Task Expand_ContentElement_TopLevel_ParentWidthIsNull()
    {
        var parser = new OptionsCapturingContentParser("test");
        var registry = new ContentParserRegistry();
        registry.Register(parser);
        var expander = new TemplateExpander(new ResourceLimits(), contentParserRegistry: registry);

        var template = new Template
        {
            Canvas = new CanvasSettings { Width = 576 },
            Elements = [new ContentElement { Source = "data", Format = "test" }]
        };

        await expander.ExpandAsync(template, new ObjectValue());

        Assert.NotNull(parser.LastContext);
        Assert.Null(parser.LastContext!.ParentWidth);
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
        public IReadOnlyList<TemplateElement> Parse(string text, ContentParserContext context, IReadOnlyDictionary<string, object>? options = null) => result;
    }

    private sealed class CapturingContentParser(
        string formatName,
        Func<string, IReadOnlyList<TemplateElement>> parseFunc) : IContentParser
    {
        public string FormatName => formatName;
        public IReadOnlyList<TemplateElement> Parse(string text, ContentParserContext context, IReadOnlyDictionary<string, object>? options = null) => parseFunc(text);
    }

    private sealed class OptionsCapturingContentParser(string formatName) : IContentParser
    {
        public string FormatName => formatName;
        public ContentParserContext? LastContext { get; private set; }
        public IReadOnlyDictionary<string, object>? LastOptions { get; private set; }

        public IReadOnlyList<TemplateElement> Parse(string text, ContentParserContext context, IReadOnlyDictionary<string, object>? options = null)
        {
            LastContext = context;
            LastOptions = options;
            return [];
        }
    }
}
