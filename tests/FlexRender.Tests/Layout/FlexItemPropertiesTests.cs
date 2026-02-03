using FlexRender.Layout;
using FlexRender.Layout.Units;
using Xunit;

namespace FlexRender.Tests.Layout;

public class FlexItemPropertiesTests
{
    [Fact]
    public void Default_HasCorrectValues()
    {
        var props = FlexItemProperties.Default;

        Assert.Equal(0f, props.Grow);
        Assert.Equal(1f, props.Shrink);
        Assert.Equal(Unit.Auto, props.Basis);
        Assert.Equal(AlignSelf.Auto, props.AlignSelf);
        Assert.Equal(0, props.Order);
    }

    [Fact]
    public void Constructor_SetsAllValues()
    {
        var props = new FlexItemProperties(
            Grow: 2f,
            Shrink: 0.5f,
            Basis: Unit.Pixels(100),
            AlignSelf: AlignSelf.Center,
            Order: 5
        );

        Assert.Equal(2f, props.Grow);
        Assert.Equal(0.5f, props.Shrink);
        Assert.Equal(Unit.Pixels(100), props.Basis);
        Assert.Equal(AlignSelf.Center, props.AlignSelf);
        Assert.Equal(5, props.Order);
    }

    [Fact]
    public void With_Grow_ReturnsNewInstance()
    {
        var original = FlexItemProperties.Default;

        var modified = original with { Grow = 1f };

        Assert.Equal(0f, original.Grow);
        Assert.Equal(1f, modified.Grow);
    }

    [Fact]
    public void Equals_SameValues_ReturnsTrue()
    {
        var props1 = new FlexItemProperties(1f, 0f, Unit.Percent(50), AlignSelf.End, 1);
        var props2 = new FlexItemProperties(1f, 0f, Unit.Percent(50), AlignSelf.End, 1);

        Assert.Equal(props1, props2);
    }
}
