using FlexRender.Configuration;
using FlexRender.Parsing;
using FlexRender.Parsing.Ast;
using FlexRender.TemplateEngine;
using Xunit;

namespace FlexRender.Tests.TemplateEngine;

/// <summary>
/// Integration tests for the <see cref="ExprValue{T}"/> pipeline.
/// Verifies end-to-end: YAML parse -> TemplatePipeline process -> typed values resolved correctly.
/// Covers expression resolution in non-string property types (float, int?, bool, enum).
/// </summary>
public sealed class ExprValueIntegrationTests
{
    private readonly TemplateParser _parser = new();

    /// <summary>
    /// Creates a configured <see cref="TemplatePipeline"/> instance for test use.
    /// </summary>
    private static TemplatePipeline CreatePipeline()
    {
        var limits = new ResourceLimits();
        var expander = new TemplateExpander(limits);
        var processor = new TemplateProcessor(limits);
        return new TemplatePipeline(expander, processor);
    }

    /// <summary>
    /// Parses a YAML template and processes it through the pipeline with the given data.
    /// </summary>
    /// <param name="yaml">The YAML template string.</param>
    /// <param name="data">The data context for expression evaluation.</param>
    /// <returns>The processed template with all expressions resolved.</returns>
    private Template ParseAndProcess(string yaml, ObjectValue data)
    {
        var template = _parser.Parse(yaml);
        var pipeline = CreatePipeline();
        return pipeline.Process(template, data);
    }

    // === 1. Expression in float property (Opacity) ===

    /// <summary>
    /// Verifies that a template expression in the opacity property (float)
    /// is resolved correctly through the full pipeline.
    /// </summary>
    [Fact]
    public void Pipeline_ExpressionInOpacity_ResolvesToFloat()
    {
        const string yaml = """
            canvas:
              width: 300
            layout:
              - type: text
                content: "Hello"
                opacity: "{{theme.opacity}}"
            """;

        var data = new ObjectValue
        {
            ["theme"] = new ObjectValue
            {
                ["opacity"] = new NumberValue(0.5m)
            }
        };

        var result = ParseAndProcess(yaml, data);

        var text = Assert.IsType<TextElement>(result.Elements[0]);
        Assert.Equal(0.5f, text.Opacity.Value);
        Assert.True(text.Opacity.IsResolved);
    }

    // === 2. Expression in int? property (MaxLines) ===

    /// <summary>
    /// Verifies that a template expression in the maxLines property (int?)
    /// is resolved correctly through the full pipeline.
    /// </summary>
    [Fact]
    public void Pipeline_ExpressionInMaxLines_ResolvesToInt()
    {
        const string yaml = """
            canvas:
              width: 300
            layout:
              - type: text
                content: "Hello"
                maxLines: "{{settings.lines}}"
            """;

        var data = new ObjectValue
        {
            ["settings"] = new ObjectValue
            {
                ["lines"] = new NumberValue(3m)
            }
        };

        var result = ParseAndProcess(yaml, data);

        var text = Assert.IsType<TextElement>(result.Elements[0]);
        Assert.Equal(3, text.MaxLines.Value);
        Assert.True(text.MaxLines.IsResolved);
    }

    // === 3. Expression in bool property (ShowText on BarcodeElement) ===

    /// <summary>
    /// Verifies that a template expression in the showText property (bool)
    /// is resolved correctly through the full pipeline.
    /// </summary>
    [Fact]
    public void Pipeline_ExpressionInShowText_ResolvesToBool()
    {
        const string yaml = """
            canvas:
              width: 300
            layout:
              - type: barcode
                data: "12345"
                showText: "{{config.showText}}"
            """;

        var data = new ObjectValue
        {
            ["config"] = new ObjectValue
            {
                ["showText"] = new BoolValue(true)
            }
        };

        var result = ParseAndProcess(yaml, data);

        var barcode = Assert.IsType<BarcodeElement>(result.Elements[0]);
        Assert.True(barcode.ShowText.Value);
        Assert.True(barcode.ShowText.IsResolved);
    }

