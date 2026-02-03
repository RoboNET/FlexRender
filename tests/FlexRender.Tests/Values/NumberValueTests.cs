using Xunit;

namespace FlexRender.Tests.Values;

public class NumberValueTests
{
    [Fact]
    public void Constructor_StoresDecimalValue()
    {
        var numberValue = new NumberValue(123.45m);

        Assert.Equal(123.45m, numberValue.Value);
    }

    [Fact]
    public void Constructor_HandlesZero()
    {
        var numberValue = new NumberValue(0m);

        Assert.Equal(0m, numberValue.Value);
    }

    [Fact]
    public void Constructor_HandlesNegative()
    {
        var numberValue = new NumberValue(-99.99m);

        Assert.Equal(-99.99m, numberValue.Value);
    }

    [Fact]
    public void ImplicitConversion_FromInt_CreatesNumberValue()
    {
        TemplateValue value = 42;

        Assert.IsType<NumberValue>(value);
        Assert.Equal(42m, ((NumberValue)value).Value);
    }

    [Fact]
    public void ImplicitConversion_FromLong_CreatesNumberValue()
    {
        TemplateValue value = 9999999999L;

        Assert.IsType<NumberValue>(value);
        Assert.Equal(9999999999m, ((NumberValue)value).Value);
    }

    [Fact]
    public void ImplicitConversion_FromDecimal_CreatesNumberValue()
    {
        TemplateValue value = 123.45m;

        Assert.IsType<NumberValue>(value);
        Assert.Equal(123.45m, ((NumberValue)value).Value);
    }

    [Fact]
    public void ImplicitConversion_FromDouble_CreatesNumberValue()
    {
        TemplateValue value = 3.14;

        Assert.IsType<NumberValue>(value);
        Assert.Equal(3.14m, ((NumberValue)value).Value);
    }

    [Fact]
    public void Equals_SameValue_ReturnsTrue()
    {
        var value1 = new NumberValue(100m);
        var value2 = new NumberValue(100m);

        Assert.Equal(value1, value2);
    }

    [Fact]
    public void Equals_DifferentValue_ReturnsFalse()
    {
        var value1 = new NumberValue(100m);
        var value2 = new NumberValue(200m);

        Assert.NotEqual(value1, value2);
    }

    [Fact]
    public void ToString_ReturnsFormattedValue()
    {
        var numberValue = new NumberValue(123.45m);

        Assert.Equal("123.45", numberValue.ToString());
    }
}
