using Xunit;

namespace FlexRender.Tests.Values;

public class BoolValueTests
{
    [Fact]
    public void Constructor_StoresTrue()
    {
        var boolValue = new BoolValue(true);

        Assert.True(boolValue.Value);
    }

    [Fact]
    public void Constructor_StoresFalse()
    {
        var boolValue = new BoolValue(false);

        Assert.False(boolValue.Value);
    }

    [Fact]
    public void ImplicitConversion_FromBool_CreatesBoolValue()
    {
        TemplateValue value = true;

        Assert.IsType<BoolValue>(value);
        Assert.True(((BoolValue)value).Value);
    }

    [Fact]
    public void Equals_SameValue_ReturnsTrue()
    {
        var value1 = new BoolValue(true);
        var value2 = new BoolValue(true);

        Assert.Equal(value1, value2);
    }

    [Fact]
    public void Equals_DifferentValue_ReturnsFalse()
    {
        var value1 = new BoolValue(true);
        var value2 = new BoolValue(false);

        Assert.NotEqual(value1, value2);
    }

    [Fact]
    public void ToString_True_ReturnsLowercase()
    {
        var boolValue = new BoolValue(true);

        Assert.Equal("true", boolValue.ToString());
    }

    [Fact]
    public void ToString_False_ReturnsLowercase()
    {
        var boolValue = new BoolValue(false);

        Assert.Equal("false", boolValue.ToString());
    }
}
