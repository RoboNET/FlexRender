using FlexRender.TemplateEngine;
using Xunit;

namespace FlexRender.Tests.TemplateEngine;

public class ExpressionEvaluatorTests
{
    // Security Tests: Input Length Validation
    [Fact]
    public void Resolve_PathExceedsMaxLength_ThrowsTemplateEngineException()
    {
        var data = new ObjectValue { ["name"] = "John" };
        var context = new TemplateContext(data);
        var longPath = new string('a', 1001); // Exceeds 1000 char limit

        var ex = Assert.Throws<TemplateEngineException>(() => ExpressionEvaluator.Resolve(longPath, context));
        Assert.Contains("exceeds maximum", ex.Message);
    }

    [Fact]
    public void Resolve_PathAtMaxLength_Succeeds()
    {
        var data = new ObjectValue { ["name"] = "John" };
        var context = new TemplateContext(data);
        var maxPath = new string('a', 1000); // Exactly at limit

        // Should not throw, just return NullValue for non-existent path
        var result = ExpressionEvaluator.Resolve(maxPath, context);
        Assert.IsType<NullValue>(result);
    }

    // Security Tests: Array Index Bounds Validation
    [Fact]
    public void Resolve_ArrayIndexExceedsMaximum_ThrowsTemplateEngineException()
    {
        var items = new ArrayValue(new TemplateValue[] { new StringValue("item") });
        var data = new ObjectValue { ["items"] = items };
        var context = new TemplateContext(data);

        var ex = Assert.Throws<TemplateEngineException>(() => ExpressionEvaluator.Resolve("items[10001]", context));
        Assert.Contains("exceeds maximum", ex.Message);
    }

    [Fact]
    public void Resolve_ArrayIndexAtMaximum_Succeeds()
    {
        var items = new ArrayValue(new TemplateValue[] { new StringValue("item") });
        var data = new ObjectValue { ["items"] = items };
        var context = new TemplateContext(data);

        // Should not throw, just return NullValue for out-of-bounds index
        var result = ExpressionEvaluator.Resolve("items[10000]", context);
        Assert.IsType<NullValue>(result);
    }

    // Task 6: Simple Path Resolution
    [Fact]
    public void Resolve_SimpleProperty_ReturnsValue()
    {
        var data = new ObjectValue { ["name"] = "John" };
        var context = new TemplateContext(data);

        var result = ExpressionEvaluator.Resolve("name", context);

        Assert.IsType<StringValue>(result);
        Assert.Equal("John", ((StringValue)result).Value);
    }

    [Fact]
    public void Resolve_MissingProperty_ReturnsNullValue()
    {
        var data = new ObjectValue();
        var context = new TemplateContext(data);

        var result = ExpressionEvaluator.Resolve("missing", context);

        Assert.IsType<NullValue>(result);
    }

    [Fact]
    public void Resolve_DotNotation_ResolvesNestedProperty()
    {
        var address = new ObjectValue { ["city"] = "NYC" };
        var user = new ObjectValue { ["address"] = address };
        var data = new ObjectValue { ["user"] = user };
        var context = new TemplateContext(data);

        var result = ExpressionEvaluator.Resolve("user.address.city", context);

        Assert.Equal("NYC", ((StringValue)result).Value);
    }

    [Fact]
    public void Resolve_DotNotation_PartialMissing_ReturnsNullValue()
    {
        var data = new ObjectValue { ["user"] = new ObjectValue() };
        var context = new TemplateContext(data);

        var result = ExpressionEvaluator.Resolve("user.address.city", context);

        Assert.IsType<NullValue>(result);
    }

    [Fact]
    public void Resolve_NumberValue_ReturnsNumber()
    {
        var data = new ObjectValue { ["count"] = 42 };
        var context = new TemplateContext(data);

        var result = ExpressionEvaluator.Resolve("count", context);

        Assert.Equal(42m, ((NumberValue)result).Value);
    }

