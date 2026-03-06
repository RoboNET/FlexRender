using FlexRender.Abstractions;
using FlexRender.Configuration;
using FlexRender.Parsing.Ast;
using FlexRender.TemplateEngine;
using Xunit;

namespace FlexRender.Tests.Integration;

/// <summary>
/// Integration tests that validate ContentElement works through the full
/// TemplatePipeline (Expand -> Resolve -> Materialize).
/// </summary>
public sealed class ContentElementIntegrationTests
{
    [Fact]
    public async Task FullPipeline_ContentElement_ExpandsAndProcesses()
    {
        // Arrange: a parser that returns text elements
        var registry = new ContentParserRegistry();
        registry.Register(new SimpleTestParser());

        var expander = new TemplateExpander(new ResourceLimits(), contentParserRegistry: registry);
        var processor = new TemplateProcessor(new ResourceLimits());
        var pipeline = new TemplatePipeline(expander, processor);

        var template = new Template
        {
            Name = "test",
            Version = 1,
            Canvas = new CanvasSettings { Width = 300 },
            Elements =
            [
                new FlexElement
                {
                    Children =
                    [
                        new TextElement { Content = "Header" },
                        new ContentElement { Source = "{{receiptBody}}", Format = "simple" },
                        new TextElement { Content = "Footer" }
                    ]
                }
            ]
        };

        var data = new ObjectValue
        {
            ["receiptBody"] = new StringValue("Item 1|10.00\nItem 2|20.00")
        };

        // Act
        var result = await pipeline.ProcessAsync(template, data);

        // Assert
        var flex = Assert.IsType<FlexElement>(result.Elements[0]);
        Assert.Equal(4, flex.Children.Count); // Header + 2 items + Footer
        Assert.Equal("Header", ((TextElement)flex.Children[0]).Content.Value);
        Assert.Equal("Item 1: 10.00", ((TextElement)flex.Children[1]).Content.Value);
        Assert.Equal("Item 2: 20.00", ((TextElement)flex.Children[2]).Content.Value);
        Assert.Equal("Footer", ((TextElement)flex.Children[3]).Content.Value);
    }

    [Fact]
    public async Task FullPipeline_ContentElement_BytesValue_ExpandsViaBinaryParser()
    {
        // Arrange: register a binary parser
        var registry = new ContentParserRegistry();
        registry.RegisterBinary(new SimpleBinaryTestParser());

        var expander = new TemplateExpander(new ResourceLimits(), contentParserRegistry: registry);
        var processor = new TemplateProcessor(new ResourceLimits());
        var pipeline = new TemplatePipeline(expander, processor);

        var template = new Template
        {
            Name = "test-binary",
            Version = 1,
            Canvas = new CanvasSettings { Width = 300 },
            Elements =
            [
                new FlexElement
                {
                    Children =
                    [
                        new TextElement { Content = "Before" },
                        new ContentElement { Source = "{{binaryData}}", Format = "binary-simple" },
                        new TextElement { Content = "After" }
                    ]
                }
            ]
        };

        var data = new ObjectValue
        {
            ["binaryData"] = new BytesValue(new byte[] { 0x48, 0x45, 0x4C, 0x4C, 0x4F })
        };

        // Act
        var result = await pipeline.ProcessAsync(template, data);

        // Assert
        var flex = Assert.IsType<FlexElement>(result.Elements[0]);
        Assert.Equal(3, flex.Children.Count);
        Assert.Equal("Before", ((TextElement)flex.Children[0]).Content.Value);
        Assert.Equal("HELLO(5 bytes)", ((TextElement)flex.Children[1]).Content.Value);
        Assert.Equal("After", ((TextElement)flex.Children[2]).Content.Value);
    }

    [Fact]
    public async Task FullPipeline_ContentElement_Base64Source_ExpandsViaBinaryParser()
    {
        // Arrange: register a binary parser
        var registry = new ContentParserRegistry();
        registry.RegisterBinary(new SimpleBinaryTestParser());

        var expander = new TemplateExpander(new ResourceLimits(), contentParserRegistry: registry);
        var processor = new TemplateProcessor(new ResourceLimits());
        var pipeline = new TemplatePipeline(expander, processor);

        var template = new Template
        {
            Name = "test-base64",
            Version = 1,
            Canvas = new CanvasSettings { Width = 300 },
            Elements =
            [
                new FlexElement
                {
                    Children =
                    [
                        new TextElement { Content = "Before" },
                        new ContentElement { Source = "base64:SEVMTE8=", Format = "binary-simple" },
                        new TextElement { Content = "After" }
                    ]
                }
            ]
        };

        // Act — no data needed, source is inline base64
        var result = await pipeline.ProcessAsync(template, new ObjectValue());

        // Assert
        var flex = Assert.IsType<FlexElement>(result.Elements[0]);
        Assert.Equal(3, flex.Children.Count);
        Assert.Equal("Before", ((TextElement)flex.Children[0]).Content.Value);
        Assert.Equal("HELLO(5 bytes)", ((TextElement)flex.Children[1]).Content.Value);
        Assert.Equal("After", ((TextElement)flex.Children[2]).Content.Value);
    }

    /// <summary>
    /// Simple test parser: splits lines, each line is "name|price" -> TextElement "name: price".
    /// </summary>
    private sealed class SimpleTestParser : IContentParser
    {
        /// <inheritdoc />
        public string FormatName => "simple";

        /// <inheritdoc />
        public IReadOnlyList<TemplateElement> Parse(string text, ContentParserContext context, IReadOnlyDictionary<string, object>? options = null)
        {
            ArgumentNullException.ThrowIfNull(text);
            if (string.IsNullOrWhiteSpace(text)) return [];

            return text.Split('\n', StringSplitOptions.RemoveEmptyEntries)
                .Select(line =>
                {
                    var parts = line.Split('|');
                    var content = parts.Length == 2 ? $"{parts[0]}: {parts[1]}" : line;
                    return (TemplateElement)new TextElement { Content = content };
                })
                .ToList();
        }
    }

    /// <summary>
    /// Simple binary test parser: converts bytes to ASCII text with length annotation.
    /// </summary>
    private sealed class SimpleBinaryTestParser : IBinaryContentParser
    {
        /// <inheritdoc />
        public string FormatName => "binary-simple";

        /// <inheritdoc />
        public IReadOnlyList<TemplateElement> Parse(ReadOnlyMemory<byte> data, ContentParserContext context, IReadOnlyDictionary<string, object>? options = null)
        {
            var text = System.Text.Encoding.ASCII.GetString(data.Span);
            return [new TextElement { Content = $"{text}({data.Length} bytes)" }];
        }
    }
}
