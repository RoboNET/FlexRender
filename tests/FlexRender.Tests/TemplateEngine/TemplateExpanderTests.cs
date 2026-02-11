using FlexRender.Configuration;
using FlexRender.Parsing.Ast;
using FlexRender.TemplateEngine;
using Xunit;

namespace FlexRender.Tests.TemplateEngine;

/// <summary>
/// Tests for TemplateExpander.
/// </summary>
public sealed class TemplateExpanderTests
{
    private readonly TemplateExpander _expander = new();

    private static Template CreateTemplate(params TemplateElement[] elements)
    {
        var template = new Template();
        foreach (var element in elements)
        {
            template.AddElement(element);
        }
        return template;
    }

    // === Each Expansion Tests ===

    [Fact]
    public void Expand_NoControlFlow_ReturnsUnchanged()
    {
        var template = CreateTemplate(
            new TextElement { Content = "Hello" },
            new TextElement { Content = "World" }
        );
        var data = new ObjectValue();

        var result = _expander.Expand(template, data);

        Assert.Equal(2, result.Elements.Count);
        Assert.Equal("Hello", ((TextElement)result.Elements[0]).Content);
        Assert.Equal("World", ((TextElement)result.Elements[1]).Content);
    }

    [Fact]
    public void Expand_EachWithArray_CreatesElementsPerItem()
    {
        var each = new EachElement(new List<TemplateElement>
        {
            new TextElement { Content = "{{name}}" }
        })
        {
            ArrayPath = "items"
        };

        var template = CreateTemplate(each);
        var data = new ObjectValue
        {
            ["items"] = new ArrayValue(new List<TemplateValue>
            {
                new ObjectValue { ["name"] = new StringValue("Item1") },
                new ObjectValue { ["name"] = new StringValue("Item2") },
                new ObjectValue { ["name"] = new StringValue("Item3") }
            })
        };

        var result = _expander.Expand(template, data);

        Assert.Equal(3, result.Elements.Count);
        Assert.All(result.Elements, e => Assert.IsType<TextElement>(e));
    }

    [Fact]
    public void Expand_EachWithEmptyArray_ReturnsEmpty()
    {
        var each = new EachElement(new List<TemplateElement>
        {
            new TextElement { Content = "test" }
        })
        {
            ArrayPath = "items"
        };

        var template = CreateTemplate(each);
        var data = new ObjectValue
        {
            ["items"] = new ArrayValue(new List<TemplateValue>())
        };

        var result = _expander.Expand(template, data);

        Assert.Empty(result.Elements);
    }

    [Fact]
    public void Expand_EachWithMissingArray_ReturnsEmpty()
    {
        var each = new EachElement(new List<TemplateElement>
        {
            new TextElement { Content = "test" }
        })
        {
            ArrayPath = "nonexistent"
        };

        var template = CreateTemplate(each);
        var data = new ObjectValue();

        var result = _expander.Expand(template, data);

        Assert.Empty(result.Elements);
    }

    [Fact]
    public void Expand_EachWithItemVariable_SetsVariableInScope()
    {
        var each = new EachElement(new List<TemplateElement>
        {
            new TextElement { Content = "{{item.name}}" }
        })
        {
            ArrayPath = "items",
            ItemVariable = "item"
        };

        var template = CreateTemplate(each);
        var data = new ObjectValue
        {
            ["items"] = new ArrayValue(new List<TemplateValue>
            {
                new ObjectValue { ["name"] = new StringValue("First") },
                new ObjectValue { ["name"] = new StringValue("Second") }
            })
        };

        var result = _expander.Expand(template, data);

        Assert.Equal(2, result.Elements.Count);
        // Verify variables are substituted during expansion
        var text1 = Assert.IsType<TextElement>(result.Elements[0]);
        var text2 = Assert.IsType<TextElement>(result.Elements[1]);
        Assert.Equal("First", text1.Content);
        Assert.Equal("Second", text2.Content);
    }

    // === If Expansion Tests ===

