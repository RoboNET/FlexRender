using FlexRender.Abstractions;
using FlexRender.Content.Ndc;
using FlexRender.Layout;
using FlexRender.Parsing.Ast;
using Xunit;

namespace FlexRender.Tests.Snapshots;

/// <summary>
/// Visual snapshot tests for NDC (ATM receipt protocol) content parser rendering.
/// Verifies that NDC printer data streams are correctly parsed and rendered
/// with charset switching, spacing controls, form feeds, and mixed encodings.
/// </summary>
/// <remarks>
/// <para>
/// These tests call <see cref="NdcContentParser.Parse"/> directly and embed
/// the resulting AST elements into a <see cref="Template"/>. This avoids
/// needing <c>ContentParserRegistry</c> in the renderer.
/// </para>
/// <para>
/// All tests use auto font size: <c>canvas_width / (columns * char_width_ratio)</c>.
/// Standard receipt (columns=40, canvas=576) yields font size 24.
/// </para>
/// <para>
/// Run with <c>UPDATE_SNAPSHOTS=true</c> to regenerate golden images.
/// </para>
/// </remarks>
public sealed class NdcSnapshotTests : SnapshotTestBase
{
    /// <summary>
    /// Initializes a new instance of the <see cref="NdcSnapshotTests"/> class.
    /// Re-registers the "default" and "bold" font names to use JetBrains Mono
    /// instead of the Inter font registered by the base class. This ensures NDC
    /// charset styles that reference <c>Font="default"</c> or <c>Font="bold"</c>
    /// resolve to monospaced JetBrains Mono for correct receipt rendering.
    /// </summary>
    public NdcSnapshotTests()
    {
        var fontsPath = GetFontsBasePath();

        Renderer.FontManager.RegisterFont("default", Path.Combine(fontsPath, "JetBrainsMono-Regular.ttf"));
        Renderer.FontManager.RegisterFont("bold", Path.Combine(fontsPath, "JetBrainsMono-Bold.ttf"));
    }

    /// <summary>
    /// Locates the Snapshots/Fonts directory by navigating from
    /// <see cref="AppContext.BaseDirectory"/> up to the project root.
    /// </summary>
    /// <returns>The absolute path to the Snapshots/Fonts directory.</returns>
    private static string GetFontsBasePath()
    {
        var current = AppContext.BaseDirectory;

        while (!string.IsNullOrEmpty(current))
        {
            if (Directory.GetFiles(current, "*.csproj").Length > 0)
            {
                return Path.Combine(current, "Snapshots", "Fonts");
            }

            var parent = Directory.GetParent(current);
            if (parent == null)
                break;

            current = parent.FullName;
        }

        // Fallback to base directory
        return Path.Combine(AppContext.BaseDirectory, "Snapshots", "Fonts");
    }

    /// <summary>
    /// Tests a simple receipt with mixed Cyrillic (JCUKEN) and ASCII charsets.
    /// Charset I renders bold Cyrillic text; charset 1 renders normal ASCII.
    /// Uses auto font size (24pt from 576 / (40 * 0.6)).
    /// </summary>
    [Fact]
    public void NdcReceipt_SimpleRussianAscii()
    {
        var ndcData = ":02\x1b(1              \x1b(Intcnjdsq ~fyr f\x1b(1\r\n" +
                      "        \x1b(Intk\x1b(1. 8 (800) 000-00-00\r\n" +
                      "\x1b(Iflhtc\x1b(1:\r\n" +
                      "MOSCOW, TESTOVAYA UL., 1";

        var options = CreateAutoFontOptions();

        var parser = new NdcContentParser();
        var elements = parser.Parse(ndcData, CreateContext(), options);

        var template = CreateTemplate();
        foreach (var el in elements)
            template.AddElement(el);

        AssertSnapshot("ndc_receipt_simple", template, new ObjectValue());
    }