    /// <summary>
    /// Verifies that a false boolean expression is resolved correctly.
    /// </summary>
    [Fact]
    public void Pipeline_ExpressionInShowText_FalseValue_ResolvesToFalse()
    {
        const string yaml = """
            canvas:
              width: 300
            layout:
              - type: barcode
                data: "12345"
                showText: "{{config.showText}}"
            """;

        var data = new ObjectValue
        {
            ["config"] = new ObjectValue
            {
                ["showText"] = new BoolValue(false)
            }
        };

        var result = ParseAndProcess(yaml, data);

        var barcode = Assert.IsType<BarcodeElement>(result.Elements[0]);
        Assert.False(barcode.ShowText.Value);
        Assert.True(barcode.ShowText.IsResolved);
    }

    // === 4. Expression in enum property (TextAlign) ===

    /// <summary>
    /// Verifies that a template expression in the align property (TextAlign enum)
    /// is resolved correctly through the full pipeline.
    /// </summary>
    [Fact]
    public void Pipeline_ExpressionInAlign_ResolvesToEnum()
    {
        const string yaml = """
            canvas:
              width: 300
            layout:
              - type: text
                content: "Hello"
                align: "{{config.align}}"
            """;

        var data = new ObjectValue
        {
            ["config"] = new ObjectValue
            {
                ["align"] = new StringValue("center")
            }
        };

        var result = ParseAndProcess(yaml, data);

        var text = Assert.IsType<TextElement>(result.Elements[0]);
        Assert.Equal(TextAlign.Center, text.Align.Value);
        Assert.True(text.Align.IsResolved);
    }

    /// <summary>
    /// Verifies that a "right" alignment expression resolves correctly.
    /// </summary>
    [Fact]
    public void Pipeline_ExpressionInAlign_RightValue_ResolvesToRight()
    {
        const string yaml = """
            canvas:
              width: 300
            layout:
              - type: text
                content: "Hello"
                align: "{{config.align}}"
            """;

        var data = new ObjectValue
        {
            ["config"] = new ObjectValue
            {
                ["align"] = new StringValue("right")
            }
        };

        var result = ParseAndProcess(yaml, data);

        var text = Assert.IsType<TextElement>(result.Elements[0]);
        Assert.Equal(TextAlign.Right, text.Align.Value);
        Assert.True(text.Align.IsResolved);
    }

    // === 5. Expression in string property (Content) - baseline ===

    /// <summary>
    /// Verifies that a template expression in the content property (string)
    /// is resolved correctly through the full pipeline. This is the baseline
    /// test confirming string expression substitution still works.
    /// </summary>
    [Fact]
    public void Pipeline_ExpressionInContent_ResolvesToString()
    {
        const string yaml = """
            canvas:
              width: 300
            layout:
              - type: text
                content: "Hello {{name}}"
            """;

        var data = new ObjectValue
        {
            ["name"] = new StringValue("World")
        };

        var result = ParseAndProcess(yaml, data);

        var text = Assert.IsType<TextElement>(result.Elements[0]);
        Assert.Equal("Hello World", text.Content.Value);
        Assert.True(text.Content.IsResolved);
    }

    // === 6. Expression in Color property (string with validation) ===

    /// <summary>
    /// Verifies that a template expression in the color property (string)
    /// is resolved correctly through the full pipeline.
    /// </summary>
    [Fact]
    public void Pipeline_ExpressionInColor_ResolvesToString()
    {
        const string yaml = """
            canvas:
              width: 300
            layout:
              - type: text
                content: "Hello"
                color: "{{theme.color}}"
            """;

        var data = new ObjectValue
        {
            ["theme"] = new ObjectValue
            {
                ["color"] = new StringValue("#FF0000")
            }
        };

        var result = ParseAndProcess(yaml, data);

        var text = Assert.IsType<TextElement>(result.Elements[0]);
        Assert.Equal("#FF0000", text.Color.Value);
        Assert.True(text.Color.IsResolved);
    }

