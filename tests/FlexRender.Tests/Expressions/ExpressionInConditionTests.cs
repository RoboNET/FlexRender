// Tests for expression support in IfElement conditions.
// When the condition field contains an expression (not just a path),
// it should be evaluated as a full expression before comparison.
//
// Compilation status: WILL NOT COMPILE until expression support in conditions
// is implemented in TemplateExpander.

using FlexRender.Configuration;
using FlexRender.Parsing.Ast;
using FlexRender.TemplateEngine;
using Xunit;

namespace FlexRender.Tests.Expressions;

/// <summary>
/// Tests for expression evaluation in IfElement conditions.
/// </summary>
public sealed class ExpressionInConditionTests
{
    private readonly TemplateExpander _expander = new(new ResourceLimits(), FilterRegistry.CreateDefault());

    private static Template CreateTemplate(params TemplateElement[] elements)
    {
        var template = new Template();
        foreach (var element in elements)
        {
            template.AddElement(element);
        }
        return template;
    }

    [Fact]
    public void Expand_IfWithArithmeticCondition_EvaluatesExpression()
    {
        // condition: price * quantity, greaterThan: 100
        var ifElem = new IfElement(
            new List<TemplateElement> { new TextElement { Content = "Free shipping!" } },
            new List<TemplateElement> { new TextElement { Content = "Shipping: $5" } })
        {
            ConditionPath = "price * quantity",
            Operator = ConditionOperator.GreaterThan,
            CompareValue = 100.0
        };

        var template = CreateTemplate(ifElem);
        var data = new ObjectValue
        {
            ["price"] = new NumberValue(25),
            ["quantity"] = new NumberValue(5)
        };

        // Act
        var result = _expander.Expand(template, data);

        // Assert: 25 * 5 = 125 > 100 => true => "Free shipping!"
        Assert.Single(result.Elements);
        var text = Assert.IsType<TextElement>(result.Elements[0]);
        Assert.Equal("Free shipping!", text.Content);
    }

    [Fact]
    public void Expand_IfWithArithmeticCondition_FalseCase()
    {
        var ifElem = new IfElement(
            new List<TemplateElement> { new TextElement { Content = "Free shipping!" } },
            new List<TemplateElement> { new TextElement { Content = "Shipping: $5" } })
        {
            ConditionPath = "price * quantity",
            Operator = ConditionOperator.GreaterThan,
            CompareValue = 100.0
        };

        var template = CreateTemplate(ifElem);
        var data = new ObjectValue
        {
            ["price"] = new NumberValue(5),
            ["quantity"] = new NumberValue(2)
        };

        // Act
        var result = _expander.Expand(template, data);

        // Assert: 5 * 2 = 10 > 100 => false => "Shipping: $5"
        Assert.Single(result.Elements);
        var text = Assert.IsType<TextElement>(result.Elements[0]);
        Assert.Equal("Shipping: $5", text.Content);
    }

    [Fact]
    public void Expand_IfWithSubtractionCondition_EvaluatesExpression()
    {
        var ifElem = new IfElement(
            new List<TemplateElement> { new TextElement { Content = "Positive balance" } },
            new List<TemplateElement> { new TextElement { Content = "No balance" } })
        {
            ConditionPath = "total - spent",
            Operator = ConditionOperator.GreaterThan,
            CompareValue = 0.0
        };

        var template = CreateTemplate(ifElem);
        var data = new ObjectValue
        {
            ["total"] = new NumberValue(100),
            ["spent"] = new NumberValue(30)
        };

        // Act
        var result = _expander.Expand(template, data);

        // Assert: 100 - 30 = 70 > 0 => true
        Assert.Single(result.Elements);
        var text = Assert.IsType<TextElement>(result.Elements[0]);
        Assert.Equal("Positive balance", text.Content);
    }

    [Fact]
    public void Expand_IfWithTruthyExpressionCondition_EvaluatesArithmeticResult()
    {
        // Truthy check on arithmetic expression: price * quantity
        // Non-zero number is truthy
        var ifElem = new IfElement(
            new List<TemplateElement> { new TextElement { Content = "Has value" } },
            new List<TemplateElement> { new TextElement { Content = "No value" } })
        {
            ConditionPath = "price * quantity"
        };

        var template = CreateTemplate(ifElem);
        var data = new ObjectValue
        {
            ["price"] = new NumberValue(10),
            ["quantity"] = new NumberValue(3)
        };

        // Act
        var result = _expander.Expand(template, data);

        // Assert: 10 * 3 = 30, truthy => "Has value"
        Assert.Single(result.Elements);
        var text = Assert.IsType<TextElement>(result.Elements[0]);
        Assert.Equal("Has value", text.Content);
    }

    [Fact]
    public void Expand_IfWithNullCoalesceCondition_EvaluatesExpression()
    {
        var ifElem = new IfElement(
            new List<TemplateElement> { new TextElement { Content = "Has name" } },
            new List<TemplateElement> { new TextElement { Content = "Guest" } })
        {
            ConditionPath = "name ?? nickname"
        };

        var template = CreateTemplate(ifElem);
        var data = new ObjectValue
        {
            // name is missing, nickname exists
            ["nickname"] = new StringValue("JohnD")
        };

        // Act
        var result = _expander.Expand(template, data);

        // Assert: name is null, nickname is "JohnD" => truthy => "Has name"
        Assert.Single(result.Elements);
        var text = Assert.IsType<TextElement>(result.Elements[0]);
        Assert.Equal("Has name", text.Content);
    }
}
