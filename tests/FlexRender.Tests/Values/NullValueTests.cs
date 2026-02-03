using Xunit;

namespace FlexRender.Tests.Values;

public class NullValueTests
{
    [Fact]
    public void Instance_ReturnsSingleton()
    {
        var instance1 = NullValue.Instance;
        var instance2 = NullValue.Instance;

        Assert.Same(instance1, instance2);
    }

    [Fact]
    public void Equals_TwoNullValues_ReturnsTrue()
    {
        var value1 = NullValue.Instance;
        var value2 = NullValue.Instance;

        Assert.Equal(value1, value2);
    }

    [Fact]
    public void Equals_NullValueAndOther_ReturnsFalse()
    {
        TemplateValue nullValue = NullValue.Instance;
        TemplateValue stringValue = new StringValue("test");

        Assert.NotEqual(nullValue, stringValue);
    }

    [Fact]
    public void ToString_ReturnsNull()
    {
        var nullValue = NullValue.Instance;

        Assert.Equal("null", nullValue.ToString());
    }
}