    [Fact]
    public void Resolve_BoolValue_ReturnsBool()
    {
        var data = new ObjectValue { ["active"] = true };
        var context = new TemplateContext(data);

        var result = ExpressionEvaluator.Resolve("active", context);

        Assert.True(((BoolValue)result).Value);
    }

    [Fact]
    public void Resolve_DotNotation_OnNonObject_ReturnsNullValue()
    {
        var data = new ObjectValue { ["name"] = "John" };
        var context = new TemplateContext(data);

        var result = ExpressionEvaluator.Resolve("name.length", context);

        Assert.IsType<NullValue>(result);
    }

    // Task 7: Array Indexing and Loop Variables
    [Fact]
    public void Resolve_ArrayIndex_ReturnsElement()
    {
        var items = new ArrayValue(new TemplateValue[]
        {
            new StringValue("first"),
            new StringValue("second"),
            new StringValue("third")
        });
        var data = new ObjectValue { ["items"] = items };
        var context = new TemplateContext(data);

        var result = ExpressionEvaluator.Resolve("items[1]", context);

        Assert.Equal("second", ((StringValue)result).Value);
    }

    [Fact]
    public void Resolve_ArrayIndexOutOfBounds_ReturnsNullValue()
    {
        var items = new ArrayValue(new TemplateValue[] { new StringValue("only") });
        var data = new ObjectValue { ["items"] = items };
        var context = new TemplateContext(data);

        var result = ExpressionEvaluator.Resolve("items[5]", context);

        Assert.IsType<NullValue>(result);
    }

    [Fact]
    public void Resolve_ArrayIndexWithProperty_ResolvesChained()
    {
        var item = new ObjectValue { ["name"] = "Product A", ["price"] = 100 };
        var items = new ArrayValue(new TemplateValue[] { item });
        var data = new ObjectValue { ["items"] = items };
        var context = new TemplateContext(data);

        var result = ExpressionEvaluator.Resolve("items[0].name", context);

        Assert.Equal("Product A", ((StringValue)result).Value);
    }

    [Fact]
    public void Resolve_LoopIndex_ReturnsCurrentIndex()
    {
        var data = new ObjectValue();
        var context = new TemplateContext(data);
        context.SetLoopVariables(index: 2, count: 5);

        var result = ExpressionEvaluator.Resolve("@index", context);

        Assert.Equal(2m, ((NumberValue)result).Value);
    }

    [Fact]
    public void Resolve_LoopFirst_ReturnsBool()
    {
        var data = new ObjectValue();
        var context = new TemplateContext(data);
        context.SetLoopVariables(index: 0, count: 5);

        var result = ExpressionEvaluator.Resolve("@first", context);

        Assert.True(((BoolValue)result).Value);
    }

    [Fact]
    public void Resolve_LoopLast_ReturnsBool()
    {
        var data = new ObjectValue();
        var context = new TemplateContext(data);
        context.SetLoopVariables(index: 4, count: 5);

        var result = ExpressionEvaluator.Resolve("@last", context);

        Assert.True(((BoolValue)result).Value);
    }

    [Fact]
    public void Resolve_LoopVariableOutsideLoop_ReturnsNullOrFalse()
    {
        var data = new ObjectValue();
        var context = new TemplateContext(data);

        var indexResult = ExpressionEvaluator.Resolve("@index", context);
        var firstResult = ExpressionEvaluator.Resolve("@first", context);
        var lastResult = ExpressionEvaluator.Resolve("@last", context);

        Assert.IsType<NullValue>(indexResult);
        Assert.False(((BoolValue)firstResult).Value);
        Assert.False(((BoolValue)lastResult).Value);
    }