    [Fact]
    public void Expand_IfTruthy_ReturnsThenBranch()
    {
        var ifElem = new IfElement(
            new List<TemplateElement> { new TextElement { Content = "Visible" } },
            new List<TemplateElement> { new TextElement { Content = "Hidden" } })
        {
            ConditionPath = "show"
        };

        var template = CreateTemplate(ifElem);
        var data = new ObjectValue { ["show"] = new BoolValue(true) };

        var result = _expander.Expand(template, data);

        Assert.Single(result.Elements);
        Assert.Equal("Visible", ((TextElement)result.Elements[0]).Content);
    }

    [Fact]
    public void Expand_IfFalsy_ReturnsElseBranch()
    {
        var ifElem = new IfElement(
            new List<TemplateElement> { new TextElement { Content = "Visible" } },
            new List<TemplateElement> { new TextElement { Content = "Hidden" } })
        {
            ConditionPath = "show"
        };

        var template = CreateTemplate(ifElem);
        var data = new ObjectValue { ["show"] = new BoolValue(false) };

        var result = _expander.Expand(template, data);

        Assert.Single(result.Elements);
        Assert.Equal("Hidden", ((TextElement)result.Elements[0]).Content);
    }

    [Fact]
    public void Expand_IfEquals_MatchingValue_ReturnsThen()
    {
        var ifElem = new IfElement(
            new List<TemplateElement> { new TextElement { Content = "Paid" } },
            new List<TemplateElement> { new TextElement { Content = "Not paid" } })
        {
            ConditionPath = "status",
            Operator = ConditionOperator.Equals,
            CompareValue = "paid"
        };

        var template = CreateTemplate(ifElem);
        var data = new ObjectValue { ["status"] = new StringValue("paid") };

        var result = _expander.Expand(template, data);

        Assert.Equal("Paid", ((TextElement)result.Elements[0]).Content);
    }

    [Fact]
    public void Expand_IfEquals_NonMatchingValue_ReturnsElse()
    {
        var ifElem = new IfElement(
            new List<TemplateElement> { new TextElement { Content = "Paid" } },
            new List<TemplateElement> { new TextElement { Content = "Not paid" } })
        {
            ConditionPath = "status",
            Operator = ConditionOperator.Equals,
            CompareValue = "paid"
        };

        var template = CreateTemplate(ifElem);
        var data = new ObjectValue { ["status"] = new StringValue("pending") };

        var result = _expander.Expand(template, data);

        Assert.Equal("Not paid", ((TextElement)result.Elements[0]).Content);
    }

    [Fact]
    public void Expand_IfNotEquals_MatchingValue_ReturnsElse()
    {
        var ifElem = new IfElement(
            new List<TemplateElement> { new TextElement { Content = "Active" } },
            new List<TemplateElement> { new TextElement { Content = "Cancelled" } })
        {
            ConditionPath = "status",
            Operator = ConditionOperator.NotEquals,
            CompareValue = "cancelled"
        };

        var template = CreateTemplate(ifElem);
        var data = new ObjectValue { ["status"] = new StringValue("cancelled") };

        var result = _expander.Expand(template, data);

        Assert.Equal("Cancelled", ((TextElement)result.Elements[0]).Content);
    }

    [Fact]
    public void Expand_IfNotEquals_NonMatchingValue_ReturnsThen()
    {
        var ifElem = new IfElement(
            new List<TemplateElement> { new TextElement { Content = "Active" } },
            new List<TemplateElement> { new TextElement { Content = "Cancelled" } })
        {
            ConditionPath = "status",
            Operator = ConditionOperator.NotEquals,
            CompareValue = "cancelled"
        };

        var template = CreateTemplate(ifElem);
        var data = new ObjectValue { ["status"] = new StringValue("active") };

        var result = _expander.Expand(template, data);

        Assert.Equal("Active", ((TextElement)result.Elements[0]).Content);
    }

    [Fact]
    public void Expand_IfElseIf_EvaluatesChain()
    {
        var elseIf = new IfElement(
            new List<TemplateElement> { new TextElement { Content = "Pending" } })
        {
            ConditionPath = "status",
            Operator = ConditionOperator.Equals,
            CompareValue = "pending"
        };

        var ifElem = new IfElement(
            new List<TemplateElement> { new TextElement { Content = "Paid" } },
            new List<TemplateElement> { new TextElement { Content = "Other" } })
        {
            ConditionPath = "status",
            Operator = ConditionOperator.Equals,
            CompareValue = "paid",
            ElseIf = elseIf
        };

        var template = CreateTemplate(ifElem);
        var data = new ObjectValue { ["status"] = new StringValue("pending") };

        var result = _expander.Expand(template, data);

        Assert.Single(result.Elements);
        Assert.Equal("Pending", ((TextElement)result.Elements[0]).Content);
    }

