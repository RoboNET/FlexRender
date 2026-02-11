using FlexRender.Configuration;
using FlexRender.TemplateEngine;
using Xunit;

namespace FlexRender.Tests.TemplateEngine;

/// <summary>
/// Tests for {{#if}} and {{#each}} text blocks in <see cref="TemplateProcessor"/>.
/// </summary>
public sealed class TemplateProcessorTextBlockTests
{
    private readonly TemplateProcessor _processor = new(new ResourceLimits(), FilterRegistry.CreateDefault());

    #region {{#if}} Tests

    [Fact]
    public void If_TruthyVariable_ShowsBlock()
    {
        var data = new ObjectValue
        {
            ["name"] = new StringValue("Alice")
        };

        var result = _processor.Process("{{#if name}}Hello {{name}}{{/if}}", data);

        Assert.Equal("Hello Alice", result);
    }

    [Fact]
    public void If_FalsyVariable_HidesBlock()
    {
        var data = new ObjectValue();

        var result = _processor.Process("{{#if name}}Hello {{name}}{{/if}}", data);

        Assert.Equal("", result);
    }

    [Fact]
    public void If_EmptyString_IsFalsy()
    {
        var data = new ObjectValue
        {
            ["name"] = new StringValue("")
        };

        var result = _processor.Process("{{#if name}}Hello{{/if}}", data);

        Assert.Equal("", result);
    }

    [Fact]
    public void If_Zero_IsFalsy()
    {
        var data = new ObjectValue
        {
            ["count"] = new NumberValue(0)
        };

        var result = _processor.Process("{{#if count}}Has items{{/if}}", data);

        Assert.Equal("", result);
    }

    [Fact]
    public void If_NonZeroNumber_IsTruthy()
    {
        var data = new ObjectValue
        {
            ["count"] = new NumberValue(5)
        };

        var result = _processor.Process("{{#if count}}Count: {{count}}{{/if}}", data);

        Assert.Equal("Count: 5", result);
    }

    [Fact]
    public void If_BoolTrue_IsTruthy()
    {
        var data = new ObjectValue
        {
            ["active"] = new BoolValue(true)
        };

        var result = _processor.Process("{{#if active}}Active{{/if}}", data);

        Assert.Equal("Active", result);
    }

    [Fact]
    public void If_BoolFalse_IsFalsy()
    {
        var data = new ObjectValue
        {
            ["active"] = new BoolValue(false)
        };

        var result = _processor.Process("{{#if active}}Active{{/if}}", data);

        Assert.Equal("", result);
    }

    [Fact]
    public void If_EmptyArray_IsFalsy()
    {
        var data = new ObjectValue
        {
            ["items"] = new ArrayValue(Array.Empty<TemplateValue>())
        };

        var result = _processor.Process("{{#if items}}Has items{{/if}}", data);

        Assert.Equal("", result);
    }

    [Fact]
    public void If_NonEmptyArray_IsTruthy()
    {
        var data = new ObjectValue
        {
            ["items"] = new ArrayValue(new TemplateValue[] { new StringValue("a") })
        };

        var result = _processor.Process("{{#if items}}Has items{{/if}}", data);

        Assert.Equal("Has items", result);
    }

    [Fact]
    public void If_NullValue_IsFalsy()
    {
        var data = new ObjectValue
        {
            ["value"] = NullValue.Instance
        };

        var result = _processor.Process("{{#if value}}Visible{{/if}}", data);

        Assert.Equal("", result);
    }

    [Fact]
    public void If_EmptyObject_IsFalsy()
    {
        var data = new ObjectValue
        {
            ["obj"] = new ObjectValue()
        };

        var result = _processor.Process("{{#if obj}}Has data{{/if}}", data);

        Assert.Equal("", result);
    }

    [Fact]
    public void If_NonEmptyObject_IsTruthy()
    {
        var data = new ObjectValue
        {
            ["obj"] = new ObjectValue { ["key"] = new StringValue("val") }
        };

        var result = _processor.Process("{{#if obj}}Has data{{/if}}", data);

        Assert.Equal("Has data", result);
    }

    [Fact]
    public void If_NegativeNumber_IsTruthy()
    {
        var data = new ObjectValue
        {
            ["val"] = new NumberValue(-1)
        };

        var result = _processor.Process("{{#if val}}Negative{{/if}}", data);

        Assert.Equal("Negative", result);
    }

