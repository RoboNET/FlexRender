using System.IO;
using FlexRender.Parsing;
using YamlDotNet.RepresentationModel;
using Xunit;

namespace FlexRender.Tests.Parsing;

/// <summary>
/// Tests for converting the YAML gradient object form to a CSS gradient string.
/// </summary>
public sealed class GradientObjectParseTests
{
    private static YamlMappingNode ParseMapping(string yaml)
    {
        var stream = new YamlStream();
        using var reader = new StringReader(yaml);
        stream.Load(reader);
        return (YamlMappingNode)stream.Documents[0].RootNode;
    }

    [Fact]
    public void LinearGradient_WithAngleAndColors_ProducesCssString()
    {
        var node = ParseMapping("""
            gradient: linear
            colors: ["#ff0000", "#0000ff"]
            angle: 45
            """);

        var css = ShapeParsers.ConvertGradientObjectToCss(node);

        Assert.Equal("linear-gradient(45deg, #ff0000, #0000ff)", css);
    }

    [Fact]
    public void LinearGradient_WithoutAngle_DefaultsToZeroDeg()
    {
        var node = ParseMapping("""
            gradient: linear
            colors: ["#fff", "#000"]
            """);

        var css = ShapeParsers.ConvertGradientObjectToCss(node);

        Assert.Equal("linear-gradient(0deg, #fff, #000)", css);
    }

    [Fact]
    public void RadialGradient_IgnoresAngle()
    {
        var node = ParseMapping("""
            gradient: radial
            colors: ["#fff", "#000"]
            angle: 90
            """);

        var css = ShapeParsers.ConvertGradientObjectToCss(node);

        Assert.Equal("radial-gradient(#fff, #000)", css);
    }

    [Fact]
    public void Gradient_WithFewerThanTwoColors_Throws()
    {
        var node = ParseMapping("""
            gradient: linear
            colors: ["#fff"]
            """);

        Assert.Throws<TemplateParseException>(() => ShapeParsers.ConvertGradientObjectToCss(node));
    }

    [Fact]
    public void Gradient_WithUnknownType_Throws()
    {
        var node = ParseMapping("""
            gradient: conic
            colors: ["#fff", "#000"]
            """);

        Assert.Throws<TemplateParseException>(() => ShapeParsers.ConvertGradientObjectToCss(node));
    }
}
