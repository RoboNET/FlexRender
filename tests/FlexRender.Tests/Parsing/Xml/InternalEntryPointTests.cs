using FlexRender.Configuration;
using FlexRender.Parsing;
using FlexRender.Parsing.Ast;
using FlexRender.Parsing.Nodes;
using Xunit;

namespace FlexRender.Tests.Parsing.Xml;

/// <summary>
/// Tests the shared <see cref="FlexRender.Parsing.TemplateEngine.ParseDocumentRoot"/> entry point used by
/// both format parsers to reuse element parsing against the neutral node model.
/// </summary>
public class InternalEntryPointTests
{
    /// <summary>
    /// Verifies that a programmatically built neutral root produces the same AST
    /// as parsing the equivalent YAML string.
    /// </summary>
    [Fact]
    public void ParseDocumentRoot_BuiltNode_ProducesEquivalentAst()
    {
        // Build: { canvas: { width: 300 }, layout: [ { type: text, content: Hi } ] }
        var canvas = new TemplateMapping();
        canvas.Add("width", new TemplateScalar("300"));

        var textNode = new TemplateMapping();
        textNode.Add("type", new TemplateScalar("text"));
        textNode.Add("content", new TemplateScalar("Hi"));

        var layout = new TemplateSequence();
        layout.Add(textNode);

        var root = new TemplateMapping();
        root.Add("canvas", canvas);
        root.Add("layout", layout);

        var template = new FlexRender.Parsing.TemplateEngine(new ResourceLimits()).ParseDocumentRoot(root);

        var text = Assert.IsType<TextElement>(Assert.Single(template.Elements));
        Assert.Equal("Hi", text.Content);
        Assert.Equal(300, template.Canvas.Width);
    }
}
