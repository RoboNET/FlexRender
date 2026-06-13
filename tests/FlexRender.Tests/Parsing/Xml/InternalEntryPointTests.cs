using FlexRender.Parsing;
using FlexRender.Parsing.Ast;
using YamlDotNet.RepresentationModel;
using Xunit;

namespace FlexRender.Tests.Parsing.Xml;

/// <summary>
/// Tests for the internal <see cref="TemplateParser.ParseDocumentRoot"/> entry point
/// used by the XML parser to reuse YAML element parsing.
/// </summary>
public class InternalEntryPointTests
{
    /// <summary>
    /// Verifies that a programmatically built YamlMappingNode root produces the same AST
    /// as parsing the equivalent YAML string.
    /// </summary>
    [Fact]
    public void ParseDocumentRoot_BuiltNode_ProducesEquivalentAst()
    {
        // Build: { canvas: { width: 300 }, layout: [ { type: text, content: Hi } ] }
        var canvas = new YamlMappingNode();
        canvas.Add("width", "300");

        var textNode = new YamlMappingNode();
        textNode.Add("type", "text");
        textNode.Add("content", "Hi");

        var layout = new YamlSequenceNode();
        layout.Add(textNode);

        var root = new YamlMappingNode();
        root.Add("canvas", canvas);
        root.Add("layout", layout);

        var template = new TemplateParser().ParseDocumentRootForTests(root);

        var text = Assert.IsType<TextElement>(Assert.Single(template.Elements));
        Assert.Equal("Hi", text.Content);
        Assert.Equal(300, template.Canvas.Width);
    }
}
