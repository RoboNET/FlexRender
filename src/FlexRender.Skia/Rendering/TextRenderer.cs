using FlexRender.Layout;
using FlexRender.Parsing.Ast;
using SkiaSharp;

namespace FlexRender.Rendering;

/// <summary>
/// Renders text elements to SkiaSharp canvas.
/// </summary>
public sealed class TextRenderer
{
    private const string Ellipsis = "...";
    private readonly FontManager _fontManager;
    private readonly bool _deterministicRendering;

    /// <summary>
    /// Creates a new TextRenderer with the specified font manager.
    /// </summary>
    /// <param name="fontManager">The font manager to use for font loading.</param>
    /// <param name="deterministicRendering">
    /// When <c>true</c>, disables font hinting and subpixel rendering
    /// to produce identical output across platforms.
    /// </param>
    /// <exception cref="ArgumentNullException">Thrown when fontManager is null.</exception>
    public TextRenderer(FontManager fontManager, bool deterministicRendering = false)
    {
        _fontManager = fontManager ?? throw new ArgumentNullException(nameof(fontManager));
        _deterministicRendering = deterministicRendering;
    }

    /// <summary>
    /// Measures the size of a text element.
    /// </summary>
    /// <param name="element">The text element.</param>
    /// <param name="maxWidth">Maximum width for wrapping.</param>
    /// <param name="baseFontSize">Base font size for em calculations.</param>
    /// <returns>The measured size.</returns>
    public SKSize MeasureText(TextElement element, float maxWidth, float baseFontSize)
    {
        if (string.IsNullOrEmpty(element.Content))
            return new SKSize(0, 0);

        using var font = CreateFont(element, baseFontSize);
        var effectiveMaxWidth = element.Overflow == TextOverflow.Visible && !element.Wrap
            ? float.MaxValue
            : maxWidth;

        var lines = GetLines(element.Content, element.Wrap, effectiveMaxWidth, font, element.MaxLines, element.Overflow);

        if (lines.Count == 0)
            return new SKSize(0, 0);

        var maxLineWidth = lines.Max(line => font.MeasureText(line));
        var lineHeight = LineHeightResolver.Resolve(element.LineHeight, font.Size, font.Spacing);
        var totalHeight = lines.Count * lineHeight;

        // Handle rotation affecting dimensions
        var rotation = RotationHelper.ParseRotation(element.Rotate);
        if (RotationHelper.SwapsDimensions(rotation))
        {
            return new SKSize(totalHeight, maxLineWidth);
        }

        return new SKSize(maxLineWidth, totalHeight);
    }

    /// <summary>
    /// Draws a text element to the canvas.
    /// </summary>
    /// <param name="canvas">The canvas to draw on.</param>
    /// <param name="element">The text element.</param>
    /// <param name="bounds">The bounding rectangle.</param>
    /// <param name="baseFontSize">Base font size for em calculations.</param>
    public void DrawText(SKCanvas canvas, TextElement element, SKRect bounds, float baseFontSize)
    {
        if (string.IsNullOrEmpty(element.Content))
            return;

        using var font = CreateFont(element, baseFontSize);
        using var paint = CreatePaint(element);
        var rotation = RotationHelper.ParseRotation(element.Rotate);
        var effectiveMaxWidth = element.Overflow == TextOverflow.Visible && !element.Wrap
            ? float.MaxValue
            : bounds.Width;

        var lines = GetLines(element.Content, element.Wrap, effectiveMaxWidth, font, element.MaxLines, element.Overflow);

        if (lines.Count == 0)
            return;

        // Apply clipping for Clip overflow mode
        if (element.Overflow == TextOverflow.Clip)
        {
            canvas.Save();
            canvas.ClipRect(bounds);
        }

        // Apply rotation if needed
        if (RotationHelper.HasRotation(rotation))
        {
            canvas.Save();
            var centerX = bounds.MidX;
            var centerY = bounds.MidY;
            canvas.RotateDegrees(rotation, centerX, centerY);
        }

        var lineHeight = LineHeightResolver.Resolve(element.LineHeight, font.Size, font.Spacing);
        var y = bounds.Top + lineHeight; // Start below top (baseline positioning)

        foreach (var line in lines)
        {
            var lineWidth = font.MeasureText(line);
            var x = CalculateX(element.Align, bounds, lineWidth);

            canvas.DrawText(line, x, y, SKTextAlign.Left, font, paint);
            y += lineHeight;
        }

        if (RotationHelper.HasRotation(rotation))
        {
            canvas.Restore();
        }

        if (element.Overflow == TextOverflow.Clip)
        {
            canvas.Restore();
        }
    }

