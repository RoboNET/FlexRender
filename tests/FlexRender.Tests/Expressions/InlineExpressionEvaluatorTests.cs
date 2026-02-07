// Tests for the InlineExpressionEvaluator that evaluates parsed expression ASTs.
//
// Compilation status: WILL NOT COMPILE until InlineExpressionEvaluator,
// InlineExpressionParser, and InlineExpression AST nodes are implemented.

using FlexRender.TemplateEngine;
using Xunit;

namespace FlexRender.Tests.Expressions;

/// <summary>
/// Tests for InlineExpressionEvaluator (evaluates parsed expression ASTs against data).
/// </summary>
public sealed class InlineExpressionEvaluatorTests
{
    private static readonly FilterRegistry DefaultFilters = FilterRegistry.CreateDefault();
    private static readonly InlineExpressionEvaluator Evaluator = new(DefaultFilters);

    private static TemplateValue Evaluate(string expression, ObjectValue data)
    {
        var ast = InlineExpressionParser.Parse(expression);
        var context = new TemplateContext(data);
        return Evaluator.Evaluate(ast, context);
    }

    // === Path resolution ===

    [Fact]
    public void Evaluate_SimplePath_ReturnsValue()
    {
        var data = new ObjectValue { ["name"] = new StringValue("John") };

        var result = Evaluate("name", data);

        var str = Assert.IsType<StringValue>(result);
        Assert.Equal("John", str.Value);
    }

    [Fact]
    public void Evaluate_DottedPath_ReturnsNestedValue()
    {
        var data = new ObjectValue
        {
            ["user"] = new ObjectValue { ["name"] = new StringValue("John") }
        };

        var result = Evaluate("user.name", data);

        var str = Assert.IsType<StringValue>(result);
        Assert.Equal("John", str.Value);
    }

    [Fact]
    public void Evaluate_MissingPath_ReturnsNullValue()
    {
        var data = new ObjectValue();

        var result = Evaluate("nonexistent", data);

        Assert.IsType<NullValue>(result);
    }

    // === Number literals ===

    [Fact]
    public void Evaluate_NumberLiteral_ReturnsNumberValue()
    {
        var data = new ObjectValue();

        var result = Evaluate("42", data);

        var num = Assert.IsType<NumberValue>(result);
        Assert.Equal(42m, num.Value);
    }

    // === String literals ===

    [Fact]
    public void Evaluate_StringLiteral_ReturnsStringValue()
    {
        var data = new ObjectValue();

        var result = Evaluate("\"hello\"", data);

        var str = Assert.IsType<StringValue>(result);
        Assert.Equal("hello", str.Value);
    }

    // === Arithmetic ===

    [Fact]
    public void Evaluate_Addition_ReturnsSum()
    {
        var data = new ObjectValue
        {
            ["a"] = new NumberValue(10),
            ["b"] = new NumberValue(20)
        };

        var result = Evaluate("a + b", data);

        var num = Assert.IsType<NumberValue>(result);
        Assert.Equal(30m, num.Value);
    }

    [Fact]
    public void Evaluate_Subtraction_ReturnsDifference()
    {
        var data = new ObjectValue
        {
            ["total"] = new NumberValue(100),
            ["discount"] = new NumberValue(15)
        };

        var result = Evaluate("total - discount", data);

        var num = Assert.IsType<NumberValue>(result);
        Assert.Equal(85m, num.Value);
    }

    [Fact]
    public void Evaluate_Multiplication_ReturnsProduct()
    {
        var data = new ObjectValue
        {
            ["price"] = new NumberValue(10.5m),
            ["quantity"] = new NumberValue(3)
        };

        var result = Evaluate("price * quantity", data);

        var num = Assert.IsType<NumberValue>(result);
        Assert.Equal(31.5m, num.Value);
    }

    [Fact]
    public void Evaluate_Division_ReturnsQuotient()
    {
        var data = new ObjectValue
        {
            ["total"] = new NumberValue(100),
            ["count"] = new NumberValue(4)
        };

        var result = Evaluate("total / count", data);

        var num = Assert.IsType<NumberValue>(result);
        Assert.Equal(25m, num.Value);
    }

