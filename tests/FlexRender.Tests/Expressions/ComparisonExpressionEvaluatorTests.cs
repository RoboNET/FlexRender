// Tests for comparison operators and logical NOT in InlineExpressionEvaluator.

using FlexRender.TemplateEngine;
using Xunit;

namespace FlexRender.Tests.Expressions;

/// <summary>
/// Tests for comparison operators (==, !=, &lt;, &gt;, &lt;=, &gt;=)
/// and logical NOT (!) in <see cref="InlineExpressionEvaluator"/>.
/// </summary>
[Collection("ExpressionCache")]
public sealed class ComparisonExpressionEvaluatorTests
{
    private readonly InlineExpressionEvaluator _evaluator = new();

    private TemplateValue Eval(string expression, ObjectValue data)
    {
        var ast = InlineExpressionParser.Parse(expression);
        var context = new TemplateContext(data);
        return _evaluator.Evaluate(ast, context);
    }

    #region Number Comparisons

    [Fact]
    public void NumberEqual_SameValues_ReturnsTrue()
    {
        var data = new ObjectValue { ["a"] = new NumberValue(10), ["b"] = new NumberValue(10) };
        var result = Eval("a == b", data);

        var boolVal = Assert.IsType<BoolValue>(result);
        Assert.True(boolVal.Value);
    }

    [Fact]
    public void NumberEqual_DifferentValues_ReturnsFalse()
    {
        var data = new ObjectValue { ["a"] = new NumberValue(10), ["b"] = new NumberValue(20) };
        var result = Eval("a == b", data);

        var boolVal = Assert.IsType<BoolValue>(result);
        Assert.False(boolVal.Value);
    }

    [Fact]
    public void NumberNotEqual_DifferentValues_ReturnsTrue()
    {
        var data = new ObjectValue { ["a"] = new NumberValue(10), ["b"] = new NumberValue(20) };
        var result = Eval("a != b", data);

        var boolVal = Assert.IsType<BoolValue>(result);
        Assert.True(boolVal.Value);
    }

    [Fact]
    public void NumberLessThan_Smaller_ReturnsTrue()
    {
        var data = new ObjectValue { ["a"] = new NumberValue(5), ["b"] = new NumberValue(10) };
        var result = Eval("a < b", data);

        var boolVal = Assert.IsType<BoolValue>(result);
        Assert.True(boolVal.Value);
    }

    [Fact]
    public void NumberLessThan_Equal_ReturnsFalse()
    {
        var data = new ObjectValue { ["a"] = new NumberValue(10), ["b"] = new NumberValue(10) };
        var result = Eval("a < b", data);

        var boolVal = Assert.IsType<BoolValue>(result);
        Assert.False(boolVal.Value);
    }

    [Fact]
    public void NumberGreaterThan_Larger_ReturnsTrue()
    {
        var data = new ObjectValue { ["a"] = new NumberValue(20), ["b"] = new NumberValue(10) };
        var result = Eval("a > b", data);

        var boolVal = Assert.IsType<BoolValue>(result);
        Assert.True(boolVal.Value);
    }

    [Fact]
    public void NumberGreaterThan_Equal_ReturnsFalse()
    {
        var data = new ObjectValue { ["a"] = new NumberValue(10), ["b"] = new NumberValue(10) };
        var result = Eval("a > b", data);

        var boolVal = Assert.IsType<BoolValue>(result);
        Assert.False(boolVal.Value);
    }

    [Fact]
    public void NumberLessThanOrEqual_Smaller_ReturnsTrue()
    {
        var data = new ObjectValue { ["a"] = new NumberValue(5), ["b"] = new NumberValue(10) };
        var result = Eval("a <= b", data);

        var boolVal = Assert.IsType<BoolValue>(result);
        Assert.True(boolVal.Value);
    }

    [Fact]
    public void NumberLessThanOrEqual_Equal_ReturnsTrue()
    {
        var data = new ObjectValue { ["a"] = new NumberValue(10), ["b"] = new NumberValue(10) };
        var result = Eval("a <= b", data);

        var boolVal = Assert.IsType<BoolValue>(result);
        Assert.True(boolVal.Value);
    }

    [Fact]
    public void NumberGreaterThanOrEqual_Larger_ReturnsTrue()
    {
        var data = new ObjectValue { ["a"] = new NumberValue(20), ["b"] = new NumberValue(10) };
        var result = Eval("a >= b", data);

        var boolVal = Assert.IsType<BoolValue>(result);
        Assert.True(boolVal.Value);
    }

