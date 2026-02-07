// Tests for the Pratt parser that parses inline expressions inside {{...}} blocks.
//
// Compilation status: WILL NOT COMPILE until InlineExpressionParser and
// InlineExpression AST nodes are implemented in src/FlexRender.Core/TemplateEngine/.

using FlexRender.TemplateEngine;
using Xunit;

namespace FlexRender.Tests.Expressions;

/// <summary>
/// Tests for InlineExpressionParser (Pratt parser for arithmetic, coalesce, filters).
/// </summary>
public sealed class InlineExpressionParserTests
{
    // === Simple path expressions ===

    [Fact]
    public void Parse_SimplePath_ReturnsPathExpression()
    {
        var result = InlineExpressionParser.Parse("name");

        var path = Assert.IsType<PathExpression>(result);
        Assert.Equal("name", path.Path);
    }

    [Fact]
    public void Parse_DottedPath_ReturnsPathExpression()
    {
        var result = InlineExpressionParser.Parse("user.name");

        var path = Assert.IsType<PathExpression>(result);
        Assert.Equal("user.name", path.Path);
    }

    [Fact]
    public void Parse_PathWithArrayIndex_ReturnsPathExpression()
    {
        var result = InlineExpressionParser.Parse("items[0].name");

        var path = Assert.IsType<PathExpression>(result);
        Assert.Equal("items[0].name", path.Path);
    }

    // === Number literals ===

    [Fact]
    public void Parse_IntegerLiteral_ReturnsNumberLiteral()
    {
        var result = InlineExpressionParser.Parse("42");

        var num = Assert.IsType<NumberLiteral>(result);
        Assert.Equal(42m, num.Value);
    }

    [Fact]
    public void Parse_DecimalLiteral_ReturnsNumberLiteral()
    {
        var result = InlineExpressionParser.Parse("3.14");

        var num = Assert.IsType<NumberLiteral>(result);
        Assert.Equal(3.14m, num.Value);
    }

    // === String literals ===

    [Fact]
    public void Parse_DoubleQuotedString_ReturnsStringLiteral()
    {
        var result = InlineExpressionParser.Parse("\"Guest\"");

        var str = Assert.IsType<StringLiteral>(result);
        Assert.Equal("Guest", str.Value);
    }

    // === Arithmetic operators with precedence ===

    [Fact]
    public void Parse_Addition_ReturnsArithmeticExpression()
    {
        var result = InlineExpressionParser.Parse("a + b");

        var arith = Assert.IsType<ArithmeticExpression>(result);
        Assert.Equal(ArithmeticOperator.Add, arith.Op);
        Assert.IsType<PathExpression>(arith.Left);
        Assert.IsType<PathExpression>(arith.Right);
    }

    [Fact]
    public void Parse_Subtraction_ReturnsArithmeticExpression()
    {
        var result = InlineExpressionParser.Parse("total - discount");

        var arith = Assert.IsType<ArithmeticExpression>(result);
        Assert.Equal(ArithmeticOperator.Subtract, arith.Op);
    }

    [Fact]
    public void Parse_Multiplication_ReturnsArithmeticExpression()
    {
        var result = InlineExpressionParser.Parse("price * quantity");

        var arith = Assert.IsType<ArithmeticExpression>(result);
        Assert.Equal(ArithmeticOperator.Multiply, arith.Op);
    }

    [Fact]
    public void Parse_Division_ReturnsArithmeticExpression()
    {
        var result = InlineExpressionParser.Parse("total / count");

        var arith = Assert.IsType<ArithmeticExpression>(result);
        Assert.Equal(ArithmeticOperator.Divide, arith.Op);
    }

    [Theory]
    [InlineData("a + b * c")]
    public void Parse_MulBeforeAdd_CorrectPrecedence(string expression)
    {
        // a + (b * c) -- multiplication binds tighter than addition
        var result = InlineExpressionParser.Parse(expression);

        var add = Assert.IsType<ArithmeticExpression>(result);
        Assert.Equal(ArithmeticOperator.Add, add.Op);
        Assert.IsType<PathExpression>(add.Left); // a
        var mul = Assert.IsType<ArithmeticExpression>(add.Right); // b * c
        Assert.Equal(ArithmeticOperator.Multiply, mul.Op);
    }

