using System.Globalization;
using System.Text;
using FlexRender.Configuration;
using FlexRender.Layout;
using FlexRender.Layout.Units;
using FlexRender.Parsing.Ast;
using FlexRender.Rendering;
using FlexRender.TemplateEngine;

namespace FlexRender.Svg.Rendering;

/// <summary>
/// Renders a <see cref="LayoutNode"/> tree to SVG XML using <see cref="StringBuilder"/>.
/// Traverses the same layout tree as SkiaRenderer but emits SVG elements instead of drawing to a canvas.
/// </summary>
internal sealed class SvgRenderingEngine
{
    private readonly ResourceLimits _limits;
    private readonly float _baseFontSize;
    private readonly TemplateExpander _expander;
    private readonly SvgPreprocessor _preprocessor;
    private readonly LayoutEngine _layoutEngine;

    /// <summary>
    /// Initializes a new instance of the <see cref="SvgRenderingEngine"/> class.
    /// </summary>
    /// <param name="limits">The resource limits to enforce.</param>
    /// <param name="expander">The template expander for control flow expansion.</param>
    /// <param name="preprocessor">The SVG preprocessor for expression resolution.</param>
    /// <param name="layoutEngine">The layout engine for computing element positions.</param>
    /// <param name="baseFontSize">The base font size in pixels.</param>
    internal SvgRenderingEngine(
        ResourceLimits limits,
        TemplateExpander expander,
        SvgPreprocessor preprocessor,
        LayoutEngine layoutEngine,
        float baseFontSize)
    {
        ArgumentNullException.ThrowIfNull(limits);
        ArgumentNullException.ThrowIfNull(expander);
        ArgumentNullException.ThrowIfNull(preprocessor);
        ArgumentNullException.ThrowIfNull(layoutEngine);
        _limits = limits;
        _expander = expander;
        _preprocessor = preprocessor;
        _layoutEngine = layoutEngine;
        _baseFontSize = baseFontSize;
    }

    /// <summary>
    /// Renders a template with data to SVG markup.
    /// </summary>
    /// <param name="template">The template to render.</param>
    /// <param name="data">The data for variable substitution.</param>
    /// <returns>The SVG markup as a string.</returns>
    internal string RenderToSvg(Template template, ObjectValue data)
    {
        ArgumentNullException.ThrowIfNull(template);
        ArgumentNullException.ThrowIfNull(data);

        var expandedTemplate = _expander.Expand(template, data);
        var processedTemplate = _preprocessor.Process(expandedTemplate, data);
        var rootNode = _layoutEngine.ComputeLayout(processedTemplate);

        var sb = new StringBuilder(4096);

        // SVG header
        sb.Append("<?xml version=\"1.0\" encoding=\"UTF-8\"?>");
        sb.Append("<svg xmlns=\"http://www.w3.org/2000/svg\"");
        sb.Append(" width=\"").Append(F(rootNode.Width)).Append('"');
        sb.Append(" height=\"").Append(F(rootNode.Height)).Append('"');
        sb.Append(" viewBox=\"0 0 ").Append(F(rootNode.Width)).Append(' ').Append(F(rootNode.Height)).Append('"');
        sb.Append('>');

        // Canvas background
        var bgColor = processedTemplate.Canvas.Background;
        if (!string.IsNullOrEmpty(bgColor))
        {
            sb.Append("<rect width=\"100%\" height=\"100%\" fill=\"");
            sb.Append(EscapeXml(bgColor));
            sb.Append("\"/>");
        }

        // Render child nodes
        foreach (var child in rootNode.Children)
        {
            RenderNode(sb, child, 0, 0, 0);
        }

        sb.Append("</svg>");

        return sb.ToString();
    }

