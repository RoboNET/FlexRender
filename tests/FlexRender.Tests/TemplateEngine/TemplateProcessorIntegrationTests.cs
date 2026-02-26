using System.Text;
using FlexRender.Configuration;
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
        Assert.Equal("#f0f0f0", resolved.Canvas.Background.Value);
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

    /// <summary>
    /// Verifies that {{#if}} conditions support null coalescing operator.
    /// </summary>
    [Fact]
    public void ProcessIfBlock_NullCoalesceInCondition_EvaluatesExpression()
    {
        var processor = new TemplateProcessor(new ResourceLimits(), FilterRegistry.CreateDefault());
        var data = new ObjectValue
        {
            ["nickname"] = new StringValue("Bob")
        };

        // "name" is missing, "nickname" exists -> coalesces to "Bob" -> truthy
        var result = processor.Process("{{#if name ?? nickname}}Hello {{name ?? nickname}}{{/if}}", data);

        Assert.Equal("Hello Bob", result);
    }

    /// <summary>
    /// Verifies that {{#if}} with null coalescing hides block when both values are missing.
    /// </summary>
    [Fact]
    public void ProcessIfBlock_NullCoalesceBothNull_HidesBlock()
    {
        var processor = new TemplateProcessor(new ResourceLimits(), FilterRegistry.CreateDefault());
        var data = new ObjectValue();

        var result = processor.Process("{{#if name ?? nickname}}Hello{{/if}}", data);

        Assert.Equal("", result);
    }

    /// <summary>
    /// Verifies that {{#if}} with single-quoted null coalescing works.
    /// </summary>
    [Fact]
    public void ProcessIfBlock_NullCoalesceWithSingleQuoteFallback_Works()
    {
        var processor = new TemplateProcessor(new ResourceLimits(), FilterRegistry.CreateDefault());
        var data = new ObjectValue();

        var result = processor.Process("{{#if name ?? 'guest'}}Welcome {{name ?? 'guest'}}{{/if}}", data);

        Assert.Equal("Welcome guest", result);
    }

    /// <summary>
    /// Verifies that inline {{#each}} iterates over ObjectValue key-value pairs,
    /// exposing <c>@key</c> and <c>.</c> (current scope) for each entry.
    /// </summary>
    [Fact]
    public void Process_InlineEachOverObject_IteratesKeyValuePairs()
    {
        var data = new ObjectValue
        {
            ["specs"] = new ObjectValue
            {
                ["Color"] = new StringValue("Red"),
                ["Size"] = new StringValue("XL")
            }
        };

        var result = _processor.Process("{{#each specs}}{{@key}}:{{.}} {{/each}}", data);

        Assert.Equal("Color:Red Size:XL ", result);
    }

    /// <summary>
    /// Verifies that inline {{#each}} over an ObjectValue allows accessing nested
    /// properties on each value when values are themselves objects.
    /// </summary>
    [Fact]
    public void Process_InlineEachOverObject_NestedProperty()
    {
        var data = new ObjectValue
        {
            ["people"] = new ObjectValue
            {
                ["alice"] = new ObjectValue { ["age"] = new NumberValue(30) },
                ["bob"] = new ObjectValue { ["age"] = new NumberValue(25) }
            }
        };

        var result = _processor.Process("{{#each people}}{{@key}}={{age}} {{/each}}", data);

        Assert.Equal("alice=30 bob=25 ", result);
    }

    /// <summary>
    /// Verifies that inline {{#each}} over an empty ObjectValue produces no output.
    /// </summary>
    [Fact]
    public void Process_InlineEachOverObject_Empty_ReturnsEmpty()
    {
        var data = new ObjectValue
        {
            ["obj"] = new ObjectValue()
        };

        var result = _processor.Process("before{{#each obj}}{{@key}}{{/each}}after", data);

        Assert.Equal("beforeafter", result);
    }

    /// <summary>
    /// Verifies that inline {{#each}} over an ObjectValue correctly sets
    /// <c>@index</c>, <c>@first</c>, and <c>@last</c> loop variables.
    /// </summary>
    [Fact]
    public void Process_InlineEachOverObject_IndexFirstLast()
    {
        var data = new ObjectValue
        {
            ["items"] = new ObjectValue
            {
                ["a"] = new StringValue("1"),
                ["b"] = new StringValue("2")
            }
        };

        var result = _processor.Process("{{#each items}}{{@index}}{{@first}}{{@last}} {{/each}}", data);

        Assert.Equal("0truefalse 1falsetrue ", result);
    }

    /// <summary>
    /// Verifies that inline {{#each}} over an ObjectValue supports computed key access
    /// to look up values from another dictionary using <c>@key</c>.
    /// </summary>
    [Fact]
    public void Process_InlineEachOverObject_WithComputedKeyAccess()
    {
        var data = new ObjectValue
        {
            ["labels"] = new ObjectValue
            {
                ["name"] = new StringValue("Name"),
                ["price"] = new StringValue("Price")
            },
            ["values"] = new ObjectValue
            {
                ["name"] = new StringValue("Widget"),
                ["price"] = new StringValue("$9.99")
            }
        };

        var result = _processor.Process("{{#each labels}}{{.}}: {{values[@key]}} {{/each}}", data);

        Assert.Equal("Name: Widget Price: $9.99 ", result);
    }
}