    [Fact]
    public void NumberGreaterThanOrEqual_Equal_ReturnsTrue()
    {
        var data = new ObjectValue { ["a"] = new NumberValue(10), ["b"] = new NumberValue(10) };
        var result = Eval("a >= b", data);

        var boolVal = Assert.IsType<BoolValue>(result);
        Assert.True(boolVal.Value);
    }

    #endregion

    #region String Comparisons

    [Fact]
    public void StringEqual_SameValues_ReturnsTrue()
    {
        var data = new ObjectValue { ["a"] = new StringValue("hello"), ["b"] = new StringValue("hello") };
        var result = Eval("a == b", data);

        var boolVal = Assert.IsType<BoolValue>(result);
        Assert.True(boolVal.Value);
    }

    [Fact]
    public void StringNotEqual_DifferentValues_ReturnsTrue()
    {
        var data = new ObjectValue { ["a"] = new StringValue("hello"), ["b"] = new StringValue("world") };
        var result = Eval("a != b", data);

        var boolVal = Assert.IsType<BoolValue>(result);
        Assert.True(boolVal.Value);
    }

    [Fact]
    public void StringLessThan_OrdinalComparison_ReturnsTrue()
    {
        var data = new ObjectValue { ["a"] = new StringValue("abc"), ["b"] = new StringValue("def") };
        var result = Eval("a < b", data);

        var boolVal = Assert.IsType<BoolValue>(result);
        Assert.True(boolVal.Value);
    }

    #endregion

    #region Null Comparisons

    [Fact]
    public void NullEqualNull_ReturnsTrue()
    {
        var data = new ObjectValue();
        var result = Eval("a == b", data);

        var boolVal = Assert.IsType<BoolValue>(result);
        Assert.True(boolVal.Value);
    }

    [Fact]
    public void NullNotEqualSomething_ReturnsTrue()
    {
        var data = new ObjectValue { ["b"] = new NumberValue(5) };
        var result = Eval("a != b", data);

        var boolVal = Assert.IsType<BoolValue>(result);
        Assert.True(boolVal.Value);
    }

    [Fact]
    public void NullLessThanSomething_ReturnsFalse()
    {
        var data = new ObjectValue { ["b"] = new NumberValue(5) };
        var result = Eval("a < b", data);

        var boolVal = Assert.IsType<BoolValue>(result);
        Assert.False(boolVal.Value);
    }

    #endregion

    #region Mixed Type Comparisons

    [Fact]
    public void MixedTypes_Equal_ReturnsFalse()
    {
        var data = new ObjectValue { ["a"] = new StringValue("5"), ["b"] = new NumberValue(5) };
        var result = Eval("a == b", data);

        var boolVal = Assert.IsType<BoolValue>(result);
        Assert.False(boolVal.Value);
    }

    [Fact]
    public void MixedTypes_NotEqual_ReturnsTrue()
    {
        var data = new ObjectValue { ["a"] = new StringValue("5"), ["b"] = new NumberValue(5) };
        var result = Eval("a != b", data);

        var boolVal = Assert.IsType<BoolValue>(result);
        Assert.True(boolVal.Value);
    }

    #endregion

    #region Logical NOT

    [Fact]
    public void Not_TrueValue_ReturnsFalse()
    {
        var data = new ObjectValue { ["flag"] = new BoolValue(true) };
        var result = Eval("!flag", data);

        var boolVal = Assert.IsType<BoolValue>(result);
        Assert.False(boolVal.Value);
    }

    [Fact]
    public void Not_FalseValue_ReturnsTrue()
    {
        var data = new ObjectValue { ["flag"] = new BoolValue(false) };
        var result = Eval("!flag", data);

        var boolVal = Assert.IsType<BoolValue>(result);
        Assert.True(boolVal.Value);
    }

    [Fact]
    public void Not_NullValue_ReturnsTrue()
    {
        var data = new ObjectValue();
        var result = Eval("!missing", data);

        var boolVal = Assert.IsType<BoolValue>(result);
        Assert.True(boolVal.Value);
    }

    [Fact]
    public void Not_EmptyString_ReturnsTrue()
    {
        var data = new ObjectValue { ["s"] = new StringValue("") };
        var result = Eval("!s", data);

        var boolVal = Assert.IsType<BoolValue>(result);
        Assert.True(boolVal.Value);
    }

