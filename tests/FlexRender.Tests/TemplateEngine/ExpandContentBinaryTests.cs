using FlexRender.Abstractions;
using FlexRender.Configuration;
using FlexRender.Loaders;
using FlexRender.Parsing.Ast;
using FlexRender.TemplateEngine;
using Xunit;

namespace FlexRender.Tests.TemplateEngine;

public sealed class ExpandContentBinaryTests
{
    [Fact]
    public async Task ExpandContent_BytesValue_DispatchesToBinaryParser()
    {
        var binaryParser = new CapturingBinaryParser("ndc");
        var registry = new ContentParserRegistry();
        registry.RegisterBinary(binaryParser);

        var expander = new TemplateExpander(new ResourceLimits(), contentParserRegistry: registry);

        var rawBytes = new byte[] { 0x1B, 0x40, 0x48, 0x65, 0x6C, 0x6C, 0x6F };
        var template = CreateTemplate(
            new ContentElement { Source = "{{payload}}", Format = "ndc" });

        var data = new ObjectValue { ["payload"] = new BytesValue(rawBytes) };
        var result = await expander.ExpandAsync(template, data);

        Assert.NotNull(binaryParser.LastData);
        Assert.Equal(rawBytes, binaryParser.LastData);
    }

    [Fact]
    public async Task ExpandContent_DataUriString_DecodesAndDispatchesToBinaryParser()
    {
        var binaryParser = new CapturingBinaryParser("ndc");
        var registry = new ContentParserRegistry();
        registry.RegisterBinary(binaryParser);

        var options = new FlexRenderOptions();
        IReadOnlyList<IResourceLoader> loaders = [new Base64ResourceLoader(options)];
        var expander = new TemplateExpander(new ResourceLimits(), contentParserRegistry: registry, resourceLoaders: loaders);

        var rawBytes = new byte[] { 0x1B, 0x40, 0x48 };
        var base64 = Convert.ToBase64String(rawBytes);
        var template = CreateTemplate(
            new ContentElement { Source = $"data:application/octet-stream;base64,{base64}", Format = "ndc" });

        var result = await expander.ExpandAsync(template, new ObjectValue());

        Assert.NotNull(binaryParser.LastData);
        Assert.Equal(rawBytes, binaryParser.LastData);
    }

    [Fact]
    public async Task ExpandContent_DataUriVariable_DecodesAndDispatchesToBinaryParser()
    {
        var binaryParser = new CapturingBinaryParser("ndc");
        var registry = new ContentParserRegistry();
        registry.RegisterBinary(binaryParser);

        var options = new FlexRenderOptions();
        IReadOnlyList<IResourceLoader> loaders = [new Base64ResourceLoader(options)];
        var expander = new TemplateExpander(new ResourceLimits(), contentParserRegistry: registry, resourceLoaders: loaders);

        var rawBytes = new byte[] { 0xDE, 0xAD, 0xBE, 0xEF };
        var base64 = Convert.ToBase64String(rawBytes);
        var template = CreateTemplate(
            new ContentElement { Source = "{{data}}", Format = "ndc" });

        var data = new ObjectValue { ["data"] = new StringValue($"data:application/octet-stream;base64,{base64}") };
        var result = await expander.ExpandAsync(template, data);

        Assert.NotNull(binaryParser.LastData);
        Assert.Equal(rawBytes, binaryParser.LastData);
    }

    [Fact]
    public async Task ExpandContent_StringSource_StillUsesStringParser()
    {
        var stringParser = new CapturingStringParser("markdown");
        var registry = new ContentParserRegistry();
        registry.Register(stringParser);

        var expander = new TemplateExpander(new ResourceLimits(), contentParserRegistry: registry);

        var template = CreateTemplate(
            new ContentElement { Source = "# Hello World", Format = "markdown" });

        var result = await expander.ExpandAsync(template, new ObjectValue());

        Assert.Equal("# Hello World", stringParser.LastText);
    }

    [Fact]
    public async Task ExpandContent_BytesValue_NoBinaryParser_ThrowsTemplateEngineException()
    {
        var registry = new ContentParserRegistry();
        // Register only a string parser, no binary parser
        registry.Register(new CapturingStringParser("ndc"));

        var expander = new TemplateExpander(new ResourceLimits(), contentParserRegistry: registry);

        var template = CreateTemplate(
            new ContentElement { Source = "{{payload}}", Format = "ndc" });

        var data = new ObjectValue { ["payload"] = new BytesValue([0x1B, 0x40]) };

        var ex = await Assert.ThrowsAsync<TemplateEngineException>(async () =>
            await expander.ExpandAsync(template, data));

        Assert.Contains("binary content parser", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ExpandContent_BinaryParser_ReceivesOptions()
    {
        var binaryParser = new CapturingBinaryParser("ndc");
        var registry = new ContentParserRegistry();
        registry.RegisterBinary(binaryParser);

        var expander = new TemplateExpander(new ResourceLimits(), contentParserRegistry: registry);

        var template = new Template
        {
            Canvas = new CanvasSettings { Width = 576 },
            Elements =
            [
                new ContentElement
                {
                    Source = "{{payload}}",
                    Format = "ndc",
                    Options = new Dictionary<string, object> { ["columns"] = "40" }
                }
            ]
        };

        var data = new ObjectValue { ["payload"] = new BytesValue([0x1B, 0x40]) };
        await expander.ExpandAsync(template, data);

        Assert.NotNull(binaryParser.LastContext);
        Assert.NotNull(binaryParser.LastContext!.Canvas);
        Assert.Equal(576, binaryParser.LastContext.Canvas!.Width);
        Assert.NotNull(binaryParser.LastOptions);
        Assert.Equal("40", binaryParser.LastOptions!["columns"]);
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

    private sealed class CapturingBinaryParser(string formatName) : IBinaryContentParser
    {
        public string FormatName => formatName;
        public byte[]? LastData { get; private set; }
        public IReadOnlyDictionary<string, object>? LastOptions { get; private set; }
        public ContentParserContext? LastContext { get; private set; }

        public IReadOnlyList<TemplateElement> Parse(ReadOnlyMemory<byte> data, ContentParserContext context, IReadOnlyDictionary<string, object>? options = null)
        {
            LastData = data.ToArray();
            LastOptions = options;
            LastContext = context;
            return [new TextElement { Content = "binary-parsed" }];
        }
    }

    private sealed class CapturingStringParser(string formatName) : IContentParser
    {
        public string FormatName => formatName;
        public string? LastText { get; private set; }

        public IReadOnlyList<TemplateElement> Parse(string text, ContentParserContext context, IReadOnlyDictionary<string, object>? options = null)
        {
            LastText = text;
            return [new TextElement { Content = text }];
        }
    }
}