    /// <summary>
    /// Creates an <see cref="SKFont"/> configured for the given text element.
    /// When deterministic rendering is enabled, font hinting and subpixel positioning
    /// are disabled to ensure identical output across macOS, Linux, and Windows.
    /// Grayscale anti-aliasing is used instead of subpixel anti-aliasing because
    /// LCD pixel layout varies across displays and platforms.
    /// </summary>
    /// <param name="element">The text element.</param>
    /// <param name="baseFontSize">Base font size for em calculations.</param>
    /// <returns>A configured <see cref="SKFont"/> instance. Caller must dispose.</returns>
    private SKFont CreateFont(TextElement element, float baseFontSize)
    {
        var typeface = _fontManager.GetTypeface(element.Font);
        var fontSize = FontSizeResolver.Resolve(element.Size, baseFontSize);

        var font = new SKFont(typeface, fontSize)
        {
            Subpixel = !_deterministicRendering
        };

        if (_deterministicRendering)
        {
            font.Hinting = SKFontHinting.None;
            font.Edging = SKFontEdging.Antialias;
        }

        return font;
    }

    /// <summary>
    /// Creates an <see cref="SKPaint"/> configured for the given text element.
    /// The paint manages color, antialias, and other non-text visual properties.
    /// </summary>
    /// <param name="element">The text element.</param>
    /// <returns>A configured <see cref="SKPaint"/> instance. Caller must dispose.</returns>
    private static SKPaint CreatePaint(TextElement element)
    {
        var color = ColorParser.Parse(element.Color);

        return new SKPaint
        {
            Color = color,
            IsAntialias = true
        };
    }

    private static float CalculateX(TextAlign align, SKRect bounds, float lineWidth)
    {
        return align switch
        {
            TextAlign.Center => bounds.Left + (bounds.Width - lineWidth) / 2,
            TextAlign.Right => bounds.Right - lineWidth,
            _ => bounds.Left
        };
    }

    private static List<string> GetLines(string text, bool wrap, float maxWidth, SKFont font, int? maxLines, TextOverflow overflow)
    {
        // Estimate capacity: count newlines + 1 for paragraphs
        var estimatedLines = 1;
        foreach (var c in text)
        {
            if (c == '\n') estimatedLines++;
        }
        var lines = new List<string>(estimatedLines);

        // Split by explicit newlines first
        var paragraphs = text.Split('\n');

        foreach (var paragraph in paragraphs)
        {
            if (!wrap)
            {
                // No wrapping - add as single line, possibly with ellipsis
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
                // Word wrap
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

                        // Check if we've hit maxLines
                        if (maxLines.HasValue && lines.Count >= maxLines.Value)
                            break;
                    }
                }

                if (!string.IsNullOrEmpty(currentLine))
                {
                    // If this is the last allowed line and we have more content, add ellipsis
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

        // Apply maxLines limit
        if (maxLines.HasValue && lines.Count > maxLines.Value)
        {
            lines.RemoveRange(maxLines.Value, lines.Count - maxLines.Value);

            // Add ellipsis to last line if needed
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

        // Binary search for the right truncation point
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
