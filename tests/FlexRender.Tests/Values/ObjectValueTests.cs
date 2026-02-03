using Xunit;

namespace FlexRender.Tests.Values;

public class ObjectValueTests
{
    [Fact]
    public void Indexer_SetAndGet_Works()
    {
        var obj = new ObjectValue();
        obj["name"] = new StringValue("John");

        Assert.Equal("John", ((StringValue)obj["name"]).Value);
    }

    [Fact]
    public void Indexer_MissingKey_ReturnsNullValue()
    {
        var obj = new ObjectValue();

        Assert.IsType<NullValue>(obj["nonexistent"]);
        Assert.Same(NullValue.Instance, obj["nonexistent"]);
    }

    [Fact]
    public void Indexer_OverwriteExisting_Works()
    {
        var obj = new ObjectValue();
        obj["key"] = new StringValue("first");
        obj["key"] = new StringValue("second");

        Assert.Equal("second", ((StringValue)obj["key"]).Value);
    }

    [Fact]
    public void Keys_ReturnsAllPropertyNames()
    {
        var obj = new ObjectValue();
        obj["a"] = new StringValue("1");
        obj["b"] = new StringValue("2");
        obj["c"] = new StringValue("3");

        var keys = obj.Keys.ToList();

        Assert.Equal(3, keys.Count);
        Assert.Contains("a", keys);
        Assert.Contains("b", keys);
        Assert.Contains("c", keys);
    }

    [Fact]
    public void Keys_Empty_ReturnsEmptyEnumerable()
    {
        var obj = new ObjectValue();

        Assert.Empty(obj.Keys);
    }

    [Fact]
    public void ContainsKey_ExistingKey_ReturnsTrue()
    {
        var obj = new ObjectValue();
        obj["exists"] = new StringValue("value");

        Assert.True(obj.ContainsKey("exists"));
    }

    [Fact]
    public void ContainsKey_MissingKey_ReturnsFalse()
    {
        var obj = new ObjectValue();

        Assert.False(obj.ContainsKey("missing"));
    }

    [Fact]
    public void TryGetValue_ExistingKey_ReturnsTrueAndValue()
    {
        var obj = new ObjectValue();
        obj["key"] = new StringValue("value");

        var found = obj.TryGetValue("key", out var result);

        Assert.True(found);
        Assert.Equal("value", ((StringValue)result!).Value);
    }

    [Fact]
    public void TryGetValue_MissingKey_ReturnsFalseAndNull()
    {
        var obj = new ObjectValue();

        var found = obj.TryGetValue("missing", out var result);

        Assert.False(found);
        Assert.Null(result);
    }

    [Fact]
    public void ImplicitConversion_InIndexer_Works()
    {
        var obj = new ObjectValue();
        obj["name"] = "John";
        obj["age"] = 30;
        obj["active"] = true;

        Assert.IsType<StringValue>(obj["name"]);
        Assert.IsType<NumberValue>(obj["age"]);
        Assert.IsType<BoolValue>(obj["active"]);
    }

    [Fact]
    public void Equals_SameProperties_ReturnsTrue()
    {
        var obj1 = new ObjectValue();
        obj1["a"] = new StringValue("1");
        obj1["b"] = new NumberValue(2);

        var obj2 = new ObjectValue();
        obj2["a"] = new StringValue("1");
        obj2["b"] = new NumberValue(2);

        Assert.Equal(obj1, obj2);
    }

    [Fact]
    public void Equals_DifferentValues_ReturnsFalse()
    {
        var obj1 = new ObjectValue();
        obj1["a"] = new StringValue("1");

        var obj2 = new ObjectValue();
        obj2["a"] = new StringValue("2");

        Assert.NotEqual(obj1, obj2);
    }

    [Fact]
    public void Equals_DifferentKeys_ReturnsFalse()
    {
        var obj1 = new ObjectValue();
        obj1["a"] = new StringValue("1");

        var obj2 = new ObjectValue();
        obj2["b"] = new StringValue("1");

        Assert.NotEqual(obj1, obj2);
    }

    [Fact]
    public void Equals_DifferentCount_ReturnsFalse()
    {
        var obj1 = new ObjectValue();
        obj1["a"] = new StringValue("1");

        var obj2 = new ObjectValue();
        obj2["a"] = new StringValue("1");
        obj2["b"] = new StringValue("2");

        Assert.NotEqual(obj1, obj2);
    }

    [Fact]
    public void Count_ReturnsPropertyCount()
    {
        var obj = new ObjectValue();
        obj["a"] = new StringValue("1");
        obj["b"] = new StringValue("2");

        Assert.Equal(2, obj.Count);
    }

    [Fact]
    public void NestedObjects_Work()
    {
        var inner = new ObjectValue();
        inner["value"] = new NumberValue(42);

        var outer = new ObjectValue();
        outer["nested"] = inner;

        var retrieved = (ObjectValue)outer["nested"];
        Assert.Equal(42m, ((NumberValue)retrieved["value"]).Value);
    }

    [Fact]
    public void ToString_ReturnsObjectRepresentation()
    {
        var obj = new ObjectValue();
        obj["name"] = new StringValue("test");

        var result = obj.ToString();

        Assert.Contains("name", result);
        Assert.Contains("test", result);
    }
}