    [Fact]
    public void Expand_IfMissingPath_ReturnsFalsy()
    {
        var ifElem = new IfElement(
            new List<TemplateElement> { new TextElement { Content = "Visible" } },
            new List<TemplateElement> { new TextElement { Content = "Hidden" } })
        {
            ConditionPath = "nonexistent"
        };

        var template = CreateTemplate(ifElem);
        var data = new ObjectValue();

        var result = _expander.Expand(template, data);

        Assert.Single(result.Elements);
        Assert.Equal("Hidden", ((TextElement)result.Elements[0]).Content);
    }

    // === Nested Expansion Tests ===

    [Fact]
    public void Expand_NestedEachInFlex_ExpandsCorrectly()
    {
        var each = new EachElement(new List<TemplateElement>
        {
            new TextElement { Content = "item" }
        })
        {
            ArrayPath = "items"
        };

        var flex = new FlexElement { Children = new List<TemplateElement> { each } };

        var template = CreateTemplate(flex);
        var data = new ObjectValue
        {
            ["items"] = new ArrayValue(new List<TemplateValue>
            {
                new ObjectValue { ["name"] = new StringValue("A") },
                new ObjectValue { ["name"] = new StringValue("B") }
            })
        };

        var result = _expander.Expand(template, data);

        Assert.Single(result.Elements);
        var resultFlex = Assert.IsType<FlexElement>(result.Elements[0]);
        Assert.Equal(2, resultFlex.Children.Count);
    }

    [Fact]
    public void Expand_NestedIfInEach_ExpandsCorrectly()
    {
        var ifElem = new IfElement(
            new List<TemplateElement> { new TextElement { Content = "active" } },
            new List<TemplateElement> { new TextElement { Content = "inactive" } })
        {
            ConditionPath = "active"
        };

        var each = new EachElement(new List<TemplateElement> { ifElem })
        {
            ArrayPath = "items"
        };

        var template = CreateTemplate(each);
        var data = new ObjectValue
        {
            ["items"] = new ArrayValue(new List<TemplateValue>
            {
                new ObjectValue { ["active"] = new BoolValue(true) },
                new ObjectValue { ["active"] = new BoolValue(false) }
            })
        };

        var result = _expander.Expand(template, data);

        Assert.Equal(2, result.Elements.Count);
        var text1 = Assert.IsType<TextElement>(result.Elements[0]);
        var text2 = Assert.IsType<TextElement>(result.Elements[1]);
        Assert.Equal("active", text1.Content);
        Assert.Equal("inactive", text2.Content);
    }

    // === Resource Limits Tests ===

    [Fact]
    public void Expand_ExceedsMaxDepth_ThrowsException()
    {
        var limits = new ResourceLimits { MaxRenderDepth = 2 };
        var expander = new TemplateExpander(limits);

        // Create deeply nested each elements
        var innerEach = new EachElement(new List<TemplateElement>
        {
            new TextElement { Content = "inner" }
        })
        {
            ArrayPath = "items"
        };

        var middleEach = new EachElement(new List<TemplateElement> { innerEach })
        {
            ArrayPath = "items"
        };

        var outerEach = new EachElement(new List<TemplateElement> { middleEach })
        {
            ArrayPath = "items"
        };

        var template = CreateTemplate(outerEach);
        var data = new ObjectValue
        {
            ["items"] = new ArrayValue(new List<TemplateValue> { new ObjectValue() })
        };

        Assert.Throws<TemplateEngineException>(() => expander.Expand(template, data));
    }

    [Fact]
    public void Expand_DeeplyNestedEach_WithLowLimit_Throws()
    {
        // Create deeply nested Each elements (20 levels)
        TemplateElement current = new TextElement { Content = "deep" };
        for (var i = 0; i < 20; i++)
        {
            current = new EachElement(new List<TemplateElement> { current })
            {
                ArrayPath = "items"
            };
        }

        var template = CreateTemplate(current);

        // Create nested data
        var innerArray = new ArrayValue(new List<TemplateValue> { new StringValue("x") });
        var data = new ObjectValue
        {
            ["items"] = innerArray
        };

        var expander = new TemplateExpander(new ResourceLimits { MaxRenderDepth = 10 });

        Assert.Throws<TemplateEngineException>(() => expander.Expand(template, data));
    }