    [Fact]
    public void If_NonEmptyString_IsTruthy()
    {
        var data = new ObjectValue
        {
            ["text"] = new StringValue("hello")
        };

        var result = _processor.Process("{{#if text}}Found{{/if}}", data);

        Assert.Equal("Found", result);
    }

    #endregion

    #region {{#if}}...{{else}} Tests

    [Fact]
    public void IfElse_TruthyCondition_ShowsThenBranch()
    {
        var data = new ObjectValue
        {
            ["name"] = new StringValue("Alice")
        };

        var result = _processor.Process("{{#if name}}Hello {{name}}{{else}}Hello guest{{/if}}", data);

        Assert.Equal("Hello Alice", result);
    }

    [Fact]
    public void IfElse_FalsyCondition_ShowsElseBranch()
    {
        var data = new ObjectValue();

        var result = _processor.Process("{{#if name}}Hello {{name}}{{else}}Hello guest{{/if}}", data);

        Assert.Equal("Hello guest", result);
    }

    [Fact]
    public void IfElse_BoolFalse_ShowsElseBranch()
    {
        var data = new ObjectValue
        {
            ["active"] = new BoolValue(false)
        };

        var result = _processor.Process("{{#if active}}Yes{{else}}No{{/if}}", data);

        Assert.Equal("No", result);
    }

    [Fact]
    public void IfElse_EmptyString_ShowsElseBranch()
    {
        var data = new ObjectValue
        {
            ["name"] = new StringValue("")
        };

        var result = _processor.Process("{{#if name}}Named{{else}}Anonymous{{/if}}", data);

        Assert.Equal("Anonymous", result);
    }

    [Fact]
    public void IfElse_ElseBranchCanContainVariables()
    {
        var data = new ObjectValue
        {
            ["fallback"] = new StringValue("default")
        };

        var result = _processor.Process("{{#if name}}{{name}}{{else}}{{fallback}}{{/if}}", data);

        Assert.Equal("default", result);
    }

    #endregion

    #region {{#if}} with Expressions

    [Fact]
    public void If_NullCoalescing_EvaluatesExpression()
    {
        var data = new ObjectValue
        {
            ["nickname"] = new StringValue("Bob")
        };

        var result = _processor.Process("{{#if name ?? nickname}}Hi {{name ?? nickname}}{{/if}}", data);

        Assert.Equal("Hi Bob", result);
    }

    [Fact]
    public void If_NullCoalescing_BothMissing_HidesBlock()
    {
        var data = new ObjectValue();

        var result = _processor.Process("{{#if name ?? nickname}}Hi{{/if}}", data);

        Assert.Equal("", result);
    }

    [Fact]
    public void If_NullCoalescing_WithStringFallback_ShowsBlock()
    {
        var data = new ObjectValue();

        var result = _processor.Process("{{#if name ?? 'guest'}}Welcome{{/if}}", data);

        Assert.Equal("Welcome", result);
    }

    [Fact]
    public void If_Arithmetic_NonZeroResult_IsTruthy()
    {
        var data = new ObjectValue
        {
            ["a"] = new NumberValue(3),
            ["b"] = new NumberValue(2)
        };

        var result = _processor.Process("{{#if a - b}}Positive{{/if}}", data);

        Assert.Equal("Positive", result);
    }

    #endregion

    #region {{#if}} with Comparison Operators

    [Fact]
    public void IfBlock_WithComparisonEquals_ShowsThenBlock()
    {
        var data = new ObjectValue
        {
            ["status"] = new StringValue("paid")
        };

        var result = _processor.Process("{{#if status == 'paid'}}YES{{else}}NO{{/if}}", data);

        Assert.Equal("YES", result);
    }

    [Fact]
    public void IfBlock_WithComparisonEquals_NotEqual_ShowsElseBlock()
    {
        var data = new ObjectValue
        {
            ["status"] = new StringValue("pending")
        };

        var result = _processor.Process("{{#if status == 'paid'}}YES{{else}}NO{{/if}}", data);

        Assert.Equal("NO", result);
    }

