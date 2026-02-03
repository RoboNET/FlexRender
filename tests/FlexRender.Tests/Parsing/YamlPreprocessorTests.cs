using FlexRender.Configuration;
using FlexRender.Parsing;
using Xunit;

namespace FlexRender.Tests.Parsing;

public class YamlPreprocessorTests
{
    [Fact]
    public void Preprocess_NullData_ReturnsOriginal()
    {
        var yaml = "key: value";

        var result = YamlPreprocessor.Preprocess(yaml, null);

        Assert.Equal(yaml, result);
    }

    [Fact]
    public void Preprocess_NoBlocks_ReturnsOriginal()
    {
        var yaml = """
            template:
              name: "test"
            layout:
              - type: text
                content: "hello"
            """;
        var data = new ObjectValue { ["name"] = "test" };

        var result = YamlPreprocessor.Preprocess(yaml, data);

        Assert.Equal(yaml, result);
    }

    [Fact]
    public void Preprocess_IfBlock_TruthyCondition_IncludesThenBlock()
    {
        var yaml = """
            before
            {{#if show}}
            included content
            {{/if}}
            after
            """;
        var data = new ObjectValue { ["show"] = true };

        var result = YamlPreprocessor.Preprocess(yaml, data);

        Assert.Contains("included content", result);
        Assert.Contains("before", result);
        Assert.Contains("after", result);
        Assert.DoesNotContain("{{#if", result);
        Assert.DoesNotContain("{{/if}}", result);
    }

    [Fact]
    public void Preprocess_IfBlock_FalsyCondition_RemovesThenBlock()
    {
        var yaml = """
            before
            {{#if show}}
            removed content
            {{/if}}
            after
            """;
        var data = new ObjectValue { ["show"] = false };

        var result = YamlPreprocessor.Preprocess(yaml, data);

        Assert.DoesNotContain("removed content", result);
        Assert.Contains("before", result);
        Assert.Contains("after", result);
    }

    [Fact]
    public void Preprocess_IfElseBlock_TruthyCondition_IncludesThenBlock()
    {
        var yaml = """
            {{#if show}}
            then content
            {{else}}
            else content
            {{/if}}
            """;
        var data = new ObjectValue { ["show"] = true };

        var result = YamlPreprocessor.Preprocess(yaml, data);

        Assert.Contains("then content", result);
        Assert.DoesNotContain("else content", result);
    }

    [Fact]
    public void Preprocess_IfElseBlock_FalsyCondition_IncludesElseBlock()
    {
        var yaml = """
            {{#if show}}
            then content
            {{else}}
            else content
            {{/if}}
            """;
        var data = new ObjectValue { ["show"] = false };

        var result = YamlPreprocessor.Preprocess(yaml, data);

        Assert.DoesNotContain("then content", result);
        Assert.Contains("else content", result);
    }

    [Fact]
    public void Preprocess_IfBlock_NullValue_Falsy()
    {
        var yaml = """
            {{#if missing}}
            should not appear
            {{/if}}
            """;
        var data = new ObjectValue { ["other"] = "value" };

        var result = YamlPreprocessor.Preprocess(yaml, data);

        Assert.DoesNotContain("should not appear", result);
    }

    [Fact]
    public void Preprocess_IfBlock_EmptyString_Falsy()
    {
        var yaml = """
            {{#if name}}
            should not appear
            {{/if}}
            """;
        var data = new ObjectValue { ["name"] = "" };

        var result = YamlPreprocessor.Preprocess(yaml, data);

        Assert.DoesNotContain("should not appear", result);
    }

    [Fact]
    public void Preprocess_IfBlock_BoolTrue_Truthy()
    {
        var yaml = """
            {{#if active}}
            visible
            {{/if}}
            """;
        var data = new ObjectValue { ["active"] = true };

        var result = YamlPreprocessor.Preprocess(yaml, data);

        Assert.Contains("visible", result);
    }

    [Fact]
    public void Preprocess_IfBlock_BoolFalse_Falsy()
    {
        var yaml = """
            {{#if active}}
            visible
            {{/if}}
            """;
        var data = new ObjectValue { ["active"] = false };

        var result = YamlPreprocessor.Preprocess(yaml, data);

        Assert.DoesNotContain("visible", result);
    }

    [Fact]
    public void Preprocess_IfBlock_NumberZero_Falsy()
    {
        var yaml = """
            {{#if count}}
            visible
            {{/if}}
            """;
        var data = new ObjectValue { ["count"] = 0 };

        var result = YamlPreprocessor.Preprocess(yaml, data);

        Assert.DoesNotContain("visible", result);
    }

