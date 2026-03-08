using FlexRender.Parsing;
using Xunit;

namespace FlexRender.Tests.Parsing;

/// <summary>
/// Tests for unknown property validation during template parsing.
/// Verifies that misspelled or invalid properties on each element type
/// are detected and reported with helpful error messages.
/// </summary>
public sealed class TemplateParserUnknownPropertyTests
{
    private readonly TemplateParser _parser = new();

    /// <summary>
    /// Verifies that a misspelled property on a text element throws with the unknown property name.
    /// </summary>
    [Fact]
    public void Parse_TextWithUnknownProperty_ThrowsWithPropertyName()
    {
        const string yaml = """
            canvas:
              width: 300
            layout:
              - type: text
                content: "Hello"
                colour: "#ff0000"
            """;

        var ex = Assert.Throws<TemplateParseException>(() => _parser.Parse(yaml));
        Assert.Contains("colour", ex.Message);
        Assert.Contains("text", ex.Message);
    }

    /// <summary>
    /// Verifies that the error message suggests a close match for a misspelled property.
    /// </summary>
    [Fact]
    public void Parse_TextWithMisspelledColor_SuggestsCorrectProperty()
    {
        const string yaml = """
            canvas:
              width: 300
            layout:
              - type: text
                content: "Hello"
                colour: "#ff0000"
            """;

        var ex = Assert.Throws<TemplateParseException>(() => _parser.Parse(yaml));
        Assert.Contains("color", ex.Message);
        Assert.Contains("Did you mean", ex.Message);
    }

    /// <summary>
    /// Verifies that multiple unknown properties are all reported in the error message.
    /// </summary>
    [Fact]
    public void Parse_TextWithMultipleUnknownProperties_ReportsAll()
    {
        const string yaml = """
            canvas:
              width: 300
            layout:
              - type: text
                content: "Hello"
                colour: "#ff0000"
                fontSize: "12"
            """;

        var ex = Assert.Throws<TemplateParseException>(() => _parser.Parse(yaml));
        Assert.Contains("colour", ex.Message);
        Assert.Contains("fontSize", ex.Message);
    }

    /// <summary>
    /// Verifies that valid text properties do not trigger a validation error.
    /// </summary>
    [Fact]
    public void Parse_TextWithAllValidProperties_DoesNotThrow()
    {
        const string yaml = """
            canvas:
              width: 300
            layout:
              - type: text
                content: "Hello"
                font: main
                size: "1em"
                color: "#000000"
                align: left
                wrap: true
                overflow: ellipsis
                maxLines: 3
                rotate: none
                background: "#ffffff"
                padding: "4"
                margin: "2"
                lineHeight: "1.5"
                fontWeight: bold
                fontStyle: italic
                fontFamily: "Arial"
            """;

        var template = _parser.Parse(yaml);
        Assert.Single(template.Elements);
    }

    /// <summary>
    /// Verifies that an unknown property on a flex element is detected.
    /// </summary>
    [Fact]
    public void Parse_FlexWithUnknownProperty_Throws()
    {
        const string yaml = """
            canvas:
              width: 300
            layout:
              - type: flex
                flexDirection: row
                children: []
            """;

        var ex = Assert.Throws<TemplateParseException>(() => _parser.Parse(yaml));
        Assert.Contains("flexDirection", ex.Message);
        Assert.Contains("flex", ex.Message);
    }

    /// <summary>
    /// Verifies that the 'direction' property on flex does not throw (it is valid).
    /// </summary>
    [Fact]
    public void Parse_FlexWithDirection_DoesNotThrow()
    {
        const string yaml = """
            canvas:
              width: 300
            layout:
              - type: flex
                direction: row
                children: []
            """;

        var template = _parser.Parse(yaml);
        Assert.Single(template.Elements);
    }

    /// <summary>
    /// Verifies that an unknown property on an image element is detected.
    /// </summary>
    [Fact]
    public void Parse_ImageWithUnknownProperty_Throws()
    {
        const string yaml = """
            canvas:
              width: 300
            layout:
              - type: image
                src: "test.png"
                scale: "2x"
            """;

        var ex = Assert.Throws<TemplateParseException>(() => _parser.Parse(yaml));
        Assert.Contains("scale", ex.Message);
        Assert.Contains("image", ex.Message);
    }