    private void RenderNode(StringBuilder sb, LayoutNode node, float offsetX, float offsetY, int depth)
    {
        if (depth > _limits.MaxRenderDepth)
        {
            throw new InvalidOperationException(
                $"Maximum render depth ({_limits.MaxRenderDepth}) exceeded. Template may be too deeply nested.");
        }

        if (node.Element.Display == Display.None)
        {
            return;
        }

        var x = node.X + offsetX;
        var y = node.Y + offsetY;

        // Check if rotation is needed
        var rotation = RotationHelper.ParseRotation(node.Element.Rotate);
        var hasRotation = RotationHelper.HasRotation(rotation);

        // Check if overflow:hidden clipping is needed
        var needsClip = node.Element is FlexElement { Overflow: Overflow.Hidden };
        var clipId = needsClip ? $"clip-{depth}-{x:F0}-{y:F0}" : null;

        // Wrap in group with transform if rotation or clipping needed
        var needsGroup = hasRotation || needsClip || node.Element.Opacity < 1.0f;
        if (needsGroup)
        {
            sb.Append("<g");

            if (hasRotation)
            {
                var cx = x + node.Width / 2;
                var cy = y + node.Height / 2;
                sb.Append(" transform=\"rotate(").Append(F(rotation));
                sb.Append(',').Append(F(cx)).Append(',').Append(F(cy)).Append(")\"");
            }

            if (node.Element.Opacity < 1.0f)
            {
                sb.Append(" opacity=\"").Append(F(node.Element.Opacity)).Append('"');
            }

            if (needsClip && clipId != null)
            {
                sb.Append(" clip-path=\"url(#").Append(clipId).Append(")\"");
            }

            sb.Append('>');

            // Emit clipPath definition
            if (needsClip && clipId != null)
            {
                sb.Append("<defs><clipPath id=\"").Append(clipId).Append("\">");
                sb.Append("<rect x=\"").Append(F(x)).Append("\" y=\"").Append(F(y));
                sb.Append("\" width=\"").Append(F(node.Width));
                sb.Append("\" height=\"").Append(F(node.Height)).Append("\"/>");
                sb.Append("</clipPath></defs>");
            }
        }

        // Draw the element itself
        DrawElement(sb, node.Element, x, y, node.Width, node.Height, node.Direction);

        // Recursively render children
        foreach (var child in node.Children)
        {
            RenderNode(sb, child, x, y, depth + 1);
        }

        if (needsGroup)
        {
            sb.Append("</g>");
        }
    }

    private void DrawElement(
        StringBuilder sb,
        TemplateElement element,
        float x,
        float y,
        float width,
        float height,
        TextDirection direction)
    {
        var borderRadius = ResolveBorderRadius(element, width, height);

        // Draw background
        if (!string.IsNullOrEmpty(element.Background))
        {
            sb.Append("<rect x=\"").Append(F(x)).Append("\" y=\"").Append(F(y));
            sb.Append("\" width=\"").Append(F(width));
            sb.Append("\" height=\"").Append(F(height)).Append('"');
            sb.Append(" fill=\"").Append(EscapeXml(element.Background)).Append('"');

            if (borderRadius > 0f)
            {
                sb.Append(" rx=\"").Append(F(borderRadius)).Append('"');
                sb.Append(" ry=\"").Append(F(borderRadius)).Append('"');
            }

            sb.Append("/>");
        }

        // Draw borders
        DrawBorders(sb, element, x, y, width, height, borderRadius);

        // Draw element-specific content
        switch (element)
        {
            case TextElement text:
                DrawText(sb, text, x, y, width, height, direction);
                break;

            case SeparatorElement separator:
                DrawSeparator(sb, separator, x, y, width, height);
                break;

            case ImageElement image:
                DrawImage(sb, image, x, y, width, height);
                break;

            case SvgElement svg:
                DrawSvgElement(sb, svg, x, y, width, height);
                break;

            case FlexElement:
                // Container only -- children rendered via recursion
                break;
        }
    }

