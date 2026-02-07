using FlexRender.Rendering;
using SkiaSharp;
using Xunit;

namespace FlexRender.Tests.VisualEffects;

/// <summary>
/// Tests for GradientParser (linear-gradient and radial-gradient).
/// </summary>
public sealed class GradientParserTests
{
    // ==========================================
    // Linear Gradient Parsing
    // ==========================================

    [Fact]
    public void TryParse_LinearGradient_TwoColors_ReturnsGradientDefinition()
    {
        var success = GradientParser.TryParse("linear-gradient(to right, #ff0000, #0000ff)", out var result);

        Assert.True(success);
        Assert.NotNull(result);
        Assert.Equal(GradientType.Linear, result.Type);
        Assert.Equal(2, result.Stops.Count);
    }

    [Fact]
    public void TryParse_LinearGradient_ThreeColors_ReturnsThreeStops()
    {
        var success = GradientParser.TryParse("linear-gradient(45deg, #ff0000, #00ff00, #0000ff)", out var result);

        Assert.True(success);
        Assert.NotNull(result);
        Assert.Equal(3, result.Stops.Count);
    }

    // === Direction keywords ===

    [Theory]
    [InlineData("linear-gradient(to right, #ff0000, #0000ff)", 90f)]
    [InlineData("linear-gradient(to left, #ff0000, #0000ff)", 270f)]
    [InlineData("linear-gradient(to bottom, #ff0000, #0000ff)", 180f)]
    [InlineData("linear-gradient(to top, #ff0000, #0000ff)", 0f)]
    public void TryParse_LinearGradient_DirectionKeyword_CorrectAngle(string input, float expectedDegrees)
    {
        var success = GradientParser.TryParse(input, out var result);

        Assert.True(success);
        Assert.NotNull(result);
        Assert.Equal(expectedDegrees, result.AngleDegrees, 0.1f);
    }

    [Fact]
    public void TryParse_LinearGradient_ToBottomRight_Parses()
    {
        var success = GradientParser.TryParse("linear-gradient(to bottom right, #ff0000, #0000ff)", out var result);

        Assert.True(success);
        Assert.NotNull(result);
        // "to bottom right" should be 135 degrees
        Assert.Equal(135f, result.AngleDegrees, 0.1f);
    }

    // === Degree values ===

    [Theory]
    [InlineData("linear-gradient(0deg, #ff0000, #0000ff)", 0f)]
    [InlineData("linear-gradient(45deg, #ff0000, #0000ff)", 45f)]
    [InlineData("linear-gradient(90deg, #ff0000, #0000ff)", 90f)]
    [InlineData("linear-gradient(180deg, #ff0000, #0000ff)", 180f)]
    [InlineData("linear-gradient(270deg, #ff0000, #0000ff)", 270f)]
    [InlineData("linear-gradient(360deg, #ff0000, #0000ff)", 360f)]
    public void TryParse_LinearGradient_DegreeValues_CorrectAngle(string input, float expectedDegrees)
    {
        var success = GradientParser.TryParse(input, out var result);

        Assert.True(success);
        Assert.NotNull(result);
        Assert.Equal(expectedDegrees, result.AngleDegrees, 0.1f);
    }

    // === Color stops with positions ===

    [Fact]
    public void TryParse_LinearGradient_ColorStopsWithPositions_ParsesPositions()
    {
        var success = GradientParser.TryParse("linear-gradient(to right, #ff0000 30%, #0000ff 70%)", out var result);

        Assert.True(success);
        Assert.NotNull(result);
        Assert.Equal(2, result.Stops.Count);
        Assert.Equal(0.3f, result.Stops[0].Position!.Value, 0.01f);
        Assert.Equal(0.7f, result.Stops[1].Position!.Value, 0.01f);
    }

    [Fact]
    public void TryParse_LinearGradient_WithoutPositions_EvenlyDistributed()
    {
        var success = GradientParser.TryParse("linear-gradient(to right, #ff0000, #00ff00, #0000ff)", out var result);

        Assert.True(success);
        Assert.NotNull(result);
        Assert.Equal(3, result.Stops.Count);
        // Without explicit positions, stops should not have positions set
        // (positions are resolved at shader creation time)
        Assert.Null(result.Stops[0].Position);
        Assert.Null(result.Stops[1].Position);
        Assert.Null(result.Stops[2].Position);
    }

    // === Default direction ===

    [Fact]
    public void TryParse_LinearGradient_NoDirection_DefaultsToBottom()
    {
        // "linear-gradient(#ff0000, #0000ff)" -- no direction specified
        var success = GradientParser.TryParse("linear-gradient(#ff0000, #0000ff)", out var result);

        Assert.True(success);
        Assert.NotNull(result);
        // Default direction: "to bottom" = 180 degrees
        Assert.Equal(180f, result.AngleDegrees, 0.1f);
    }

    // ==========================================
    // Radial Gradient Parsing
    // ==========================================

