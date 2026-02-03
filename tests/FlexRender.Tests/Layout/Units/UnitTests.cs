using FlexRender.Layout.Units;
using Xunit;

namespace FlexRender.Tests.Layout.Units;

public class UnitTests
{
    [Fact]
    public void Pixels_StoresValue()
    {
        var unit = Unit.Pixels(100);

        Assert.Equal(UnitType.Pixels, unit.Type);
        Assert.Equal(100f, unit.Value);
    }

    [Fact]
    public void Percent_StoresValue()
    {
        var unit = Unit.Percent(50);

        Assert.Equal(UnitType.Percent, unit.Type);
        Assert.Equal(50f, unit.Value);
    }

    [Fact]
    public void Em_StoresValue()
    {
        var unit = Unit.Em(1.5f);

        Assert.Equal(UnitType.Em, unit.Type);
        Assert.Equal(1.5f, unit.Value);
    }

    [Fact]
    public void Auto_HasAutoType()
    {
        var unit = Unit.Auto;

        Assert.Equal(UnitType.Auto, unit.Type);
    }

    [Fact]
    public void Resolve_Pixels_ReturnsValue()
    {
        var unit = Unit.Pixels(100);

        var result = unit.Resolve(parentSize: 500, fontSize: 16);

        Assert.Equal(100f, result);
    }

    [Fact]
    public void Resolve_Percent_CalculatesFromParent()
    {
        var unit = Unit.Percent(50);

        var result = unit.Resolve(parentSize: 200, fontSize: 16);

        Assert.Equal(100f, result);
    }

    [Fact]
    public void Resolve_Em_CalculatesFromFontSize()
    {
        var unit = Unit.Em(1.5f);

        var result = unit.Resolve(parentSize: 500, fontSize: 16);

        Assert.Equal(24f, result);
    }

    [Fact]
    public void Resolve_Auto_ReturnsNull()
    {
        var unit = Unit.Auto;

        var result = unit.Resolve(parentSize: 500, fontSize: 16);

        Assert.Null(result);
    }

    [Fact]
    public void Equals_SameValues_ReturnsTrue()
    {
        var unit1 = Unit.Pixels(100);
        var unit2 = Unit.Pixels(100);

        Assert.Equal(unit1, unit2);
    }

    [Fact]
    public void Equals_DifferentType_ReturnsFalse()
    {
        var unit1 = Unit.Pixels(100);
        var unit2 = Unit.Percent(100);

        Assert.NotEqual(unit1, unit2);
    }
}
