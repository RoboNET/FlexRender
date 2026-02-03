using FlexRender.Parsing;
using FlexRender.Parsing.Ast;
using Xunit;

namespace FlexRender.Tests.Parsing;

/// <summary>
/// Tests for parsing control flow elements (EachElement and IfElement).
/// These tests are expected to fail until the parser implementations are added.
/// </summary>
public sealed class ControlFlowParsingTests
{
    private readonly TemplateParser _parser = new();

    // === EachElement Tests ===

    /// <summary>
    /// Verifies that an each element with an array path is parsed correctly.
    /// </summary>
    [Fact]
    public void Parse_EachElement_ParsesArrayPath()
    {
        const string yaml = """
            canvas:
              width: 300
            layout:
              - type: each
                array: items
                children:
                  - type: text
                    content: test
            """;

        var template = _parser.Parse(yaml);

        var each = Assert.IsType<EachElement>(template.Elements[0]);
        Assert.Equal("items", each.ArrayPath);
    }

    /// <summary>
    /// Verifies that an each element with an item variable is parsed correctly.
    /// </summary>
    [Fact]
    public void Parse_EachElement_ParsesItemVariable()
    {
        const string yaml = """
            canvas:
              width: 300
            layout:
              - type: each
                array: items
                as: item
                children:
                  - type: text
                    content: "{{item.name}}"
            """;

        var template = _parser.Parse(yaml);

        var each = Assert.IsType<EachElement>(template.Elements[0]);
        Assert.Equal("item", each.ItemVariable);
    }

    /// <summary>
    /// Verifies that an each element with multiple children is parsed correctly.
    /// </summary>
    [Fact]
    public void Parse_EachElement_ParsesChildren()
    {
        const string yaml = """
            canvas:
              width: 300
            layout:
              - type: each
                array: items
                children:
                  - type: text
                    content: first
                  - type: text
                    content: second
            """;

        var template = _parser.Parse(yaml);

        var each = Assert.IsType<EachElement>(template.Elements[0]);
        Assert.Equal(2, each.ItemTemplate.Count);
        Assert.All(each.ItemTemplate, e => Assert.IsType<TextElement>(e));
    }

    /// <summary>
    /// Verifies that an each element without the required array property throws an exception.
    /// </summary>
    [Fact]
    public void Parse_EachElement_WithoutArray_ThrowsTemplateParseException()
    {
        const string yaml = """
            canvas:
              width: 300
            layout:
              - type: each
                children:
                  - type: text
                    content: test
            """;

        var ex = Assert.Throws<TemplateParseException>(() => _parser.Parse(yaml));
        Assert.Contains("array", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Verifies that an each element with a nested array path is parsed correctly.
    /// </summary>
    [Fact]
    public void Parse_EachElement_NestedPath()
    {
        const string yaml = """
            canvas:
              width: 300
            layout:
              - type: each
                array: order.items
                children:
                  - type: text
                    content: test
            """;

        var template = _parser.Parse(yaml);

        var each = Assert.IsType<EachElement>(template.Elements[0]);
        Assert.Equal("order.items", each.ArrayPath);
    }

    // === IfElement Tests ===

    /// <summary>
    /// Verifies that an if element with a truthy condition is parsed correctly.
    /// </summary>
    [Fact]
    public void Parse_IfElement_TruthyCondition()
    {
        const string yaml = """
            canvas:
              width: 300
            layout:
              - type: if
                condition: isPremium
                then:
                  - type: text
                    content: Premium
            """;

        var template = _parser.Parse(yaml);

        var ifElement = Assert.IsType<IfElement>(template.Elements[0]);
        Assert.Equal("isPremium", ifElement.ConditionPath);
        Assert.Null(ifElement.Operator);
        Assert.Single(ifElement.ThenBranch);
    }

    /// <summary>
    /// Verifies that an if element with an equals operator is parsed correctly.
    /// </summary>
    [Fact]
    public void Parse_IfElement_EqualsOperator()
    {
        const string yaml = """
            canvas:
              width: 300
            layout:
              - type: if
                condition: status
                equals: paid
                then:
                  - type: text
                    content: Paid
            """;

        var template = _parser.Parse(yaml);

        var ifElement = Assert.IsType<IfElement>(template.Elements[0]);
        Assert.Equal(ConditionOperator.Equals, ifElement.Operator);
        Assert.Equal("paid", ifElement.CompareValue);
    }

    /// <summary>
    /// Verifies that an if element with a notEquals operator is parsed correctly.
    /// </summary>
    [Fact]
    public void Parse_IfElement_NotEqualsOperator()
    {
        const string yaml = """
            canvas:
              width: 300
            layout:
              - type: if
                condition: status
                notEquals: cancelled
                then:
                  - type: text
                    content: Active
            """;

        var template = _parser.Parse(yaml);

        var ifElement = Assert.IsType<IfElement>(template.Elements[0]);
        Assert.Equal(ConditionOperator.NotEquals, ifElement.Operator);
        Assert.Equal("cancelled", ifElement.CompareValue);
    }

    /// <summary>
    /// Verifies that an if element with an else branch is parsed correctly.
    /// </summary>
    [Fact]
    public void Parse_IfElement_WithElse()
    {
        const string yaml = """
            canvas:
              width: 300
            layout:
              - type: if
                condition: isPremium
                then:
                  - type: text
                    content: Premium
                else:
                  - type: text
                    content: Regular
            """;

        var template = _parser.Parse(yaml);

        var ifElement = Assert.IsType<IfElement>(template.Elements[0]);
        Assert.Single(ifElement.ThenBranch);
        Assert.Single(ifElement.ElseBranch);
    }

    /// <summary>
    /// Verifies that an if element with an elseIf chain is parsed correctly.
    /// </summary>
    [Fact]
    public void Parse_IfElement_WithElseIf()
    {
        const string yaml = """
            canvas:
              width: 300
            layout:
              - type: if
                condition: status
                equals: paid
                then:
                  - type: text
                    content: Paid
                elseIf:
                  condition: status
                  equals: pending
                  then:
                    - type: text
                      content: Pending
                else:
                  - type: text
                    content: Unknown
            """;

        var template = _parser.Parse(yaml);

        var ifElement = Assert.IsType<IfElement>(template.Elements[0]);
        Assert.NotNull(ifElement.ElseIf);
        Assert.Equal("status", ifElement.ElseIf.ConditionPath);
        Assert.Equal("pending", ifElement.ElseIf.CompareValue);
        Assert.Single(ifElement.ElseBranch);
    }

    /// <summary>
    /// Verifies that an if element without the required condition property throws an exception.
    /// </summary>
    [Fact]
    public void Parse_IfElement_WithoutCondition_ThrowsTemplateParseException()
    {
        const string yaml = """
            canvas:
              width: 300
            layout:
              - type: if
                then:
                  - type: text
                    content: test
            """;

        var ex = Assert.Throws<TemplateParseException>(() => _parser.Parse(yaml));
        Assert.Contains("condition", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Verifies that an if element with a nested condition path is parsed correctly.
    /// </summary>
    [Fact]
    public void Parse_IfElement_NestedConditionPath()
    {
        const string yaml = """
            canvas:
              width: 300
            layout:
              - type: if
                condition: user.subscription.active
                then:
                  - type: text
                    content: Active
            """;

        var template = _parser.Parse(yaml);

        var ifElement = Assert.IsType<IfElement>(template.Elements[0]);
        Assert.Equal("user.subscription.active", ifElement.ConditionPath);
    }
}
