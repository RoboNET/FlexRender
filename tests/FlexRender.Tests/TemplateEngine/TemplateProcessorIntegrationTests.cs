using System.Text;
using FlexRender.Parsing;
using FlexRender.Parsing.Ast;
using FlexRender.TemplateEngine;
using Xunit;

namespace FlexRender.Tests.TemplateEngine;

public class TemplateProcessorIntegrationTests
{
    private readonly TemplateParser _parser = new();
    private readonly TemplateProcessor _processor = new();

    // Security Tests: Maximum Nesting Depth
    [Fact]
    public void Process_ExceedsMaxNestingDepth_ThrowsTemplateEngineException()
    {
        // Create a template with 101 nested if blocks (exceeds limit of 100)
        var sb = new StringBuilder();
        for (int i = 0; i < 101; i++)
        {
            sb.Append("{{#if show}}");
        }
        sb.Append("content");
        for (int i = 0; i < 101; i++)
        {
            sb.Append("{{/if}}");
        }

        var data = new ObjectValue { ["show"] = true };

        var ex = Assert.Throws<TemplateEngineException>(() => _processor.Process(sb.ToString(), data));
        Assert.Contains("nesting depth", ex.Message.ToLowerInvariant());
    }

    [Fact]
    public void Process_AtMaxNestingDepth_Succeeds()
    {
        // Create a template with exactly 100 nested if blocks (at the limit)
        var sb = new StringBuilder();
        for (int i = 0; i < 100; i++)
        {
            sb.Append("{{#if show}}");
        }
        sb.Append("content");
        for (int i = 0; i < 100; i++)
        {
            sb.Append("{{/if}}");
        }

        var data = new ObjectValue { ["show"] = true };

        var result = _processor.Process(sb.ToString(), data);
        Assert.Equal("content", result);
    }

    // Security Tests: Unclosed Block Detection
    [Fact]
    public void Process_UnclosedIfBlock_ThrowsTemplateEngineException()
    {
        var template = "{{#if show}}content";
        var data = new ObjectValue { ["show"] = true };

        var ex = Assert.Throws<TemplateEngineException>(() => _processor.Process(template, data));
        Assert.Contains("unclosed", ex.Message.ToLowerInvariant());
    }

    [Fact]
    public void Process_UnclosedEachBlock_ThrowsTemplateEngineException()
    {
        var template = "{{#each items}}content";
        var items = new ArrayValue(new TemplateValue[] { new StringValue("item") });
        var data = new ObjectValue { ["items"] = items };

        var ex = Assert.Throws<TemplateEngineException>(() => _processor.Process(template, data));
        Assert.Contains("unclosed", ex.Message.ToLowerInvariant());
    }

    [Fact]
    public void Process_UnclosedNestedBlocks_ThrowsTemplateEngineException()
    {
        var template = "{{#if show}}{{#each items}}content{{/each}}";
        var items = new ArrayValue(new TemplateValue[] { new StringValue("item") });
        var data = new ObjectValue { ["show"] = true, ["items"] = items };

        var ex = Assert.Throws<TemplateEngineException>(() => _processor.Process(template, data));
        Assert.Contains("unclosed", ex.Message.ToLowerInvariant());
    }

    [Fact]
    public void ProcessTemplate_TextElement_SubstitutesVariables()
    {
        const string yaml = """
            canvas:
              width: 300
            layout:
              - type: text
                content: "Hello {{name}}!"
            """;

        var template = _parser.Parse(yaml);
        var data = new ObjectValue { ["name"] = "John" };

        var resolved = _processor.ProcessTemplate(template, data);

        var text = Assert.IsType<TextElement>(resolved.Elements[0]);
        Assert.Equal("Hello John!", text.Content);
    }

    [Fact]
    public void ProcessTemplate_MultipleElements_SubstitutesAll()
    {
        const string yaml = """
            canvas:
              width: 300
            layout:
              - type: text
                content: "Name: {{name}}"
              - type: text
                content: "Total: {{total}}"
            """;

        var template = _parser.Parse(yaml);
        var data = new ObjectValue { ["name"] = "Shop", ["total"] = 1500 };

        var resolved = _processor.ProcessTemplate(template, data);

        Assert.Equal(2, resolved.Elements.Count);
        Assert.Equal("Name: Shop", ((TextElement)resolved.Elements[0]).Content);
        Assert.Equal("Total: 1500", ((TextElement)resolved.Elements[1]).Content);
    }

    [Fact]
    public void ProcessTemplate_NoExpressions_ReturnsUnchanged()
    {
        const string yaml = """
            canvas:
              width: 300
            layout:
              - type: text
                content: "Plain text"
            """;

        var template = _parser.Parse(yaml);
        var data = new ObjectValue();

        var resolved = _processor.ProcessTemplate(template, data);

        var text = Assert.IsType<TextElement>(resolved.Elements[0]);
        Assert.Equal("Plain text", text.Content);
    }

    [Fact]
    public void ProcessTemplate_PreservesOtherProperties()
    {
        const string yaml = """
            canvas:
              width: 300
            layout:
              - type: text
                content: "{{message}}"
                font: bold
                size: 2em
                color: "#ff0000"
                align: center
            """;

        var template = _parser.Parse(yaml);
        var data = new ObjectValue { ["message"] = "Hello" };

        var resolved = _processor.ProcessTemplate(template, data);

        var text = Assert.IsType<TextElement>(resolved.Elements[0]);
        Assert.Equal("Hello", text.Content);
        Assert.Equal("bold", text.Font);
        Assert.Equal("2em", text.Size);
        Assert.Equal("#ff0000", text.Color);
        Assert.Equal(TextAlign.Center, text.Align);
    }

    [Fact]
    public void ProcessTemplate_PreservesCanvasSettings()
    {
        const string yaml = """
            canvas:
              fixed: height
              height: 500
              background: "#f0f0f0"
            layout:
              - type: text
                content: "{{text}}"
            """;

        var template = _parser.Parse(yaml);
        var data = new ObjectValue { ["text"] = "Test" };

        var resolved = _processor.ProcessTemplate(template, data);

        Assert.Equal(FixedDimension.Height, resolved.Canvas.Fixed);
        Assert.Equal(500, resolved.Canvas.Height);
        Assert.Equal("#f0f0f0", resolved.Canvas.Background);
    }

    [Fact]
    public void ProcessTemplate_PreservesTemplateMetadata()
    {
        const string yaml = """
            template:
              name: "receipt"
              version: 2
            canvas:
              width: 300
            layout:
              - type: text
                content: "{{text}}"
            """;

        var template = _parser.Parse(yaml);
        var data = new ObjectValue { ["text"] = "Test" };

        var resolved = _processor.ProcessTemplate(template, data);

        Assert.Equal("receipt", resolved.Name);
        Assert.Equal(2, resolved.Version);
    }
}
