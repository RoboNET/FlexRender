using FlexRender.Layout;
using FlexRender.Layout.Units;
using Xunit;

namespace FlexRender.Tests.Layout;

public class FlexContainerPropertiesTests
{
    [Fact]
    public void Default_HasCorrectValues()
    {
        var props = FlexContainerProperties.Default;

        Assert.Equal(FlexDirection.Row, props.Direction);
        Assert.Equal(FlexWrap.NoWrap, props.Wrap);
        Assert.Equal(Unit.Pixels(0), props.Gap);
        Assert.Equal(Unit.Pixels(0), props.Padding);
        Assert.Equal(JustifyContent.Start, props.Justify);
        Assert.Equal(AlignItems.Stretch, props.Align);
        Assert.Equal(AlignContent.Stretch, props.AlignContent);
    }

    [Fact]
    public void Constructor_SetsAllValues()
    {
        var props = new FlexContainerProperties(
            Direction: FlexDirection.Column,
            Wrap: FlexWrap.Wrap,
            Gap: Unit.Pixels(10),
            Padding: Unit.Percent(5),
            Justify: JustifyContent.SpaceBetween,
            Align: AlignItems.Center,
            AlignContent: AlignContent.SpaceAround
        );

        Assert.Equal(FlexDirection.Column, props.Direction);
        Assert.Equal(FlexWrap.Wrap, props.Wrap);
        Assert.Equal(Unit.Pixels(10), props.Gap);
        Assert.Equal(Unit.Percent(5), props.Padding);
        Assert.Equal(JustifyContent.SpaceBetween, props.Justify);
        Assert.Equal(AlignItems.Center, props.Align);
        Assert.Equal(AlignContent.SpaceAround, props.AlignContent);
    }

    [Fact]
    public void With_Direction_ReturnsNewInstance()
    {
        var original = FlexContainerProperties.Default;

        var modified = original with { Direction = FlexDirection.Column };

        Assert.Equal(FlexDirection.Row, original.Direction);
        Assert.Equal(FlexDirection.Column, modified.Direction);
    }

    [Fact]
    public void IsMainAxisHorizontal_Row_ReturnsTrue()
    {
        var props = FlexContainerProperties.Default;

        Assert.True(props.IsMainAxisHorizontal);
    }

    [Fact]
    public void IsMainAxisHorizontal_Column_ReturnsFalse()
    {
        var props = FlexContainerProperties.Default with { Direction = FlexDirection.Column };

        Assert.False(props.IsMainAxisHorizontal);
    }
}