    [Fact]
    public void IfBlock_WithComparisonGreaterThan_ShowsThenBlock()
    {
        var data = new ObjectValue
        {
            ["total"] = new NumberValue(150)
        };

        var result = _processor.Process("{{#if total > 100}}Big order{{else}}Small{{/if}}", data);

        Assert.Equal("Big order", result);
    }

    [Fact]
    public void IfBlock_WithComparisonGreaterThan_ShowsElseBlock()
    {
        var data = new ObjectValue
        {
            ["total"] = new NumberValue(50)
        };

        var result = _processor.Process("{{#if total > 100}}Big order{{else}}Small{{/if}}", data);

        Assert.Equal("Small", result);
    }

    [Fact]
    public void IfBlock_WithLogicalNot_InvertsCondition()
    {
        var data = new ObjectValue
        {
            ["active"] = new BoolValue(false)
        };

        var result = _processor.Process("{{#if !active}}Inactive{{else}}Active{{/if}}", data);

        Assert.Equal("Inactive", result);
    }

    [Fact]
    public void IfBlock_WithLogicalNot_TrueValue_ShowsElseBranch()
    {
        var data = new ObjectValue
        {
            ["active"] = new BoolValue(true)
        };

        var result = _processor.Process("{{#if !active}}Inactive{{else}}Active{{/if}}", data);

        Assert.Equal("Active", result);
    }

    [Fact]
    public void IfBlock_WithNotEqual_ShowsThenBlock()
    {
        var data = new ObjectValue
        {
            ["status"] = new StringValue("pending")
        };

        var result = _processor.Process("{{#if status != 'paid'}}Unpaid{{else}}Paid{{/if}}", data);

        Assert.Equal("Unpaid", result);
    }

    [Fact]
    public void IfBlock_WithLessThanOrEqual_ShowsThenBlock()
    {
        var data = new ObjectValue
        {
            ["qty"] = new NumberValue(10)
        };

        var result = _processor.Process("{{#if qty <= 10}}Low stock{{else}}OK{{/if}}", data);

        Assert.Equal("Low stock", result);
    }

    [Fact]
    public void IfBlock_ComparisonWithTrueLiteral_Works()
    {
        var data = new ObjectValue { ["active"] = new BoolValue(true) };
        var result = _processor.Process("{{#if active == true}}yes{{else}}no{{/if}}", data);
        Assert.Equal("yes", result);
    }

    [Fact]
    public void IfBlock_ComparisonWithNullLiteral_Works()
    {
        var data = new ObjectValue();
        var result = _processor.Process("{{#if missing == null}}null{{else}}exists{{/if}}", data);
        Assert.Equal("null", result);
    }

    [Fact]
    public void IfBlock_ComparisonWithFalseLiteral_Works()
    {
        var data = new ObjectValue { ["disabled"] = new BoolValue(false) };
        var result = _processor.Process("{{#if disabled == false}}not disabled{{else}}disabled{{/if}}", data);
        Assert.Equal("not disabled", result);
    }

    #endregion

    #region Nested {{#if}} Tests

    [Fact]
    public void If_Nested_BothTrue_ShowsInnerBlock()
    {
        var data = new ObjectValue
        {
            ["show"] = new BoolValue(true),
            ["name"] = new StringValue("Alice")
        };

        var result = _processor.Process("{{#if show}}[{{#if name}}{{name}}{{/if}}]{{/if}}", data);

        Assert.Equal("[Alice]", result);
    }

    [Fact]
    public void If_Nested_OuterFalse_HidesAll()
    {
        var data = new ObjectValue
        {
            ["name"] = new StringValue("Alice")
        };

        var result = _processor.Process("{{#if show}}[{{#if name}}{{name}}{{/if}}]{{/if}}", data);

        Assert.Equal("", result);
    }

    [Fact]
    public void If_Nested_InnerFalse_ShowsOuterOnly()
    {
        var data = new ObjectValue
        {
            ["show"] = new BoolValue(true)
        };

        var result = _processor.Process("{{#if show}}Hello {{#if name}}{{name}}{{else}}guest{{/if}}{{/if}}", data);

        Assert.Equal("Hello guest", result);
    }

