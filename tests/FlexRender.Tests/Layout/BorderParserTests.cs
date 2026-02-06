using FlexRender.Layout.Units;
using Xunit;

namespace FlexRender.Tests.Layout;

/// <summary>
/// Tests for <see cref="BorderParser"/> which parses CSS-like border shorthand
/// and per-side properties into resolved <see cref="BorderValues"/>.
/// </summary>
public sealed class BorderParserTests
{
    private const float ParentSize = 300f;
    private const float FontSize = 16f;

    // ============================================
    // Shorthand Parsing: "width style color"
    // ============================================

    [Fact]
    public void ParseShorthand_FullShorthand_ParsesAllParts()
    {
        // "2 solid #333" -> width=2, style=Solid, color=#333
        var side = BorderParser.ParseShorthand("2 solid #333", ParentSize, FontSize);

        Assert.Equal(2f, side.Width, 0.1f);
        Assert.Equal(BorderLineStyle.Solid, side.Style);
        Assert.Equal("#333", side.Color);
    }

    [Fact]
    public void ParseShorthand_WidthOnly_DefaultsStyleAndColor()
    {
        // "3" -> width=3, style=Solid (default), color=#000000 (default)
        var side = BorderParser.ParseShorthand("3", ParentSize, FontSize);

        Assert.Equal(3f, side.Width, 0.1f);
        Assert.Equal(BorderLineStyle.Solid, side.Style);
        Assert.Equal("#000000", side.Color);
    }

    [Fact]
    public void ParseShorthand_WidthAndStyle_DefaultsColor()
    {
        // "1 dashed" -> width=1, style=Dashed, color=#000000
        var side = BorderParser.ParseShorthand("1 dashed", ParentSize, FontSize);

        Assert.Equal(1f, side.Width, 0.1f);
        Assert.Equal(BorderLineStyle.Dashed, side.Style);
        Assert.Equal("#000000", side.Color);
    }

    [Fact]
    public void ParseShorthand_DottedStyle_ParsesCorrectly()
    {
        var side = BorderParser.ParseShorthand("1 dotted #ccc", ParentSize, FontSize);

        Assert.Equal(1f, side.Width, 0.1f);
        Assert.Equal(BorderLineStyle.Dotted, side.Style);
        Assert.Equal("#ccc", side.Color);
    }

    [Fact]
    public void ParseShorthand_NoneStyle_ParsesCorrectly()
    {
        var side = BorderParser.ParseShorthand("0 none #000", ParentSize, FontSize);

        Assert.Equal(0f, side.Width, 0.1f);
        Assert.Equal(BorderLineStyle.None, side.Style);
    }

    [Fact]
    public void ParseShorthand_ZeroWidth_NotVisible()
    {
        var side = BorderParser.ParseShorthand("0", ParentSize, FontSize);

        Assert.Equal(0f, side.Width, 0.1f);
        Assert.False(side.IsVisible);
    }

    [Fact]
    public void ParseShorthand_EmWidth_ResolvesRelativeToFontSize()
    {
        // "0.5em solid #000" with fontSize=16 -> width = 8px
        var side = BorderParser.ParseShorthand("0.5em solid #000", ParentSize, FontSize);

        Assert.Equal(8f, side.Width, 0.1f);
    }

    [Fact]
    public void ParseShorthand_NullOrEmpty_ReturnsNone()
    {
        Assert.Equal(BorderSide.None, BorderParser.ParseShorthand(null!, ParentSize, FontSize));
        Assert.Equal(BorderSide.None, BorderParser.ParseShorthand("", ParentSize, FontSize));
    }

    // ============================================
    // BorderSide Value Tests
    // ============================================

    [Fact]
    public void BorderSide_None_IsNotVisible()
    {
        Assert.False(BorderSide.None.IsVisible);
        Assert.Equal(0f, BorderSide.None.Width);
    }

    [Fact]
    public void BorderSide_SolidWithWidth_IsVisible()
    {
        var side = new BorderSide(2f, BorderLineStyle.Solid, "#000");
        Assert.True(side.IsVisible);
    }