    [Fact]
    public void Preprocess_IfBlock_NumberNonZero_Truthy()
    {
        var yaml = """
            {{#if count}}
            visible
            {{/if}}
            """;
        var data = new ObjectValue { ["count"] = 42 };

        var result = YamlPreprocessor.Preprocess(yaml, data);

        Assert.Contains("visible", result);
    }

    [Fact]
    public void Preprocess_NestedIfBlocks_ProcessedCorrectly()
    {
        var yaml = """
            {{#if outer}}
            outer-start
            {{#if inner}}
            inner content
            {{/if}}
            outer-end
            {{/if}}
            """;
        var data = new ObjectValue
        {
            ["outer"] = true,
            ["inner"] = true
        };

        var result = YamlPreprocessor.Preprocess(yaml, data);

        Assert.Contains("outer-start", result);
        Assert.Contains("inner content", result);
        Assert.Contains("outer-end", result);
    }

    [Fact]
    public void Preprocess_NestedIfBlocks_OuterTrueInnerFalse()
    {
        var yaml = """
            {{#if outer}}
            outer-start
            {{#if inner}}
            inner content
            {{/if}}
            outer-end
            {{/if}}
            """;
        var data = new ObjectValue
        {
            ["outer"] = true,
            ["inner"] = false
        };

        var result = YamlPreprocessor.Preprocess(yaml, data);

        Assert.Contains("outer-start", result);
        Assert.DoesNotContain("inner content", result);
        Assert.Contains("outer-end", result);
    }

    [Fact]
    public void Preprocess_NestedIfBlocks_OuterFalse_RemovesEverything()
    {
        var yaml = """
            {{#if outer}}
            outer-start
            {{#if inner}}
            inner content
            {{/if}}
            outer-end
            {{/if}}
            """;
        var data = new ObjectValue
        {
            ["outer"] = false,
            ["inner"] = true
        };

        var result = YamlPreprocessor.Preprocess(yaml, data);

        Assert.DoesNotContain("outer-start", result);
        Assert.DoesNotContain("inner content", result);
        Assert.DoesNotContain("outer-end", result);
    }

    [Fact]
    public void Preprocess_IfInsideEach_ProcessedCorrectly()
    {
        var yaml = """
            {{#each items}}
            - name: {{name}}
              {{#if highlight}}
              style: bold
              {{/if}}
            {{/each}}
            """;
        var data = new ObjectValue
        {
            ["items"] = new ArrayValue(new TemplateValue[]
            {
                new ObjectValue { ["name"] = "first", ["highlight"] = true },
                new ObjectValue { ["name"] = "second", ["highlight"] = false }
            })
        };

        var result = YamlPreprocessor.Preprocess(yaml, data);

        Assert.Contains("- name: first", result);
        Assert.Contains("- name: second", result);
        // The first item has highlight=true, but note: inside {{#each}},
        // the context is the item itself. The {{#if highlight}} resolves
        // against the item context.
        Assert.Contains("style: bold", result);
    }

    [Fact]
    public void Preprocess_EachInsideIf_ProcessedCorrectly()
    {
        var yaml = """
            {{#if showList}}
            {{#each items}}
            - {{this}}
            {{/each}}
            {{/if}}
            """;
        var data = new ObjectValue
        {
            ["showList"] = true,
            ["items"] = new ArrayValue(new TemplateValue[]
            {
                new StringValue("alpha"),
                new StringValue("beta")
            })
        };

        var result = YamlPreprocessor.Preprocess(yaml, data);

        Assert.Contains("- alpha", result);
        Assert.Contains("- beta", result);
    }

    [Fact]
    public void Preprocess_EachInsideIf_FalsyCondition_RemovesAll()
    {
        var yaml = """
            {{#if showList}}
            {{#each items}}
            - {{this}}
            {{/each}}
            {{/if}}
            """;
        var data = new ObjectValue
        {
            ["showList"] = false,
            ["items"] = new ArrayValue(new TemplateValue[]
            {
                new StringValue("alpha"),
                new StringValue("beta")
            })
        };

        var result = YamlPreprocessor.Preprocess(yaml, data);

        Assert.DoesNotContain("alpha", result);
        Assert.DoesNotContain("beta", result);
    }

    [Fact]
    public void Preprocess_UnclosedIfBlock_Throws()
    {
        var yaml = """
            {{#if show}}
            content without closing tag
            """;
        var data = new ObjectValue { ["show"] = true };

        var ex = Assert.Throws<TemplateParseException>(() =>
            YamlPreprocessor.Preprocess(yaml, data));

        Assert.Contains("Unclosed {{#if}} block", ex.Message);
    }