    // === Regular Element Pass-through Tests ===

    [Fact]
    public void Expand_RegularElements_PassesThrough()
    {
        var template = CreateTemplate(
            new TextElement { Content = "Hello" },
            new SeparatorElement(),
            new TextElement { Content = "World" }
        );

        var data = new ObjectValue();

        var result = _expander.Expand(template, data);

        Assert.Equal(3, result.Elements.Count);
        Assert.IsType<TextElement>(result.Elements[0]);
        Assert.IsType<SeparatorElement>(result.Elements[1]);
        Assert.IsType<TextElement>(result.Elements[2]);
    }

    // === Null Argument Tests ===

    [Fact]
    public void Expand_NullTemplate_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => _expander.Expand(null!, new ObjectValue()));
    }

    [Fact]
    public void Expand_NullData_ThrowsArgumentNullException()
    {
        var template = CreateTemplate();
        Assert.Throws<ArgumentNullException>(() => _expander.Expand(template, null!));
    }

    // === Template Metadata Preservation Tests ===

    [Fact]
    public void Expand_PreservesTemplateMetadata()
    {
        var template = new Template
        {
            Name = "TestTemplate",
            Version = 2,
            Canvas = new CanvasSettings { Width = 500, Background = "#FFFFFF" }
        };
        template.Fonts["custom"] = new FontDefinition { Path = "test.ttf" };
        template.AddElement(new TextElement { Content = "test" });

        var data = new ObjectValue();

        var result = _expander.Expand(template, data);

        Assert.Equal("TestTemplate", result.Name);
        Assert.Equal(2, result.Version);
        Assert.Equal(500, result.Canvas.Width);
        Assert.Equal("#FFFFFF", result.Canvas.Background.Value);
        Assert.True(result.Fonts.ContainsKey("custom"));
    }

    // === Constructor Tests ===

    [Fact]
    public void Constructor_WithNullLimits_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => new TemplateExpander(null!));
    }

    [Fact]
    public void Constructor_WithDefaultLimits_Succeeds()
    {
        var expander = new TemplateExpander();
        Assert.NotNull(expander);
    }

    [Fact]
    public void Constructor_WithCustomLimits_Succeeds()
    {
        var limits = new ResourceLimits { MaxRenderDepth = 50 };
        var expander = new TemplateExpander(limits);
        Assert.NotNull(expander);
    }

    // === Variable Substitution Tests ===

    [Fact]
    public void Expand_EachWithDirectAccess_SubstitutesVariables()
    {
        var each = new EachElement(new List<TemplateElement>
        {
            new TextElement { Content = "{{name}} - {{price}}" }
        })
        {
            ArrayPath = "items"
        };

        var template = CreateTemplate(each);
        var data = new ObjectValue
        {
            ["items"] = new ArrayValue(new List<TemplateValue>
            {
                new ObjectValue { ["name"] = new StringValue("Coffee"), ["price"] = new StringValue("$4.50") },
                new ObjectValue { ["name"] = new StringValue("Tea"), ["price"] = new StringValue("$3.00") }
            })
        };

        var result = _expander.Expand(template, data);

        Assert.Equal(2, result.Elements.Count);
        var text1 = Assert.IsType<TextElement>(result.Elements[0]);
        var text2 = Assert.IsType<TextElement>(result.Elements[1]);
        Assert.Equal("Coffee - $4.50", text1.Content);
        Assert.Equal("Tea - $3.00", text2.Content);
    }

    [Fact]
    public void Expand_NestedEach_SubstitutesVariablesFromCurrentScope()
    {
        // Inner loop accesses variables from its current scope (the inner item)
        var innerEach = new EachElement(new List<TemplateElement>
        {
            new TextElement { Content = "{{name}} ({{price}})" }
        })
        {
            ArrayPath = "products"
        };

        // Outer loop iterates over categories
        var outerEach = new EachElement(new List<TemplateElement> { innerEach })
        {
            ArrayPath = "categories"
        };

        var template = CreateTemplate(outerEach);
        var data = new ObjectValue
        {
            ["categories"] = new ArrayValue(new List<TemplateValue>
            {
                new ObjectValue
                {
                    ["category"] = new StringValue("Drinks"),
                    ["products"] = new ArrayValue(new List<TemplateValue>
                    {
                        new ObjectValue { ["name"] = new StringValue("Coffee"), ["price"] = new StringValue("$4") },
                        new ObjectValue { ["name"] = new StringValue("Tea"), ["price"] = new StringValue("$3") }
                    })
                }
            })
        };

        var result = _expander.Expand(template, data);

        Assert.Equal(2, result.Elements.Count);
        var text1 = Assert.IsType<TextElement>(result.Elements[0]);
        var text2 = Assert.IsType<TextElement>(result.Elements[1]);
        Assert.Equal("Coffee ($4)", text1.Content);
        Assert.Equal("Tea ($3)", text2.Content);
    }

    [Fact]
    public void Expand_QrElement_SubstitutesDataVariable()
    {
        var each = new EachElement(new List<TemplateElement>
        {
            new QrElement { Data = "{{url}}", Size = 100 }
        })
        {
            ArrayPath = "items"
        };

        var template = CreateTemplate(each);
        var data = new ObjectValue
        {
            ["items"] = new ArrayValue(new List<TemplateValue>
            {
                new ObjectValue { ["url"] = new StringValue("https://example.com/1") },
                new ObjectValue { ["url"] = new StringValue("https://example.com/2") }
            })
        };

        var result = _expander.Expand(template, data);

        Assert.Equal(2, result.Elements.Count);
        var qr1 = Assert.IsType<QrElement>(result.Elements[0]);
        var qr2 = Assert.IsType<QrElement>(result.Elements[1]);
        Assert.Equal("https://example.com/1", qr1.Data);
        Assert.Equal("https://example.com/2", qr2.Data);
    }

    [Fact]
    public void Expand_ImageElement_SubstitutesSourceVariable()
    {
        var each = new EachElement(new List<TemplateElement>
        {
            new ImageElement { Src = "{{imagePath}}" }
        })
        {
            ArrayPath = "items"
        };

        var template = CreateTemplate(each);
        var data = new ObjectValue
        {
            ["items"] = new ArrayValue(new List<TemplateValue>
            {
                new ObjectValue { ["imagePath"] = new StringValue("/images/product1.png") },
                new ObjectValue { ["imagePath"] = new StringValue("/images/product2.png") }
            })
        };

        var result = _expander.Expand(template, data);

        Assert.Equal(2, result.Elements.Count);
        var img1 = Assert.IsType<ImageElement>(result.Elements[0]);
        var img2 = Assert.IsType<ImageElement>(result.Elements[1]);
        Assert.Equal("/images/product1.png", img1.Src);
        Assert.Equal("/images/product2.png", img2.Src);
    }

    [Fact]
    public void Expand_BarcodeElement_SubstitutesDataVariable()
    {
        var template = CreateTemplate(new BarcodeElement { Data = "{{barcode}}" });
        var data = new ObjectValue
        {
            ["barcode"] = new StringValue("1234567890")
        };

        var result = _expander.Expand(template, data);

        Assert.Single(result.Elements);
        var barcode = Assert.IsType<BarcodeElement>(result.Elements[0]);
        Assert.Equal("1234567890", barcode.Data);
    }

    [Fact]
    public void Expand_TextWithNoVariables_PreservesContent()
    {
        var template = CreateTemplate(new TextElement { Content = "Hello World" });
        var data = new ObjectValue();

        var result = _expander.Expand(template, data);

        var text = Assert.IsType<TextElement>(result.Elements[0]);
        Assert.Equal("Hello World", text.Content);
    }

    [Fact]
    public void Expand_TextWithMissingVariable_ReplacesWithEmpty()
    {
        var template = CreateTemplate(new TextElement { Content = "Hello {{name}}" });
        var data = new ObjectValue();

        var result = _expander.Expand(template, data);

        var text = Assert.IsType<TextElement>(result.Elements[0]);
        Assert.Equal("Hello ", text.Content);
    }
}
