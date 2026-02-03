using Xunit;

namespace FlexRender.Tests;

public class ITemplateDataTests
{
    private class SampleData : ITemplateData
    {
        public string Name { get; set; } = "";
        public int Age { get; set; }

        public ObjectValue ToTemplateValue()
        {
            return new ObjectValue
            {
                ["name"] = Name,
                ["age"] = Age
            };
        }
    }

    [Fact]
    public void ToTemplateValue_ReturnsObjectValue()
    {
        var data = new SampleData { Name = "John", Age = 30 };

        var result = data.ToTemplateValue();

        Assert.IsType<ObjectValue>(result);
    }

    [Fact]
    public void ToTemplateValue_ContainsCorrectValues()
    {
        var data = new SampleData { Name = "Jane", Age = 25 };

        var result = data.ToTemplateValue();

        Assert.Equal("Jane", ((StringValue)result["name"]).Value);
        Assert.Equal(25m, ((NumberValue)result["age"]).Value);
    }

    [Fact]
    public void ToTemplateValue_CanBeUsedPolymorphically()
    {
        ITemplateData data = new SampleData { Name = "Test", Age = 42 };

        ObjectValue result = data.ToTemplateValue();

        Assert.Equal("Test", ((StringValue)result["name"]).Value);
    }
}