    /// <summary>
    /// Verifies that an unknown property on a QR element is detected.
    /// </summary>
    [Fact]
    public void Parse_QrWithUnknownProperty_Throws()
    {
        const string yaml = """
            canvas:
              width: 300
            layout:
              - type: qr
                data: "https://example.com"
                colour: "#000000"
            """;

        var ex = Assert.Throws<TemplateParseException>(() => _parser.Parse(yaml));
        Assert.Contains("colour", ex.Message);
    }

    /// <summary>
    /// Verifies that an unknown property on a barcode element is detected.
    /// </summary>
    [Fact]
    public void Parse_BarcodeWithUnknownProperty_Throws()
    {
        const string yaml = """
            canvas:
              width: 300
            layout:
              - type: barcode
                data: "12345"
                encoding: "code128"
            """;

        var ex = Assert.Throws<TemplateParseException>(() => _parser.Parse(yaml));
        Assert.Contains("encoding", ex.Message);
    }

    /// <summary>
    /// Verifies that an unknown property on a separator element is detected.
    /// </summary>
    [Fact]
    public void Parse_SeparatorWithUnknownProperty_Throws()
    {
        const string yaml = """
            canvas:
              width: 300
            layout:
              - type: separator
                direction: horizontal
            """;

        var ex = Assert.Throws<TemplateParseException>(() => _parser.Parse(yaml));
        Assert.Contains("direction", ex.Message);
        Assert.Contains("separator", ex.Message);
    }

    /// <summary>
    /// Verifies that 'orientation' on separator is accepted (not 'direction').
    /// </summary>
    [Fact]
    public void Parse_SeparatorWithOrientation_DoesNotThrow()
    {
        const string yaml = """
            canvas:
              width: 300
            layout:
              - type: separator
                orientation: horizontal
                style: solid
                thickness: 2
                color: "#000000"
            """;

        var template = _parser.Parse(yaml);
        Assert.Single(template.Elements);
    }

    /// <summary>
    /// Verifies that an unknown property on an SVG element is detected.
    /// </summary>
    [Fact]
    public void Parse_SvgWithUnknownProperty_Throws()
    {
        const string yaml = """
            canvas:
              width: 300
            layout:
              - type: svg
                src: "icon.svg"
                viewBox: "0 0 100 100"
            """;

        var ex = Assert.Throws<TemplateParseException>(() => _parser.Parse(yaml));
        Assert.Contains("viewBox", ex.Message);
    }

    /// <summary>
    /// Verifies that an unknown property on a table element is detected.
    /// </summary>
    [Fact]
    public void Parse_TableWithUnknownProperty_Throws()
    {
        const string yaml = """
            canvas:
              width: 300
            layout:
              - type: table
                dataSource: "items"
                columns:
                  - key: name
                    label: Name
            """;

        var ex = Assert.Throws<TemplateParseException>(() => _parser.Parse(yaml));
        Assert.Contains("dataSource", ex.Message);
    }

    /// <summary>
    /// Verifies that an unknown property on an each element is detected.
    /// </summary>
    [Fact]
    public void Parse_EachWithUnknownProperty_Throws()
    {
        const string yaml = """
            canvas:
              width: 300
            layout:
              - type: each
                array: "items"
                itemVar: "item"
                children: []
            """;

        var ex = Assert.Throws<TemplateParseException>(() => _parser.Parse(yaml));
        Assert.Contains("itemVar", ex.Message);
    }

    /// <summary>
    /// Verifies that an unknown property on an if element is detected.
    /// </summary>
    [Fact]
    public void Parse_IfWithUnknownProperty_Throws()
    {
        const string yaml = """
            canvas:
              width: 300
            layout:
              - type: if
                condition: "show"
                visible: true
                then: []
            """;

        var ex = Assert.Throws<TemplateParseException>(() => _parser.Parse(yaml));
        Assert.Contains("visible", ex.Message);
    }

    /// <summary>
    /// Verifies that an unknown property on a content element is detected.
    /// </summary>
    [Fact]
    public void Parse_ContentWithUnknownProperty_Throws()
    {
        const string yaml = """
            canvas:
              width: 300
            layout:
              - type: content
                source: "data.json"
                template: "default"
            """;

        var ex = Assert.Throws<TemplateParseException>(() => _parser.Parse(yaml));
        Assert.Contains("template", ex.Message);
    }

    /// <summary>
    /// Verifies that common flex-item properties (grow, shrink, etc.) are accepted on any element.
    /// </summary>
    [Fact]
    public void Parse_TextWithFlexItemProperties_DoesNotThrow()
    {
        const string yaml = """
            canvas:
              width: 300
            layout:
              - type: text
                content: "Hello"
                grow: 1
                shrink: 0
                basis: "auto"
                order: 2
                display: flex
                alignSelf: center
                opacity: 0.5
                position: relative
                top: "10"
            """;

        var template = _parser.Parse(yaml);
        Assert.Single(template.Elements);
    }

