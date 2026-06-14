using FlexRender.Parsing.Nodes;
using Xunit;

namespace FlexRender.Tests.Parsing.Nodes;

/// <summary>Tests for the format-neutral node model.</summary>
public sealed class TemplateMappingTests
{
    [Fact]
    public void GetScalar_ReturnsValue_WhenKeyIsScalar()
    {
        var m = new TemplateMapping();
        m.Add("color", new TemplateScalar("#fff"));

        Assert.Equal("#fff", m.GetScalar("color"));
    }

    [Fact]
    public void GetScalar_ReturnsNull_WhenKeyMissingOrNotScalar()
    {
        var m = new TemplateMapping();
        m.Add("child", new TemplateSequence());

        Assert.Null(m.GetScalar("missing"));
        Assert.Null(m.GetScalar("child"));
    }

    [Fact]
    public void TryGetMapping_And_TryGetSequence_DiscriminateByType()
    {
        var m = new TemplateMapping();
        m.Add("map", new TemplateMapping());
        m.Add("seq", new TemplateSequence());
        m.Add("scalar", new TemplateScalar("x"));

        Assert.True(m.TryGetMapping("map", out _));
        Assert.False(m.TryGetMapping("seq", out _));
        Assert.True(m.TryGetSequence("seq", out _));
        Assert.False(m.TryGetSequence("scalar", out _));
    }

    [Fact]
    public void Keys_PreserveInsertionOrder_AndAddOverwritesValueKeepingPosition()
    {
        var m = new TemplateMapping();
        m.Add("a", new TemplateScalar("1"));
        m.Add("b", new TemplateScalar("2"));
        m.Add("a", new TemplateScalar("3"));

        Assert.Equal(new[] { "a", "b" }, m.Keys);
        Assert.Equal("3", m.GetScalar("a"));
    }

    [Fact]
    public void Sequence_PreservesOrder()
    {
        var s = new TemplateSequence();
        s.Add(new TemplateScalar("1"));
        s.Add(new TemplateScalar("2"));

        Assert.Equal(2, s.Items.Count);
        Assert.Equal("1", ((TemplateScalar)s.Items[0]).Value);
        Assert.Equal("2", ((TemplateScalar)s.Items[1]).Value);
    }
}