    [Theory]
    [InlineData("a * b + c")]
    public void Parse_MulThenAdd_CorrectPrecedence(string expression)
    {
        // (a * b) + c
        var result = InlineExpressionParser.Parse(expression);

        var add = Assert.IsType<ArithmeticExpression>(result);
        Assert.Equal(ArithmeticOperator.Add, add.Op);
        var mul = Assert.IsType<ArithmeticExpression>(add.Left); // a * b
        Assert.Equal(ArithmeticOperator.Multiply, mul.Op);
        Assert.IsType<PathExpression>(add.Right); // c
    }

    [Fact]
    public void Parse_ComplexArithmetic_CorrectPrecedence()
    {
        // (price * quantity - discount) + tax
        var result = InlineExpressionParser.Parse("price * quantity - discount + tax");

        // Should parse as ((price * quantity) - discount) + tax
        var outerAdd = Assert.IsType<ArithmeticExpression>(result);
        Assert.Equal(ArithmeticOperator.Add, outerAdd.Op);
    }

    // === Parentheses grouping ===

    [Fact]
    public void Parse_Parentheses_OverridePrecedence()
    {
        // (a + b) * c
        var result = InlineExpressionParser.Parse("(a + b) * c");

        var mul = Assert.IsType<ArithmeticExpression>(result);
        Assert.Equal(ArithmeticOperator.Multiply, mul.Op);
        var add = Assert.IsType<ArithmeticExpression>(mul.Left); // (a + b)
        Assert.Equal(ArithmeticOperator.Add, add.Op);
        Assert.IsType<PathExpression>(mul.Right); // c
    }

    [Fact]
    public void Parse_NestedParentheses_ParsesCorrectly()
    {
        // ((a + b) * (c - d))
        var result = InlineExpressionParser.Parse("((a + b) * (c - d))");

        var mul = Assert.IsType<ArithmeticExpression>(result);
        Assert.Equal(ArithmeticOperator.Multiply, mul.Op);
        var add = Assert.IsType<ArithmeticExpression>(mul.Left);
        Assert.Equal(ArithmeticOperator.Add, add.Op);
        var sub = Assert.IsType<ArithmeticExpression>(mul.Right);
        Assert.Equal(ArithmeticOperator.Subtract, sub.Op);
    }

    // === Unary negation ===

    [Fact]
    public void Parse_UnaryMinus_ReturnsNegateExpression()
    {
        var result = InlineExpressionParser.Parse("-price");

        var negate = Assert.IsType<NegateExpression>(result);
        var path = Assert.IsType<PathExpression>(negate.Operand);
        Assert.Equal("price", path.Path);
    }

    [Fact]
    public void Parse_UnaryMinusNumber_ReturnsNegateExpression()
    {
        var result = InlineExpressionParser.Parse("-42");

        var negate = Assert.IsType<NegateExpression>(result);
        var num = Assert.IsType<NumberLiteral>(negate.Operand);
        Assert.Equal(42m, num.Value);
    }

    [Theory]
    [InlineData("-price", "price")]
    [InlineData("-total", "total")]
    [InlineData("-_discount", "_discount")]
    public void Parse_UnaryMinusVariable_ReturnsNegateExpression(string expression, string expectedPath)
    {
        var result = InlineExpressionParser.Parse(expression);

        var negate = Assert.IsType<NegateExpression>(result);
        var path = Assert.IsType<PathExpression>(negate.Operand);
        Assert.Equal(expectedPath, path.Path);
    }