    // === 7. Null expression for nullable int? ===

    /// <summary>
    /// Verifies that when an expression resolves to an empty string for a nullable int? property,
    /// the materialized value is null.
    /// </summary>
    [Fact]
    public void Pipeline_ExpressionInMaxLines_EmptyValue_ResolvesToNull()
    {
        const string yaml = """
            canvas:
              width: 300
            layout:
              - type: text
                content: "Hello"
                maxLines: "{{config.lines}}"
            """;

        var data = new ObjectValue
        {
            ["config"] = new ObjectValue
            {
                ["lines"] = NullValue.Instance
            }
        };

        var result = ParseAndProcess(yaml, data);

        var text = Assert.IsType<TextElement>(result.Elements[0]);
        Assert.Null(text.MaxLines.Value);
        Assert.True(text.MaxLines.IsResolved);
    }

    /// <summary>
    /// Verifies that when the expression variable is missing from data,
    /// the nullable int? property resolves to null.
    /// </summary>
    [Fact]
    public void Pipeline_ExpressionInMaxLines_MissingVariable_ResolvesToNull()
    {
        const string yaml = """
            canvas:
              width: 300
            layout:
              - type: text
                content: "Hello"
                maxLines: "{{config.lines}}"
            """;

        // Data does not contain config.lines
        var data = new ObjectValue();

        var result = ParseAndProcess(yaml, data);

        var text = Assert.IsType<TextElement>(result.Elements[0]);
        Assert.Null(text.MaxLines.Value);
        Assert.True(text.MaxLines.IsResolved);
    }

    // === 8. Multiple expressions in same template ===

    /// <summary>
    /// Verifies that multiple expression-driven properties are all resolved correctly
    /// in a single pipeline pass.
    /// </summary>
    [Fact]
    public void Pipeline_MultipleExpressions_AllResolvedCorrectly()
    {
        const string yaml = """
            canvas:
              width: 300
            layout:
              - type: text
                content: "{{greeting}}"
                opacity: "{{theme.opacity}}"
                color: "{{theme.color}}"
                maxLines: "{{settings.lines}}"
                align: "{{settings.align}}"
                wrap: "{{settings.wrap}}"
            """;

        var data = new ObjectValue
        {
            ["greeting"] = new StringValue("Hello World"),
            ["theme"] = new ObjectValue
            {
                ["opacity"] = new NumberValue(0.8m),
                ["color"] = new StringValue("#00FF00")
            },
            ["settings"] = new ObjectValue
            {
                ["lines"] = new NumberValue(2m),
                ["align"] = new StringValue("right"),
                ["wrap"] = new BoolValue(false)
            }
        };

        var result = ParseAndProcess(yaml, data);

        var text = Assert.IsType<TextElement>(result.Elements[0]);
        Assert.Equal("Hello World", text.Content.Value);
        Assert.Equal(0.8f, text.Opacity.Value);
        Assert.Equal("#00FF00", text.Color.Value);
        Assert.Equal(2, text.MaxLines.Value);
        Assert.Equal(TextAlign.Right, text.Align.Value);
        Assert.False(text.Wrap.Value);

        Assert.True(text.Content.IsResolved);
        Assert.True(text.Opacity.IsResolved);
        Assert.True(text.Color.IsResolved);
        Assert.True(text.MaxLines.IsResolved);
        Assert.True(text.Align.IsResolved);
        Assert.True(text.Wrap.IsResolved);
    }

    // === 9. Expression resolution preserves literals ===