    [Fact]
    public void Preprocess_IfBlock_DotNotationPath_ResolvesCorrectly()
    {
        var yaml = """
            {{#if user.active}}
            user is active
            {{/if}}
            """;
        var data = new ObjectValue
        {
            ["user"] = new ObjectValue { ["active"] = true }
        };

        var result = YamlPreprocessor.Preprocess(yaml, data);

        Assert.Contains("user is active", result);
    }

    [Fact]
    public void Preprocess_IfBlock_DotNotationPath_FalsyNested()
    {
        var yaml = """
            {{#if user.active}}
            user is active
            {{/if}}
            """;
        var data = new ObjectValue
        {
            ["user"] = new ObjectValue { ["active"] = false }
        };

        var result = YamlPreprocessor.Preprocess(yaml, data);

        Assert.DoesNotContain("user is active", result);
    }

    [Fact]
    public void Preprocess_InlineIfBlock_NotProcessed()
    {
        // Inline {{#if}} inside a YAML value should NOT be processed by the preprocessor
        var yaml = """
            content: "{{#if isProfit}}+{{/if}}{{pnlPercent}}%"
            color: "{{#if isProfit}}#00DC67{{else}}#F53F44{{/if}}"
            """;
        var data = new ObjectValue { ["isProfit"] = true };

        var result = YamlPreprocessor.Preprocess(yaml, data);

        // Inline expressions should be preserved as-is for TemplateProcessor
        Assert.Contains("{{#if isProfit}}+{{/if}}", result);
        Assert.Contains("{{#if isProfit}}#00DC67{{else}}#F53F44{{/if}}", result);
    }

    [Fact]
    public void Preprocess_IfBlock_NullValueInstance_Falsy()
    {
        var yaml = """
            {{#if value}}
            should not appear
            {{/if}}
            """;
        var data = new ObjectValue { ["value"] = NullValue.Instance };

        var result = YamlPreprocessor.Preprocess(yaml, data);

        Assert.DoesNotContain("should not appear", result);
    }

    [Fact]
    public void Preprocess_IfBlock_NonEmptyArray_Truthy()
    {
        var yaml = """
            {{#if items}}
            has items
            {{/if}}
            """;
        var data = new ObjectValue
        {
            ["items"] = new ArrayValue(new TemplateValue[] { new StringValue("one") })
        };

        var result = YamlPreprocessor.Preprocess(yaml, data);

        Assert.Contains("has items", result);
    }

    [Fact]
    public void Preprocess_IfBlock_EmptyArray_Falsy()
    {
        var yaml = """
            {{#if items}}
            has items
            {{/if}}
            """;
        var data = new ObjectValue
        {
            ["items"] = new ArrayValue(Array.Empty<TemplateValue>())
        };

        var result = YamlPreprocessor.Preprocess(yaml, data);

        Assert.DoesNotContain("has items", result);
    }

    [Fact]
    public void Preprocess_IfBlock_ObjectValue_Truthy()
    {
        var yaml = """
            {{#if config}}
            has config
            {{/if}}
            """;
        var data = new ObjectValue
        {
            ["config"] = new ObjectValue { ["key"] = "value" }
        };

        var result = YamlPreprocessor.Preprocess(yaml, data);

        Assert.Contains("has config", result);
    }

    [Fact]
    public void Preprocess_IfBlock_NonEmptyString_Truthy()
    {
        var yaml = """
            {{#if name}}
            has name
            {{/if}}
            """;
        var data = new ObjectValue { ["name"] = "Alice" };

        var result = YamlPreprocessor.Preprocess(yaml, data);

        Assert.Contains("has name", result);
    }

    [Fact]
    public void Preprocess_NestedIfWithElse_ProcessedCorrectly()
    {
        var yaml = """
            {{#if outer}}
            {{#if inner}}
            both true
            {{else}}
            outer only
            {{/if}}
            {{else}}
            neither
            {{/if}}
            """;
        var data = new ObjectValue
        {
            ["outer"] = true,
            ["inner"] = false
        };

        var result = YamlPreprocessor.Preprocess(yaml, data);

        Assert.DoesNotContain("both true", result);
        Assert.Contains("outer only", result);
        Assert.DoesNotContain("neither", result);
    }

    [Fact]
    public void Preprocess_NestedIfWithElse_OuterFalse()
    {
        var yaml = """
            {{#if outer}}
            {{#if inner}}
            both true
            {{else}}
            outer only
            {{/if}}
            {{else}}
            neither
            {{/if}}
            """;
        var data = new ObjectValue
        {
            ["outer"] = false,
            ["inner"] = true
        };

        var result = YamlPreprocessor.Preprocess(yaml, data);

        Assert.DoesNotContain("both true", result);
        Assert.DoesNotContain("outer only", result);
        Assert.Contains("neither", result);
    }