    [Fact]
    public void Not_Zero_ReturnsTrue()
    {
        var data = new ObjectValue { ["n"] = new NumberValue(0) };
        var result = Eval("!n", data);

        var boolVal = Assert.IsType<BoolValue>(result);
        Assert.True(boolVal.Value);
    }

    [Fact]
    public void Not_NonEmptyString_ReturnsFalse()
    {
        var data = new ObjectValue { ["s"] = new StringValue("hello") };
        var result = Eval("!s", data);

        var boolVal = Assert.IsType<BoolValue>(result);
        Assert.False(boolVal.Value);
    }

    [Fact]
    public void Not_NonZeroNumber_ReturnsFalse()
    {
        var data = new ObjectValue { ["n"] = new NumberValue(1) };
        var result = Eval("!n", data);

        var boolVal = Assert.IsType<BoolValue>(result);
        Assert.False(boolVal.Value);
    }

    #endregion

    #region Comparison with Literals

    [Fact]
    public void ComparisonWithStringLiteral_Equal_ReturnsTrue()
    {
        var data = new ObjectValue { ["status"] = new StringValue("paid") };
        var result = Eval("status == \"paid\"", data);

        var boolVal = Assert.IsType<BoolValue>(result);
        Assert.True(boolVal.Value);
    }

    [Fact]
    public void ComparisonWithNumberLiteral_GreaterThan_ReturnsTrue()
    {
        var data = new ObjectValue { ["price"] = new NumberValue(150) };
        var result = Eval("price > 100", data);

        var boolVal = Assert.IsType<BoolValue>(result);
        Assert.True(boolVal.Value);
    }

    #endregion

    #region Bool Comparisons

    [Fact]
    public void BoolEqual_SameValues_ReturnsTrue()
    {
        var data = new ObjectValue { ["a"] = new BoolValue(true), ["b"] = new BoolValue(true) };
        var result = Eval("a == b", data);

        var boolVal = Assert.IsType<BoolValue>(result);
        Assert.True(boolVal.Value);
    }

    [Fact]
    public void BoolNotEqual_DifferentValues_ReturnsTrue()
    {
        var data = new ObjectValue { ["a"] = new BoolValue(true), ["b"] = new BoolValue(false) };
        var result = Eval("a != b", data);

        var boolVal = Assert.IsType<BoolValue>(result);
        Assert.True(boolVal.Value);
    }

    [Fact]
    public void BoolOrderedComparison_ReturnsFalse()
    {
        var data = new ObjectValue { ["a"] = new BoolValue(true), ["b"] = new BoolValue(false) };
        var result = Eval("a < b", data);

        var boolVal = Assert.IsType<BoolValue>(result);
        Assert.False(boolVal.Value);
    }

    #endregion

    #region Logical OR (||)

    [Fact]
    public void LogicalOr_LeftTruthy_ReturnsLeft()
    {
        var data = new ObjectValue { ["name"] = "John" };
        var result = Eval("name || 'Guest'", data);
        var str = Assert.IsType<StringValue>(result);
        Assert.Equal("John", str.Value);
    }

    [Fact]
    public void LogicalOr_LeftNull_ReturnsRight()
    {
        var data = new ObjectValue();
        var result = Eval("name || 'Guest'", data);
        var str = Assert.IsType<StringValue>(result);
        Assert.Equal("Guest", str.Value);
    }

    [Fact]
    public void LogicalOr_LeftEmptyString_ReturnsRight()
    {
        var data = new ObjectValue { ["name"] = "" };
        var result = Eval("name || 'Guest'", data);
        var str = Assert.IsType<StringValue>(result);
        Assert.Equal("Guest", str.Value);
    }

    [Fact]
    public void LogicalOr_LeftZero_ReturnsRight()
    {
        var data = new ObjectValue { ["count"] = 0m };
        var result = Eval("count || 42", data);
        var num = Assert.IsType<NumberValue>(result);
        Assert.Equal(42m, num.Value);
    }

    [Fact]
    public void LogicalOr_LeftFalse_ReturnsRight()
    {
        var data = new ObjectValue { ["active"] = false };
        var result = Eval("active || true", data);
        var b = Assert.IsType<BoolValue>(result);
        Assert.True(b.Value);
    }

