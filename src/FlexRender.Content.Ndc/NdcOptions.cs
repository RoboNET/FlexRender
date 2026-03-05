namespace FlexRender.Content.Ndc;

/// <summary>
/// Parsed options for the NDC content parser, extracted from the YAML options block.
/// </summary>
internal sealed class NdcOptions
{
    private static readonly CharsetStyle DefaultStyle = new();

    /// <summary>
    /// Input byte encoding (e.g., "latin1", "cp866", "utf-8"). Default: "latin1".
    /// </summary>
    public string InputEncoding { get; private init; } = "latin1";

    /// <summary>
    /// Maximum number of characters per line (typical receipt width: 40-44). Default: 40.
    /// Used for auto-wrapping lines that exceed this width.
    /// </summary>
    public int Columns { get; private init; } = 40;

    /// <summary>
    /// Font family for all text in the receipt (e.g., "JetBrains Mono", "Courier New").
    /// When set, all generated text elements will use this font family.
    /// </summary>
    public string? FontFamily { get; private init; }

    /// <summary>
    /// Character width as a fraction of font size for monospace fonts. Default: 0.6.
    /// </summary>
    public double CharWidthRatio { get; private init; } = 0.6;

    /// <summary>
    /// Canvas width in pixels, sourced from <see cref="FlexRender.Abstractions.ContentParserContext"/>.
    /// Used for auto font size calculation.
    /// </summary>
    public int? CanvasWidth { get; private init; }

    /// <summary>
    /// Measured maximum line width from actual data. Set by the parser after scanning tokens.
    /// When set, used instead of <see cref="Columns"/> for <see cref="AutoFontSize"/> calculation.
    /// </summary>
    internal int? MeasuredColumns { get; private init; }

    /// <summary>
    /// Auto-calculated font size based on canvas width, columns, and char width ratio.
    /// Uses <see cref="MeasuredColumns"/> when available, otherwise falls back to <see cref="Columns"/>.
    /// Returns null when canvas width is not available.
    /// </summary>
    internal int? AutoFontSize => CanvasWidth.HasValue
        ? (int)(CanvasWidth.Value / ((MeasuredColumns ?? Columns) * CharWidthRatio))
        : null;

    /// <summary>
    /// Per-charset style mappings keyed by designator character (e.g., "I", "1", ">").
    /// </summary>
    public IReadOnlyDictionary<string, CharsetStyle> Charsets { get; private init; } =
        new Dictionary<string, CharsetStyle>(StringComparer.Ordinal);

    /// <summary>
    /// Creates a copy of these options with the specified measured columns value.
    /// </summary>
    internal NdcOptions WithMeasuredColumns(int measuredColumns) => new()
    {
        InputEncoding = InputEncoding,
        Columns = Columns,
        FontFamily = FontFamily,
        CharWidthRatio = CharWidthRatio,
        CanvasWidth = CanvasWidth,
        Charsets = Charsets,
        MeasuredColumns = measuredColumns
    };

    /// <summary>
    /// Gets the style for a charset designator, or the default style if not configured.
    /// </summary>
    internal CharsetStyle GetStyleForCharset(string designator) =>
        Charsets.TryGetValue(designator, out var style) ? style : DefaultStyle;

    /// <summary>
    /// Creates <see cref="NdcOptions"/> from the raw YAML options dictionary and canvas width from context.
    /// </summary>
    /// <param name="dict">The raw options dictionary from the YAML content element.</param>
    /// <param name="canvasWidth">Canvas width in pixels from <see cref="FlexRender.Abstractions.ContentParserContext"/>.</param>
    internal static NdcOptions FromDictionary(IReadOnlyDictionary<string, object>? dict, int? canvasWidth = null)
    {
        if (dict is null)
            return new NdcOptions { CanvasWidth = canvasWidth > 0 ? canvasWidth : null };

        var inputEncoding = dict.TryGetValue("input_encoding", out var enc)
            ? enc?.ToString() ?? "latin1"
            : "latin1";

        var columns = dict.TryGetValue("columns", out var col) && int.TryParse(col?.ToString(), out var colVal) && colVal > 0
            ? colVal
            : 40;

        var fontFamily = dict.TryGetValue("font_family", out var ff) ? ff?.ToString() : null;

        var charWidthRatio = dict.TryGetValue("char_width_ratio", out var cwr)
            && double.TryParse(cwr?.ToString(), System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture, out var cwrVal)
            && cwrVal > 0
                ? cwrVal
                : 0.6;

        var charsets = new Dictionary<string, CharsetStyle>(StringComparer.Ordinal);
        if (dict.TryGetValue("charsets", out var charsetsObj) && charsetsObj is IReadOnlyDictionary<string, object> charsetsDict)
        {
            foreach (var (key, value) in charsetsDict)
            {
                if (value is IReadOnlyDictionary<string, object> styleDict)
                {
                    charsets[key] = ParseCharsetStyle(styleDict);
                }
            }
        }

        return new NdcOptions
        {
            InputEncoding = inputEncoding,
            Columns = columns,
            FontFamily = fontFamily,
            CharWidthRatio = charWidthRatio,
            CanvasWidth = canvasWidth > 0 ? canvasWidth : null,
            Charsets = charsets
        };
    }

    private static CharsetStyle ParseCharsetStyle(IReadOnlyDictionary<string, object> dict)
    {
        var font = dict.TryGetValue("font", out var f) ? f?.ToString() : null;
        var fontFamily = dict.TryGetValue("font_family", out var ff) ? ff?.ToString() : null;

        // Support both new "font_style" and legacy "bold" for backward compatibility.
        string? fontStyle = null;
        if (dict.TryGetValue("font_style", out var fst))
        {
            fontStyle = fst?.ToString();
        }
        else if (dict.TryGetValue("bold", out var b) &&
                 string.Equals(b?.ToString(), "true", StringComparison.OrdinalIgnoreCase))
        {
            // Legacy: bold=true maps to font_style="bold" and font="bold"
            fontStyle = "bold";
            font ??= "bold";
        }

        int? fontSize = dict.TryGetValue("font_size", out var fs) && int.TryParse(fs?.ToString(), out var fsVal)
            ? fsVal
            : null;

        var color = dict.TryGetValue("color", out var c) ? c?.ToString() : null;

        var encoding = dict.TryGetValue("encoding", out var e) ? e?.ToString() ?? "none" : "none";

        var uppercase = dict.TryGetValue("uppercase", out var u) &&
                        string.Equals(u?.ToString(), "true", StringComparison.OrdinalIgnoreCase);

        return new CharsetStyle(font, fontFamily, fontStyle, fontSize, color, encoding, uppercase);
    }
}