    [Fact]
    public void TryParse_RadialGradient_TwoColors_ReturnsGradientDefinition()
    {
        var success = GradientParser.TryParse("radial-gradient(#ffffff, #000000)", out var result);

        Assert.True(success);
        Assert.NotNull(result);
        Assert.Equal(GradientType.Radial, result.Type);
        Assert.Equal(2, result.Stops.Count);
    }

    [Fact]
    public void TryParse_RadialGradient_ThreeColors_ReturnsThreeStops()
    {
        var success = GradientParser.TryParse("radial-gradient(#ff0000, #00ff00, #0000ff)", out var result);

        Assert.True(success);
        Assert.NotNull(result);
        Assert.Equal(3, result.Stops.Count);
    }

    [Fact]
    public void TryParse_RadialGradient_WithPositions_ParsesPositions()
    {
        var success = GradientParser.TryParse("radial-gradient(#ff0000 20%, #0000ff 80%)", out var result);

        Assert.True(success);
        Assert.NotNull(result);
        Assert.Equal(0.2f, result.Stops[0].Position!.Value, 0.01f);
        Assert.Equal(0.8f, result.Stops[1].Position!.Value, 0.01f);
    }

    // ==========================================
    // Color parsing
    // ==========================================

    [Fact]
    public void TryParse_HexColors_ParsesCorrectly()
    {
        var success = GradientParser.TryParse("linear-gradient(to right, #ff0000, #0000ff)", out var result);

        Assert.True(success);
        Assert.NotNull(result);
        Assert.Equal(255, result.Stops[0].Color.Red);
        Assert.Equal(0, result.Stops[0].Color.Blue);
        Assert.Equal(0, result.Stops[1].Color.Red);
        Assert.Equal(255, result.Stops[1].Color.Blue);
    }

    [Fact]
    public void TryParse_RgbaColors_ParsesCorrectly()
    {
        var success = GradientParser.TryParse("linear-gradient(to right, rgba(255,0,0,1.0), rgba(0,0,255,0.5))", out var result);

        Assert.True(success);
        Assert.NotNull(result);
        Assert.Equal(255, result.Stops[0].Color.Red);
        Assert.True(result.Stops[1].Color.Alpha < 200); // 0.5 alpha
    }

    // ==========================================
    // Non-gradient strings
    // ==========================================

    [Fact]
    public void TryParse_SolidColor_ReturnsFalse()
    {
        var success = GradientParser.TryParse("#ff0000", out _);

        Assert.False(success);
    }

    [Fact]
    public void TryParse_Null_ReturnsFalse()
    {
        var success = GradientParser.TryParse(null, out _);

        Assert.False(success);
    }

    [Fact]
    public void TryParse_EmptyString_ReturnsFalse()
    {
        var success = GradientParser.TryParse("", out _);

        Assert.False(success);
    }

    [Fact]
    public void TryParse_RandomText_ReturnsFalse()
    {
        var success = GradientParser.TryParse("not a gradient", out _);

        Assert.False(success);
    }

    // ==========================================
    // IsGradient helper
    // ==========================================

    [Theory]
    [InlineData("linear-gradient(to right, #ff0000, #0000ff)", true)]
    [InlineData("radial-gradient(#ffffff, #000000)", true)]
    [InlineData("#ff0000", false)]
    [InlineData("", false)]
    [InlineData(null, false)]
    public void IsGradient_VariousInputs_ReturnsExpected(string? input, bool expected)
    {
        Assert.Equal(expected, GradientParser.IsGradient(input));
    }

    // ==========================================
    // YAML parsing (gradient stored in background)
    // ==========================================

    [Fact]
    public void Parse_BackgroundWithLinearGradient_ParsedAsString()
    {
        var parser = new FlexRender.Parsing.TemplateParser();
        var yaml = """
            canvas:
              width: 300
            layout:
              - type: flex
                background: "linear-gradient(to right, #ff0000, #0000ff)"
                children:
                  - type: text
                    content: "Gradient"
            """;

        using var stream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(yaml));
        var template = parser.Parse(stream);

        var flex = Assert.IsType<FlexRender.Parsing.Ast.FlexElement>(template.Elements[0]);
        Assert.Equal("linear-gradient(to right, #ff0000, #0000ff)", flex.Background);
    }

    [Fact]
    public void Parse_BackgroundWithRadialGradient_ParsedAsString()
    {
        var parser = new FlexRender.Parsing.TemplateParser();
        var yaml = """
            canvas:
              width: 300
            layout:
              - type: flex
                background: "radial-gradient(#ffffff, #000000)"
                children:
                  - type: text
                    content: "Gradient"
            """;

        using var stream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(yaml));
        var template = parser.Parse(stream);

        var flex = Assert.IsType<FlexRender.Parsing.Ast.FlexElement>(template.Elements[0]);
        Assert.Equal("radial-gradient(#ffffff, #000000)", flex.Background);
    }
}
