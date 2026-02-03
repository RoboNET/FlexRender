using Xunit;

namespace FlexRender.Tests.Values;

public class StringValueTests
{
    [Fact]
    public void Constructor_StoresValue()
    {
        var stringValue = new StringValue("hello");

        Assert.Equal("hello", stringValue.Value);
    }

    [Fact]
    public void Constructor_HandlesEmptyString()
    {
        var stringValue = new StringValue("");

        Assert.Equal("", stringValue.Value);
    }

    [Fact]
    public void ImplicitConversion_FromString_CreatesStringValue()
    {
        TemplateValue value = "test";

        Assert.IsType<StringValue>(value);
        Assert.Equal("test", ((StringValue)value).Value);
    }

    [Fact]
    public void Equals_SameValue_ReturnsTrue()
    {
        var value1 = new StringValue("test");
        var value2 = new StringValue("test");

        Assert.Equal(value1, value2);
    }

    [Fact]
    public void Equals_DifferentValue_ReturnsFalse()
    {
        var value1 = new StringValue("test1");
        var value2 = new StringValue("test2");

        Assert.NotEqual(value1, value2);
    }

    [Fact]
    public void ToString_ReturnsValue()
    {
        var stringValue = new StringValue("hello");

        Assert.Equal("hello", stringValue.ToString());
    }

    [Fact]
    public void Constructor_NullValue_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => new StringValue(null!));
    }
}