    /// <summary>
    /// Verifies that properties with literal values (not expressions) pass through
    /// the pipeline unchanged and are correctly resolved.
    /// </summary>
    [Fact]
    public void Pipeline_LiteralValues_PreservedUnchanged()
    {
        const string yaml = """
            canvas:
              width: 300
            layout:
              - type: text
                content: "Static text"
                opacity: 0.7
                color: "#123456"
                maxLines: 5
                align: center
                wrap: false
            """;

        var data = new ObjectValue();

        var result = ParseAndProcess(yaml, data);

        var text = Assert.IsType<TextElement>(result.Elements[0]);
        Assert.Equal("Static text", text.Content.Value);
        Assert.Equal(0.7f, text.Opacity.Value, 0.001f);
        Assert.Equal("#123456", text.Color.Value);
        Assert.Equal(5, text.MaxLines.Value);
        Assert.Equal(TextAlign.Center, text.Align.Value);
        Assert.False(text.Wrap.Value);

        Assert.True(text.Content.IsResolved);
        Assert.True(text.Opacity.IsResolved);
        Assert.True(text.Color.IsResolved);
        Assert.True(text.MaxLines.IsResolved);
        Assert.True(text.Align.IsResolved);
        Assert.True(text.Wrap.IsResolved);
    }

    // === Additional edge cases ===

    /// <summary>
    /// Verifies that expression in the wrap property (bool) of a text element
    /// is resolved correctly.
    /// </summary>
    [Fact]
    public void Pipeline_ExpressionInWrap_ResolvesToBool()
    {
        const string yaml = """
            canvas:
              width: 300
            layout:
              - type: text
                content: "Hello"
                wrap: "{{config.wrap}}"
            """;

        var data = new ObjectValue
        {
            ["config"] = new ObjectValue
            {
                ["wrap"] = new BoolValue(false)
            }
        };

        var result = ParseAndProcess(yaml, data);

        var text = Assert.IsType<TextElement>(result.Elements[0]);
        Assert.False(text.Wrap.Value);
        Assert.True(text.Wrap.IsResolved);
    }

    /// <summary>
    /// Verifies that expression in the grow property (float) on a flex child
    /// is resolved correctly.
    /// </summary>
    [Fact]
    public void Pipeline_ExpressionInGrow_ResolvesToFloat()
    {
        const string yaml = """
            canvas:
              width: 300
            layout:
              - type: text
                content: "Hello"
                grow: "{{layout.grow}}"
            """;

        var data = new ObjectValue
        {
            ["layout"] = new ObjectValue
            {
                ["grow"] = new NumberValue(2m)
            }
        };

        var result = ParseAndProcess(yaml, data);

        var text = Assert.IsType<TextElement>(result.Elements[0]);
        Assert.Equal(2.0f, text.Grow.Value);
        Assert.True(text.Grow.IsResolved);
    }

    /// <summary>
    /// Verifies that expression in the order property (int)
    /// is resolved correctly.
    /// </summary>
    [Fact]
    public void Pipeline_ExpressionInOrder_ResolvesToInt()
    {
        const string yaml = """
            canvas:
              width: 300
            layout:
              - type: text
                content: "Hello"
                order: "{{layout.order}}"
            """;

        var data = new ObjectValue
        {
            ["layout"] = new ObjectValue
            {
                ["order"] = new NumberValue(3m)
            }
        };

        var result = ParseAndProcess(yaml, data);

        var text = Assert.IsType<TextElement>(result.Elements[0]);
        Assert.Equal(3, text.Order.Value);
        Assert.True(text.Order.IsResolved);
    }

    /// <summary>
    /// Verifies that mixed literal and expression properties coexist correctly.
    /// Some properties are literal, others are expressions, and all are resolved.
    /// </summary>
    [Fact]
    public void Pipeline_MixedLiteralAndExpression_AllResolvedCorrectly()
    {
        const string yaml = """
            canvas:
              width: 300
            layout:
              - type: text
                content: "Hello {{name}}"
                opacity: 0.9
                color: "{{theme.color}}"
                maxLines: 3
                align: "{{config.align}}"
            """;

        var data = new ObjectValue
        {
            ["name"] = new StringValue("Bob"),
            ["theme"] = new ObjectValue
            {
                ["color"] = new StringValue("#ABCDEF")
            },
            ["config"] = new ObjectValue
            {
                ["align"] = new StringValue("center")
            }
        };

        var result = ParseAndProcess(yaml, data);

        var text = Assert.IsType<TextElement>(result.Elements[0]);

        // Expression properties resolved
        Assert.Equal("Hello Bob", text.Content.Value);
        Assert.Equal("#ABCDEF", text.Color.Value);
        Assert.Equal(TextAlign.Center, text.Align.Value);

        // Literal properties preserved
        Assert.Equal(0.9f, text.Opacity.Value, 0.001f);
        Assert.Equal(3, text.MaxLines.Value);

        // All marked as resolved
        Assert.True(text.Content.IsResolved);
        Assert.True(text.Color.IsResolved);
        Assert.True(text.Align.IsResolved);
        Assert.True(text.Opacity.IsResolved);
        Assert.True(text.MaxLines.IsResolved);
    }