    [Fact]
    public void Evaluate_ComplexArithmetic_CorrectResult()
    {
        // (price * quantity - discount) + tax
        var data = new ObjectValue
        {
            ["price"] = new NumberValue(10),
            ["quantity"] = new NumberValue(3),
            ["discount"] = new NumberValue(5),
            ["tax"] = new NumberValue(2.5m)
        };

        var result = Evaluate("price * quantity - discount + tax", data);

        var num = Assert.IsType<NumberValue>(result);
        Assert.Equal(27.5m, num.Value); // (10*3) - 5 + 2.5 = 27.5
    }

    [Fact]
    public void Evaluate_ParenthesizedExpression_OverridesPrecedence()
    {
        var data = new ObjectValue
        {
            ["a"] = new NumberValue(2),
            ["b"] = new NumberValue(3),
            ["c"] = new NumberValue(4)
        };

        var result = Evaluate("(a + b) * c", data);

        var num = Assert.IsType<NumberValue>(result);
        Assert.Equal(20m, num.Value); // (2+3) * 4 = 20
    }

    // === Negation ===

    [Fact]
    public void Evaluate_UnaryMinus_NegatesValue()
    {
        var data = new ObjectValue
        {
            ["price"] = new NumberValue(42)
        };

        var result = Evaluate("-price", data);

        var num = Assert.IsType<NumberValue>(result);
        Assert.Equal(-42m, num.Value);
    }

    [Fact]
    public void Evaluate_UnaryMinusDecimal_NegatesValue()
    {
        var data = new ObjectValue
        {
            ["price"] = new NumberValue(49.99m)
        };

        var result = Evaluate("-price", data);

        var num = Assert.IsType<NumberValue>(result);
        Assert.Equal(-49.99m, num.Value);
    }

    // === Null arithmetic edge cases ===

    [Fact]
    public void Evaluate_NullPlusNumber_ReturnsNullValue()
    {
        // null + 5 -> NullValue (no implicit coercion)
        var data = new ObjectValue();

        var result = Evaluate("missing + 5", data);

        Assert.IsType<NullValue>(result);
    }

    [Fact]
    public void Evaluate_DivisionByZero_ReturnsNullValue()
    {
        // Division by zero returns NullValue (no throw)
        var data = new ObjectValue
        {
            ["total"] = new NumberValue(100),
            ["zero"] = new NumberValue(0)
        };

        var result = Evaluate("total / zero", data);

        Assert.IsType<NullValue>(result);
    }

    [Fact]
    public void Evaluate_StringPlusNumber_ReturnsNullValue()
    {
        // String + number: no implicit coercion, returns NullValue
        var data = new ObjectValue
        {
            ["name"] = new StringValue("hello"),
            ["num"] = new NumberValue(5)
        };

        var result = Evaluate("name + num", data);

        Assert.IsType<NullValue>(result);
    }

    // === Null coalesce ===

    [Fact]
    public void Evaluate_NullCoalesce_NonNull_ReturnsLeft()
    {
        var data = new ObjectValue
        {
            ["name"] = new StringValue("John")
        };

        var result = Evaluate("name ?? \"Guest\"", data);

        var str = Assert.IsType<StringValue>(result);
        Assert.Equal("John", str.Value);
    }

    [Fact]
    public void Evaluate_NullCoalesce_Null_ReturnsRight()
    {
        var data = new ObjectValue(); // name is missing

        var result = Evaluate("name ?? \"Guest\"", data);

        var str = Assert.IsType<StringValue>(result);
        Assert.Equal("Guest", str.Value);
    }

    [Fact]
    public void Evaluate_NullCoalesce_WithPath_ReturnsRightPath()
    {
        var data = new ObjectValue
        {
            ["user"] = new ObjectValue { ["name"] = new StringValue("John") }
        };

        var result = Evaluate("nickname ?? user.name", data);

        var str = Assert.IsType<StringValue>(result);
        Assert.Equal("John", str.Value);
    }