    [Fact]
    public void If_TripleNested_AllTrue_ShowsDeepContent()
    {
        var data = new ObjectValue
        {
            ["a"] = new BoolValue(true),
            ["b"] = new BoolValue(true),
            ["c"] = new BoolValue(true)
        };

        var result = _processor.Process("{{#if a}}1{{#if b}}2{{#if c}}3{{/if}}{{/if}}{{/if}}", data);

        Assert.Equal("123", result);
    }

    [Fact]
    public void If_NestedElse_InnerElseInThenBranch()
    {
        var data = new ObjectValue
        {
            ["outer"] = new BoolValue(true),
            ["inner"] = new BoolValue(false)
        };

        var result = _processor.Process("{{#if outer}}[{{#if inner}}YES{{else}}NO{{/if}}]{{/if}}", data);

        Assert.Equal("[NO]", result);
    }

    [Fact]
    public void If_NestedElse_InnerElseInElseBranch()
    {
        var data = new ObjectValue
        {
            ["outer"] = new BoolValue(false),
            ["inner"] = new BoolValue(true)
        };

        var result = _processor.Process("{{#if outer}}A{{else}}{{#if inner}}B{{else}}C{{/if}}{{/if}}", data);

        Assert.Equal("B", result);
    }

    #endregion

    #region {{#each}} Tests

    [Fact]
    public void Each_IteratesOverArray()
    {
        var data = new ObjectValue
        {
            ["items"] = new ArrayValue(new TemplateValue[]
            {
                new ObjectValue { ["name"] = new StringValue("A") },
                new ObjectValue { ["name"] = new StringValue("B") },
                new ObjectValue { ["name"] = new StringValue("C") }
            })
        };

        var result = _processor.Process("{{#each items}}{{name}} {{/each}}", data);

        Assert.Equal("A B C ", result);
    }

    [Fact]
    public void Each_EmptyArray_ProducesNothing()
    {
        var data = new ObjectValue
        {
            ["items"] = new ArrayValue(Array.Empty<TemplateValue>())
        };

        var result = _processor.Process("{{#each items}}{{name}}{{/each}}", data);

        Assert.Equal("", result);
    }

    [Fact]
    public void Each_MissingArray_ProducesNothing()
    {
        var data = new ObjectValue();

        var result = _processor.Process("{{#each items}}{{name}}{{/each}}", data);

        Assert.Equal("", result);
    }

    [Fact]
    public void Each_SingleItem_IteratesOnce()
    {
        var data = new ObjectValue
        {
            ["items"] = new ArrayValue(new TemplateValue[]
            {
                new ObjectValue { ["val"] = new StringValue("only") }
            })
        };

        var result = _processor.Process("{{#each items}}{{val}}{{/each}}", data);

        Assert.Equal("only", result);
    }

    [Fact]
    public void Each_IndexVariable_Available()
    {
        var data = new ObjectValue
        {
            ["items"] = new ArrayValue(new TemplateValue[]
            {
                new StringValue("A"),
                new StringValue("B"),
                new StringValue("C")
            })
        };

        var result = _processor.Process("{{#each items}}{{@index}} {{/each}}", data);

        Assert.Equal("0 1 2 ", result);
    }

    [Fact]
    public void Each_FirstLastVariables_Available()
    {
        var data = new ObjectValue
        {
            ["items"] = new ArrayValue(new TemplateValue[]
            {
                new StringValue("A"),
                new StringValue("B"),
                new StringValue("C")
            })
        };

        var result = _processor.Process("{{#each items}}{{#if @first}}[{{/if}}{{@index}}{{#if @last}}]{{/if}}{{/each}}", data);

        Assert.Equal("[012]", result);
    }

    [Fact]
    public void Each_FirstVariable_TrueOnlyForFirstItem()
    {
        var data = new ObjectValue
        {
            ["items"] = new ArrayValue(new TemplateValue[]
            {
                new ObjectValue { ["n"] = new StringValue("A") },
                new ObjectValue { ["n"] = new StringValue("B") },
                new ObjectValue { ["n"] = new StringValue("C") }
            })
        };

        var result = _processor.Process("{{#each items}}{{#if @first}}FIRST:{{/if}}{{n}} {{/each}}", data);

        Assert.Equal("FIRST:A B C ", result);
    }

