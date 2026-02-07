using FlexRender.Parsing.Ast;

namespace FlexRender.Layout;

/// <summary>
/// A heuristic-based text shaper that uses approximate character width
/// (<c>fontSize * 0.6</c>) for line-break calculations.
/// Used as a fallback when no font-metric-aware shaper (e.g., SkiaSharp) is available,
/// such as in SVG-only rendering mode.
/// </summary>
/// <remarks>
/// This shaper provides reasonable word-wrap behavior without requiring
/// native font libraries. For pixel-perfect accuracy, use a font-metric-aware
/// shaper like <c>SkiaTextShaper</c>.
/// </remarks>
public sealed class ApproximateTextShaper : ITextShaper
{
    private const string Ellipsis = "...";

    /// <summary>
    /// Average character width as a fraction of font size.
    /// Empirically, 0.6 is a reasonable approximation for proportional Latin fonts.
    /// </summary>
    private const float CharWidthFactor = 0.6f;

    /// <summary>
    /// Default line height multiplier when no explicit line-height is specified.
    /// </summary>
    private const float DefaultLineHeightMultiplier = 1.4f;

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

        var charWidth = fontSize * CharWidthFactor;
        var lineHeight = LineHeightResolver.Resolve(
            element.LineHeight,
            fontSize,
            fontSize * DefaultLineHeightMultiplier);

        var effectiveMaxWidth = element.Overflow == TextOverflow.Visible && !element.Wrap
            ? float.MaxValue
            : maxWidth;

        var lines = GetLines(element.Content, element.Wrap, effectiveMaxWidth, charWidth,
            element.MaxLines, element.Overflow);

        if (lines.Count == 0)
        {
            return new TextShapingResult(
                Array.Empty<string>(),
                new LayoutSize(0f, 0f),
                lineHeight);
        }

        var maxLineWidth = 0f;
        foreach (var line in lines)
        {
            var w = line.Length * charWidth;
            if (w > maxLineWidth) maxLineWidth = w;
        }

        var totalHeight = lines.Count * lineHeight;

        return new TextShapingResult(lines, new LayoutSize(maxLineWidth, totalHeight), lineHeight);
    }

    private static List<string> GetLines(string text, bool wrap, float maxWidth, float charWidth,
        int? maxLines, TextOverflow overflow)
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
                if (overflow == TextOverflow.Ellipsis && line.Length * charWidth > maxWidth)
                {
                    line = TruncateWithEllipsis(line, maxWidth, charWidth);
                }
                lines.Add(line);
            }
            else if (paragraph.Length * charWidth <= maxWidth)
            {
                lines.Add(paragraph);
            }
            else
            {
                // Word wrap
                var words = paragraph.Split(' ');
                var currentLine = "";

                foreach (var word in words)
                {
                    var testLine = string.IsNullOrEmpty(currentLine) ? word : $"{currentLine} {word}";

                    if (testLine.Length * charWidth <= maxWidth)
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
                        currentLine = TruncateWithEllipsis(currentLine, maxWidth, charWidth);
                    }
                    lines.Add(currentLine);
                }
            }

            if (maxLines.HasValue && lines.Count >= maxLines.Value)
                break;
        }

        // Apply maxLines limit
        if (maxLines.HasValue && lines.Count > maxLines.Value)
        {
            lines.RemoveRange(maxLines.Value, lines.Count - maxLines.Value);

            if (overflow == TextOverflow.Ellipsis && lines.Count > 0)
            {
                var lastIndex = lines.Count - 1;
                lines[lastIndex] = TruncateWithEllipsis(lines[lastIndex], maxWidth, charWidth);
            }
        }

        return lines;
    }

    private static string TruncateWithEllipsis(string text, float maxWidth, float charWidth)
    {
        var ellipsisWidth = Ellipsis.Length * charWidth;

        if (text.Length * charWidth <= maxWidth)
            return text;

        var availableWidth = maxWidth - ellipsisWidth;
        if (availableWidth <= 0)
            return Ellipsis;

        var maxChars = (int)(availableWidth / charWidth);
        if (maxChars <= 0)
            return Ellipsis;

        if (maxChars >= text.Length)
            return text;

        return text[..maxChars] + Ellipsis;
    }
}