    /// <summary>
    /// Tests that a form feed character (0x0C) produces a <see cref="SeparatorElement"/>
    /// between two pages of content.
    /// Uses auto font size (24pt from 576 / (40 * 0.6)).
    /// </summary>
    [Fact]
    public void NdcReceipt_WithFormFeed()
    {
        var ndcData = "\x1b(1PAGE 1 CONTENT\r\n" +
                      "Line 2\x0c" +
                      "PAGE 2 CONTENT\r\n" +
                      "Line 2 of page 2";

        var options = new Dictionary<string, object>
        {
            ["columns"] = 40,
            ["font_family"] = "JetBrains Mono"
        };

        var parser = new NdcContentParser();
        var elements = parser.Parse(ndcData, CreateContext(), options);

        var template = CreateTemplate();
        foreach (var el in elements)
            template.AddElement(el);

        AssertSnapshot("ndc_receipt_formfeed", template, new ObjectValue());
    }

    /// <summary>
    /// Tests SO (Shift Out, 0x0E) spacing control that inserts a specified
    /// number of spaces between text segments on the same line.
    /// Uses auto font size (24pt from 576 / (40 * 0.6)).
    /// </summary>
    [Fact]
    public void NdcReceipt_WithSpacing()
    {
        var ndcData = "\x1b(1Label\x0e5Value\r\n" +
                      "Left\x0e9Right";

        var options = new Dictionary<string, object>
        {
            ["columns"] = 40,
            ["font_family"] = "JetBrains Mono"
        };

        var parser = new NdcContentParser();
        var elements = parser.Parse(ndcData, CreateContext(), options);

        var template = CreateTemplate();
        foreach (var el in elements)
            template.AddElement(el);

        AssertSnapshot("ndc_receipt_spacing", template, new ObjectValue());
    }

    /// <summary>
    /// Tests charset switching between normal and double-size fonts.
    /// Charset &gt; renders bold text at 48pt (double of auto 24); charset 1 uses auto 24pt.
    /// </summary>
    [Fact]
    public void NdcReceipt_DoubleSizeCharset()
    {
        var ndcData = "\x1b(>HEADER\r\n" +
                      "\x1b(1Normal text\r\n" +
                      "\x1b(>BIG TEXT";

        var options = new Dictionary<string, object>
        {
            ["columns"] = 40,
            ["font_family"] = "JetBrains Mono",
            ["charsets"] = new Dictionary<string, object>
            {
                [">"] = new Dictionary<string, object>
                {
                    ["font"] = "bold",
                    ["font_style"] = "bold",
                    ["font_size"] = 48 // double of auto (24)
                },
                ["1"] = new Dictionary<string, object>
                {
                    ["font"] = "default"
                    // auto font size = 24
                }
            }
        };

        var parser = new NdcContentParser();
        var elements = parser.Parse(ndcData, CreateContext(), options);

        var template = CreateTemplate();
        foreach (var el in elements)
            template.AddElement(el);

        AssertSnapshot("ndc_receipt_doublesize", template, new ObjectValue());
    }

    /// <summary>
    /// Renders the full Bank A mini statement receipt from real ATM data.
    /// Uses auto font size (24pt from 576 / (40 * 0.6)).
    /// </summary>
    [Fact]
    public void NdcReceipt_BankAMiniStatement()
    {
        var text = LoadTestData("bank-a-mini-statement.bin");

        var options = CreateAutoFontOptions();
        var parser = new NdcContentParser();
        var elements = parser.Parse(text, CreateContext(), options);

        var template = CreateTemplate();
        foreach (var el in elements)
            template.AddElement(el);

        AssertSnapshot("ndc_receipt_bank_a", template, new ObjectValue());
    }

    /// <summary>
    /// Renders the full Bank E balance/service receipt from real ATM data.
    /// Contains two pages separated by a form feed.
    /// Uses 44 columns with auto font size (~21pt from 576 / (44 * 0.6)).
    /// </summary>
    [Fact]
    public void NdcReceipt_BankEBalance()
    {
        var text = LoadTestData("bank-e-balance.bin");

        var options = CreateAutoFontOptions(cyrillicCharset: "I", asciiCharset: "2", columns: 44);
        var parser = new NdcContentParser();
        var elements = parser.Parse(text, CreateContext(), options);

        var template = CreateTemplate();
        foreach (var el in elements)
            template.AddElement(el);

        AssertSnapshot("ndc_receipt_bank_e", template, new ObjectValue());
    }