    [Fact]
    public void Each_LastVariable_TrueOnlyForLastItem()
    {
        var data = new ObjectValue
        {
            ["items"] = new ArrayValue(new TemplateValue[]
            {
                new ObjectValue { ["n"] = new StringValue("A") },
                new ObjectValue { ["n"] = new StringValue("B") },
                new ObjectValue { ["n"] = new StringValue("C") }
            })
        };

        var result = _processor.Process("{{#each items}}{{n}}{{#if @last}} LAST{{/if}} {{/each}}", data);

        Assert.Equal("A B C LAST ", result);
    }

    [Fact]
    public void Each_SingleItem_IsFirstAndLast()
    {
        var data = new ObjectValue
        {
            ["items"] = new ArrayValue(new TemplateValue[]
            {
                new ObjectValue { ["n"] = new StringValue("only") }
            })
        };

        var result = _processor.Process("{{#each items}}{{#if @first}}F{{/if}}{{#if @last}}L{{/if}}{{/each}}", data);

        Assert.Equal("FL", result);
    }

    [Fact]
    public void Each_IndexBasedSeparator()
    {
        var data = new ObjectValue
        {
            ["items"] = new ArrayValue(new TemplateValue[]
            {
                new ObjectValue { ["n"] = new StringValue("A") },
                new ObjectValue { ["n"] = new StringValue("B") },
                new ObjectValue { ["n"] = new StringValue("C") }
            })
        };

        // Use @last to avoid trailing comma
        var result = _processor.Process("{{#each items}}{{n}}{{#if @last}}{{else}}, {{/if}}{{/each}}", data);

        Assert.Equal("A, B, C", result);
    }

    [Fact]
    public void Each_StringArray_IndexWorks()
    {
        var data = new ObjectValue
        {
            ["tags"] = new ArrayValue(new TemplateValue[]
            {
                new StringValue("red"),
                new StringValue("blue")
            })
        };

        var result = _processor.Process("{{#each tags}}#{{@index}} {{/each}}", data);

        Assert.Equal("#0 #1 ", result);
    }

    #endregion

    #region Nested {{#each}} Tests

    [Fact]
    public void Each_Nested_InnerLoopIteratesCorrectly()
    {
        var data = new ObjectValue
        {
            ["groups"] = new ArrayValue(new TemplateValue[]
            {
                new ObjectValue
                {
                    ["name"] = new StringValue("G1"),
                    ["items"] = new ArrayValue(new TemplateValue[]
                    {
                        new ObjectValue { ["val"] = new StringValue("a") },
                        new ObjectValue { ["val"] = new StringValue("b") }
                    })
                },
                new ObjectValue
                {
                    ["name"] = new StringValue("G2"),
                    ["items"] = new ArrayValue(new TemplateValue[]
                    {
                        new ObjectValue { ["val"] = new StringValue("c") }
                    })
                }
            })
        };

        var result = _processor.Process("{{#each groups}}[{{#each items}}{{val}}{{/each}}]{{/each}}", data);

        Assert.Equal("[ab][c]", result);
    }

    [Fact]
    public void Each_Nested_InnerIndexIsIndependent()
    {
        var data = new ObjectValue
        {
            ["rows"] = new ArrayValue(new TemplateValue[]
            {
                new ObjectValue
                {
                    ["cells"] = new ArrayValue(new TemplateValue[]
                    {
                        new StringValue("x"),
                        new StringValue("y")
                    })
                },
                new ObjectValue
                {
                    ["cells"] = new ArrayValue(new TemplateValue[]
                    {
                        new StringValue("z")
                    })
                }
            })
        };

        var result = _processor.Process("{{#each rows}}({{#each cells}}{{@index}}{{/each}}){{/each}}", data);

        Assert.Equal("(01)(0)", result);
    }

    #endregion

    #region {{#if}} inside {{#each}} Tests

    [Fact]
    public void Each_WithInnerIf_ConditionallyRendersItems()
    {
        var data = new ObjectValue
        {
            ["users"] = new ArrayValue(new TemplateValue[]
            {
                new ObjectValue
                {
                    ["name"] = new StringValue("Alice"),
                    ["active"] = new BoolValue(true)
                },
                new ObjectValue
                {
                    ["name"] = new StringValue("Bob"),
                    ["active"] = new BoolValue(false)
                },
                new ObjectValue
                {
                    ["name"] = new StringValue("Charlie"),
                    ["active"] = new BoolValue(true)
                }
            })
        };

        var result = _processor.Process("{{#each users}}{{#if active}}{{name}} {{/if}}{{/each}}", data);

        Assert.Equal("Alice Charlie ", result);
    }