    [Theory]
    [InlineData("-price")]
    [InlineData("-total")]
    [InlineData("-_discount")]
    [InlineData("- price")]
    [InlineData("-42")]
    public void NeedsFullParsing_UnaryMinus_ReturnsTrue(string content)
    {
        Assert.True(InlineExpressionParser.NeedsFullParsing(content));
    }

    // === Null coalesce ===

    [Fact]
    public void Parse_NullCoalesce_ReturnsCoalesceExpression()
    {
        var result = InlineExpressionParser.Parse("name ?? \"Guest\"");

        var coalesce = Assert.IsType<CoalesceExpression>(result);
        var left = Assert.IsType<PathExpression>(coalesce.Left);
        Assert.Equal("name", left.Path);
        var right = Assert.IsType<StringLiteral>(coalesce.Right);
        Assert.Equal("Guest", right.Value);
    }

    [Fact]
    public void Parse_NullCoalesceWithPath_ReturnsCoalesceExpression()
    {
        var result = InlineExpressionParser.Parse("nickname ?? user.name");

        var coalesce = Assert.IsType<CoalesceExpression>(result);
        Assert.IsType<PathExpression>(coalesce.Left);
        Assert.IsType<PathExpression>(coalesce.Right);
    }

    [Fact]
    public void Parse_NullCoalesceWithNumber_ReturnsCoalesceExpression()
    {
        var result = InlineExpressionParser.Parse("count ?? 0");

        var coalesce = Assert.IsType<CoalesceExpression>(result);
        Assert.IsType<PathExpression>(coalesce.Left);
        Assert.IsType<NumberLiteral>(coalesce.Right);
    }

    // === Filter pipes ===

    [Fact]
    public void Parse_SingleFilter_ReturnsFilterExpression()
    {
        var result = InlineExpressionParser.Parse("price | currency");

        var filter = Assert.IsType<FilterExpression>(result);
        Assert.Equal("currency", filter.FilterName);
        Assert.IsType<PathExpression>(filter.Input);
        Assert.Null(filter.Argument);
    }

    [Fact]
    public void Parse_FilterWithArgument_ReturnsFilterWithArgument()
    {
        var result = InlineExpressionParser.Parse("val | number:2");

        var filter = Assert.IsType<FilterExpression>(result);
        Assert.Equal("number", filter.FilterName);
        Assert.Equal("2", filter.Argument);
    }

    [Fact]
    public void Parse_FilterWithStringArgument_ReturnsFilterWithArgument()
    {
        var result = InlineExpressionParser.Parse("date | format:\"dd.MM.yyyy\"");

        var filter = Assert.IsType<FilterExpression>(result);
        Assert.Equal("format", filter.FilterName);
        Assert.Equal("dd.MM.yyyy", filter.Argument);
    }

    [Fact]
    public void Parse_ChainedFilters_ReturnsNestedFilterExpressions()
    {
        // name | trim | upper
        var result = InlineExpressionParser.Parse("name | trim | upper");

        var outerFilter = Assert.IsType<FilterExpression>(result);
        Assert.Equal("upper", outerFilter.FilterName);

        var innerFilter = Assert.IsType<FilterExpression>(outerFilter.Input);
        Assert.Equal("trim", innerFilter.FilterName);

        Assert.IsType<PathExpression>(innerFilter.Input);
    }

    [Fact]
    public void Parse_ArithmeticThenFilter_CorrectPrecedence()
    {
        // price * quantity | currency
        // Filter pipe has lowest precedence, so: (price * quantity) | currency
        var result = InlineExpressionParser.Parse("price * quantity | currency");

        var filter = Assert.IsType<FilterExpression>(result);
        Assert.Equal("currency", filter.FilterName);

        var arith = Assert.IsType<ArithmeticExpression>(filter.Input);
        Assert.Equal(ArithmeticOperator.Multiply, arith.Op);
    }

    // === Precedence: filter < coalesce < add/sub < mul/div < unary ===

