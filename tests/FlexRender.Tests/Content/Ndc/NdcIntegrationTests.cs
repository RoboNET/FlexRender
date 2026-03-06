using System.Text;
using FlexRender.Abstractions;
using FlexRender.Content.Ndc;
using FlexRender.Parsing.Ast;
using Xunit;

namespace FlexRender.Tests.Content.Ndc;

public sealed class NdcIntegrationTests
{
    private static readonly ContentParserContext EmptyContext = new();
    [Fact]
    public void Parse_BankAMiniStatement_Pattern()
    {
        // Simplified pattern from Bank A mini statement receipt
        var options = new Dictionary<string, object>
        {
            ["charsets"] = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase)
            {
                ["I"] = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase)
                {
                    ["font"] = "bold",
                    ["font_style"] = "bold",
                    ["encoding"] = "qwerty-jcuken"
                },
                ["1"] = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase)
                {
                    ["font"] = "default"
                }
            }
        };

        // Simulated: ESC(I bank ESC(1 \r\n ESC(I date ESC(1 01.01.25
        var input = "\x1B(Intcnjdsq ~fyr f\x1B(1\r\n\x1B(Ilfnf\x1B(1 01.01.25";

        var parser = new NdcContentParser();
        var result = parser.Parse(input, EmptyContext, options);

        var root = Assert.IsType<FlexElement>(Assert.Single(result));
        Assert.Equal(2, root.Children.Count); // 2 lines

        // Line 1: decoded bank name
        var line1 = Assert.IsType<FlexElement>(root.Children[0]);
        var bankName = Assert.IsType<TextElement>(line1.Children[0]);
        Assert.Equal("\u0442\u0435\u0441\u0442\u043e\u0432\u044b\u0439 \u0431\u0430\u043d\u043a \u0430", bankName.Content.Value);
        Assert.Equal(FontWeight.Bold, bankName.FontWeight.Value);

        // Line 2: "дата" (bold) + " 01.01.25" (normal)
        var line2 = Assert.IsType<FlexElement>(root.Children[1]);
        Assert.Equal(2, line2.Children.Count);

        var dateLabel = Assert.IsType<TextElement>(line2.Children[0]);
        Assert.Equal("\u0434\u0430\u0442\u0430", dateLabel.Content.Value);
        Assert.Equal(FontWeight.Bold, dateLabel.FontWeight.Value);

        var dateValue = Assert.IsType<TextElement>(line2.Children[1]);
        Assert.Equal(" 01.01.25", dateValue.Content.Value);
    }

    [Fact]
    public void Parse_BankEBalance_WithSpacesAndFormFeed()
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
                },
                ["2"] = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase)
                {
                    ["font"] = "default"
                }
            }
        };

        // Pattern: ESC(I card ESC(2: 999999*0000 \n data \n FF ESC(I check 2 ESC(2
        var input = "\x1B(Irfhnf\x1B(2: 999999*0000\n1:  300\x0C\x1B(Ixtr 2\x1B(2";

        var parser = new NdcContentParser();
        var result = parser.Parse(input, EmptyContext, options);

        var root = Assert.IsType<FlexElement>(Assert.Single(result));

        // Should have: row("карта" + ": 999999*0000"), row("1:  300"), separator, row("чек 2")
        Assert.True(root.Children.Count >= 4);

        // Check decoded card label is bold
        var line1 = Assert.IsType<FlexElement>(root.Children[0]);
        var cardLabel = Assert.IsType<TextElement>(line1.Children[0]);
        Assert.Equal("\u043a\u0430\u0440\u0442\u0430", cardLabel.Content.Value);
        Assert.Equal(FontWeight.Bold, cardLabel.FontWeight.Value);

        // Check separator exists
        Assert.Contains(root.Children, c => c is SeparatorElement);
    }

    [Fact]
    public void Parse_WithBuilderRegistration()
    {
        // Verify the builder extension works end-to-end
        var builder = new FlexRender.Configuration.FlexRenderBuilder();
        builder.WithNdc(); // should not throw
    }

    [Fact]
    public void Parse_RealBankAReceipt_ProducesNonEmptyAst()
    {
        var rawBytes = File.ReadAllBytes("Content/Ndc/TestData/bank-a-mini-statement.bin");
        // Read as Latin1 to preserve byte values
        var text = System.Text.Encoding.Latin1.GetString(rawBytes);

        var options = new Dictionary<string, object>
        {
            ["charsets"] = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase)
            {
                ["I"] = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase)
                {
                    ["font"] = "bold",
                    ["font_style"] = "bold",
                    ["encoding"] = "qwerty-jcuken"
                },
                ["1"] = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase)
                {
                    ["font"] = "default"
                }
            }
        };

        var parser = new NdcContentParser();
        var result = parser.Parse(text, EmptyContext, options);

        Assert.NotEmpty(result);
        var root = Assert.IsType<FlexElement>(result[0]);
        Assert.True(root.Children.Count > 10, "Receipt should produce many lines");

        // Verify some decoded Russian text exists
        var allText = GetAllTextContent(root);
        Assert.Contains("\u0442\u0435\u0441\u0442\u043e\u0432\u044b\u0439 \u0431\u0430\u043d\u043a \u0430", allText, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("\u0434\u0430\u0442\u0430", allText, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("\u043e\u043f\u0435\u0440\u0430\u0446\u0438\u044f", allText, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Parse_RealBankEReceipt_ProducesMultiPageAst()
    {
        var rawBytes = File.ReadAllBytes("Content/Ndc/TestData/bank-e-balance.bin");
        var text = System.Text.Encoding.Latin1.GetString(rawBytes);

        var options = new Dictionary<string, object>
        {
            ["charsets"] = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase)
            {
                ["I"] = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase)
                {
                    ["font"] = "bold",
                    ["font_style"] = "bold",
                    ["encoding"] = "qwerty-jcuken"
                },
                ["2"] = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase)
                {
                    ["font"] = "default"
                }
            }
        };

        var parser = new NdcContentParser();
        var result = parser.Parse(text, EmptyContext, options);

        Assert.NotEmpty(result);
        var root = Assert.IsType<FlexElement>(result[0]);

        // Bank E receipt has FF (form feed) so should have SeparatorElements
        Assert.Contains(root.Children, c => c is SeparatorElement);

        // Verify decoded text
        var allText = GetAllTextContent(root);
        Assert.Contains("\u043a\u0430\u0440\u0442\u0430", allText, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("\u0431\u0430\u043b\u0430\u043d\u0441", allText, StringComparison.OrdinalIgnoreCase);
    }

    private static string GetAllTextContent(FlexElement root)
    {
        var sb = new StringBuilder();
        CollectText(root, sb);
        return sb.ToString();
    }

    private static void CollectText(TemplateElement element, StringBuilder sb)
    {
        if (element is TextElement text)
            sb.Append(text.Content.Value);
        if (element is FlexElement flex)
            foreach (var child in flex.Children)
                CollectText(child, sb);
    }
}