    [Fact]
    public void Evaluate_NullCoalesce_WithNumber_ReturnsDefault()
    {
        var data = new ObjectValue();

        var result = Evaluate("count ?? 0", data);

        var num = Assert.IsType<NumberValue>(result);
        Assert.Equal(0m, num.Value);
    }

    // === Filter evaluation ===

    [Fact]
    public void Evaluate_UpperFilter_ConvertsToUpperCase()
    {
        var data = new ObjectValue { ["name"] = new StringValue("john") };

        var result = Evaluate("name | upper", data);

        var str = Assert.IsType<StringValue>(result);
        Assert.Equal("JOHN", str.Value);
    }

    [Fact]
    public void Evaluate_LowerFilter_ConvertsToLowerCase()
    {
        var data = new ObjectValue { ["name"] = new StringValue("JOHN") };

        var result = Evaluate("name | lower", data);

        var str = Assert.IsType<StringValue>(result);
        Assert.Equal("john", str.Value);
    }

    [Fact]
    public void Evaluate_TrimFilter_TrimsWhitespace()
    {
        var data = new ObjectValue { ["name"] = new StringValue("  John  ") };

        var result = Evaluate("name | trim", data);

        var str = Assert.IsType<StringValue>(result);
        Assert.Equal("John", str.Value);
    }

    [Fact]
    public void Evaluate_ChainedFilters_AppliedInOrder()
    {
        // name | trim | upper
        var data = new ObjectValue { ["name"] = new StringValue("  john  ") };

        var result = Evaluate("name | trim | upper", data);

        var str = Assert.IsType<StringValue>(result);
        Assert.Equal("JOHN", str.Value);
    }

    [Fact]
    public void Evaluate_CurrencyFilter_FormatsNumber()
    {
        var data = new ObjectValue { ["price"] = new NumberValue(1234.56m) };

        var result = Evaluate("price | currency", data);

        var str = Assert.IsType<StringValue>(result);
        Assert.Equal("1,234.56", str.Value);
    }

    [Fact]
    public void Evaluate_NumberFilter_FormatsWithPrecision()
    {
        var data = new ObjectValue { ["val"] = new NumberValue(1234.567m) };

        var result = Evaluate("val | number:2", data);

        var str = Assert.IsType<StringValue>(result);
        Assert.Equal("1234.57", str.Value);
    }

    [Fact]
    public void Evaluate_TruncateFilter_TruncatesWithEllipsis()
    {
        var data = new ObjectValue
        {
            ["desc"] = new StringValue("This is a very long description that should be truncated")
        };

        var result = Evaluate("desc | truncate:20", data);

        var str = Assert.IsType<StringValue>(result);
        Assert.Equal(20, str.Value.Length); // maxLen including "..."
        Assert.EndsWith("...", str.Value);
    }

    [Fact]
    public void Evaluate_TruncateFilter_ShortString_NoTruncation()
    {
        var data = new ObjectValue { ["desc"] = new StringValue("Short") };

        var result = Evaluate("desc | truncate:20", data);

        var str = Assert.IsType<StringValue>(result);
        Assert.Equal("Short", str.Value);
    }

    [Fact]
    public void Evaluate_ArithmeticThenFilter_EvaluatesCorrectly()
    {
        // price * quantity | currency
        var data = new ObjectValue
        {
            ["price"] = new NumberValue(10.5m),
            ["quantity"] = new NumberValue(3)
        };

        var result = Evaluate("price * quantity | currency", data);

        var str = Assert.IsType<StringValue>(result);
        Assert.Equal("31.50", str.Value);
    }

    // === Unknown filter ===

    [Fact]
    public void Evaluate_UnknownFilter_ThrowsTemplateEngineException()
    {
        var data = new ObjectValue { ["name"] = new StringValue("John") };
        var ast = InlineExpressionParser.Parse("name | nonexistent");
        var context = new TemplateContext(data);

        Assert.Throws<TemplateEngineException>(
            () => Evaluator.Evaluate(ast, context));
    }
}