    [Fact]
    public void Each_WithInnerIfElse_ShowsAlternateContent()
    {
        var data = new ObjectValue
        {
            ["items"] = new ArrayValue(new TemplateValue[]
            {
                new ObjectValue
                {
                    ["name"] = new StringValue("A"),
                    ["special"] = new BoolValue(true)
                },
                new ObjectValue
                {
                    ["name"] = new StringValue("B"),
                    ["special"] = new BoolValue(false)
                }
            })
        };

        var result = _processor.Process("{{#each items}}{{#if special}}*{{name}}*{{else}}{{name}}{{/if}} {{/each}}", data);

        Assert.Equal("*A* B ", result);
    }

    #endregion

    #region {{#each}} inside {{#if}} Tests

    [Fact]
    public void If_WithInnerEach_RendersLoopWhenTruthy()
    {
        var data = new ObjectValue
        {
            ["show"] = new BoolValue(true),
            ["items"] = new ArrayValue(new TemplateValue[]
            {
                new ObjectValue { ["n"] = new StringValue("X") },
                new ObjectValue { ["n"] = new StringValue("Y") }
            })
        };

        var result = _processor.Process("{{#if show}}Items: {{#each items}}{{n}}{{/each}}{{/if}}", data);

        Assert.Equal("Items: XY", result);
    }

    [Fact]
    public void If_WithInnerEach_HidesLoopWhenFalsy()
    {
        var data = new ObjectValue
        {
            ["show"] = new BoolValue(false),
            ["items"] = new ArrayValue(new TemplateValue[]
            {
                new ObjectValue { ["n"] = new StringValue("X") }
            })
        };

        var result = _processor.Process("{{#if show}}Items: {{#each items}}{{n}}{{/each}}{{/if}}", data);

        Assert.Equal("", result);
    }

    #endregion

    #region Nesting Depth Limit

    [Fact]
    public void If_ExceedsNestingDepth_Throws()
    {
        var limits = new ResourceLimits { MaxTemplateNestingDepth = 2 };
        var processor = new TemplateProcessor(limits, FilterRegistry.CreateDefault());

        var data = new ObjectValue
        {
            ["a"] = new BoolValue(true)
        };

        // 3 levels deep: should exceed depth of 2
        var template = "{{#if a}}{{#if a}}{{#if a}}deep{{/if}}{{/if}}{{/if}}";

        Assert.Throws<TemplateEngineException>(() => processor.Process(template, data));
    }

    [Fact]
    public void Each_ExceedsNestingDepth_Throws()
    {
        var limits = new ResourceLimits { MaxTemplateNestingDepth = 1 };
        var processor = new TemplateProcessor(limits, FilterRegistry.CreateDefault());

        var data = new ObjectValue
        {
            ["items"] = new ArrayValue(new TemplateValue[]
            {
                new ObjectValue
                {
                    ["sub"] = new ArrayValue(new TemplateValue[]
                    {
                        new StringValue("x")
                    })
                }
            })
        };

        var template = "{{#each items}}{{#each sub}}{{@index}}{{/each}}{{/each}}";

        Assert.Throws<TemplateEngineException>(() => processor.Process(template, data));
    }

    [Fact]
    public void If_AtExactNestingDepth_Succeeds()
    {
        var limits = new ResourceLimits { MaxTemplateNestingDepth = 2 };
        var processor = new TemplateProcessor(limits, FilterRegistry.CreateDefault());

        var data = new ObjectValue
        {
            ["a"] = new BoolValue(true)
        };

        // 2 levels deep: should be exactly at the limit
        var template = "{{#if a}}{{#if a}}ok{{/if}}{{/if}}";

        var result = processor.Process(template, data);

        Assert.Equal("ok", result);
    }

    #endregion

    #region Unclosed Block Errors

    [Fact]
    public void If_Unclosed_Throws()
    {
        var data = new ObjectValue();

        Assert.Throws<TemplateEngineException>(() => _processor.Process("{{#if name}}Hello", data));
    }