    [Fact]
    public void Parse_CoalesceThenFilter_CorrectPrecedence()
    {
        // name ?? "Guest" | upper
        // Filter binds loosest: (name ?? "Guest") | upper
        var result = InlineExpressionParser.Parse("name ?? \"Guest\" | upper");

        var filter = Assert.IsType<FilterExpression>(result);
        Assert.Equal("upper", filter.FilterName);
        Assert.IsType<CoalesceExpression>(filter.Input);
    }

    // === Edge cases: minus in paths vs subtraction ===

    [Fact]
    public void Parse_PathWithHyphen_NoSpaces_TreatedAsPath()
    {
        // "my-var" with no spaces should be a path
        var result = InlineExpressionParser.Parse("my-var");

        var path = Assert.IsType<PathExpression>(result);
        Assert.Equal("my-var", path.Path);
    }

    [Fact]
    public void Parse_SubtractionWithSpaces_TreatedAsArithmetic()
    {
        // "a - b" with spaces should be subtraction
        var result = InlineExpressionParser.Parse("a - b");

        var arith = Assert.IsType<ArithmeticExpression>(result);
        Assert.Equal(ArithmeticOperator.Subtract, arith.Op);
    }

    // === Expression caching ===

    [Fact]
    public void Parse_SameComplexExpression_ReturnsCachedInstance()
    {
        InlineExpressionParser.ClearCache();

        var first = InlineExpressionParser.Parse("price * quantity");
        var second = InlineExpressionParser.Parse("price * quantity");

        Assert.Same(first, second);
    }

    [Fact]
    public void Parse_DifferentExpressions_ReturnsDifferentInstances()
    {
        InlineExpressionParser.ClearCache();

        var a = InlineExpressionParser.Parse("a + b");
        var b = InlineExpressionParser.Parse("a - b");

        Assert.NotSame(a, b);
    }

    [Fact]
    public void Parse_SimplePath_NotCached()
    {
        // Simple paths use fast path and bypass cache
        InlineExpressionParser.ClearCache();

        _ = InlineExpressionParser.Parse("name");
        _ = InlineExpressionParser.Parse("user.address");

        Assert.Equal(0, InlineExpressionParser.CacheCount);
    }

    [Fact]
    public void Parse_ComplexExpression_IncrementsCacheCount()
    {
        InlineExpressionParser.ClearCache();
        var baseline = InlineExpressionParser.CacheCount;

        _ = InlineExpressionParser.Parse("unique_a + unique_b");
        _ = InlineExpressionParser.Parse("unique_c * unique_d");
        _ = InlineExpressionParser.Parse("unique_x ?? unique_y");

        // At least 3 new entries should be added (other parallel tests may also add)
        Assert.True(InlineExpressionParser.CacheCount >= baseline + 3);
    }

    [Fact]
    public void Parse_CacheEvictsWhenFull()
    {
        InlineExpressionParser.ClearCache();

        // Fill cache to capacity with unique expressions
        for (var i = 0; i < InlineExpressionParser.MaxCacheSize; i++)
        {
            _ = InlineExpressionParser.Parse($"evict_a + {i}");
        }

        var countBeforeOverflow = InlineExpressionParser.CacheCount;
        Assert.True(countBeforeOverflow >= InlineExpressionParser.MaxCacheSize,
            $"Cache should have at least {InlineExpressionParser.MaxCacheSize} entries, had {countBeforeOverflow}");

        // One more should trigger eviction
        _ = InlineExpressionParser.Parse("evict_overflow + 1");

        // After eviction, cache should be much smaller than before
        Assert.True(InlineExpressionParser.CacheCount < countBeforeOverflow,
            $"Cache should have been evicted. Before: {countBeforeOverflow}, After: {InlineExpressionParser.CacheCount}");
    }

    [Fact]
    public void ClearCache_ResetsCount()
    {
        _ = InlineExpressionParser.Parse("clear_a + clear_b");
        Assert.True(InlineExpressionParser.CacheCount > 0);

        InlineExpressionParser.ClearCache();
        Assert.Equal(0, InlineExpressionParser.CacheCount);
    }
}
