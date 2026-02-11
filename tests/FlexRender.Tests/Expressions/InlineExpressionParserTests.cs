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
[Collection("ExpressionCache")]
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

    #region Single Quote Support

    /// <summary>
    /// Verifies that single-quoted string literals are parsed correctly.
    /// </summary>
    [Fact]
    public void Parse_SingleQuoteString_ReturnsStringLiteral()
    {
        var result = InlineExpressionParser.Parse("'hello'");

        var literal = Assert.IsType<StringLiteral>(result);
        Assert.Equal("hello", literal.Value);
    }

    /// <summary>
    /// Verifies that null coalescing works with single-quoted fallback.
    /// </summary>
    [Fact]
    public void Parse_NullCoalesceWithSingleQuotes_ReturnsCoalesceExpression()
    {
        var result = InlineExpressionParser.Parse("name ?? 'default'");

        var coalesce = Assert.IsType<CoalesceExpression>(result);
        var left = Assert.IsType<PathExpression>(coalesce.Left);
        Assert.Equal("name", left.Path);
        var right = Assert.IsType<StringLiteral>(coalesce.Right);
        Assert.Equal("default", right.Value);
    }

    /// <summary>
    /// Verifies that NeedsFullParsing detects single quotes.
    /// </summary>
    [Fact]
    public void NeedsFullParsing_SingleQuote_ReturnsTrue()
    {
        Assert.True(InlineExpressionParser.NeedsFullParsing("name ?? 'default'"));
    }

    /// <summary>
    /// Verifies that unterminated single-quoted string throws.
    /// </summary>
    [Fact]
    public void Parse_UnterminatedSingleQuote_Throws()
    {
        Assert.Throws<TemplateEngineException>(() => InlineExpressionParser.Parse("'unterminated"));
    }

    #endregion

    #region Escape Sequences

    /// <summary>
    /// Verifies that escaped double quote inside double-quoted string works.
    /// </summary>
    [Fact]
    public void Parse_EscapedDoubleQuote_ParsesCorrectly()
    {
        var result = InlineExpressionParser.Parse("\"He said \\\"hello\\\"\"");

        var literal = Assert.IsType<StringLiteral>(result);
        Assert.Equal("He said \"hello\"", literal.Value);
    }

    /// <summary>
    /// Verifies that escaped single quote inside single-quoted string works.
    /// </summary>
    [Fact]
    public void Parse_EscapedSingleQuote_ParsesCorrectly()
    {
        var result = InlineExpressionParser.Parse("'It\\'s nice'");

        var literal = Assert.IsType<StringLiteral>(result);
        Assert.Equal("It's nice", literal.Value);
    }

    /// <summary>
    /// Verifies that escaped backslash produces a literal backslash.
    /// </summary>
    [Fact]
    public void Parse_EscapedBackslash_ParsesCorrectly()
    {
        var result = InlineExpressionParser.Parse("'path\\\\to\\\\file'");

        var literal = Assert.IsType<StringLiteral>(result);
        Assert.Equal("path\\to\\file", literal.Value);
    }

    /// <summary>
    /// Verifies that \n escape sequence produces newline.
    /// </summary>
    [Fact]
    public void Parse_EscapedNewline_ParsesCorrectly()
    {
        var result = InlineExpressionParser.Parse("'line1\\nline2'");

        var literal = Assert.IsType<StringLiteral>(result);
        Assert.Equal("line1\nline2", literal.Value);
    }

    /// <summary>
    /// Verifies that \t escape sequence produces tab.
    /// </summary>
    [Fact]
    public void Parse_EscapedTab_ParsesCorrectly()
    {
        var result = InlineExpressionParser.Parse("'col1\\tcol2'");

        var literal = Assert.IsType<StringLiteral>(result);
        Assert.Equal("col1\tcol2", literal.Value);
    }

    /// <summary>
    /// Verifies that strings without escapes use fast path (no allocation overhead).
    /// </summary>
    [Fact]
    public void Parse_NoEscapeSequences_ReturnsSameContent()
    {
        var result = InlineExpressionParser.Parse("'simple string'");

        var literal = Assert.IsType<StringLiteral>(result);
        Assert.Equal("simple string", literal.Value);
    }

    /// <summary>
    /// Verifies that null coalescing with escaped quotes works end-to-end.
    /// </summary>
    [Fact]
    public void Parse_NullCoalesceWithEscapedQuote_Works()
    {
        var result = InlineExpressionParser.Parse("name ?? 'it\\'s a default'");

        var coalesce = Assert.IsType<CoalesceExpression>(result);
        var right = Assert.IsType<StringLiteral>(coalesce.Right);
        Assert.Equal("it's a default", right.Value);
    }

    /// <summary>
    /// Verifies that empty strings are handled correctly.
    /// </summary>
    [Theory]
    [InlineData("\"\"", "")]
    [InlineData("''", "")]
    public void Parse_EmptyString_ReturnsEmptyStringLiteral(string input, string expected)
    {
        var result = InlineExpressionParser.Parse(input);

        var literal = Assert.IsType<StringLiteral>(result);
        Assert.Equal(expected, literal.Value);
    }

    #endregion

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
    public void ClearCache_ResetsCount()
    {
        _ = InlineExpressionParser.Parse("clear_a + clear_b");
        Assert.True(InlineExpressionParser.CacheCount > 0);

        InlineExpressionParser.ClearCache();
        Assert.Equal(0, InlineExpressionParser.CacheCount);
    }

    #region Comparison Operators

    [Fact]
    public void Parse_EqualOperator_ReturnsComparisonExpression()
    {
        var result = InlineExpressionParser.Parse("a == b");

        var comp = Assert.IsType<ComparisonExpression>(result);
        Assert.Equal(ComparisonOperator.Equal, comp.Op);
        Assert.IsType<PathExpression>(comp.Left);
        Assert.IsType<PathExpression>(comp.Right);
    }

    [Fact]
    public void Parse_NotEqualOperator_ReturnsComparisonExpression()
    {
        var result = InlineExpressionParser.Parse("a != b");

        var comp = Assert.IsType<ComparisonExpression>(result);
        Assert.Equal(ComparisonOperator.NotEqual, comp.Op);
        Assert.IsType<PathExpression>(comp.Left);
        Assert.IsType<PathExpression>(comp.Right);
    }

    [Fact]
    public void Parse_LessThanOperator_ReturnsComparisonExpression()
    {
        var result = InlineExpressionParser.Parse("a < b");

        var comp = Assert.IsType<ComparisonExpression>(result);
        Assert.Equal(ComparisonOperator.LessThan, comp.Op);
        Assert.IsType<PathExpression>(comp.Left);
        Assert.IsType<PathExpression>(comp.Right);
    }

    [Fact]
    public void Parse_GreaterThanOperator_ReturnsComparisonExpression()
    {
        var result = InlineExpressionParser.Parse("a > b");

        var comp = Assert.IsType<ComparisonExpression>(result);
        Assert.Equal(ComparisonOperator.GreaterThan, comp.Op);
        Assert.IsType<PathExpression>(comp.Left);
        Assert.IsType<PathExpression>(comp.Right);
    }

    [Fact]
    public void Parse_LessThanOrEqualOperator_ReturnsComparisonExpression()
    {
        var result = InlineExpressionParser.Parse("a <= b");

        var comp = Assert.IsType<ComparisonExpression>(result);
        Assert.Equal(ComparisonOperator.LessThanOrEqual, comp.Op);
        Assert.IsType<PathExpression>(comp.Left);
        Assert.IsType<PathExpression>(comp.Right);
    }

    [Fact]
    public void Parse_GreaterThanOrEqualOperator_ReturnsComparisonExpression()
    {
        var result = InlineExpressionParser.Parse("a >= b");

        var comp = Assert.IsType<ComparisonExpression>(result);
        Assert.Equal(ComparisonOperator.GreaterThanOrEqual, comp.Op);
        Assert.IsType<PathExpression>(comp.Left);
        Assert.IsType<PathExpression>(comp.Right);
    }

    [Fact]
    public void Parse_ComparisonWithStringLiteral_ParsesCorrectly()
    {
        var result = InlineExpressionParser.Parse("status == \"paid\"");

        var comp = Assert.IsType<ComparisonExpression>(result);
        Assert.Equal(ComparisonOperator.Equal, comp.Op);
        var left = Assert.IsType<PathExpression>(comp.Left);
        Assert.Equal("status", left.Path);
        var right = Assert.IsType<StringLiteral>(comp.Right);
        Assert.Equal("paid", right.Value);
    }

    [Fact]
    public void Parse_ComparisonWithNumberLiteral_ParsesCorrectly()
    {
        var result = InlineExpressionParser.Parse("price > 100");

        var comp = Assert.IsType<ComparisonExpression>(result);
        Assert.Equal(ComparisonOperator.GreaterThan, comp.Op);
        var left = Assert.IsType<PathExpression>(comp.Left);
        Assert.Equal("price", left.Path);
        var right = Assert.IsType<NumberLiteral>(comp.Right);
        Assert.Equal(100m, right.Value);
    }

    [Fact]
    public void Parse_ArithmeticThenComparison_CorrectPrecedence()
    {
        // a + b == c + d should parse as (a + b) == (c + d)
        var result = InlineExpressionParser.Parse("a + b == c + d");

        var comp = Assert.IsType<ComparisonExpression>(result);
        Assert.Equal(ComparisonOperator.Equal, comp.Op);
        var leftArith = Assert.IsType<ArithmeticExpression>(comp.Left);
        Assert.Equal(ArithmeticOperator.Add, leftArith.Op);
        var rightArith = Assert.IsType<ArithmeticExpression>(comp.Right);
        Assert.Equal(ArithmeticOperator.Add, rightArith.Op);
    }

    [Fact]
    public void Parse_ComparisonThenCoalesce_CoalesceWrapsComparison()
    {
        // a == b ?? c should parse as (a == b) ?? c because coalesce is lower precedence
        var result = InlineExpressionParser.Parse("a == b ?? c");

        var coalesce = Assert.IsType<CoalesceExpression>(result);
        var comp = Assert.IsType<ComparisonExpression>(coalesce.Left);
        Assert.Equal(ComparisonOperator.Equal, comp.Op);
        Assert.IsType<PathExpression>(coalesce.Right);
    }

    [Theory]
    [InlineData("a == b")]
    [InlineData("a != b")]
    [InlineData("a < b")]
    [InlineData("a > b")]
    [InlineData("a <= b")]
    [InlineData("a >= b")]
    public void NeedsFullParsing_ComparisonOperators_ReturnsTrue(string content)
    {
        Assert.True(InlineExpressionParser.NeedsFullParsing(content));
    }

    [Fact]
    public void Parse_ChainedComparison_Throws()
    {
        var ex = Assert.Throws<TemplateEngineException>(
            () => InlineExpressionParser.Parse("a < b < c"));
        Assert.Contains("Chained comparisons", ex.Message);
    }

    [Fact]
    public void Parse_ChainedComparisonWithDifferentOps_Throws()
    {
        Assert.Throws<TemplateEngineException>(
            () => InlineExpressionParser.Parse("a == b != c"));
    }

    #endregion

    #region Logical NOT

    [Fact]
    public void Parse_LogicalNot_ReturnsNotExpression()
    {
        var result = InlineExpressionParser.Parse("!active");

        var not = Assert.IsType<NotExpression>(result);
        var path = Assert.IsType<PathExpression>(not.Operand);
        Assert.Equal("active", path.Path);
    }

    [Fact]
    public void Parse_LogicalNotWithComparison_NotBindsTighter()
    {
        // !active == true should parse as (!active) == true
        var result = InlineExpressionParser.Parse("!active == true");

        var comp = Assert.IsType<ComparisonExpression>(result);
        Assert.Equal(ComparisonOperator.Equal, comp.Op);
        Assert.IsType<NotExpression>(comp.Left);
        Assert.IsType<BoolLiteral>(comp.Right); // NOW it's BoolLiteral, not PathExpression
    }

    [Theory]
    [InlineData("!active")]
    [InlineData("!flag")]
    public void NeedsFullParsing_LogicalNot_ReturnsTrue(string content)
    {
        Assert.True(InlineExpressionParser.NeedsFullParsing(content));
    }

    #endregion

    #region Boolean and Null Literals

    [Theory]
    [InlineData("true", true)]
    [InlineData("false", false)]
    public void Parse_BooleanKeyword_ReturnsBoolLiteral(string expression, bool expected)
    {
        var result = InlineExpressionParser.Parse(expression);

        var literal = Assert.IsType<BoolLiteral>(result);
        Assert.Equal(expected, literal.Value);
    }

    [Fact]
    public void Parse_NullKeyword_ReturnsNullLiteral()
    {
        var result = InlineExpressionParser.Parse("null");

        Assert.IsType<NullLiteral>(result);
    }

    [Fact]
    public void Parse_ComparisonWithBoolLiteral_Works()
    {
        var result = InlineExpressionParser.Parse("active == true");

        var comp = Assert.IsType<ComparisonExpression>(result);
        Assert.IsType<PathExpression>(comp.Left);
        var right = Assert.IsType<BoolLiteral>(comp.Right);
        Assert.True(right.Value);
    }

    [Fact]
    public void Parse_ComparisonWithNullLiteral_Works()
    {
        var result = InlineExpressionParser.Parse("value == null");

        var comp = Assert.IsType<ComparisonExpression>(result);
        Assert.IsType<PathExpression>(comp.Left);
        Assert.IsType<NullLiteral>(comp.Right);
    }

    [Fact]
    public void Parse_NullCoalesceWithNullLiteral_DoesNotMatchKeyword()
    {
        // "nullable" should still be a path, not a null keyword
        var result = InlineExpressionParser.Parse("nullable");

        var path = Assert.IsType<PathExpression>(result);
        Assert.Equal("nullable", path.Path);
    }

    [Fact]
    public void Parse_TruePrefix_IsPath()
    {
        // "trueName" should still be a path, not a bool keyword
        var result = InlineExpressionParser.Parse("trueName");

        var path = Assert.IsType<PathExpression>(result);
        Assert.Equal("trueName", path.Path);
    }

    #endregion
}
