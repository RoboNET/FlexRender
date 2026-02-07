using FlexRender.Layout;
using FlexRender.Parsing.Ast;
using SkiaSharp;

namespace FlexRender.Rendering;

/// <summary>
/// SkiaSharp-based text shaper that uses <see cref="SKFont"/> metrics
/// for accurate word-wrap, max-lines, and ellipsis computation.
/// Extracts and centralizes the line-breaking logic previously duplicated
/// between <see cref="TextRenderer.MeasureText"/> and <see cref="TextRenderer.DrawText"/>.
/// </summary>
public sealed class SkiaTextShaper : ITextShaper
{
    private const string Ellipsis = "...";

    private readonly FontManager _fontManager;
    private readonly RenderOptions _defaultRenderOptions;

    /// <summary>
    /// Creates a new <see cref="SkiaTextShaper"/> with the specified font manager.
    /// </summary>
    /// <param name="fontManager">The font manager for typeface resolution.</param>
    /// <param name="defaultRenderOptions">
    /// Default render options used for font configuration during text shaping.
    /// When <c>null</c>, <see cref="RenderOptions.Default"/> is used.
    /// </param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="fontManager"/> is null.</exception>
    public SkiaTextShaper(FontManager fontManager, RenderOptions? defaultRenderOptions = null)
    {
        ArgumentNullException.ThrowIfNull(fontManager);
        _fontManager = fontManager;
        _defaultRenderOptions = defaultRenderOptions ?? RenderOptions.Default;
    }

    /// <inheritdoc />
    public TextShapingResult ShapeText(TextElement element, float fontSize, float maxWidth)
    {
        ArgumentNullException.ThrowIfNull(element);

        if (string.IsNullOrEmpty(element.Content))
        {
            return new TextShapingResult(
                Array.Empty<string>(),
                new LayoutSize(0f, 0f),
                0f);
        }

        using var font = CreateFont(element, fontSize);

        var effectiveMaxWidth = element.Overflow == TextOverflow.Visible && !element.Wrap
            ? float.MaxValue
            : maxWidth;

        var lines = GetLines(element.Content, element.Wrap, effectiveMaxWidth, font, element.MaxLines, element.Overflow);

        if (lines.Count == 0)
        {
            return new TextShapingResult(
                Array.Empty<string>(),
                new LayoutSize(0f, 0f),
                0f);
        }

        var maxLineWidth = 0f;
        foreach (var line in lines)
        {
            var w = font.MeasureText(line);
            if (w > maxLineWidth) maxLineWidth = w;
        }
        maxLineWidth = (float)Math.Ceiling(maxLineWidth);

        var defaultLineHeight = Math.Abs(font.Metrics.Top) + font.Metrics.Bottom;
        var lineHeight = LineHeightResolver.Resolve(element.LineHeight, font.Size, defaultLineHeight);
        var totalHeight = lines.Count * lineHeight;

        return new TextShapingResult(
            lines,
            new LayoutSize(maxLineWidth, totalHeight),
            lineHeight);
    }

    /// <summary>
    /// Creates an <see cref="SKFont"/> configured for the given text element.
    /// </summary>
    private SKFont CreateFont(TextElement element, float fontSize)
    {
        var typeface = _fontManager.GetTypeface(element.Font);
        var font = new SKFont(typeface, fontSize)
        {
            Subpixel = _defaultRenderOptions.SubpixelText,
            Hinting = MapFontHinting(_defaultRenderOptions.FontHinting),
            Edging = MapTextRendering(_defaultRenderOptions.TextRendering)
        };
        return font;
    }

    private static SKFontHinting MapFontHinting(FontHinting hinting) => hinting switch
    {
        FontHinting.None => SKFontHinting.None,
        FontHinting.Slight => SKFontHinting.Slight,
        FontHinting.Normal => SKFontHinting.Normal,
        FontHinting.Full => SKFontHinting.Full,
        _ => SKFontHinting.Normal
    };

    private static SKFontEdging MapTextRendering(TextRendering rendering) => rendering switch
    {
        TextRendering.Aliased => SKFontEdging.Alias,
        TextRendering.Grayscale => SKFontEdging.Antialias,
        TextRendering.SubpixelLcd => SKFontEdging.SubpixelAntialias,
        _ => SKFontEdging.SubpixelAntialias
    };

    /// <summary>
    /// Splits text into lines with word wrapping, max-lines, and ellipsis support.
    /// This is the centralized line-breaking algorithm extracted from TextRenderer.
    /// </summary>
    private static List<string> GetLines(string text, bool wrap, float maxWidth, SKFont font, int? maxLines, TextOverflow overflow)
    {
        var estimatedLines = 1;
        foreach (var c in text)
        {
            if (c == '\n') estimatedLines++;
        }
        var lines = new List<string>(estimatedLines);

        var paragraphs = text.Split('\n');

        foreach (var paragraph in paragraphs)
        {
            if (!wrap)
            {
                var line = paragraph;
                if (overflow == TextOverflow.Ellipsis && font.MeasureText(line) > maxWidth)
                {
                    line = TruncateWithEllipsis(line, maxWidth, font);
                }
                lines.Add(line);
            }
            else if (font.MeasureText(paragraph) <= maxWidth)
            {
                lines.Add(paragraph);
            }
            else
            {
                var words = paragraph.Split(' ');
                var currentLine = "";

                foreach (var word in words)
                {
                    var testLine = string.IsNullOrEmpty(currentLine) ? word : $"{currentLine} {word}";
                    var testWidth = font.MeasureText(testLine);

                    if (testWidth <= maxWidth)
                    {
                        currentLine = testLine;
                    }
                    else
                    {
                        if (!string.IsNullOrEmpty(currentLine))
                            lines.Add(currentLine);
                        currentLine = word;

                        if (maxLines.HasValue && lines.Count >= maxLines.Value)
                            break;
                    }
                }

                if (!string.IsNullOrEmpty(currentLine))
                {
                    if (maxLines.HasValue && lines.Count == maxLines.Value - 1 && overflow == TextOverflow.Ellipsis)
                    {
                        currentLine = TruncateWithEllipsis(currentLine, maxWidth, font);
                    }
                    lines.Add(currentLine);
                }
            }

            if (maxLines.HasValue && lines.Count >= maxLines.Value)
                break;
        }

        if (maxLines.HasValue && lines.Count > maxLines.Value)
        {
            lines.RemoveRange(maxLines.Value, lines.Count - maxLines.Value);

            if (overflow == TextOverflow.Ellipsis && lines.Count > 0)
            {
                var lastIndex = lines.Count - 1;
                lines[lastIndex] = TruncateWithEllipsis(lines[lastIndex], maxWidth, font);
            }
        }

        return lines;
    }

    private static string TruncateWithEllipsis(string text, float maxWidth, SKFont font)
    {
        var ellipsisWidth = font.MeasureText(Ellipsis);

        if (font.MeasureText(text) <= maxWidth)
            return text;

        var availableWidth = maxWidth - ellipsisWidth;
        if (availableWidth <= 0)
            return Ellipsis;

        var low = 0;
        var high = text.Length;

        while (low < high)
        {
            var mid = (low + high + 1) / 2;
            var substring = text[..mid];
            if (font.MeasureText(substring) <= availableWidth)
            {
                low = mid;
            }
            else
            {
                high = mid - 1;
            }
        }

        if (low == 0)
            return Ellipsis;

        return text[..low] + Ellipsis;
    }
}