    /// <summary>
    /// Renders a Bank C balance receipt. Uses charsets I (Cyrillic) and 2.
    /// Uses auto font size (24pt from 576 / (40 * 0.6)).
    /// </summary>
    [Fact]
    public void NdcReceipt_BankCBalance()
    {
        var text = LoadTestData("bank-c-balance-receipt.bin");

        var options = CreateAutoFontOptions(cyrillicCharset: "I", asciiCharset: "2");
        var parser = new NdcContentParser();
        var elements = parser.Parse(text, CreateContext(), options);

        var template = CreateTemplate();
        foreach (var el in elements)
            template.AddElement(el);

        AssertSnapshot("ndc_receipt_bank_c_balance", template, new ObjectValue());
    }

    /// <summary>
    /// Renders a Bank C mini statement receipt with transaction history.
    /// Uses auto font size (24pt from 576 / (40 * 0.6)).
    /// </summary>
    [Fact]
    public void NdcReceipt_BankCStatement()
    {
        var text = LoadTestData("bank-c-statement-receipt.bin");

        var options = CreateAutoFontOptions(cyrillicCharset: "I", asciiCharset: "2");
        var parser = new NdcContentParser();
        var elements = parser.Parse(text, CreateContext(), options);

        var template = CreateTemplate();
        foreach (var el in elements)
            template.AddElement(el);

        AssertSnapshot("ndc_receipt_bank_c_statement", template, new ObjectValue());
    }

    /// <summary>
    /// Renders a Bank D balance receipt. Uses charsets I and J for Cyrillic.
    /// Uses auto font size (24pt from 576 / (40 * 0.6)).
    /// </summary>
    [Fact]
    public void NdcReceipt_BankDBalance()
    {
        var text = LoadTestData("bank-d-balance-receipt.bin");

        var options = new Dictionary<string, object>
        {
            ["columns"] = 40,
            ["font_family"] = "JetBrains Mono",
            ["charsets"] = new Dictionary<string, object>
            {
                ["I"] = new Dictionary<string, object>
                {
                    ["font"] = "bold",
                    ["font_style"] = "bold",
                    ["encoding"] = "qwerty-jcuken",
                    ["uppercase"] = true
                },
                ["J"] = new Dictionary<string, object>
                {
                    ["encoding"] = "qwerty-jcuken"
                },
                ["2"] = new Dictionary<string, object>
                {
                    ["font"] = "default"
                }
            }
        };

        var parser = new NdcContentParser();
        var elements = parser.Parse(text, CreateContext(), options);

        var template = CreateTemplate();
        foreach (var el in elements)
            template.AddElement(el);

        AssertSnapshot("ndc_receipt_bank_d_balance", template, new ObjectValue());
    }

    /// <summary>
    /// Renders a Bank A cash withdrawal receipt.
    /// Uses auto font size (24pt from 576 / (40 * 0.6)).
    /// </summary>
    [Fact]
    public void NdcReceipt_BankACashout()
    {
        var text = LoadTestData("bank-a-cashout-receipt.bin");

        var options = CreateAutoFontOptions();
        var parser = new NdcContentParser();
        var elements = parser.Parse(text, CreateContext(), options);

        var template = CreateTemplate();
        foreach (var el in elements)
            template.AddElement(el);

        AssertSnapshot("ndc_receipt_bank_a_cashout", template, new ObjectValue());
    }

