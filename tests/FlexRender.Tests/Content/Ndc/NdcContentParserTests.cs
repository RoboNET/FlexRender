using FlexRender.Abstractions;
using FlexRender.Content.Ndc;
using FlexRender.Layout;
using FlexRender.Parsing.Ast;
using Xunit;

namespace FlexRender.Tests.Content.Ndc;

public sealed class NdcContentParserTests
{
    private readonly NdcContentParser _parser = new();
    private static readonly ContentParserContext EmptyContext = new();

    private static ContentParserContext ContextWithCanvas(int width) =>
        new() { Canvas = new CanvasSettings { Width = width } };

    [Fact]
    public void FormatName_IsNdc()
    {
        Assert.Equal("ndc", _parser.FormatName);
    }

    [Fact]
    public void Parse_NullText_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => _parser.Parse((string)null!, EmptyContext));
    }

    [Fact]
    public void Parse_EmptyText_ReturnsEmpty()
    {
        var result = _parser.Parse("", EmptyContext);
        Assert.Empty(result);
    }

    [Fact]
    public void Parse_PlainText_ReturnsSingleTextElement()
    {
        var result = _parser.Parse("Hello World", EmptyContext);

        // Should be wrapped: FlexElement(column) > FlexElement(row) > TextElement
        var root = Assert.IsType<FlexElement>(Assert.Single(result));
        Assert.Equal(FlexDirection.Column, root.Direction.Value);
        var row = Assert.IsType<FlexElement>(Assert.Single(root.Children));
        var text = Assert.IsType<TextElement>(Assert.Single(row.Children));
        Assert.Equal("Hello World", text.Content.Value);
    }

    [Fact]
    public void Parse_MultipleLines_ReturnsMultipleRows()
    {
        var result = _parser.Parse("Line1\nLine2\nLine3", EmptyContext);

        var root = Assert.IsType<FlexElement>(Assert.Single(result));
        Assert.Equal(3, root.Children.Count);
        Assert.All(root.Children, child => Assert.IsType<FlexElement>(child));
    }

    [Fact]
    public void Parse_CharsetSwitch_AppliesStyleFromOptions()
    {
        var options = new Dictionary<string, object>
        {
            ["charsets"] = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase)
            {
                ["I"] = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase)
                {
                    ["font"] = "bold",
                    ["font_style"] = "bold",
                    ["encoding"] = "qwerty-jcuken"
                }
            }
        };

        // "ESC(I lfnf ESC(1 03.04.25" -> decoded: "дата" (bold) + " 03.04.25" (normal)
        var input = "\x1B(Ilfnf\x1B(1 03.04.25";
        var result = _parser.Parse(input, EmptyContext, options);

        var root = Assert.IsType<FlexElement>(Assert.Single(result));
        var row = Assert.IsType<FlexElement>(Assert.Single(root.Children));
        Assert.Equal(2, row.Children.Count);

        var boldText = Assert.IsType<TextElement>(row.Children[0]);
        Assert.Equal("\u0434\u0430\u0442\u0430", boldText.Content.Value);
        Assert.Equal(FontWeight.Bold, boldText.FontWeight.Value);

        var normalText = Assert.IsType<TextElement>(row.Children[1]);
        Assert.Equal(" 03.04.25", normalText.Content.Value);
    }

    [Fact]
    public void Parse_FormFeed_InsertsSeparatorElement()
    {
        var result = _parser.Parse("Page1\x0CPage2", EmptyContext);

        var root = Assert.IsType<FlexElement>(Assert.Single(result));
        Assert.Equal(3, root.Children.Count);
        Assert.IsType<FlexElement>(root.Children[0]); // Page1 row
        Assert.IsType<SeparatorElement>(root.Children[1]); // FF separator
        Assert.IsType<FlexElement>(root.Children[2]); // Page2 row
    }

    [Fact]
    public void Parse_ShiftOutSpaces_InsertsSpacesInText()
    {
        var result = _parser.Parse("A\x0E" + "4B", EmptyContext); // SO 4 -> 4 spaces

        var root = Assert.IsType<FlexElement>(Assert.Single(result));
        var row = Assert.IsType<FlexElement>(Assert.Single(root.Children));
        // Could be single TextElement "A    B" or multiple segments
        var allText = string.Concat(row.Children.OfType<TextElement>().Select(t => t.Content.Value));
        Assert.Equal("A    B", allText);
    }

    [Fact]
    public void Parse_Barcode_ReturnsBarcodeElement()
    {
        // ESC k 4 TESTDATA ESC \  -> Code39 barcode
        var input = "\x1Bk4TESTDATA\x1B\\";
        var result = _parser.Parse(input, EmptyContext);

        var root = Assert.IsType<FlexElement>(Assert.Single(result));
        var row = Assert.IsType<FlexElement>(Assert.Single(root.Children));
        var barcode = Assert.IsType<BarcodeElement>(Assert.Single(row.Children));
        Assert.Equal("TESTDATA", barcode.Data.Value);
        Assert.Equal(BarcodeFormat.Code39, barcode.Format.Value);
    }

    [Fact]
    public void Parse_FontSizeFromOptions_AppliedToTextElement()
    {
        var options = new Dictionary<string, object>
        {
            ["charsets"] = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase)
            {
                ["1"] = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase)
                {
                    ["font_size"] = "14"
                }
            }
        };

        var result = _parser.Parse("text", EmptyContext, options);

        var root = Assert.IsType<FlexElement>(Assert.Single(result));
        var row = Assert.IsType<FlexElement>(Assert.Single(root.Children));
        var text = Assert.IsType<TextElement>(Assert.Single(row.Children));
        Assert.Equal("14", text.Size.Value);
    }

    [Fact]
    public void Parse_ColorFromOptions_AppliedToTextElement()
    {
        var options = new Dictionary<string, object>
        {
            ["charsets"] = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase)
            {
                ["1"] = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase)
                {
                    ["color"] = "#333"
                }
            }
        };

        var result = _parser.Parse("text", EmptyContext, options);

        var root = Assert.IsType<FlexElement>(Assert.Single(result));
        var row = Assert.IsType<FlexElement>(Assert.Single(root.Children));
        var text = Assert.IsType<TextElement>(Assert.Single(row.Children));
        Assert.Equal("#333", text.Color.Value);
    }

    [Fact]
    public void Parse_FieldSeparator_IsIgnored()
    {
        // GS 3 between text
        var result = _parser.Parse("A\x1D" + "3B", EmptyContext);

        var root = Assert.IsType<FlexElement>(Assert.Single(result));
        var row = Assert.IsType<FlexElement>(Assert.Single(root.Children));
        var allText = string.Concat(row.Children.OfType<TextElement>().Select(t => t.Content.Value));
        Assert.Equal("AB", allText);
    }

    [Fact]
    public void Parse_FitContent_SetOnRootWhenCanvasWidthPresent()
    {
        var options = new Dictionary<string, object>
        {
            ["columns"] = "40"
        };

        var result = _parser.Parse("Hello", ContextWithCanvas(576), options);

        var root = Assert.IsType<FlexElement>(Assert.Single(result));
        Assert.Equal("fit-content", root.FontSize.Value);
        // Text elements should NOT have auto-calculated size — they inherit from container
        var row = Assert.IsType<FlexElement>(Assert.Single(root.Children));
        var text = Assert.IsType<TextElement>(Assert.Single(row.Children));
        Assert.Equal("1em", text.Size.Value);
    }

    [Fact]
    public void Parse_FitContent_SetOnRootForMultiLineText()
    {
        var result = _parser.Parse("ABCDEFGHIJ\n12345", ContextWithCanvas(576));

        var root = Assert.IsType<FlexElement>(Assert.Single(result));
        Assert.Equal("fit-content", root.FontSize.Value);
    }

    [Fact]
    public void Parse_FitContent_SetOnRootWithSpacesInLine()
    {
        // "AB" + SO(5 spaces) + "CD" = 2+5+2 = 9 chars per line
        var result = _parser.Parse("AB\x0e" + "5CD", ContextWithCanvas(576));

        var root = Assert.IsType<FlexElement>(Assert.Single(result));
        Assert.Equal("fit-content", root.FontSize.Value);
        var row = Assert.IsType<FlexElement>(Assert.Single(root.Children));
        var allText = string.Concat(row.Children.OfType<TextElement>().Select(t => t.Content.Value));
        Assert.Equal("AB     CD", allText);
    }

    [Fact]
    public void Parse_ExplicitFontSize_OverridesAutoFontSize()
    {
        var options = new Dictionary<string, object>
        {
            ["columns"] = "40",
            ["charsets"] = new Dictionary<string, object>
            {
                ["1"] = new Dictionary<string, object>
                {
                    ["font_size"] = "14"
                }
            }
        };

        var result = _parser.Parse("Hello", ContextWithCanvas(576), options);

        var root = Assert.IsType<FlexElement>(Assert.Single(result));
        var row = Assert.IsType<FlexElement>(Assert.Single(root.Children));
        var text = Assert.IsType<TextElement>(Assert.Single(row.Children));
        Assert.Equal("14", text.Size.Value);
    }

    [Fact]
    public void Parse_EmptyLinesBetweenContent_ProduceRowsWithSpaceForHeight()
    {
        var result = _parser.Parse("A\n\nB", EmptyContext);

        var root = Assert.IsType<FlexElement>(Assert.Single(result));
        Assert.Equal(3, root.Children.Count); // row A, empty-line row, row B

        // The empty-line row should contain a space TextElement for non-zero height
        var emptyRow = Assert.IsType<FlexElement>(root.Children[1]);
        var spacer = Assert.IsType<TextElement>(Assert.Single(emptyRow.Children));
        Assert.Equal(" ", spacer.Content.Value);
    }
}