    [Fact]
    public void LogicalOr_Chain_ReturnsFirstTruthy()
    {
        var data = new ObjectValue { ["c"] = "found" };
        var result = Eval("a || b || c", data);
        var str = Assert.IsType<StringValue>(result);
        Assert.Equal("found", str.Value);
    }

    [Fact]
    public void LogicalOr_LeftTruthy_DoesNotEvaluateRight()
    {
        // If right were evaluated, the missing filter would throw "No filter registry"
        var evaluator = new InlineExpressionEvaluator(); // no filter registry
        var ast = InlineExpressionParser.Parse("a || (b | upper)");
        var data = new ObjectValue { ["a"] = "truthy" };
        var context = new TemplateContext(data);
        var result = evaluator.Evaluate(ast, context);
        var str = Assert.IsType<StringValue>(result);
        Assert.Equal("truthy", str.Value); // short-circuited, no filter error
    }

    #endregion

    #region Logical AND (&&)

    [Fact]
    public void LogicalAnd_BothTruthy_ReturnsRight()
    {
        var data = new ObjectValue { ["a"] = "yes", ["b"] = "also yes" };
        var result = Eval("a && b", data);
        var str = Assert.IsType<StringValue>(result);
        Assert.Equal("also yes", str.Value);
    }

    [Fact]
    public void LogicalAnd_LeftFalsy_ReturnsLeft()
    {
        var data = new ObjectValue { ["a"] = "", ["b"] = "yes" };
        var result = Eval("a && b", data);
        var str = Assert.IsType<StringValue>(result);
        Assert.Equal("", str.Value);
    }

    [Fact]
    public void LogicalAnd_LeftNull_ReturnsNull()
    {
        var data = new ObjectValue { ["b"] = "yes" };
        var result = Eval("a && b", data);
        Assert.IsType<NullValue>(result);
    }

    [Fact]
    public void LogicalAnd_LeftZero_ReturnsZero()
    {
        var data = new ObjectValue { ["a"] = 0m, ["b"] = 42m };
        var result = Eval("a && b", data);
        var num = Assert.IsType<NumberValue>(result);
        Assert.Equal(0m, num.Value);
    }

    [Fact]
    public void LogicalAnd_Chain_AllTruthy_ReturnsLast()
    {
        var data = new ObjectValue { ["a"] = "x", ["b"] = "y", ["c"] = "z" };
        var result = Eval("a && b && c", data);
        var str = Assert.IsType<StringValue>(result);
        Assert.Equal("z", str.Value);
    }

    [Fact]
    public void LogicalAnd_LeftFalsy_DoesNotEvaluateRight()
    {
        // If right were evaluated, the missing filter would throw "No filter registry"
        var evaluator = new InlineExpressionEvaluator(); // no filter registry
        var ast = InlineExpressionParser.Parse("a && (b | upper)");
        var data = new ObjectValue(); // a is null (falsy)
        var context = new TemplateContext(data);
        var result = evaluator.Evaluate(ast, context);
        Assert.IsType<NullValue>(result); // short-circuited, no filter error
    }

    #endregion

    #region Boolean and Null Literal Comparisons

    [Fact]
    public void Comparison_VariableEqualsTrueLiteral_ReturnsTrue()
    {
        var data = new ObjectValue { ["active"] = new BoolValue(true) };
        var result = Eval("active == true", data);

        var boolResult = Assert.IsType<BoolValue>(result);
        Assert.True(boolResult.Value);
    }

    [Fact]
    public void Comparison_VariableEqualsNullLiteral_WhenNull_ReturnsTrue()
    {
        var data = new ObjectValue();
        var result = Eval("missing == null", data);

        var boolResult = Assert.IsType<BoolValue>(result);
        Assert.True(boolResult.Value);
    }

    [Fact]
    public void Comparison_VariableNotEqualsNull_WhenExists_ReturnsTrue()
    {
        var data = new ObjectValue { ["name"] = new StringValue("John") };
        var result = Eval("name != null", data);

        var boolResult = Assert.IsType<BoolValue>(result);
        Assert.True(boolResult.Value);
    }

    [Fact]
    public void Comparison_VariableEqualsFalseLiteral_ReturnsTrue()
    {
        var data = new ObjectValue { ["disabled"] = new BoolValue(false) };
        var result = Eval("disabled == false", data);

        var boolResult = Assert.IsType<BoolValue>(result);
        Assert.True(boolResult.Value);
    }

    #endregion
}