    private static void DrawText(
        StringBuilder sb,
        TextElement text,
        float x,
        float y,
        float width,
        float height,
        TextDirection direction)
    {
        if (string.IsNullOrEmpty(text.Content))
        {
            return;
        }

        // Determine text-anchor from alignment
        var anchor = text.Align switch
        {
            TextAlign.Center => "middle",
            TextAlign.Right => "end",
            TextAlign.End => direction == TextDirection.Rtl ? "start" : "end",
            TextAlign.Start => direction == TextDirection.Rtl ? "end" : "start",
            _ => "start"
        };

        // Compute text x position based on alignment
        var textX = text.Align switch
        {
            TextAlign.Center => x + width / 2,
            TextAlign.Right => x + width,
            TextAlign.End => direction == TextDirection.Rtl ? x : x + width,
            TextAlign.Start => direction == TextDirection.Rtl ? x + width : x,
            _ => x
        };

        // Parse font size
        var fontSize = ParseFontSize(text.Size);

        sb.Append("<text");
        sb.Append(" x=\"").Append(F(textX)).Append('"');
        sb.Append(" y=\"").Append(F(y + fontSize)).Append('"'); // baseline offset
        sb.Append(" font-family=\"").Append(EscapeXml(text.Font)).Append('"');
        sb.Append(" font-size=\"").Append(F(fontSize)).Append('"');
        sb.Append(" fill=\"").Append(EscapeXml(text.Color)).Append('"');

        if (anchor != "start")
        {
            sb.Append(" text-anchor=\"").Append(anchor).Append('"');
        }

        if (direction == TextDirection.Rtl)
        {
            sb.Append(" direction=\"rtl\"");
        }

        sb.Append('>');

        // Split into lines and emit tspan per line
        var lines = text.Content.Split('\n');
        for (var i = 0; i < lines.Length; i++)
        {
            if (i == 0)
            {
                sb.Append("<tspan>");
            }
            else
            {
                sb.Append("<tspan x=\"").Append(F(textX)).Append("\" dy=\"").Append(F(fontSize * 1.2f)).Append("\">");
            }

            sb.Append(EscapeXml(lines[i]));
            sb.Append("</tspan>");
        }

        sb.Append("</text>");
    }

    private static void DrawSeparator(
        StringBuilder sb,
        SeparatorElement separator,
        float x,
        float y,
        float width,
        float height)
    {
        float x1, y1, x2, y2;

        if (separator.Orientation == SeparatorOrientation.Horizontal)
        {
            var cy = y + height / 2;
            x1 = x;
            y1 = cy;
            x2 = x + width;
            y2 = cy;
        }
        else
        {
            var cx = x + width / 2;
            x1 = cx;
            y1 = y;
            x2 = cx;
            y2 = y + height;
        }

        sb.Append("<line x1=\"").Append(F(x1)).Append("\" y1=\"").Append(F(y1));
        sb.Append("\" x2=\"").Append(F(x2)).Append("\" y2=\"").Append(F(y2)).Append('"');
        sb.Append(" stroke=\"").Append(EscapeXml(separator.Color)).Append('"');
        sb.Append(" stroke-width=\"").Append(F(separator.Thickness)).Append('"');

        switch (separator.Style)
        {
            case SeparatorStyle.Dashed:
                sb.Append(" stroke-dasharray=\"6,3\"");
                break;
            case SeparatorStyle.Dotted:
                sb.Append(" stroke-dasharray=\"2,2\"");
                break;
        }

        sb.Append("/>");
    }

    private static void DrawImage(
        StringBuilder sb,
        ImageElement image,
        float x,
        float y,
        float width,
        float height)
    {
        // For MVP, render image as a placeholder rect with the src as a data URI if it's base64,
        // or as an href reference
        sb.Append("<image x=\"").Append(F(x)).Append("\" y=\"").Append(F(y));
        sb.Append("\" width=\"").Append(F(width));
        sb.Append("\" height=\"").Append(F(height)).Append('"');
        sb.Append(" href=\"").Append(EscapeXml(image.Src)).Append('"');
        sb.Append(" preserveAspectRatio=\"");

        sb.Append(image.Fit switch
        {
            ImageFit.Fill => "none",
            ImageFit.Contain => "xMidYMid meet",
            ImageFit.Cover => "xMidYMid slice",
            _ => "xMidYMid meet"
        });

        sb.Append("\"/>");
    }

