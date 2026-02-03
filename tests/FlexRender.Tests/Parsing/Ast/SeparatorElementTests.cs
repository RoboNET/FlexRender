using FlexRender.Layout;
using FlexRender.Parsing.Ast;
using Xunit;

namespace FlexRender.Tests.Parsing.Ast;

/// <summary>
/// Tests for SeparatorElement AST model.
/// </summary>
public sealed class SeparatorElementTests
{
    /// <summary>
    /// Verifies default values are set correctly.
    /// </summary>
    [Fact]
    public void SeparatorElement_DefaultValues()
    {
        var separator = new SeparatorElement();

        Assert.Equal(SeparatorOrientation.Horizontal, separator.Orientation);
        Assert.Equal(SeparatorStyle.Dotted, separator.Style);
        Assert.Equal("#000000", separator.Color);
        Assert.Equal(1f, separator.Thickness);
        Assert.Null(separator.Background);
        Assert.Equal("none", separator.Rotate);
        Assert.Equal("0", separator.Padding);
        Assert.Equal("0", separator.Margin);
    }

    /// <summary>
    /// Verifies custom values can be set.
    /// </summary>
    [Fact]
    public void SeparatorElement_CustomValues()
    {
        var separator = new SeparatorElement
        {
            Orientation = SeparatorOrientation.Vertical,
            Style = SeparatorStyle.Solid,
            Color = "#cccccc",
            Thickness = 3f,
            Background = "#ffffff",
            Rotate = "right"
        };

        Assert.Equal(SeparatorOrientation.Vertical, separator.Orientation);
        Assert.Equal(SeparatorStyle.Solid, separator.Style);
        Assert.Equal("#cccccc", separator.Color);
        Assert.Equal(3f, separator.Thickness);
        Assert.Equal("#ffffff", separator.Background);
        Assert.Equal("right", separator.Rotate);
    }

    /// <summary>
    /// Verifies SeparatorElement has correct ElementType.
    /// </summary>
    [Fact]
    public void SeparatorElement_HasCorrectType()
    {
        var separator = new SeparatorElement();

        Assert.IsAssignableFrom<TemplateElement>(separator);
        Assert.Equal(ElementType.Separator, separator.Type);
    }

    /// <summary>
    /// Verifies all orientation values are available.
    /// </summary>
    [Theory]
    [InlineData(SeparatorOrientation.Horizontal)]
    [InlineData(SeparatorOrientation.Vertical)]
    public void SeparatorOrientation_AllValuesExist(SeparatorOrientation orientation)
    {
        var separator = new SeparatorElement { Orientation = orientation };
        Assert.Equal(orientation, separator.Orientation);
    }

    /// <summary>
    /// Verifies all style values are available.
    /// </summary>
    [Theory]
    [InlineData(SeparatorStyle.Dotted)]
    [InlineData(SeparatorStyle.Dashed)]
    [InlineData(SeparatorStyle.Solid)]
    public void SeparatorStyle_AllValuesExist(SeparatorStyle style)
    {
        var separator = new SeparatorElement { Style = style };
        Assert.Equal(style, separator.Style);
    }

    /// <summary>
    /// Verifies flex item properties have correct defaults.
    /// </summary>
    [Fact]
    public void SeparatorElement_FlexItemProperties_DefaultValues()
    {
        var separator = new SeparatorElement();

        Assert.Equal(0f, separator.Grow);
        Assert.Equal(1f, separator.Shrink);
        Assert.Equal("auto", separator.Basis);
        Assert.Equal(AlignSelf.Auto, separator.AlignSelf);
        Assert.Equal(0, separator.Order);
        Assert.Null(separator.Width);
        Assert.Null(separator.Height);
    }

    /// <summary>
    /// Verifies that negative thickness can be set on the model (validation happens at parse time).
    /// </summary>
    [Fact]
    public void SeparatorElement_NegativeThickness_CanBeSet()
    {
        var separator = new SeparatorElement { Thickness = -5f };

        Assert.Equal(-5f, separator.Thickness);
    }
}