    [Fact]
    public void BorderSide_NoneStyleWithWidth_IsNotVisible()
    {
        var side = new BorderSide(2f, BorderLineStyle.None, "#000");
        Assert.False(side.IsVisible);
    }

    // ============================================
    // BorderValues Calculations
    // ============================================

    [Fact]
    public void BorderValues_Zero_HasNoVisibleBorder()
    {
        Assert.False(BorderValues.Zero.HasVisibleBorder);
        Assert.Equal(0f, BorderValues.Zero.Horizontal);
        Assert.Equal(0f, BorderValues.Zero.Vertical);
    }

    [Fact]
    public void BorderValues_Horizontal_SumsLeftAndRight()
    {
        var left = new BorderSide(3f, BorderLineStyle.Solid, "#000");
        var right = new BorderSide(5f, BorderLineStyle.Solid, "#000");
        var values = new BorderValues(BorderSide.None, right, BorderSide.None, left);

        Assert.Equal(8f, values.Horizontal, 0.1f);
    }

    [Fact]
    public void BorderValues_Vertical_SumsTopAndBottom()
    {
        var top = new BorderSide(2f, BorderLineStyle.Solid, "#000");
        var bottom = new BorderSide(4f, BorderLineStyle.Solid, "#000");
        var values = new BorderValues(top, BorderSide.None, bottom, BorderSide.None);

        Assert.Equal(6f, values.Vertical, 0.1f);
    }

    [Fact]
    public void BorderValues_HasVisibleBorder_TrueWhenAnySideVisible()
    {
        var solid = new BorderSide(1f, BorderLineStyle.Solid, "#000");
        var values = new BorderValues(solid, BorderSide.None, BorderSide.None, BorderSide.None);

        Assert.True(values.HasVisibleBorder);
    }

    // ============================================
    // Resolve from TemplateElement
    // ============================================

    [Fact]
    public void Resolve_NoBorderProperties_ReturnsZero()
    {
        var element = new FlexRender.Parsing.Ast.TextElement { Content = "Test" };

        var border = BorderParser.Resolve(element, ParentSize, FontSize);

        Assert.Equal(BorderValues.Zero, border);
    }

    [Fact]
    public void Resolve_BorderShorthand_AppliesAllSides()
    {
        var element = new FlexRender.Parsing.Ast.TextElement
        {
            Content = "Test",
            Border = "2 solid #333"
        };

        var border = BorderParser.Resolve(element, ParentSize, FontSize);

        Assert.Equal(2f, border.Top.Width, 0.1f);
        Assert.Equal(2f, border.Right.Width, 0.1f);
        Assert.Equal(2f, border.Bottom.Width, 0.1f);
        Assert.Equal(2f, border.Left.Width, 0.1f);
        Assert.True(border.HasVisibleBorder);
    }

    [Fact]
    public void Resolve_PerSideBorder_OverridesShorthand()
    {
        var element = new FlexRender.Parsing.Ast.TextElement
        {
            Content = "Test",
            Border = "1 solid #000",
            BorderTop = "3 dashed #f00"
        };

        var border = BorderParser.Resolve(element, ParentSize, FontSize);

        // Top should be overridden
        Assert.Equal(3f, border.Top.Width, 0.1f);
        Assert.Equal(BorderLineStyle.Dashed, border.Top.Style);

        // Other sides from shorthand
        Assert.Equal(1f, border.Right.Width, 0.1f);
        Assert.Equal(1f, border.Bottom.Width, 0.1f);
        Assert.Equal(1f, border.Left.Width, 0.1f);
    }

    [Fact]
    public void Resolve_IndividualProperties_OverrideShorthand()
    {
        var element = new FlexRender.Parsing.Ast.TextElement
        {
            Content = "Test",
            Border = "1 solid #000",
            BorderWidth = "3",
            BorderColor = "#f00"
        };

        var border = BorderParser.Resolve(element, ParentSize, FontSize);

        // All sides should have width=3, color=#f00
        Assert.Equal(3f, border.Top.Width, 0.1f);
        Assert.Equal("#f00", border.Top.Color);
    }
}
