using System.IO;
using FlexRender.Parsing;
using FlexRender.Parsing.Nodes;
using YamlDotNet.RepresentationModel;
using Xunit;

namespace FlexRender.Tests.Parsing;

/// <summary>Tests that the YamlDotNet DOM is faithfully converted to the neutral node model.</summary>
public sealed class YamlNodeConverterTests
{
    private static YamlMappingNode Load(string yaml)
    {
        var stream = new YamlStream();
        using var reader = new StringReader(yaml);
        stream.Load(reader);
        return (YamlMappingNode)stream.Documents[0].RootNode;
    }

    [Fact]
    public void Convert_MappingScalarSequence_ProducesNeutralTree()
    {
        var yaml = Load("""
            canvas:
              width: 300
            layout:
              - type: text
                content: Hi
            """);

        var root = YamlNodeConverter.Convert(yaml);

        Assert.True(root.TryGetMapping("canvas", out var canvas));
        Assert.Equal("300", canvas.GetScalar("width"));

        Assert.True(root.TryGetSequence("layout", out var layout));
        var item = Assert.IsType<TemplateMapping>(Assert.Single(layout.Items));
        Assert.Equal("text", item.GetScalar("type"));
        Assert.Equal("Hi", item.GetScalar("content"));
    }

    [Fact]
    public void Convert_NestedSequenceOfSequences_Preserved()
    {
        var yaml = Load("""
            series:
              - data:
                  - [1, 2]
                  - [3, 4]
            """);

        var root = YamlNodeConverter.Convert(yaml);
        Assert.True(root.TryGetSequence("series", out var series));
        var s0 = Assert.IsType<TemplateMapping>(series.Items[0]);
        Assert.True(s0.TryGetSequence("data", out var data));
        var tuple0 = Assert.IsType<TemplateSequence>(data.Items[0]);
        Assert.Equal("1", ((TemplateScalar)tuple0.Items[0]).Value);
        Assert.Equal("2", ((TemplateScalar)tuple0.Items[1]).Value);
    }
}
