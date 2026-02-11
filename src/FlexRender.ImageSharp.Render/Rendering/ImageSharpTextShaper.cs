using FlexRender.Layout;
using FlexRender.Parsing.Ast;
using SixLabors.Fonts;

namespace FlexRender.ImageSharp.Rendering;

/// <summary>
/// ImageSharp-based text shaper that uses SixLabors.Fonts for accurate
/// word-wrap, max-lines, and ellipsis computation.
/// Implements <see cref="ITextShaper"/> so the layout engine can compute
/// precise text dimensions without depending on SkiaSharp.
/// </summary>
internal sealed class ImageSharpTextShaper : ITextShaper
{
    private const string Ellipsis = "...";

    private readonly ImageSharpFontManager _fontManager;

    /// <summary>
    /// Creates a new <see cref="ImageSharpTextShaper"/> with the specified font manager.
    /// </summary>
    /// <param name="fontManager">The font manager for font resolution.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="fontManager"/> is null.</exception>
    public ImageSharpTextShaper(ImageSharpFontManager fontManager)
    {
        ArgumentNullException.ThrowIfNull(fontManager);
        _fontManager = fontManager;
    }

    /// <inheritdoc />
    public TextShapingResult ShapeText(TextElement element, float fontSize, float maxWidth)
    {
        ArgumentNullException.ThrowIfNull(element);

        if (string.IsNullOrEmpty(element.Content.Value))
        {
            return new TextShapingResult(
                Array.Empty<string>(),
                new LayoutSize(0f, 0f),
                0f);
        }

        var font = CreateFont(element, fontSize);

        var effectiveMaxWidth = element.Overflow.Value == TextOverflow.Visible && !element.Wrap.Value
            ? float.MaxValue
            : maxWidth;

        var lines = GetLines(element.Content.Value, element.Wrap.Value, effectiveMaxWidth, font, element.MaxLines.Value, element.Overflow.Value);

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
            var measured = TextMeasurer.MeasureSize(line, new TextOptions(font));
            if (measured.Width > maxLineWidth)
                maxLineWidth = measured.Width;
        }

        maxLineWidth = MathF.Ceiling(maxLineWidth);
        var lineHeight = ResolveLineHeight(element.LineHeight.Value, font);
        var totalHeight = lines.Count * lineHeight;

        return new TextShapingResult(
            lines,
            new LayoutSize(maxLineWidth, totalHeight),
            lineHeight);
    }

    private Font CreateFont(TextElement element, float fontSize)
    {
        return _fontManager.GetFont(element.Font.Value, fontSize);
    }

    private static float ResolveLineHeight(string? lineHeight, Font font)
    {
        // Use TextMeasurer to compute the default line height for a single line of text
        var singleLineSize = TextMeasurer.MeasureSize("Ag", new TextOptions(font));
        var defaultLineHeight = singleLineSize.Height;

        if (string.IsNullOrEmpty(lineHeight))
            return defaultLineHeight;

        return LineHeightResolver.Resolve(lineHeight, font.Size, defaultLineHeight);
    }

    private static float MeasureLineWidth(string text, Font font)
    {
        return TextMeasurer.MeasureSize(text, new TextOptions(font)).Width;
    }

    private static List<string> GetLines(string text, bool wrap, float maxWidth, Font font, int? maxLines, TextOverflow overflow)
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
                if (overflow == TextOverflow.Ellipsis && MeasureLineWidth(line, font) > maxWidth)
                {
                    line = TruncateWithEllipsis(line, maxWidth, font);
                }
                lines.Add(line);
            }
            else if (MeasureLineWidth(paragraph, font) <= maxWidth)
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
                    var testWidth = MeasureLineWidth(testLine, font);

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

    private static string TruncateWithEllipsis(string text, float maxWidth, Font font)
    {
        var ellipsisWidth = MeasureLineWidth(Ellipsis, font);

        if (MeasureLineWidth(text, font) <= maxWidth)
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
            if (MeasureLineWidth(substring, font) <= availableWidth)
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