    [Fact]
    public void Each_Unclosed_Throws()
    {
        var data = new ObjectValue
        {
            ["items"] = new ArrayValue(new TemplateValue[] { new StringValue("a") })
        };

        Assert.Throws<TemplateEngineException>(() => _processor.Process("{{#each items}}{{@index}}", data));
    }

    [Fact]
    public void If_UnclosedNested_Throws()
    {
        var data = new ObjectValue
        {
            ["a"] = new BoolValue(true)
        };

        Assert.Throws<TemplateEngineException>(() =>
            _processor.Process("{{#if a}}{{#if a}}inner{{/if}}", data));
    }

    #endregion

    #region Text Around Blocks

    [Fact]
    public void If_TextBeforeAndAfterBlock_Preserved()
    {
        var data = new ObjectValue
        {
            ["name"] = new StringValue("Alice")
        };

        var result = _processor.Process("Hello {{#if name}}{{name}}{{/if}}!", data);

        Assert.Equal("Hello Alice!", result);
    }

    [Fact]
    public void If_TextBeforeAndAfterBlock_PreservedWhenFalsy()
    {
        var data = new ObjectValue();

        var result = _processor.Process("Hello {{#if name}}{{name}}{{/if}}!", data);

        Assert.Equal("Hello !", result);
    }

    [Fact]
    public void Each_TextBeforeAndAfterBlock_Preserved()
    {
        var data = new ObjectValue
        {
            ["items"] = new ArrayValue(new TemplateValue[]
            {
                new ObjectValue { ["n"] = new StringValue("A") },
                new ObjectValue { ["n"] = new StringValue("B") }
            })
        };

        var result = _processor.Process("Items: {{#each items}}{{n}},{{/each}} done", data);

        Assert.Equal("Items: A,B, done", result);
    }

    [Fact]
    public void MultipleIfBlocks_InSameTemplate()
    {
        var data = new ObjectValue
        {
            ["first"] = new StringValue("A"),
            ["third"] = new StringValue("C")
        };

        var result = _processor.Process(
            "{{#if first}}1{{/if}}-{{#if second}}2{{/if}}-{{#if third}}3{{/if}}", data);

        Assert.Equal("1--3", result);
    }

    [Fact]
    public void MultipleEachBlocks_InSameTemplate()
    {
        var data = new ObjectValue
        {
            ["xs"] = new ArrayValue(new TemplateValue[]
            {
                new ObjectValue { ["v"] = new StringValue("a") },
                new ObjectValue { ["v"] = new StringValue("b") }
            }),
            ["ys"] = new ArrayValue(new TemplateValue[]
            {
                new ObjectValue { ["v"] = new StringValue("c") }
            })
        };

        var result = _processor.Process(
            "[{{#each xs}}{{v}}{{/each}}][{{#each ys}}{{v}}{{/each}}]", data);

        Assert.Equal("[ab][c]", result);
    }

    #endregion

    #region Edge Cases

    [Fact]
    public void If_TemplateWithNoBlocks_ReturnsLiteralText()
    {
        var data = new ObjectValue();

        var result = _processor.Process("plain text", data);

        Assert.Equal("plain text", result);
    }

    [Fact]
    public void If_EmptyTemplate_ReturnsEmpty()
    {
        var data = new ObjectValue();

        var result = _processor.Process("", data);

        Assert.Equal("", result);
    }

    [Fact]
    public void If_BlockWithOnlyLiteralText_ShowsWhenTruthy()
    {
        var data = new ObjectValue
        {
            ["flag"] = new BoolValue(true)
        };

        var result = _processor.Process("{{#if flag}}static text{{/if}}", data);

        Assert.Equal("static text", result);
    }

    [Fact]
    public void Each_ObjectProperties_AccessibleInLoop()
    {
        var data = new ObjectValue
        {
            ["people"] = new ArrayValue(new TemplateValue[]
            {
                new ObjectValue
                {
                    ["first"] = new StringValue("John"),
                    ["last"] = new StringValue("Doe")
                },
                new ObjectValue
                {
                    ["first"] = new StringValue("Jane"),
                    ["last"] = new StringValue("Smith")
                }
            })
        };

        var result = _processor.Process("{{#each people}}{{first}} {{last}} {{/each}}", data);

        Assert.Equal("John Doe Jane Smith ", result);
    }

    #endregion
}
