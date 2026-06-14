using FlexRender.Parsing;
using FlexRender.Parsing.Nodes;
using Xunit;

namespace FlexRender.Tests.Parsing;

/// <summary>
/// Tests for converting the gradient object form to a CSS gradient string.
/// </summary>
public sealed class GradientObjectParseTests
{
    private static TemplateScalar S(string v) => new(v);

    private static TemplateMapping Linear(params string[] colors)
    {
        var m = new TemplateMapping();
        m.Add("gradient", S("linear"));
        var seq = new TemplateSequence();
        foreach (var c in colors) seq.Add(S(c));
        m.Add("colors", seq);
        return m;
    }

    [Fact]
    public void LinearGradient_WithAngleAndColors_ProducesCssString()
    {
        var node = Linear("#ff0000", "#0000ff");
        node.Add("angle", S("45"));

        var css = ShapeParsers.ConvertGradientObjectToCss(node);

        Assert.Equal("linear-gradient(45deg, #ff0000, #0000ff)", css);
    }

    [Fact]
    public void LinearGradient_WithoutAngle_DefaultsToZeroDeg()
    {
        var css = ShapeParsers.ConvertGradientObjectToCss(Linear("#fff", "#000"));
        Assert.Equal("linear-gradient(0deg, #fff, #000)", css);
    }

    [Fact]
    public void RadialGradient_IgnoresAngle()
    {
        var node = new TemplateMapping();
        node.Add("gradient", S("radial"));
        var seq = new TemplateSequence();
        seq.Add(S("#fff"));
        seq.Add(S("#000"));
        node.Add("colors", seq);
        node.Add("angle", S("90"));

        var css = ShapeParsers.ConvertGradientObjectToCss(node);
        Assert.Equal("radial-gradient(#fff, #000)", css);
    }

    [Fact]
    public void Gradient_WithFewerThanTwoColors_Throws()
    {
        Assert.Throws<TemplateParseException>(() => ShapeParsers.ConvertGradientObjectToCss(Linear("#fff")));
    }

    [Fact]
    public void Gradient_WithUnknownType_Throws()
    {
        var node = new TemplateMapping();
        node.Add("gradient", S("conic"));
        var seq = new TemplateSequence();
        seq.Add(S("#fff"));
        seq.Add(S("#000"));
        node.Add("colors", seq);

        Assert.Throws<TemplateParseException>(() => ShapeParsers.ConvertGradientObjectToCss(node));
    }
}
