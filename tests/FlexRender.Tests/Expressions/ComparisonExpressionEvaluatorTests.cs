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
