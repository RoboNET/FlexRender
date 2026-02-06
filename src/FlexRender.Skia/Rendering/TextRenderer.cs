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
    private readonly RenderOptions _defaultRenderOptions;

    /// <summary>
    /// Creates a new TextRenderer with the specified font manager.
    /// </summary>
    /// <param name="fontManager">The font manager to use for font loading.</param>
    /// <param name="defaultRenderOptions">
    /// Default render options used for text measurement when no explicit options are provided.
    /// When <c>null</c>, <see cref="RenderOptions.Default"/> is used.
    /// </param>
    /// <exception cref="ArgumentNullException">Thrown when fontManager is null.</exception>
    public TextRenderer(FontManager fontManager, RenderOptions? defaultRenderOptions = null)
    {
        ArgumentNullException.ThrowIfNull(fontManager);
        _fontManager = fontManager;
        _defaultRenderOptions = defaultRenderOptions ?? RenderOptions.Default;
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

        using var font = CreateFont(element, baseFontSize, _defaultRenderOptions);
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
    /// <param name="renderOptions">Per-call rendering options controlling antialiasing, font hinting, and text rendering mode.</param>
    public void DrawText(SKCanvas canvas, TextElement element, SKRect bounds, float baseFontSize, RenderOptions? renderOptions = null)
    {
        if (string.IsNullOrEmpty(element.Content))
            return;

        var effectiveOptions = renderOptions ?? RenderOptions.Default;
        using var font = CreateFont(element, baseFontSize, effectiveOptions);
        using var paint = CreatePaint(element, effectiveOptions.Antialiasing);
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
    /// Font hinting, subpixel positioning, and edge rendering are controlled by
    /// the per-call <see cref="RenderOptions"/>.
    /// </summary>
    /// <param name="element">The text element.</param>
    /// <param name="baseFontSize">Base font size for em calculations.</param>
    /// <param name="renderOptions">Per-call rendering options.</param>
    /// <returns>A configured <see cref="SKFont"/> instance. Caller must dispose.</returns>
    private SKFont CreateFont(TextElement element, float baseFontSize, RenderOptions renderOptions)
    {
        var typeface = _fontManager.GetTypeface(element.Font);
        var fontSize = FontSizeResolver.Resolve(element.Size, baseFontSize);

        var font = new SKFont(typeface, fontSize)
        {
            Subpixel = renderOptions.SubpixelText,
            Hinting = MapFontHinting(renderOptions.FontHinting),
            Edging = MapTextRendering(renderOptions.TextRendering)
        };

        return font;
    }

    /// <summary>
    /// Maps the <see cref="FontHinting"/> enum to the SkiaSharp <see cref="SKFontHinting"/> enum.
    /// </summary>
    private static SKFontHinting MapFontHinting(FontHinting hinting) => hinting switch
    {
        FontHinting.None => SKFontHinting.None,
        FontHinting.Slight => SKFontHinting.Slight,
        FontHinting.Normal => SKFontHinting.Normal,
        FontHinting.Full => SKFontHinting.Full,
        _ => SKFontHinting.Normal
    };

    /// <summary>
    /// Maps the <see cref="TextRendering"/> enum to the SkiaSharp <see cref="SKFontEdging"/> enum.
    /// </summary>
    private static SKFontEdging MapTextRendering(TextRendering rendering) => rendering switch
    {
        TextRendering.Aliased => SKFontEdging.Alias,
        TextRendering.Grayscale => SKFontEdging.Antialias,
        TextRendering.SubpixelLcd => SKFontEdging.SubpixelAntialias,
        _ => SKFontEdging.SubpixelAntialias
    };

    /// <summary>
    /// Creates an <see cref="SKPaint"/> configured for the given text element.
    /// The paint manages color, antialias, and other non-text visual properties.
    /// </summary>
    /// <param name="element">The text element.</param>
    /// <param name="antialiasing">Whether to enable antialiasing.</param>
    /// <returns>A configured <see cref="SKPaint"/> instance. Caller must dispose.</returns>
    private static SKPaint CreatePaint(TextElement element, bool antialiasing)
    {
        var color = ColorParser.Parse(element.Color);

        return new SKPaint
        {
            Color = color,
            IsAntialias = antialiasing
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
