using System.Text;
using FlexRender.Abstractions;
using FlexRender.Layout;
using FlexRender.Parsing.Ast;

namespace FlexRender.Content.Ndc;

/// <summary>
/// Parses NDC (NCR ATM protocol) printer data streams into FlexRender template elements.
/// Supports character set switching, spacing controls, form feeds, and barcodes.
/// Implements both string and binary parsing; binary data is decoded using a configurable
/// input encoding (default: Latin-1) before being handed to the string parser.
/// </summary>
public sealed class NdcContentParser : IContentParser, IBinaryContentParser
{
    /// <summary>
    /// Registers the code-pages encoding provider exactly once so that non-Latin1
    /// ISO/Windows code pages (e.g. ISO-8859-5 / code page 28595) are available to
    /// <see cref="Encoding.GetEncoding(string)"/> and <see cref="Encoding.GetEncoding(int)"/>.
    /// Static initialization is thread-safe and runs before any member is accessed.
    /// </summary>
    static NdcContentParser()
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
    }

    /// <inheritdoc />
    public string FormatName => "ndc";

    /// <inheritdoc />
    public IReadOnlyList<TemplateElement> Parse(string text, ContentParserContext context, IReadOnlyDictionary<string, object>? options = null)
    {
        ArgumentNullException.ThrowIfNull(text);
        ArgumentNullException.ThrowIfNull(context);
        if (string.IsNullOrWhiteSpace(text))
            return [];

        var effectiveWidth = context.ParentWidth ?? context.Canvas?.Width;
        var ndcOptions = NdcOptions.FromDictionary(options, effectiveWidth);
        var tokens = NdcTokenizer.Tokenize(text);

        // Measure actual max line width and use it for auto font size
        var measuredColumns = CalculateMaxLineWidth(tokens);
        ndcOptions = ndcOptions.WithMeasuredColumns(measuredColumns);

        return ConvertTokensToAst(tokens, ndcOptions);
    }

    /// <inheritdoc />
    public IReadOnlyList<TemplateElement> Parse(ReadOnlyMemory<byte> data, ContentParserContext context, IReadOnlyDictionary<string, object>? options = null)
    {
        ArgumentNullException.ThrowIfNull(context);
        if (data.IsEmpty)
            return [];

        var encodingName = "latin1";
        if (options is not null && options.TryGetValue("input_encoding", out var enc) && enc is not null)
            encodingName = enc.ToString() ?? "latin1";

        var encoding = ResolveEncoding(encodingName);
        var textContent = encoding.GetString(data.Span);
        return Parse(textContent, context, options);
    }

    /// <summary>
    /// Resolves an encoding identifier to a <see cref="System.Text.Encoding"/> instance.
    /// </summary>
    /// <param name="name">
    /// The encoding identifier. All values are resolved through <see cref="Encoding.GetEncoding(string)"/>
    /// (encoding names such as <c>latin1</c>, <c>iso-8859-1</c>, <c>iso-8859-5</c>, <c>utf-8</c>, <c>ascii</c>)
    /// or <see cref="Encoding.GetEncoding(int)"/> (numeric code pages such as <c>28595</c>). Common names
    /// already return the corresponding framework singletons. The dashless <c>utf8</c> alias is mapped to
    /// <c>utf-8</c> for backward compatibility. Non-Latin1 ISO/Windows code pages are supported through the
    /// registered code-pages provider.
    /// </param>
    /// <returns>The resolved <see cref="System.Text.Encoding"/>.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="name"/> is <see langword="null"/>.</exception>
    /// <exception cref="NotSupportedException">
    /// Thrown when <paramref name="name"/> does not correspond to a known encoding name or code page.
    /// </exception>
    internal static Encoding ResolveEncoding(string name)
    {
        ArgumentNullException.ThrowIfNull(name);

        var trimmed = name.Trim();

        // GetEncoding does not recognize the dashless "utf8" form; map it to the canonical "utf-8".
        if (string.Equals(trimmed, "utf8", StringComparison.OrdinalIgnoreCase))
            trimmed = "utf-8";

        try
        {
            return int.TryParse(trimmed, System.Globalization.CultureInfo.InvariantCulture, out var codePage)
                ? Encoding.GetEncoding(codePage)
                : Encoding.GetEncoding(trimmed);
        }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException)
        {
            throw new NotSupportedException(
                $"Unknown or unsupported input encoding: '{name}'. Use a known encoding name (e.g. 'iso-8859-5') or a numeric code page (e.g. '28595').",
                ex);
        }
    }

    private static int CalculateMaxLineWidth(List<NdcToken> tokens, int tabWidth = 8)
    {
        var maxWidth = 0;
        var currentWidth = 0;

        foreach (var token in tokens)
        {
            switch (token.Type)
            {
                case NdcTokenType.Text:
                    currentWidth += token.Value.Length;
                    break;

                case NdcTokenType.Spaces:
                    if (int.TryParse(token.Value, out var count))
                        currentWidth += count;
                    break;

                case NdcTokenType.HorizontalTab:
                {
                    var spacesNeeded = tabWidth - (currentWidth % tabWidth);
                    if (spacesNeeded == 0) spacesNeeded = tabWidth;
                    currentWidth += spacesNeeded;
                    break;
                }

                case NdcTokenType.LineFeed:
                case NdcTokenType.FormFeed:
                    if (currentWidth > maxWidth)
                        maxWidth = currentWidth;
                    currentWidth = 0;
                    break;

                    // Other token types don't affect line width
            }
        }

        // Don't forget the last line (no trailing LF)
        if (currentWidth > maxWidth)
            maxWidth = currentWidth;

        return maxWidth > 0 ? maxWidth : 1; // Avoid division by zero
    }

    private static IReadOnlyList<TemplateElement> ConvertTokensToAst(
        IEnumerable<NdcToken> tokens,
        NdcOptions options)
    {
        var root = new FlexElement { Direction = FlexDirection.Column };
        if (options.CanvasWidth.HasValue)
            root.FontSize = "fit-content";
        var currentLine = new List<TemplateElement>();
        var currentCharset = "1"; // Default charset per NDC spec
        var currentColumn = 0;
        var columns = options.Columns;
        var tabWidth = 8;

        foreach (var token in tokens)
        {
            switch (token.Type)
            {
                case NdcTokenType.Text:
                    var style = options.GetStyleForCharset(currentCharset);
                    var decoded = NdcEncodings.Decode(token.Value, style.Encoding, style.Uppercase);
                    AddTextWithWrapping(root, currentLine, decoded, style, ref currentColumn, columns, options);
                    break;

                case NdcTokenType.CharsetSwitch:
                    currentCharset = token.Value;
                    break;

                case NdcTokenType.Spaces:
                    if (int.TryParse(token.Value, out var count))
                    {
                        var spaceStyle = options.GetStyleForCharset(currentCharset);
                        var spaceText = new string(' ', count);
                        AddTextWithWrapping(root, currentLine, spaceText, spaceStyle, ref currentColumn, columns, options);
                    }
                    break;

                case NdcTokenType.LineFeed:
                    FlushLine(root, currentLine, options.GetStyleForCharset(currentCharset), options);
                    currentLine = [];
                    currentColumn = 0;
                    break;

                case NdcTokenType.FormFeed:
                    FlushLine(root, currentLine, options.GetStyleForCharset(currentCharset), options);
                    currentLine = [];
                    currentColumn = 0;
                    root.AddChild(new SeparatorElement());
                    break;

                case NdcTokenType.FieldSeparator:
                    // GS + digit -- printer field separator, no visual output
                    break;

                case NdcTokenType.Barcode:
                    var barcode = ParseBarcodeToken(token.Value);
                    if (barcode is not null)
                        currentLine.Add(barcode);
                    break;

                case NdcTokenType.HorizontalTab:
                {
                    var spacesNeeded = tabWidth - (currentColumn % tabWidth);
                    if (spacesNeeded == 0) spacesNeeded = tabWidth;
                    var tabStyle = options.GetStyleForCharset(currentCharset);
                    var tabText = new string(' ', spacesNeeded);
                    AddTextWithWrapping(root, currentLine, tabText, tabStyle, ref currentColumn, columns, options);
                    break;
                }

                case NdcTokenType.SetLeftMargin:
                    // Left margin positioning not yet implemented
                    break;

                case NdcTokenType.SetRightMargin:
                    if (int.TryParse(token.Value, out var rm) && rm > 0)
                        columns = Math.Clamp(rm, 1, 132);
                    break;

                case NdcTokenType.SetLinesPerInch:
                case NdcTokenType.SelectCodePage:
                case NdcTokenType.SelectInternationalCharset:
                case NdcTokenType.SelectArabicCharset:
                case NdcTokenType.BarcodeHriPosition:
                case NdcTokenType.BarcodeWidth:
                case NdcTokenType.BarcodeHeight:
                case NdcTokenType.PrintGraphics:
                case NdcTokenType.PrintBitImage:
                case NdcTokenType.PrintChequeImage:
                case NdcTokenType.DefineCharset:
                case NdcTokenType.DefineBitImage:
                case NdcTokenType.DualSidedPrinting:
                    // These token types have no visual representation in the rendered output
                    break;
            }
        }

        // Flush remaining line only if there's content
        if (currentLine.Count > 0)
            FlushLine(root, currentLine, options.GetStyleForCharset(currentCharset), options);

        return root.Children.Count > 0 ? [root] : [];
    }

    private static void AddTextWithWrapping(
        FlexElement root,
        List<TemplateElement> currentLine,
        string text,
        CharsetStyle style,
        ref int currentColumn,
        int columns,
        NdcOptions options)
    {
        var remaining = text.AsSpan();
        while (remaining.Length > 0)
        {
            var available = columns - currentColumn;
            if (available <= 0)
            {
                // Line full — wrap
                FlushLine(root, currentLine, style, options);
                currentLine.Clear();
                currentColumn = 0;
                available = columns;
            }

            if (remaining.Length <= available)
            {
                currentLine.Add(CreateTextElement(remaining.ToString(), style, options));
                currentColumn += remaining.Length;
                break;
            }

            // Split at column boundary
            currentLine.Add(CreateTextElement(remaining[..available].ToString(), style, options));
            remaining = remaining[available..];
            FlushLine(root, currentLine, style, options);
            currentLine.Clear();
            currentColumn = 0;
        }
    }

    private static void FlushLine(FlexElement root, List<TemplateElement> lineElements, CharsetStyle currentStyle, NdcOptions options)
    {
        var row = new FlexElement { Direction = FlexDirection.Row, Align = AlignItems.Baseline };
        if (lineElements.Count == 0)
        {
            // Empty line — add a space to preserve line height
            row.AddChild(CreateTextElement(" ", currentStyle, options));
        }
        else
        {
            foreach (var el in lineElements)
                row.AddChild(el);
        }
        root.AddChild(row);
    }

    private static TextElement CreateTextElement(string content, CharsetStyle style, NdcOptions options)
    {
        var text = new TextElement { Content = content, Wrap = false };

        // Font registration name (e.g., "bold", "default")
        if (style.Font is not null)
            text.Font = style.Font;

        // Font family: charset-specific overrides global
        if (style.FontFamily is not null)
            text.FontFamily = style.FontFamily;
        else if (options.FontFamily is not null)
            text.FontFamily = options.FontFamily;

        // Font style string maps to FontWeight and FontStyle on the text element
        if (style.FontStyle is not null)
        {
            switch (style.FontStyle.ToLowerInvariant())
            {
                case "bold":
                    text.FontWeight = FontWeight.Bold;
                    break;
                case "italic":
                    text.FontStyle = Parsing.Ast.FontStyle.Italic;
                    break;
                case "bold-italic" or "bolditalic":
                    text.FontWeight = FontWeight.Bold;
                    text.FontStyle = Parsing.Ast.FontStyle.Italic;
                    break;
                    // "regular" / "normal" / unknown -> keep defaults
            }
        }

        if (style.FontSize.HasValue)
            text.Size = style.FontSize.Value.ToString(System.Globalization.CultureInfo.InvariantCulture);

        if (style.Color is not null)
            text.Color = style.Color;

        return text;
    }

    private static BarcodeElement? ParseBarcodeToken(string value)
    {
        // Format: "type:data"
        var colonIndex = value.IndexOf(':');
        if (colonIndex < 0 || colonIndex >= value.Length - 1)
            return null;

        var typeChar = value[0];
        var data = value[(colonIndex + 1)..];

        var format = typeChar switch
        {
            '0' => BarcodeFormat.Upc,       // UPC-A
            '1' => BarcodeFormat.Upc,       // UPC-E -> mapped to UPC
            '2' => BarcodeFormat.Ean13,     // JAN13/EAN-13
            '3' => BarcodeFormat.Ean8,      // JAN8/EAN-8
            '4' => BarcodeFormat.Code39,    // Code 39
            '5' => BarcodeFormat.Code128,   // Interleaved 2 of 5 -> closest: Code128
            '6' => BarcodeFormat.Code128,   // Codabar -> closest: Code128
            _ => BarcodeFormat.Code128      // Default fallback
        };

        return new BarcodeElement { Data = data, Format = format };
    }
}