    [Fact]
    public void Resolve_ComplexPath_WithArrayAndDotNotation()
    {
        var address = new ObjectValue { ["street"] = "123 Main St" };
        var user = new ObjectValue { ["name"] = "John", ["address"] = address };
        var users = new ArrayValue(new TemplateValue[] { user });
        var data = new ObjectValue { ["users"] = users };
        var context = new TemplateContext(data);

        var result = ExpressionEvaluator.Resolve("users[0].address.street", context);

        Assert.Equal("123 Main St", ((StringValue)result).Value);
    }

    // Task 8: Truthiness Check
    [Theory]
    [InlineData(true, true)]
    [InlineData(false, false)]
    public void IsTruthy_BoolValue_ReturnsCorrectly(bool input, bool expected)
    {
        var result = ExpressionEvaluator.IsTruthy(new BoolValue(input));

        Assert.Equal(expected, result);
    }

    [Fact]
    public void IsTruthy_NullValue_ReturnsFalse()
    {
        var result = ExpressionEvaluator.IsTruthy(NullValue.Instance);

        Assert.False(result);
    }

    [Theory]
    [InlineData("hello", true)]
    [InlineData("", false)]
    public void IsTruthy_StringValue_ReturnsBasedOnContent(string input, bool expected)
    {
        var result = ExpressionEvaluator.IsTruthy(new StringValue(input));

        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData(0, false)]
    [InlineData(1, true)]
    [InlineData(-1, true)]
    [InlineData(0.0001, true)]
    public void IsTruthy_NumberValue_ReturnsBasedOnValue(double input, bool expected)
    {
        var result = ExpressionEvaluator.IsTruthy(new NumberValue((decimal)input));

        Assert.Equal(expected, result);
    }

    [Fact]
    public void IsTruthy_EmptyArray_ReturnsFalse()
    {
        var result = ExpressionEvaluator.IsTruthy(new ArrayValue(Array.Empty<TemplateValue>()));

        Assert.False(result);
    }

    [Fact]
    public void IsTruthy_NonEmptyArray_ReturnsTrue()
    {
        var result = ExpressionEvaluator.IsTruthy(new ArrayValue(new[] { new StringValue("item") }));

        Assert.True(result);
    }

    [Fact]
    public void IsTruthy_EmptyObject_ReturnsFalse()
    {
        var result = ExpressionEvaluator.IsTruthy(new ObjectValue());

        Assert.False(result);
    }

    [Fact]
    public void IsTruthy_NonEmptyObject_ReturnsTrue()
    {
        var obj = new ObjectValue { ["key"] = "value" };
        var result = ExpressionEvaluator.IsTruthy(obj);

        Assert.True(result);
    }

    [Fact]
    public void Resolve_CurrentScope_ReturnsScope()
    {
        var data = new StringValue("current");
        var context = new TemplateContext(data);

        var result = ExpressionEvaluator.Resolve(".", context);

        Assert.Same(data, result);
    }

    [Fact]
    public void Resolve_LoopKey_ReturnsStringValue()
    {
        var data = new ObjectValue { ["x"] = "test" };
        var context = new TemplateContext(data);
        context.SetLoopVariables(0, 3);
        context.SetLoopKey("myKey");

        var result = ExpressionEvaluator.Resolve("@key", context);

        var str = Assert.IsType<StringValue>(result);
        Assert.Equal("myKey", str.Value);
    }

    [Fact]
    public void Resolve_LoopKey_OutsideLoop_ReturnsNullValue()
    {
        var data = new ObjectValue { ["x"] = "test" };
        var context = new TemplateContext(data);

        var result = ExpressionEvaluator.Resolve("@key", context);

        Assert.IsType<NullValue>(result);
    }

    [Fact]
    public void Resolve_LoopKey_InArrayLoop_ReturnsNullValue()
    {
        var data = new ObjectValue { ["x"] = "test" };
        var context = new TemplateContext(data);
        context.SetLoopVariables(0, 3);
        // LoopKey not set — simulates array iteration

        var result = ExpressionEvaluator.Resolve("@key", context);

        Assert.IsType<NullValue>(result);
    }
}
