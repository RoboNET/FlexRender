using FlexRender.Layout;
using FlexRender.Parsing.Ast;
using SixLabors.Fonts;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Drawing.Processing;
using SixLabors.ImageSharp.Processing;

namespace FlexRender.ImageSharp.Rendering;

/// <summary>
/// Measures and draws text using SixLabors.Fonts and ImageSharp.Drawing.
/// </summary>
internal sealed class ImageSharpTextRenderer
{
    private const string Ellipsis = "...";
    private readonly ImageSharpFontManager _fontManager;

    /// <summary>
    /// Creates a new text renderer with the specified font manager.
    /// </summary>
    /// <param name="fontManager">The font manager for font loading.</param>
    public ImageSharpTextRenderer(ImageSharpFontManager fontManager)
    {
        ArgumentNullException.ThrowIfNull(fontManager);
        _fontManager = fontManager;
    }

    /// <summary>
    /// Measures the size of a text element within the given constraints.
    /// </summary>
    /// <param name="element">The text element to measure.</param>
    /// <param name="maxWidth">Maximum available width for wrapping.</param>
    /// <param name="baseFontSize">Base font size for em/% calculations.</param>
    /// <returns>The measured size as a LayoutSize.</returns>
    public LayoutSize MeasureText(TextElement element, float maxWidth, float baseFontSize)
    {
        if (string.IsNullOrEmpty(element.Content))
            return new LayoutSize(0, 0);

        var font = CreateFont(element, baseFontSize);
        var effectiveMaxWidth = element.Overflow == TextOverflow.Visible && !element.Wrap
            ? float.MaxValue
            : maxWidth;

        var lines = GetLines(element.Content, element.Wrap, effectiveMaxWidth, font, element.MaxLines, element.Overflow);

        if (lines.Count == 0)
            return new LayoutSize(0, 0);

        var maxLineWidth = 0f;
        foreach (var line in lines)
        {
            var measured = TextMeasurer.MeasureSize(line, new TextOptions(font));
            if (measured.Width > maxLineWidth)
                maxLineWidth = measured.Width;
        }

        maxLineWidth = MathF.Ceiling(maxLineWidth);
        var lineHeight = ResolveLineHeight(element.LineHeight, font);
        var totalHeight = lines.Count * lineHeight;

        return new LayoutSize(maxLineWidth, totalHeight);
    }

    /// <summary>
    /// Draws a text element onto the image processing context.
    /// </summary>
    /// <param name="ctx">The image processing context to draw on.</param>
    /// <param name="element">The text element to draw.</param>
    /// <param name="x">X position of the text bounding box.</param>
    /// <param name="y">Y position of the text bounding box.</param>
    /// <param name="width">Width of the text bounding box.</param>
    /// <param name="height">Height of the text bounding box.</param>
    /// <param name="baseFontSize">Base font size for em/% calculations.</param>
    public void DrawText(
        IImageProcessingContext ctx,
        TextElement element,
        float x,
        float y,
        float width,
        float height,
        float baseFontSize)
    {
        if (string.IsNullOrEmpty(element.Content))
            return;

        var font = CreateFont(element, baseFontSize);
        var color = ImageSharpColorParser.Parse(element.Color);
        var effectiveMaxWidth = element.Overflow == TextOverflow.Visible && !element.Wrap
            ? float.MaxValue
            : width;

        var lines = GetLines(element.Content, element.Wrap, effectiveMaxWidth, font, element.MaxLines, element.Overflow);

        if (lines.Count == 0)
            return;

        var lineHeight = ResolveLineHeight(element.LineHeight, font);
        var currentY = y;

        foreach (var line in lines)
        {
            var measured = TextMeasurer.MeasureSize(line, new TextOptions(font));
            var lineX = CalculateX(element.Align, x, width, measured.Width);

            ctx.DrawText(line, font, color, new PointF(lineX, currentY));
            currentY += lineHeight;
        }
    }

    private Font CreateFont(TextElement element, float baseFontSize)
    {
        var fontSize = FontSizeResolver.Resolve(element.Size, baseFontSize);
        return _fontManager.GetFont(element.Font, fontSize);
    }

    private static float ResolveLineHeight(string? lineHeight, Font font)
    {
        // Use TextMeasurer to compute the default line height for a single line of text.
        // This accounts for ascender, descender, and line gap from the font metrics.
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

    private static float CalculateX(TextAlign align, float boundsX, float boundsWidth, float lineWidth)
    {
        return align switch
        {
            TextAlign.Center => boundsX + (boundsWidth - lineWidth) / 2,
            TextAlign.Right => boundsX + boundsWidth - lineWidth,
            _ => boundsX
        };
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