    /// <summary>
    /// Renders a Bank B balance receipt.
    /// Uses charset 1 with pre-encoded Cyrillic (no JCUKEN mapping needed).
    /// Uses auto font size (24pt from 576 / (40 * 0.6)).
    /// </summary>
    [Fact]
    public void NdcReceipt_BankBBalance()
    {
        var text = LoadTestData("bank-b-balance-receipt.bin", System.Text.Encoding.UTF8);

        var options = new Dictionary<string, object>
        {
            ["columns"] = 40,
            ["font_family"] = "JetBrains Mono"
        };

        var parser = new NdcContentParser();
        var elements = parser.Parse(text, CreateContext(), options);

        var template = CreateTemplate();
        foreach (var el in elements)
            template.AddElement(el);

        AssertSnapshot("ndc_receipt_bank_b_balance", template, new ObjectValue());
    }

    /// <summary>
    /// Tests auto font size calculation from canvas width and columns.
    /// No explicit font_size on charsets -- auto = 576 / (40 * 0.6) = 24.
    /// </summary>
    [Fact]
    public void NdcReceipt_AutoFontSize()
    {
        var ndcData = ":02\x1b(1              \x1b(Intcnjdsq ~fyr f\x1b(1\r\n" +
                      "        \x1b(Intk\x1b(1. 8 (800) 000-00-00\r\n" +
                      "\x1b(Iflhtc\x1b(1:\r\n" +
                      "MOSCOW, TESTOVAYA UL., 1";

        var options = new Dictionary<string, object>
        {
            ["font_family"] = "JetBrains Mono",
            ["columns"] = 40,
            ["charsets"] = new Dictionary<string, object>
            {
                ["I"] = new Dictionary<string, object>
                {
                    ["font"] = "bold",
                    ["font_style"] = "bold",
                    ["encoding"] = "qwerty-jcuken",
                    ["uppercase"] = true
                    // no font_size — auto = 576 / (40 * 0.6) = 24
                },
                ["1"] = new Dictionary<string, object>
                {
                    ["font"] = "default"
                    // no font_size — auto = 24
                }
            }
        };

        var parser = new NdcContentParser();
        var elements = parser.Parse(ndcData, CreateContext(), options);

        var template = CreateTemplate(576);
        foreach (var el in elements)
            template.AddElement(el);

        AssertSnapshot("ndc_receipt_autofont", template, new ObjectValue());
    }

    /// <summary>
    /// Renders a composite receipt with a branded header, NDC receipt body (Bank A cashout),
    /// and a footer disclaimer. The receipt body is wrapped in a container with padding and
    /// a rounded border. Verifies that fit-content works correctly when NDC-parsed content
    /// is embedded within a larger composed template with non-NDC elements and decorations.
    /// </summary>
    [Fact]
    public void NdcReceipt_CompositeWithHeaderFooter()
    {
        var text = LoadTestData("bank-a-cashout-receipt.bin");

        var options = CreateAutoFontOptions();
        var parser = new NdcContentParser();
        var elements = parser.Parse(text, CreateContext(), options);

        var template = CreateTemplate(600);

        // Card with rounded corners, border, and clipped overflow.
        // Margin reduces available width via stretch pre-adjustment in LayoutEngine.
        var card = new FlexElement { Direction = FlexDirection.Column };
        card.Margin = "12";
        card.BorderRadius = "10";
        card.Border = "1 solid #d0d0d0";
        card.Overflow = Overflow.Hidden;

        // Header with gradient background
        var header = new FlexElement
        {
            Direction = FlexDirection.Row,
            Justify = JustifyContent.SpaceBetween,
            Align = AlignItems.Center
        };
        header.Background = "linear-gradient(to right, #2c3e50, #3498db)";
        header.Padding = "10 14";
        var headerTitle = new TextElement
        {
            Content = "ТЕСТОВЫЙ БАНК А",
            Color = "#ffffff",
            Size = "18",
            FontWeight = FontWeight.Bold
        };
        var headerDate = new TextElement
        {
            Content = "01.01.2025",
            Color = "#cce0ff",
            Size = "12"
        };
        header.AddChild(headerTitle);
        header.AddChild(headerDate);
        card.AddChild(header);

        // NDC receipt body with padding and light background
        var receiptBody = new FlexElement { Direction = FlexDirection.Column };
        receiptBody.Padding = "14 12";
        receiptBody.Background = "#f8f9fa";
        foreach (var el in elements)
            receiptBody.AddChild(el);
        card.AddChild(receiptBody);

        // Footer with subtle background
        var footer = new FlexElement
        {
            Direction = FlexDirection.Row,
            Justify = JustifyContent.Center
        };
        footer.Background = "linear-gradient(to right, #ecf0f1, #dfe6e9)";
        footer.Padding = "8";
        footer.BorderTop = "1 solid #d0d0d0";
        var footerText = new TextElement
        {
            Content = "Чек сохранен в электронном виде",
            Color = "#666666",
            Size = "10"
        };
        footer.AddChild(footerText);
        card.AddChild(footer);

        template.AddElement(card);

        AssertSnapshot("ndc_receipt_composite", template, new ObjectValue());
    }