    private static void DrawSvgElement(
        StringBuilder sb,
        SvgElement svg,
        float x,
        float y,
        float width,
        float height)
    {
        if (!string.IsNullOrEmpty(svg.Content))
        {
            // Inline SVG: embed as nested <svg> element
            sb.Append("<svg x=\"").Append(F(x)).Append("\" y=\"").Append(F(y));
            sb.Append("\" width=\"").Append(F(width));
            sb.Append("\" height=\"").Append(F(height)).Append("\">");
            sb.Append(svg.Content);
            sb.Append("</svg>");
        }
        else if (!string.IsNullOrEmpty(svg.Src))
        {
            // File-based SVG: render as image reference
            sb.Append("<image x=\"").Append(F(x)).Append("\" y=\"").Append(F(y));
            sb.Append("\" width=\"").Append(F(width));
            sb.Append("\" height=\"").Append(F(height)).Append('"');
            sb.Append(" href=\"").Append(EscapeXml(svg.Src)).Append('"');
            sb.Append(" preserveAspectRatio=\"");

            sb.Append(svg.Fit switch
            {
                ImageFit.Fill => "none",
                ImageFit.Contain => "xMidYMid meet",
                ImageFit.Cover => "xMidYMid slice",
                _ => "xMidYMid meet"
            });

            sb.Append("\"/>");
        }
    }

    private static void DrawBorders(
        StringBuilder sb,
        TemplateElement element,
        float x,
        float y,
        float width,
        float height,
        float borderRadius)
    {
        // Parse the shorthand border property
        if (string.IsNullOrEmpty(element.Border) &&
            string.IsNullOrEmpty(element.BorderTop) &&
            string.IsNullOrEmpty(element.BorderRight) &&
            string.IsNullOrEmpty(element.BorderBottom) &&
            string.IsNullOrEmpty(element.BorderLeft) &&
            string.IsNullOrEmpty(element.BorderWidth))
        {
            return;
        }

        // Parse border shorthand for all sides
        var borderWidth = 1f;
        var borderColor = "#000000";
        var borderStyle = "solid";

        if (!string.IsNullOrEmpty(element.Border))
        {
            ParseBorderShorthand(element.Border, out borderWidth, out borderStyle, out borderColor);
        }

        // Apply overrides
        if (!string.IsNullOrEmpty(element.BorderWidth) &&
            float.TryParse(element.BorderWidth, NumberStyles.Float, CultureInfo.InvariantCulture, out var bw))
        {
            borderWidth = bw;
        }

        if (!string.IsNullOrEmpty(element.BorderColor))
        {
            borderColor = element.BorderColor;
        }

        if (!string.IsNullOrEmpty(element.BorderStyle))
        {
            borderStyle = element.BorderStyle;
        }

        // Check for per-side borders
        var hasPerSideBorders = !string.IsNullOrEmpty(element.BorderTop) ||
                                !string.IsNullOrEmpty(element.BorderRight) ||
                                !string.IsNullOrEmpty(element.BorderBottom) ||
                                !string.IsNullOrEmpty(element.BorderLeft);

        if (hasPerSideBorders)
        {
            // Render individual border lines
            DrawBorderSide(sb, element.BorderTop, x, y, x + width, y, borderWidth, borderColor, borderStyle);
            DrawBorderSide(sb, element.BorderRight, x + width, y, x + width, y + height, borderWidth, borderColor, borderStyle);
            DrawBorderSide(sb, element.BorderBottom, x, y + height, x + width, y + height, borderWidth, borderColor, borderStyle);
            DrawBorderSide(sb, element.BorderLeft, x, y, x, y + height, borderWidth, borderColor, borderStyle);
        }
        else if (borderWidth > 0)
        {
            // Render as a rect stroke
            sb.Append("<rect x=\"").Append(F(x)).Append("\" y=\"").Append(F(y));
            sb.Append("\" width=\"").Append(F(width));
            sb.Append("\" height=\"").Append(F(height)).Append('"');
            sb.Append(" fill=\"none\"");
            sb.Append(" stroke=\"").Append(EscapeXml(borderColor)).Append('"');
            sb.Append(" stroke-width=\"").Append(F(borderWidth)).Append('"');

            AppendStrokeDasharray(sb, borderStyle);

            if (borderRadius > 0f)
            {
                sb.Append(" rx=\"").Append(F(borderRadius)).Append('"');
                sb.Append(" ry=\"").Append(F(borderRadius)).Append('"');
            }

            sb.Append("/>");
        }
    }

