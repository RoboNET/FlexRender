using FlexRender.Parsing.Ast;
using FlexRender.TemplateEngine;
using Xunit;

namespace FlexRender.Tests.TemplateEngine;

/// <summary>
/// Tests for extended condition operators in TemplateExpander.
/// </summary>
public sealed class ConditionOperatorTests
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

    private static IfElement CreateIfElement(
        string conditionPath,
        ConditionOperator? op,
        object? compareValue,
        string thenContent = "true",
        string elseContent = "false")
    {
        return new IfElement(
            new List<TemplateElement> { new TextElement { Content = thenContent } },
            new List<TemplateElement> { new TextElement { Content = elseContent } })
        {
            ConditionPath = conditionPath,
            Operator = op,
            CompareValue = compareValue
        };
    }

    private static string GetResultContent(Template result)
    {
        return ((TextElement)result.Elements[0]).Content;
    }

    // === In Operator Tests ===

    [Fact]
    public void In_ValueInList_ReturnsTrue()
    {
        var ifElem = CreateIfElement("status", ConditionOperator.In, new List<string> { "paid", "completed", "shipped" });
        var template = CreateTemplate(ifElem);
        var data = new ObjectValue { ["status"] = new StringValue("paid") };

        var result = _expander.Expand(template, data);

        Assert.Equal("true", GetResultContent(result));
    }

    [Fact]
    public void In_ValueNotInList_ReturnsFalse()
    {
        var ifElem = CreateIfElement("status", ConditionOperator.In, new List<string> { "paid", "completed", "shipped" });
        var template = CreateTemplate(ifElem);
        var data = new ObjectValue { ["status"] = new StringValue("pending") };

        var result = _expander.Expand(template, data);

        Assert.Equal("false", GetResultContent(result));
    }

    [Fact]
    public void In_NumberValueInList_ReturnsTrue()
    {
        var ifElem = CreateIfElement("code", ConditionOperator.In, new List<string> { "100", "200", "300" });
        var template = CreateTemplate(ifElem);
        var data = new ObjectValue { ["code"] = new NumberValue(200) };

        var result = _expander.Expand(template, data);

        Assert.Equal("true", GetResultContent(result));
    }

    // === NotIn Operator Tests ===

    [Fact]
    public void NotIn_ValueNotInList_ReturnsTrue()
    {
        var ifElem = CreateIfElement("status", ConditionOperator.NotIn, new List<string> { "cancelled", "refunded" });
        var template = CreateTemplate(ifElem);
        var data = new ObjectValue { ["status"] = new StringValue("active") };

        var result = _expander.Expand(template, data);

        Assert.Equal("true", GetResultContent(result));
    }

    [Fact]
    public void NotIn_ValueInList_ReturnsFalse()
    {
        var ifElem = CreateIfElement("status", ConditionOperator.NotIn, new List<string> { "cancelled", "refunded" });
        var template = CreateTemplate(ifElem);
        var data = new ObjectValue { ["status"] = new StringValue("cancelled") };

        var result = _expander.Expand(template, data);

        Assert.Equal("false", GetResultContent(result));
    }

    // === Contains Operator Tests ===

    [Fact]
    public void Contains_ArrayContainsValue_ReturnsTrue()
    {
        var ifElem = CreateIfElement("roles", ConditionOperator.Contains, "admin");
        var template = CreateTemplate(ifElem);
        var data = new ObjectValue
        {
            ["roles"] = new ArrayValue(new List<TemplateValue>
            {
                new StringValue("user"),
                new StringValue("admin"),
                new StringValue("moderator")
            })
        };

        var result = _expander.Expand(template, data);

        Assert.Equal("true", GetResultContent(result));
    }

    [Fact]
    public void Contains_ArrayDoesNotContain_ReturnsFalse()
    {
        var ifElem = CreateIfElement("roles", ConditionOperator.Contains, "superadmin");
        var template = CreateTemplate(ifElem);
        var data = new ObjectValue
        {
            ["roles"] = new ArrayValue(new List<TemplateValue>
            {
                new StringValue("user"),
                new StringValue("admin")
            })
        };

        var result = _expander.Expand(template, data);

        Assert.Equal("false", GetResultContent(result));
    }

    [Fact]
    public void Contains_ArrayWithNumbers_ReturnsTrue()
    {
        var ifElem = CreateIfElement("ids", ConditionOperator.Contains, "42");
        var template = CreateTemplate(ifElem);
        var data = new ObjectValue
        {
            ["ids"] = new ArrayValue(new List<TemplateValue>
            {
                new NumberValue(10),
                new NumberValue(42),
                new NumberValue(100)
            })
        };

        var result = _expander.Expand(template, data);

        Assert.Equal("true", GetResultContent(result));
    }

    [Fact]
    public void Contains_NonArrayValue_ReturnsFalse()
    {
        var ifElem = CreateIfElement("name", ConditionOperator.Contains, "test");
        var template = CreateTemplate(ifElem);
        var data = new ObjectValue { ["name"] = new StringValue("test string") };

        var result = _expander.Expand(template, data);

        Assert.Equal("false", GetResultContent(result));
    }

    // === GreaterThan Operator Tests ===

    [Fact]
    public void GreaterThan_ValueGreater_ReturnsTrue()
    {
        var ifElem = CreateIfElement("amount", ConditionOperator.GreaterThan, 1000.0);
        var template = CreateTemplate(ifElem);
        var data = new ObjectValue { ["amount"] = new NumberValue(1500) };

        var result = _expander.Expand(template, data);

        Assert.Equal("true", GetResultContent(result));
    }

    [Fact]
    public void GreaterThan_ValueEqual_ReturnsFalse()
    {
        var ifElem = CreateIfElement("amount", ConditionOperator.GreaterThan, 1000.0);
        var template = CreateTemplate(ifElem);
        var data = new ObjectValue { ["amount"] = new NumberValue(1000) };

        var result = _expander.Expand(template, data);

        Assert.Equal("false", GetResultContent(result));
    }

    [Fact]
    public void GreaterThan_ValueLess_ReturnsFalse()
    {
        var ifElem = CreateIfElement("amount", ConditionOperator.GreaterThan, 1000.0);
        var template = CreateTemplate(ifElem);
        var data = new ObjectValue { ["amount"] = new NumberValue(500) };

        var result = _expander.Expand(template, data);

        Assert.Equal("false", GetResultContent(result));
    }

    // === GreaterThanOrEqual Operator Tests ===

    [Fact]
    public void GreaterThanOrEqual_ValueGreater_ReturnsTrue()
    {
        var ifElem = CreateIfElement("amount", ConditionOperator.GreaterThanOrEqual, 1000.0);
        var template = CreateTemplate(ifElem);
        var data = new ObjectValue { ["amount"] = new NumberValue(1500) };

        var result = _expander.Expand(template, data);

        Assert.Equal("true", GetResultContent(result));
    }

    [Fact]
    public void GreaterThanOrEqual_ValueEqual_ReturnsTrue()
    {
        var ifElem = CreateIfElement("amount", ConditionOperator.GreaterThanOrEqual, 1000.0);
        var template = CreateTemplate(ifElem);
        var data = new ObjectValue { ["amount"] = new NumberValue(1000) };

        var result = _expander.Expand(template, data);

        Assert.Equal("true", GetResultContent(result));
    }

    [Fact]
    public void GreaterThanOrEqual_ValueLess_ReturnsFalse()
    {
        var ifElem = CreateIfElement("amount", ConditionOperator.GreaterThanOrEqual, 1000.0);
        var template = CreateTemplate(ifElem);
        var data = new ObjectValue { ["amount"] = new NumberValue(999) };

        var result = _expander.Expand(template, data);

        Assert.Equal("false", GetResultContent(result));
    }

    // === LessThan Operator Tests ===

    [Fact]
    public void LessThan_ValueLess_ReturnsTrue()
    {
        var ifElem = CreateIfElement("discount", ConditionOperator.LessThan, 50.0);
        var template = CreateTemplate(ifElem);
        var data = new ObjectValue { ["discount"] = new NumberValue(25) };

        var result = _expander.Expand(template, data);

        Assert.Equal("true", GetResultContent(result));
    }

    [Fact]
    public void LessThan_ValueEqual_ReturnsFalse()
    {
        var ifElem = CreateIfElement("discount", ConditionOperator.LessThan, 50.0);
        var template = CreateTemplate(ifElem);
        var data = new ObjectValue { ["discount"] = new NumberValue(50) };

        var result = _expander.Expand(template, data);

        Assert.Equal("false", GetResultContent(result));
    }

    [Fact]
    public void LessThan_ValueGreater_ReturnsFalse()
    {
        var ifElem = CreateIfElement("discount", ConditionOperator.LessThan, 50.0);
        var template = CreateTemplate(ifElem);
        var data = new ObjectValue { ["discount"] = new NumberValue(75) };

        var result = _expander.Expand(template, data);

        Assert.Equal("false", GetResultContent(result));
    }

    // === LessThanOrEqual Operator Tests ===

    [Fact]
    public void LessThanOrEqual_ValueLess_ReturnsTrue()
    {
        var ifElem = CreateIfElement("discount", ConditionOperator.LessThanOrEqual, 50.0);
        var template = CreateTemplate(ifElem);
        var data = new ObjectValue { ["discount"] = new NumberValue(25) };

        var result = _expander.Expand(template, data);

        Assert.Equal("true", GetResultContent(result));
    }

    [Fact]
    public void LessThanOrEqual_ValueEqual_ReturnsTrue()
    {
        var ifElem = CreateIfElement("discount", ConditionOperator.LessThanOrEqual, 50.0);
        var template = CreateTemplate(ifElem);
        var data = new ObjectValue { ["discount"] = new NumberValue(50) };

        var result = _expander.Expand(template, data);

        Assert.Equal("true", GetResultContent(result));
    }

    [Fact]
    public void LessThanOrEqual_ValueGreater_ReturnsFalse()
    {
        var ifElem = CreateIfElement("discount", ConditionOperator.LessThanOrEqual, 50.0);
        var template = CreateTemplate(ifElem);
        var data = new ObjectValue { ["discount"] = new NumberValue(51) };

        var result = _expander.Expand(template, data);

        Assert.Equal("false", GetResultContent(result));
    }

    // === HasItems Operator Tests ===

    [Fact]
    public void HasItems_NonEmptyArray_ReturnsTrue()
    {
        var ifElem = CreateIfElement("items", ConditionOperator.HasItems, true);
        var template = CreateTemplate(ifElem);
        var data = new ObjectValue
        {
            ["items"] = new ArrayValue(new List<TemplateValue>
            {
                new StringValue("item1"),
                new StringValue("item2")
            })
        };

        var result = _expander.Expand(template, data);

        Assert.Equal("true", GetResultContent(result));
    }

    [Fact]
    public void HasItems_EmptyArray_ReturnsFalse()
    {
        var ifElem = CreateIfElement("items", ConditionOperator.HasItems, true);
        var template = CreateTemplate(ifElem);
        var data = new ObjectValue
        {
            ["items"] = new ArrayValue(new List<TemplateValue>())
        };

        var result = _expander.Expand(template, data);

        Assert.Equal("false", GetResultContent(result));
    }

    [Fact]
    public void HasItems_EmptyArrayExpectedFalse_ReturnsTrue()
    {
        var ifElem = CreateIfElement("items", ConditionOperator.HasItems, false);
        var template = CreateTemplate(ifElem);
        var data = new ObjectValue
        {
            ["items"] = new ArrayValue(new List<TemplateValue>())
        };

        var result = _expander.Expand(template, data);

        Assert.Equal("true", GetResultContent(result));
    }

    [Fact]
    public void HasItems_NonArrayValue_ReturnsFalse()
    {
        var ifElem = CreateIfElement("name", ConditionOperator.HasItems, true);
        var template = CreateTemplate(ifElem);
        var data = new ObjectValue { ["name"] = new StringValue("test") };

        var result = _expander.Expand(template, data);

        Assert.Equal("false", GetResultContent(result));
    }

    // === CountEquals Operator Tests ===

    [Fact]
    public void CountEquals_CorrectCount_ReturnsTrue()
    {
        var ifElem = CreateIfElement("items", ConditionOperator.CountEquals, 3);
        var template = CreateTemplate(ifElem);
        var data = new ObjectValue
        {
            ["items"] = new ArrayValue(new List<TemplateValue>
            {
                new StringValue("a"),
                new StringValue("b"),
                new StringValue("c")
            })
        };

        var result = _expander.Expand(template, data);

        Assert.Equal("true", GetResultContent(result));
    }

    [Fact]
    public void CountEquals_WrongCount_ReturnsFalse()
    {
        var ifElem = CreateIfElement("items", ConditionOperator.CountEquals, 5);
        var template = CreateTemplate(ifElem);
        var data = new ObjectValue
        {
            ["items"] = new ArrayValue(new List<TemplateValue>
            {
                new StringValue("a"),
                new StringValue("b")
            })
        };

        var result = _expander.Expand(template, data);

        Assert.Equal("false", GetResultContent(result));
    }

    [Fact]
    public void CountEquals_EmptyArrayZero_ReturnsTrue()
    {
        var ifElem = CreateIfElement("items", ConditionOperator.CountEquals, 0);
        var template = CreateTemplate(ifElem);
        var data = new ObjectValue
        {
            ["items"] = new ArrayValue(new List<TemplateValue>())
        };

        var result = _expander.Expand(template, data);

        Assert.Equal("true", GetResultContent(result));
    }

    // === CountGreaterThan Operator Tests ===

    [Fact]
    public void CountGreaterThan_CountGreater_ReturnsTrue()
    {
        var ifElem = CreateIfElement("attachments", ConditionOperator.CountGreaterThan, 0);
        var template = CreateTemplate(ifElem);
        var data = new ObjectValue
        {
            ["attachments"] = new ArrayValue(new List<TemplateValue>
            {
                new StringValue("file1.pdf"),
                new StringValue("file2.pdf")
            })
        };

        var result = _expander.Expand(template, data);

        Assert.Equal("true", GetResultContent(result));
    }

    [Fact]
    public void CountGreaterThan_CountEqual_ReturnsFalse()
    {
        var ifElem = CreateIfElement("attachments", ConditionOperator.CountGreaterThan, 2);
        var template = CreateTemplate(ifElem);
        var data = new ObjectValue
        {
            ["attachments"] = new ArrayValue(new List<TemplateValue>
            {
                new StringValue("file1.pdf"),
                new StringValue("file2.pdf")
            })
        };

        var result = _expander.Expand(template, data);

        Assert.Equal("false", GetResultContent(result));
    }

    [Fact]
    public void CountGreaterThan_CountLess_ReturnsFalse()
    {
        var ifElem = CreateIfElement("attachments", ConditionOperator.CountGreaterThan, 5);
        var template = CreateTemplate(ifElem);
        var data = new ObjectValue
        {
            ["attachments"] = new ArrayValue(new List<TemplateValue>
            {
                new StringValue("file1.pdf")
            })
        };

        var result = _expander.Expand(template, data);

        Assert.Equal("false", GetResultContent(result));
    }

    // === Equals Operator Tests (Extended) ===

    [Fact]
    public void Equals_Numbers_ReturnsTrue()
    {
        var ifElem = CreateIfElement("quantity", ConditionOperator.Equals, 42.0);
        var template = CreateTemplate(ifElem);
        var data = new ObjectValue { ["quantity"] = new NumberValue(42) };

        var result = _expander.Expand(template, data);

        Assert.Equal("true", GetResultContent(result));
    }

    [Fact]
    public void Equals_NumbersNotEqual_ReturnsFalse()
    {
        var ifElem = CreateIfElement("quantity", ConditionOperator.Equals, 42.0);
        var template = CreateTemplate(ifElem);
        var data = new ObjectValue { ["quantity"] = new NumberValue(43) };

        var result = _expander.Expand(template, data);

        Assert.Equal("false", GetResultContent(result));
    }

    [Fact]
    public void Equals_Booleans_ReturnsTrue()
    {
        var ifElem = CreateIfElement("active", ConditionOperator.Equals, true);
        var template = CreateTemplate(ifElem);
        var data = new ObjectValue { ["active"] = new BoolValue(true) };

        var result = _expander.Expand(template, data);

        Assert.Equal("true", GetResultContent(result));
    }

    [Fact]
    public void Equals_Null_ReturnsTrue()
    {
        var ifElem = CreateIfElement("missing", ConditionOperator.Equals, null);
        var template = CreateTemplate(ifElem);
        var data = new ObjectValue { ["missing"] = NullValue.Instance };

        var result = _expander.Expand(template, data);

        Assert.Equal("true", GetResultContent(result));
    }

    [Fact]
    public void Equals_NonNullWithNull_ReturnsFalse()
    {
        var ifElem = CreateIfElement("value", ConditionOperator.Equals, null);
        var template = CreateTemplate(ifElem);
        var data = new ObjectValue { ["value"] = new StringValue("something") };

        var result = _expander.Expand(template, data);

        Assert.Equal("false", GetResultContent(result));
    }

    [Fact]
    public void Equals_MissingPathEqualsNull_ReturnsTrue()
    {
        var ifElem = CreateIfElement("nonexistent", ConditionOperator.Equals, null);
        var template = CreateTemplate(ifElem);
        var data = new ObjectValue();

        var result = _expander.Expand(template, data);

        Assert.Equal("true", GetResultContent(result));
    }

    [Fact]
    public void Equals_Arrays_ReturnsTrue()
    {
        var compareArray = new ArrayValue(new List<TemplateValue>
        {
            new StringValue("a"),
            new StringValue("b")
        });
        var ifElem = CreateIfElement("items", ConditionOperator.Equals, compareArray);
        var template = CreateTemplate(ifElem);
        var data = new ObjectValue
        {
            ["items"] = new ArrayValue(new List<TemplateValue>
            {
                new StringValue("a"),
                new StringValue("b")
            })
        };

        var result = _expander.Expand(template, data);

        Assert.Equal("true", GetResultContent(result));
    }

    // === NotEquals Operator Tests (Extended) ===

    [Fact]
    public void NotEquals_DifferentValues_ReturnsTrue()
    {
        var ifElem = CreateIfElement("status", ConditionOperator.NotEquals, "cancelled");
        var template = CreateTemplate(ifElem);
        var data = new ObjectValue { ["status"] = new StringValue("active") };

        var result = _expander.Expand(template, data);

        Assert.Equal("true", GetResultContent(result));
    }

    [Fact]
    public void NotEquals_SameValues_ReturnsFalse()
    {
        var ifElem = CreateIfElement("status", ConditionOperator.NotEquals, "cancelled");
        var template = CreateTemplate(ifElem);
        var data = new ObjectValue { ["status"] = new StringValue("cancelled") };

        var result = _expander.Expand(template, data);

        Assert.Equal("false", GetResultContent(result));
    }

    // === Edge Cases ===

    [Fact]
    public void NumericComparison_NonNumericValue_ReturnsFalse()
    {
        var ifElem = CreateIfElement("name", ConditionOperator.GreaterThan, 100.0);
        var template = CreateTemplate(ifElem);
        var data = new ObjectValue { ["name"] = new StringValue("test") };

        var result = _expander.Expand(template, data);

        Assert.Equal("false", GetResultContent(result));
    }

    [Fact]
    public void In_NullList_ReturnsFalse()
    {
        var ifElem = CreateIfElement("status", ConditionOperator.In, null);
        var template = CreateTemplate(ifElem);
        var data = new ObjectValue { ["status"] = new StringValue("active") };

        var result = _expander.Expand(template, data);

        Assert.Equal("false", GetResultContent(result));
    }

    [Fact]
    public void Contains_NullElement_ReturnsFalse()
    {
        var ifElem = CreateIfElement("items", ConditionOperator.Contains, null);
        var template = CreateTemplate(ifElem);
        var data = new ObjectValue
        {
            ["items"] = new ArrayValue(new List<TemplateValue> { new StringValue("test") })
        };

        var result = _expander.Expand(template, data);

        Assert.Equal("false", GetResultContent(result));
    }

    [Fact]
    public void CountEquals_NonArrayValue_ReturnsFalse()
    {
        var ifElem = CreateIfElement("name", ConditionOperator.CountEquals, 5);
        var template = CreateTemplate(ifElem);
        var data = new ObjectValue { ["name"] = new StringValue("test") };

        var result = _expander.Expand(template, data);

        Assert.Equal("false", GetResultContent(result));
    }

    [Fact]
    public void DecimalPrecision_GreaterThan_WorksCorrectly()
    {
        var ifElem = CreateIfElement("price", ConditionOperator.GreaterThan, 99.99);
        var template = CreateTemplate(ifElem);
        var data = new ObjectValue { ["price"] = new NumberValue(100.00m) };

        var result = _expander.Expand(template, data);

        Assert.Equal("true", GetResultContent(result));
    }

    [Fact]
    public void NegativeNumbers_LessThan_WorksCorrectly()
    {
        var ifElem = CreateIfElement("temperature", ConditionOperator.LessThan, 0.0);
        var template = CreateTemplate(ifElem);
        var data = new ObjectValue { ["temperature"] = new NumberValue(-10) };

        var result = _expander.Expand(template, data);

        Assert.Equal("true", GetResultContent(result));
    }
}