    /// <summary>
    /// Creates a <see cref="ContentParserContext"/> with the specified canvas width.
    /// </summary>
    /// <param name="canvasWidth">The canvas width in pixels.</param>
    /// <returns>A context with canvas settings configured.</returns>
    private static ContentParserContext CreateContext(int canvasWidth = 576)
    {
        return new ContentParserContext
        {
            Canvas = new CanvasSettings { Width = canvasWidth }
        };
    }

    /// <summary>
    /// Creates options for standard NDC receipt rendering with auto font size.
    /// Auto font size = canvas width / (<paramref name="columns"/> * 0.6).
    /// Canvas width is provided via <see cref="ContentParserContext"/>.
    /// </summary>
    /// <param name="cyrillicCharset">The charset identifier for Cyrillic text (bold, JCUKEN, uppercase).</param>
    /// <param name="asciiCharset">The charset identifier for ASCII text (normal weight).</param>
    /// <param name="columns">Number of character columns on the receipt.</param>
    /// <returns>A dictionary of NDC parser options configured for auto font sizing.</returns>
    private static Dictionary<string, object> CreateAutoFontOptions(
        string cyrillicCharset = "I",
        string asciiCharset = "1",
        int columns = 40)
    {
        return new Dictionary<string, object>
        {
            ["columns"] = columns,
            ["font_family"] = "JetBrains Mono",
            ["charsets"] = new Dictionary<string, object>
            {
                [cyrillicCharset] = new Dictionary<string, object>
                {
                    ["font"] = "bold",
                    ["font_style"] = "bold",
                    ["encoding"] = "qwerty-jcuken",
                    ["uppercase"] = true
                },
                [asciiCharset] = new Dictionary<string, object>
                {
                    ["font"] = "default"
                }
            }
        };
    }

    /// <summary>
    /// Loads binary test data from the Content/Ndc/TestData directory.
    /// </summary>
    /// <param name="fileName">The test data file name.</param>
    /// <param name="encoding">
    /// The encoding to use for reading the file. Defaults to Latin1 for raw NDC binary data.
    /// </param>
    /// <returns>The file contents as a string decoded with the specified encoding.</returns>
    private static string LoadTestData(string fileName, System.Text.Encoding? encoding = null)
    {
        var assemblyDir = AppContext.BaseDirectory;
        var testDataPath = Path.Combine(assemblyDir, "Content", "Ndc", "TestData", fileName);
        var rawBytes = File.ReadAllBytes(testDataPath);
        return (encoding ?? System.Text.Encoding.Latin1).GetString(rawBytes);
    }

    /// <summary>
    /// Creates a template with the specified canvas width, white background, and fixed width.
    /// Default width is 576px, matching the standard 40-column thermal receipt width.
    /// </summary>
    /// <param name="width">The canvas width in pixels.</param>
    /// <returns>A new template configured for NDC receipt rendering.</returns>
    private static Template CreateTemplate(int width = 576)
    {
        return new Template
        {
            Canvas = new CanvasSettings
            {
                Fixed = FixedDimension.Width,
                Width = width,
                Background = "#ffffff"
            }
        };
    }
}