    private static void DrawBorderSide(
        StringBuilder sb,
        string? sideSpec,
        float x1,
        float y1,
        float x2,
        float y2,
        float defaultWidth,
        string defaultColor,
        string defaultStyle)
    {
        var bw = defaultWidth;
        var bc = defaultColor;
        var bs = defaultStyle;

        if (!string.IsNullOrEmpty(sideSpec))
        {
            ParseBorderShorthand(sideSpec, out bw, out bs, out bc);
        }
        else
        {
            return; // No border on this side
        }

        if (bw <= 0)
        {
            return;
        }

        sb.Append("<line x1=\"").Append(F(x1)).Append("\" y1=\"").Append(F(y1));
        sb.Append("\" x2=\"").Append(F(x2)).Append("\" y2=\"").Append(F(y2)).Append('"');
        sb.Append(" stroke=\"").Append(EscapeXml(bc)).Append('"');
        sb.Append(" stroke-width=\"").Append(F(bw)).Append('"');

        AppendStrokeDasharray(sb, bs);

        sb.Append("/>");
    }

    private static void ParseBorderShorthand(string shorthand, out float width, out string style, out string color)
    {
        width = 1f;
        style = "solid";
        color = "#000000";

        var parts = shorthand.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length >= 1 && float.TryParse(parts[0], NumberStyles.Float, CultureInfo.InvariantCulture, out var w))
        {
            width = w;
        }

        if (parts.Length >= 2)
        {
            style = parts[1];
        }

        if (parts.Length >= 3)
        {
            color = parts[2];
        }
    }

    private static void AppendStrokeDasharray(StringBuilder sb, string style)
    {
        switch (style.ToLowerInvariant())
        {
            case "dashed":
                sb.Append(" stroke-dasharray=\"6,3\"");
                break;
            case "dotted":
                sb.Append(" stroke-dasharray=\"2,2\"");
                break;
        }
    }

    private float ResolveBorderRadius(TemplateElement element, float width, float height)
    {
        if (string.IsNullOrEmpty(element.BorderRadius))
        {
            return 0f;
        }

        var unit = UnitParser.Parse(element.BorderRadius);
        return unit.Resolve(_baseFontSize, Math.Min(width, height)) ?? 0f;
    }

    private static float ParseFontSize(string size)
    {
        if (string.IsNullOrEmpty(size))
        {
            return 12f;
        }

        // Handle "em" units
        if (size.EndsWith("em", StringComparison.OrdinalIgnoreCase))
        {
            var numStr = size[..^2];
            if (float.TryParse(numStr, NumberStyles.Float, CultureInfo.InvariantCulture, out var emValue))
            {
                return emValue * 12f; // default base font size
            }
        }

        // Handle plain numeric
        if (float.TryParse(size, NumberStyles.Float, CultureInfo.InvariantCulture, out var px))
        {
            return px;
        }

        return 12f;
    }

    /// <summary>
    /// Formats a float using invariant culture with no trailing zeros.
    /// </summary>
    private static string F(float value)
    {
        return value.ToString("G", CultureInfo.InvariantCulture);
    }

    /// <summary>
    /// Escapes XML special characters in attribute values and text content.
    /// </summary>
    private static string EscapeXml(string value)
    {
        if (value.AsSpan().IndexOfAny("&<>\"'") < 0)
        {
            return value;
        }

        var sb = new StringBuilder(value.Length + 8);
        foreach (var c in value)
        {
            sb.Append(c switch
            {
                '&' => "&amp;",
                '<' => "&lt;",
                '>' => "&gt;",
                '"' => "&quot;",
                '\'' => "&apos;",
                _ => c.ToString()
            });
        }

        return sb.ToString();
    }
}