    /// <summary>
    /// Verifies that border-related properties (kebab-case and camelCase) are accepted.
    /// </summary>
    [Fact]
    public void Parse_TextWithBorderProperties_DoesNotThrow()
    {
        const string yaml = """
            canvas:
              width: 300
            layout:
              - type: text
                content: "Hello"
                border: "1px solid #000"
                border-radius: "4"
                borderColor: "#ff0000"
                box-shadow: "2 2 4 #000"
            """;

        var template = _parser.Parse(yaml);
        Assert.Single(template.Elements);
    }

    /// <summary>
    /// Verifies that min/max constraint properties in both naming conventions are accepted.
    /// </summary>
    [Fact]
    public void Parse_TextWithMinMaxConstraints_DoesNotThrow()
    {
        const string yaml = """
            canvas:
              width: 300
            layout:
              - type: text
                content: "Hello"
                min-width: "100"
                maxWidth: "200"
                minHeight: "50"
                max-height: "100"
            """;

        var template = _parser.Parse(yaml);
        Assert.Single(template.Elements);
    }

    /// <summary>
    /// Verifies that a completely unrelated property name with no close match
    /// does not include a "Did you mean" suggestion.
    /// </summary>
    [Fact]
    public void Parse_TextWithCompletelyUnrelatedProperty_NoSuggestion()
    {
        const string yaml = """
            canvas:
              width: 300
            layout:
              - type: text
                content: "Hello"
                xyzzy: "value"
            """;

        var ex = Assert.Throws<TemplateParseException>(() => _parser.Parse(yaml));
        Assert.Contains("xyzzy", ex.Message);
        Assert.DoesNotContain("Did you mean", ex.Message);
    }

    /// <summary>
    /// Verifies that valid if element properties with operators do not throw.
    /// </summary>
    [Fact]
    public void Parse_IfWithValidOperators_DoesNotThrow()
    {
        const string yaml = """
            canvas:
              width: 300
            layout:
              - type: if
                condition: "status"
                equals: "active"
                then:
                  - type: text
                    content: "Active"
                else:
                  - type: text
                    content: "Inactive"
            """;

        var template = _parser.Parse(yaml);
        Assert.Single(template.Elements);
    }

    /// <summary>
    /// Verifies that valid each element with 'as' property does not throw.
    /// </summary>
    [Fact]
    public void Parse_EachWithValidProperties_DoesNotThrow()
    {
        const string yaml = """
            canvas:
              width: 300
            layout:
              - type: each
                array: "items"
                as: "item"
                children:
                  - type: text
                    content: "{{item.name}}"
            """;

        var template = _parser.Parse(yaml);
        Assert.Single(template.Elements);
    }

    /// <summary>
    /// Verifies that font-size in flex elements (both variants) is accepted.
    /// </summary>
    [Fact]
    public void Parse_FlexWithFontSize_DoesNotThrow()
    {
        const string yaml = """
            canvas:
              width: 300
            layout:
              - type: flex
                direction: column
                font-size: "14"
                children: []
            """;

        var template = _parser.Parse(yaml);
        Assert.Single(template.Elements);
    }

    /// <summary>
    /// Verifies that nested flex children also get validated for unknown properties.
    /// </summary>
    [Fact]
    public void Parse_NestedFlexChildWithUnknownProperty_Throws()
    {
        const string yaml = """
            canvas:
              width: 300
            layout:
              - type: flex
                direction: column
                children:
                  - type: text
                    content: "Hello"
                    colour: "#000"
            """;

        var ex = Assert.Throws<TemplateParseException>(() => _parser.Parse(yaml));
        Assert.Contains("colour", ex.Message);
        Assert.Contains("text", ex.Message);
    }

    /// <summary>
    /// Verifies that 'fontSize' (camelCase, not a valid text property -- should be 'size') is caught on text.
    /// </summary>
    [Fact]
    public void Parse_TextWithFontSize_ThrowsSuggestsSize()
    {
        const string yaml = """
            canvas:
              width: 300
            layout:
              - type: text
                content: "Hello"
                fontSize: "14"
            """;

        var ex = Assert.Throws<TemplateParseException>(() => _parser.Parse(yaml));
        Assert.Contains("fontSize", ex.Message);
    }
}