    /// <summary>
    /// Verifies that when an opacity expression resolves to a number, the value
    /// is correctly parsed as a float.
    /// </summary>
    [Fact]
    public void Pipeline_ExpressionInOpacity_IntegerValue_ResolvesToFloat()
    {
        const string yaml = """
            canvas:
              width: 300
            layout:
              - type: text
                content: "Hello"
                opacity: "{{theme.opacity}}"
            """;

        var data = new ObjectValue
        {
            ["theme"] = new ObjectValue
            {
                ["opacity"] = new NumberValue(1m)
            }
        };

        var result = ParseAndProcess(yaml, data);

        var text = Assert.IsType<TextElement>(result.Elements[0]);
        Assert.Equal(1.0f, text.Opacity.Value);
        Assert.True(text.Opacity.IsResolved);
    }

    /// <summary>
    /// Verifies that the YAML parser creates <see cref="ExprValue{T}"/> with IsExpression=true
    /// when a non-string typed property contains a template expression.
    /// This test validates the parser behavior directly, without the pipeline.
    /// </summary>
    [Fact]
    public void Parser_ExpressionInTypedProperty_CreatesExpressionExprValue()
    {
        const string yaml = """
            canvas:
              width: 300
            layout:
              - type: text
                content: "Hello"
                opacity: "{{theme.opacity}}"
                maxLines: "{{settings.lines}}"
                wrap: "{{config.wrap}}"
                align: "{{config.align}}"
            """;

        var template = _parser.Parse(yaml);

        var text = Assert.IsType<TextElement>(template.Elements[0]);
        Assert.True(text.Opacity.IsExpression, "Opacity should be marked as expression");
        Assert.Equal("{{theme.opacity}}", text.Opacity.RawValue);

        Assert.True(text.MaxLines.IsExpression, "MaxLines should be marked as expression");
        Assert.Equal("{{settings.lines}}", text.MaxLines.RawValue);

        Assert.True(text.Wrap.IsExpression, "Wrap should be marked as expression");
        Assert.Equal("{{config.wrap}}", text.Wrap.RawValue);

        Assert.True(text.Align.IsExpression, "Align should be marked as expression");
        Assert.Equal("{{config.align}}", text.Align.RawValue);
    }

    /// <summary>
    /// Verifies that the YAML parser correctly parses literal values in typed properties
    /// (they should not be marked as expressions).
    /// </summary>
    [Fact]
    public void Parser_LiteralInTypedProperty_CreatesLiteralExprValue()
    {
        const string yaml = """
            canvas:
              width: 300
            layout:
              - type: text
                content: "Hello"
                opacity: 0.5
                maxLines: 3
                wrap: false
                align: center
            """;

        var template = _parser.Parse(yaml);

        var text = Assert.IsType<TextElement>(template.Elements[0]);
        Assert.False(text.Opacity.IsExpression);
        Assert.Equal(0.5f, text.Opacity.Value);

        Assert.False(text.MaxLines.IsExpression);
        Assert.Equal(3, text.MaxLines.Value);

        Assert.False(text.Wrap.IsExpression);
        Assert.False(text.Wrap.Value);

        Assert.False(text.Align.IsExpression);
        Assert.Equal(TextAlign.Center, text.Align.Value);
    }
}