    [Fact]
    public void Preprocess_MultipleIfBlocks_AllProcessed()
    {
        var yaml = """
            {{#if first}}
            first block
            {{/if}}
            middle
            {{#if second}}
            second block
            {{/if}}
            """;
        var data = new ObjectValue
        {
            ["first"] = true,
            ["second"] = false
        };

        var result = YamlPreprocessor.Preprocess(yaml, data);

        Assert.Contains("first block", result);
        Assert.Contains("middle", result);
        Assert.DoesNotContain("second block", result);
    }

    [Fact]
    public void Preprocess_PnlCardStyleStandaloneIfBlock()
    {
        // Simulates a conditional block pattern where {{#if}} wraps entire YAML nodes
        var yaml = """
            children:
              - type: text
                content: "ticker"

              {{#if username}}
              - type: flex
                background: "rgba(255,255,255,0.05)"
                padding: 16
                children:
                  - type: text
                    content: "@user"
              {{/if}}
            """;
        var data = new ObjectValue { ["username"] = "trader_pro" };

        var result = YamlPreprocessor.Preprocess(yaml, data);

        Assert.Contains("- type: flex", result);
        Assert.Contains("content: \"@user\"", result);
    }

    [Fact]
    public void Preprocess_PnlCardStyleStandaloneIfBlock_NoUsername()
    {
        var yaml = """
            children:
              - type: text
                content: "ticker"

              {{#if username}}
              - type: flex
                background: "rgba(255,255,255,0.05)"
                padding: 16
                children:
                  - type: text
                    content: "@user"
              {{/if}}
            """;
        var data = new ObjectValue { ["ticker"] = "SOL/USDT" };

        var result = YamlPreprocessor.Preprocess(yaml, data);

        Assert.DoesNotContain("- type: flex", result);
        Assert.DoesNotContain("content: \"@user\"", result);
        Assert.Contains("content: \"ticker\"", result);
    }

    [Fact]
    public void Preprocess_EmptyYaml_ReturnsEmpty()
    {
        var result = YamlPreprocessor.Preprocess("", new ObjectValue());

        Assert.Equal("", result);
    }

    [Fact]
    public void Preprocess_NullYaml_ReturnsNull()
    {
        var result = YamlPreprocessor.Preprocess(null!, new ObjectValue());

        Assert.Null(result);
    }

    [Fact]
    public void Preprocess_IfBlock_WithIndentation_Preserved()
    {
        var yaml = "parent:\n  {{#if show}}\n  child: value\n  {{/if}}\n";
        var data = new ObjectValue { ["show"] = true };

        var result = YamlPreprocessor.Preprocess(yaml, data);

        Assert.Contains("child: value", result);
        Assert.Contains("parent:", result);
    }

    [Fact]
    public void Preprocess_NegativeNumber_Truthy()
    {
        var yaml = """
            {{#if amount}}
            has amount
            {{/if}}
            """;
        var data = new ObjectValue { ["amount"] = -5 };

        var result = YamlPreprocessor.Preprocess(yaml, data);

        Assert.Contains("has amount", result);
    }

    [Fact]
    public void Preprocess_WithCustomMaxInputSize_RejectsOversizedInput()
    {
        var limits = new ResourceLimits { MaxPreprocessorInputSize = 100 };
        var yaml = new string('a', 150); // > 100 bytes
        var data = new ObjectValue { ["key"] = "value" };

        var ex = Assert.Throws<ArgumentException>(() =>
            YamlPreprocessor.Preprocess(yaml, data, limits));
        Assert.Contains("exceeds maximum", ex.Message.ToLowerInvariant());
    }

    [Fact]
    public void Preprocess_WithCustomMaxNestingDepth_RejectsDeeplyNested()
    {
        var limits = new ResourceLimits { MaxPreprocessorNestingDepth = 2 };
        // 3 levels of nesting should exceed depth of 2
        var yaml = """
            {{#if a}}
            {{#if b}}
            {{#if c}}
            content
            {{/if}}
            {{/if}}
            {{/if}}
            """;
        var data = new ObjectValue
        {
            ["a"] = true,
            ["b"] = true,
            ["c"] = true
        };

        var ex = Assert.Throws<TemplateParseException>(() =>
            YamlPreprocessor.Preprocess(yaml, data, limits));
        Assert.Contains("maximum nesting depth", ex.Message.ToLowerInvariant());
    }

    [Fact]
    public void Preprocess_WithDefaultLimits_BehavesAsOriginal()
    {
        // The 2-arg overload should still work as before
        var yaml = """
            {{#if show}}
            visible
            {{/if}}
            """;
        var data = new ObjectValue { ["show"] = true };

        var result = YamlPreprocessor.Preprocess(yaml, data);

        Assert.Contains("visible", result);
    }
}
