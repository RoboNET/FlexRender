// Tests for the InlineExpressionEvaluator that evaluates parsed expression ASTs.
//
// Compilation status: WILL NOT COMPILE until InlineExpressionEvaluator,
// InlineExpressionParser, and InlineExpression AST nodes are implemented.

using System.Globalization;
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

    // === Index Access (Computed Key) ===

    [Fact]
    public void Evaluate_IndexAccess_StringKey_ReturnsValue()
    {
        var data = new ObjectValue
        {
            ["dict"] = new ObjectValue { ["en"] = new StringValue("Hello"), ["ru"] = new StringValue("Привет") },
            ["lang"] = new StringValue("ru")
        };
        var result = Evaluate("dict[lang]", data);
        var str = Assert.IsType<StringValue>(result);
        Assert.Equal("Привет", str.Value);
    }

    [Fact]
    public void Evaluate_IndexAccess_StringLiteral_ReturnsValue()
    {
        var data = new ObjectValue
        {
            ["dict"] = new ObjectValue { ["key"] = new StringValue("value") }
        };
        var result = Evaluate("dict[\"key\"]", data);
        var str = Assert.IsType<StringValue>(result);
        Assert.Equal("value", str.Value);
    }

    [Fact]
    public void Evaluate_IndexAccess_NumericIndex_ReturnsArrayItem()
    {
        var data = new ObjectValue
        {
            ["arr"] = new ArrayValue(new List<TemplateValue> { new StringValue("a"), new StringValue("b"), new StringValue("c") }),
            ["idx"] = new NumberValue(1)
        };
        var result = Evaluate("arr[idx]", data);
        var str = Assert.IsType<StringValue>(result);
        Assert.Equal("b", str.Value);
    }

    [Fact]
    public void Evaluate_IndexAccess_MissingKey_ReturnsNull()
    {
        var data = new ObjectValue
        {
            ["dict"] = new ObjectValue { ["a"] = new StringValue("1") },
            ["key"] = new StringValue("missing")
        };
        var result = Evaluate("dict[key]", data);
        Assert.IsType<NullValue>(result);
    }

    [Fact]
    public void Evaluate_IndexAccess_NullKey_ReturnsNull()
    {
        var data = new ObjectValue
        {
            ["dict"] = new ObjectValue { ["a"] = new StringValue("1") }
        };
        var result = Evaluate("dict[key]", data);
        Assert.IsType<NullValue>(result);
    }

    [Fact]
    public void Evaluate_IndexAccess_BoolKey_ReturnsNull()
    {
        var data = new ObjectValue
        {
            ["dict"] = new ObjectValue { ["a"] = new StringValue("1") },
            ["flag"] = new BoolValue(true)
        };
        var result = Evaluate("dict[flag]", data);
        Assert.IsType<NullValue>(result);
    }

    [Fact]
    public void Evaluate_IndexAccess_NumberKeyOnObject_ConvertsToString()
    {
        var data = new ObjectValue
        {
            ["dict"] = new ObjectValue { ["42"] = new StringValue("answer") },
            ["num"] = new NumberValue(42)
        };
        var result = Evaluate("dict[num]", data);
        var str = Assert.IsType<StringValue>(result);
        Assert.Equal("answer", str.Value);
    }

    [Fact]
    public void Evaluate_IndexAccess_StringKeyOnArray_ReturnsNull()
    {
        var data = new ObjectValue
        {
            ["arr"] = new ArrayValue(new List<TemplateValue> { new StringValue("a") }),
            ["key"] = new StringValue("notAnIndex")
        };
        var result = Evaluate("arr[key]", data);
        Assert.IsType<NullValue>(result);
    }

    [Fact]
    public void Evaluate_IndexAccess_OnStringValue_ReturnsNull()
    {
        var data = new ObjectValue
        {
            ["str"] = new StringValue("hello"),
            ["key"] = new StringValue("x")
        };
        var result = Evaluate("str[key]", data);
        Assert.IsType<NullValue>(result);
    }

    [Fact]
    public void Evaluate_IndexAccess_Chained_Works()
    {
        var data = new ObjectValue
        {
            ["sections"] = new ObjectValue
            {
                ["header"] = new ObjectValue { ["title"] = new StringValue("Hello") }
            },
            ["current"] = new StringValue("header")
        };
        var result = Evaluate("sections[current].title", data);
        var str = Assert.IsType<StringValue>(result);
        Assert.Equal("Hello", str.Value);
    }

    [Fact]
    public void Evaluate_IndexAccess_Nested_Works()
    {
        var data = new ObjectValue
        {
            ["dict"] = new ObjectValue { ["en"] = new StringValue("Hello") },
            ["keys"] = new ArrayValue(new List<TemplateValue> { new StringValue("en"), new StringValue("ru") })
        };
        var result = Evaluate("dict[keys[0]]", data);
        var str = Assert.IsType<StringValue>(result);
        Assert.Equal("Hello", str.Value);
    }

    [Fact]
    public void Evaluate_IndexAccess_WithArithmeticExpression_Works()
    {
        var data = new ObjectValue
        {
            ["arr"] = new ArrayValue(new List<TemplateValue> { new StringValue("zero"), new StringValue("one"), new StringValue("two") }),
            ["base"] = new NumberValue(1),
            ["offset"] = new NumberValue(1)
        };
        var result = Evaluate("arr[base + offset]", data);
        var str = Assert.IsType<StringValue>(result);
        Assert.Equal("two", str.Value);
    }

    [Fact]
    public void Evaluate_IndexAccess_FractionalIndex_TruncatesToInt()
    {
        var data = new ObjectValue
        {
            ["arr"] = new ArrayValue(new List<TemplateValue> { new StringValue("zero"), new StringValue("one") }),
            ["idx"] = new NumberValue(1.7m)
        };
        var result = Evaluate("arr[idx]", data);
        var str = Assert.IsType<StringValue>(result);
        Assert.Equal("one", str.Value);
    }

    [Fact]
    public void Evaluate_IndexAccess_NegativeIndex_ReturnsNull()
    {
        var data = new ObjectValue
        {
            ["arr"] = new ArrayValue(new List<TemplateValue> { new StringValue("a") }),
            ["idx"] = new NumberValue(-1)
        };
        var result = Evaluate("arr[idx]", data);
        Assert.IsType<NullValue>(result);
    }

    // === Named filter parameters ===

    [Fact]
    public void Evaluate_FilterWithNamedParam_PassesToFilter()
    {
        var registry = new FilterRegistry();
        registry.Register(new TestNamedParamFilter());
        var evaluator = new InlineExpressionEvaluator(registry);
        var context = new TemplateContext(new ObjectValue { ["x"] = new StringValue("hello") });

        var expr = InlineExpressionParser.Parse("x | testnamed:5 label:'world'");
        var result = evaluator.Evaluate(expr, context);

        var str = Assert.IsType<StringValue>(result);
        Assert.Equal("hello|5|world", str.Value);
    }

    [Fact]
    public void Evaluate_FilterWithFlag_PassesToFilter()
    {
        var registry = new FilterRegistry();
        registry.Register(new TestNamedParamFilter());
        var evaluator = new InlineExpressionEvaluator(registry);
        var context = new TemplateContext(new ObjectValue { ["x"] = new StringValue("hello") });

        var expr = InlineExpressionParser.Parse("x | testnamed:5 reverse");
        var result = evaluator.Evaluate(expr, context);

        var str = Assert.IsType<StringValue>(result);
        Assert.Equal("hello|5|reversed", str.Value);
    }

    private sealed class TestNamedParamFilter : ITemplateFilter
    {
        public string Name => "testnamed";

        public TemplateValue Apply(TemplateValue input, FilterArguments arguments, CultureInfo culture)
        {
            var inputStr = input is StringValue sv ? sv.Value : "null";
            var pos = arguments.Positional is StringValue ps ? ps.Value : "none";
            var label = arguments.GetNamed("label", NullValue.Instance) is StringValue ls ? ls.Value : "none";
            var reversed = arguments.HasFlag("reverse") ? "reversed" : label;
            return new StringValue($"{inputStr}|{pos}|{reversed}");
        }
    }
}
