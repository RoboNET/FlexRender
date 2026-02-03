using Xunit;

namespace FlexRender.Tests.Values;

public class ArrayValueTests
{
    [Fact]
    public void Constructor_StoresItems()
    {
        var items = new List<TemplateValue> { new StringValue("a"), new StringValue("b") };
        var arrayValue = new ArrayValue(items);

        Assert.Equal(2, arrayValue.Items.Count);
        Assert.Equal("a", ((StringValue)arrayValue.Items[0]).Value);
        Assert.Equal("b", ((StringValue)arrayValue.Items[1]).Value);
    }

    [Fact]
    public void Constructor_HandlesEmptyList()
    {
        var arrayValue = new ArrayValue(new List<TemplateValue>());

        Assert.Empty(arrayValue.Items);
    }

    [Fact]
    public void Constructor_HandlesMixedTypes()
    {
        var items = new List<TemplateValue>
        {
            new StringValue("text"),
            new NumberValue(42),
            new BoolValue(true),
            NullValue.Instance
        };
        var arrayValue = new ArrayValue(items);

        Assert.Equal(4, arrayValue.Items.Count);
        Assert.IsType<StringValue>(arrayValue.Items[0]);
        Assert.IsType<NumberValue>(arrayValue.Items[1]);
        Assert.IsType<BoolValue>(arrayValue.Items[2]);
        Assert.IsType<NullValue>(arrayValue.Items[3]);
    }

    [Fact]
    public void Items_IsImmutable()
    {
        var items = new List<TemplateValue> { new StringValue("a") };
        var arrayValue = new ArrayValue(items);

        // Modifying original list should not affect ArrayValue
        items.Add(new StringValue("b"));

        Assert.Single(arrayValue.Items);
    }

    [Fact]
    public void Count_ReturnsItemCount()
    {
        var arrayValue = new ArrayValue(new List<TemplateValue>
        {
            new StringValue("a"),
            new StringValue("b"),
            new StringValue("c")
        });

        Assert.Equal(3, arrayValue.Count);
    }

    [Fact]
    public void Indexer_ReturnsCorrectItem()
    {
        var arrayValue = new ArrayValue(new List<TemplateValue>
        {
            new StringValue("first"),
            new StringValue("second")
        });

        Assert.Equal("first", ((StringValue)arrayValue[0]).Value);
        Assert.Equal("second", ((StringValue)arrayValue[1]).Value);
    }

    [Fact]
    public void Equals_SameItems_ReturnsTrue()
    {
        var value1 = new ArrayValue(new List<TemplateValue> { new StringValue("a"), new NumberValue(1) });
        var value2 = new ArrayValue(new List<TemplateValue> { new StringValue("a"), new NumberValue(1) });

        Assert.Equal(value1, value2);
    }

    [Fact]
    public void Equals_DifferentItems_ReturnsFalse()
    {
        var value1 = new ArrayValue(new List<TemplateValue> { new StringValue("a") });
        var value2 = new ArrayValue(new List<TemplateValue> { new StringValue("b") });

        Assert.NotEqual(value1, value2);
    }

    [Fact]
    public void Equals_DifferentLength_ReturnsFalse()
    {
        var value1 = new ArrayValue(new List<TemplateValue> { new StringValue("a") });
        var value2 = new ArrayValue(new List<TemplateValue> { new StringValue("a"), new StringValue("b") });

        Assert.NotEqual(value1, value2);
    }

    [Fact]
    public void ToString_ReturnsArrayRepresentation()
    {
        var arrayValue = new ArrayValue(new List<TemplateValue>
        {
            new StringValue("a"),
            new NumberValue(1)
        });

        Assert.Equal("[a, 1]", arrayValue.ToString());
    }

    [Fact]
    public void GetEnumerator_AllowsIteration()
    {
        var arrayValue = new ArrayValue(new List<TemplateValue>
        {
            new StringValue("a"),
            new StringValue("b")
        });

        var values = new List<string>();
        foreach (var item in arrayValue)
        {
            values.Add(((StringValue)item).Value);
        }

        Assert.Equal(new[] { "a", "b" }, values);
    }
}
