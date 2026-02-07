using System.Globalization;
using System.Text;
using FlexRender.Configuration;
using FlexRender.Layout;
using FlexRender.Layout.Units;
using FlexRender.Parsing.Ast;
using FlexRender.Providers;
using FlexRender.Rendering;
using FlexRender.TemplateEngine;

namespace FlexRender.Svg.Rendering;

/// <summary>
/// Renders a <see cref="LayoutNode"/> tree to SVG XML using <see cref="StringBuilder"/>.
/// Traverses the same layout tree as SkiaRenderer but emits SVG elements instead of drawing to a canvas.
/// </summary>
internal sealed class SvgRenderingEngine
{
    private static readonly string[] UrlPrefixes = ["http://", "https://", "data:"];

    private readonly ResourceLimits _limits;
    private readonly float _baseFontSize;
    private readonly TemplateExpander _expander;
    private readonly SvgPreprocessor _preprocessor;
    private readonly LayoutEngine _layoutEngine;
    private readonly IContentProvider<QrElement>? _qrProvider;
    private readonly IContentProvider<BarcodeElement>? _barcodeProvider;
    private readonly ISvgContentProvider<QrElement>? _qrSvgProvider;
    private readonly ISvgContentProvider<BarcodeElement>? _barcodeSvgProvider;
    private readonly ISvgContentProvider<SvgElement>? _svgElementSvgProvider;
    private readonly FlexRenderOptions? _options;

    /// <summary>
    /// Initializes a new instance of the <see cref="SvgRenderingEngine"/> class.
    /// </summary>
    /// <param name="limits">The resource limits to enforce.</param>
    /// <param name="expander">The template expander for control flow expansion.</param>
    /// <param name="preprocessor">The SVG preprocessor for expression resolution.</param>
    /// <param name="layoutEngine">The layout engine for computing element positions.</param>
    /// <param name="baseFontSize">The base font size in pixels.</param>
    /// <param name="options">Optional rendering options for path resolution.</param>
    /// <param name="qrProvider">Optional raster QR code content provider for embedding QR codes as bitmap images.</param>
    /// <param name="barcodeProvider">Optional raster barcode content provider for embedding barcodes as bitmap images.</param>
    /// <param name="qrSvgProvider">Optional SVG-native QR code provider for vector QR code embedding.</param>
    /// <param name="barcodeSvgProvider">Optional SVG-native barcode provider for vector barcode embedding.</param>
    /// <param name="svgElementSvgProvider">Optional SVG-native SVG element provider.</param>
    internal SvgRenderingEngine(
        ResourceLimits limits,
        TemplateExpander expander,
        SvgPreprocessor preprocessor,
        LayoutEngine layoutEngine,
        float baseFontSize,
        FlexRenderOptions? options = null,
        IContentProvider<QrElement>? qrProvider = null,
        IContentProvider<BarcodeElement>? barcodeProvider = null,
        ISvgContentProvider<QrElement>? qrSvgProvider = null,
        ISvgContentProvider<BarcodeElement>? barcodeSvgProvider = null,
        ISvgContentProvider<SvgElement>? svgElementSvgProvider = null)
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
        _options = options;
        _qrProvider = qrProvider;
        _barcodeProvider = barcodeProvider;
        _qrSvgProvider = qrSvgProvider;
        _barcodeSvgProvider = barcodeSvgProvider;
        _svgElementSvgProvider = svgElementSvgProvider;
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

        // Build font family map and font face declarations from the template.
        // Returned as local variables to ensure thread safety when RenderToSvg is called concurrently.
        var (fontFamilyMap, fontFaces) = BuildFontMap(processedTemplate);

        var rootNode = _layoutEngine.ComputeLayout(processedTemplate);

        var sb = new StringBuilder(4096);

        // SVG header
        sb.Append("<?xml version=\"1.0\" encoding=\"UTF-8\"?>");
        sb.Append("<svg xmlns=\"http://www.w3.org/2000/svg\"");
        sb.Append(" width=\"").Append(F(rootNode.Width)).Append('"');
        sb.Append(" height=\"").Append(F(rootNode.Height)).Append('"');
        sb.Append(" viewBox=\"0 0 ").Append(F(rootNode.Width)).Append(' ').Append(F(rootNode.Height)).Append('"');
        sb.Append('>');

        // Emit embedded font-face declarations if any fonts were resolved
        if (fontFaces.Count > 0)
        {
            sb.Append("<defs><style>");
            foreach (var fontFace in fontFaces.Values)
            {
                sb.Append("@font-face{font-family:'");
                sb.Append(fontFace.FamilyName);
                sb.Append("';src:url('data:font/");
                sb.Append(fontFace.Format);
                sb.Append(";base64,");
                sb.Append(fontFace.Base64Data);
                sb.Append("') format('");
                sb.Append(fontFace.Format);
                sb.Append("');}");
            }
            sb.Append("</style></defs>");
        }

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
            RenderNode(sb, child, fontFamilyMap, 0, 0, 0);
        }

        sb.Append("</svg>");

        return sb.ToString();
    }

    private void RenderNode(
        StringBuilder sb,
        LayoutNode node,
        Dictionary<string, string> fontFamilyMap,
        float offsetX,
        float offsetY,
        int depth)
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
        DrawElement(sb, node, fontFamilyMap, x, y);

        // Recursively render children
        foreach (var child in node.Children)
        {
            RenderNode(sb, child, fontFamilyMap, x, y, depth + 1);
        }

        if (needsGroup)
        {
            sb.Append("</g>");
        }
    }

    private void DrawElement(
        StringBuilder sb,
        LayoutNode node,
        Dictionary<string, string> fontFamilyMap,
        float x,
        float y)
    {
        var element = node.Element;
        var width = node.Width;
        var height = node.Height;
        var direction = node.Direction;
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
                DrawText(sb, text, fontFamilyMap, x, y, width, height, direction, node.TextLines, node.ComputedLineHeight);
                break;

            case SeparatorElement separator:
                DrawSeparator(sb, separator, x, y, width, height);
                break;

            case ImageElement image:
                DrawImage(sb, image, x, y, width, height);
                break;

            case SvgElement svg when _svgElementSvgProvider is not null:
                DrawSvgContentProvider(sb, _svgElementSvgProvider, svg, x, y, width, height);
                break;

            case SvgElement svg:
                DrawSvgElement(sb, svg, x, y, width, height);
                break;

            case QrElement qr when _qrSvgProvider is not null:
                DrawSvgContentProvider(sb, _qrSvgProvider, qr, x, y, width, height);
                break;

            case QrElement qr when _qrProvider is ISvgContentProvider<QrElement> svgQrProvider:
                DrawSvgContentProvider(sb, svgQrProvider, qr, x, y, width, height);
                break;

            case QrElement qr when _qrProvider is not null:
            {
                var result = _qrProvider.Generate(qr, (int)width, (int)height);
                DrawBitmapElement(sb, result, x, y, width, height);
                break;
            }

            case BarcodeElement barcode when _barcodeSvgProvider is not null:
                DrawSvgContentProvider(sb, _barcodeSvgProvider, barcode, x, y, width, height);
                break;

            case BarcodeElement barcode when _barcodeProvider is not null:
            {
                var result = _barcodeProvider.Generate(barcode, (int)width, (int)height);
                DrawBitmapElement(sb, result, x, y, width, height);
                break;
            }

            case FlexElement:
                // Container only -- children rendered via recursion
                break;
        }
    }

    private void DrawText(
        StringBuilder sb,
        TextElement text,
        Dictionary<string, string> fontFamilyMap,
        float x,
        float y,
        float width,
        float height,
        TextDirection direction,
        IReadOnlyList<string>? precomputedLines = null,
        float precomputedLineHeight = 0f)
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

        // Resolve font name to actual CSS font family name
        var fontFamily = ResolveFontFamily(text.Font, fontFamilyMap);

        sb.Append("<text");
        sb.Append(" x=\"").Append(F(textX)).Append('"');
        sb.Append(" y=\"").Append(F(y + fontSize)).Append('"'); // baseline offset
        sb.Append(" font-family=\"").Append(EscapeXml(fontFamily)).Append('"');
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

        // Use pre-computed lines from LayoutEngine (includes word-wrap) or fall back to newline split
        IReadOnlyList<string> lines;
        float effectiveLineHeight;
        if (precomputedLines != null && precomputedLines.Count > 0)
        {
            lines = precomputedLines;
            effectiveLineHeight = precomputedLineHeight > 0f ? precomputedLineHeight : fontSize * 1.2f;
        }
        else
        {
            lines = text.Content.Split('\n');
            effectiveLineHeight = fontSize * 1.2f;
        }

        for (var i = 0; i < lines.Count; i++)
        {
            if (i == 0)
            {
                sb.Append("<tspan>");
            }
            else
            {
                sb.Append("<tspan x=\"").Append(F(textX)).Append("\" dy=\"").Append(F(effectiveLineHeight)).Append("\">");
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

    private void DrawImage(
        StringBuilder sb,
        ImageElement image,
        float x,
        float y,
        float width,
        float height)
    {
        var href = ResolveImageSrc(image.Src);

        sb.Append("<image x=\"").Append(F(x)).Append("\" y=\"").Append(F(y));
        sb.Append("\" width=\"").Append(F(width));
        sb.Append("\" height=\"").Append(F(height)).Append('"');
        sb.Append(" href=\"").Append(EscapeXml(href)).Append('"');
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

    private void DrawSvgElement(
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
            sb.Append(SvgFormatting.SanitizeSvgContent(svg.Content));
            sb.Append("</svg>");
        }
        else if (!string.IsNullOrEmpty(svg.Src))
        {
            var href = ResolveImageSrc(svg.Src);

            // File-based SVG: render as image reference
            sb.Append("<image x=\"").Append(F(x)).Append("\" y=\"").Append(F(y));
            sb.Append("\" width=\"").Append(F(width));
            sb.Append("\" height=\"").Append(F(height)).Append('"');
            sb.Append(" href=\"").Append(EscapeXml(href)).Append('"');
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

    private static void DrawBitmapElement(
        StringBuilder sb,
        ContentResult result,
        float x,
        float y,
        float width,
        float height)
    {
        var base64 = Convert.ToBase64String(result.PngBytes);

        sb.Append("<image x=\"").Append(F(x)).Append("\" y=\"").Append(F(y));
        sb.Append("\" width=\"").Append(F(width));
        sb.Append("\" height=\"").Append(F(height)).Append('"');
        sb.Append(" href=\"data:image/png;base64,").Append(base64).Append('"');
        sb.Append(" preserveAspectRatio=\"xMidYMid meet\"");
        sb.Append("/>");
    }

    /// <summary>
    /// Renders an element using its SVG-native content provider.
    /// Wraps the generated SVG content in a positioned nested SVG element.
    /// </summary>
    private static void DrawSvgContentProvider<TElement>(
        StringBuilder sb,
        ISvgContentProvider<TElement> provider,
        TElement element,
        float x,
        float y,
        float width,
        float height)
    {
        var svgContent = provider.GenerateSvgContent(element, width, height);

        if (string.IsNullOrEmpty(svgContent))
        {
            return;
        }

        // Wrap in a positioned SVG element
        sb.Append("<svg x=\"").Append(F(x)).Append("\" y=\"").Append(F(y));
        sb.Append("\" width=\"").Append(F(width));
        sb.Append("\" height=\"").Append(F(height)).Append("\">");
        sb.Append(SvgFormatting.SanitizeSvgContent(svgContent));
        sb.Append("</svg>");
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
        if (string.Equals(style, "dashed", StringComparison.OrdinalIgnoreCase))
            sb.Append(" stroke-dasharray=\"6,3\"");
        else if (string.Equals(style, "dotted", StringComparison.OrdinalIgnoreCase))
            sb.Append(" stroke-dasharray=\"2,2\"");
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
    /// Delegates to <see cref="SvgFormatting.FormatFloat"/>.
    /// </summary>
    private static string F(float value) => SvgFormatting.FormatFloat(value);

    /// <summary>
    /// Escapes XML special characters in attribute values and text content.
    /// Delegates to <see cref="SvgFormatting.EscapeXml"/>.
    /// </summary>
    private static string EscapeXml(string value) => SvgFormatting.EscapeXml(value);

    /// <summary>
    /// Builds the font family map and font face declarations from the template's font definitions.
    /// For each font definition, resolves the file path, reads the font bytes once, then uses
    /// <see cref="FontNameReader.ReadFamilyName(byte[])"/> to extract the actual CSS font family name and prepares
    /// a base64-encoded <c>@font-face</c> declaration from the same bytes.
    /// </summary>
    /// <param name="template">The processed template containing font definitions.</param>
    /// <returns>
    /// A tuple containing the font family map (template name to CSS family name) and
    /// the font face declarations (CSS family name to embedded font data).
    /// </returns>
    /// <exception cref="InvalidOperationException">
    /// Thrown when a font file exceeds the maximum allowed size defined by <see cref="ResourceLimits.MaxImageSize"/>.
    /// </exception>
    private (Dictionary<string, string> FontFamilyMap, Dictionary<string, SvgFontFace> FontFaces) BuildFontMap(
        Template template)
    {
        var fontFamilyMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var fontFaces = new Dictionary<string, SvgFontFace>(StringComparer.OrdinalIgnoreCase);

        foreach (var (fontName, fontDef) in template.Fonts)
        {
            var resolvedPath = ResolvePath(fontDef.Path);

            if (!File.Exists(resolvedPath))
            {
                // Cannot resolve font file -- use fallback or the font name itself
                var fallback = fontDef.Fallback ?? _options?.DefaultFontFamily ?? "sans-serif";
                fontFamilyMap[fontName] = fallback;

                // Also register "default" as "main"
                if (string.Equals(fontName, "default", StringComparison.OrdinalIgnoreCase))
                {
                    fontFamilyMap["main"] = fallback;
                }

                continue;
            }

            // Validate file size against resource limits
            var fileInfo = new FileInfo(resolvedPath);
            if (fileInfo.Length > _limits.MaxImageSize)
            {
                throw new InvalidOperationException(
                    $"Font file '{resolvedPath}' size ({fileInfo.Length} bytes) exceeds maximum allowed size ({_limits.MaxImageSize} bytes).");
            }

            // Read font bytes once and use them for both family name extraction and base64 encoding
            var fontBytes = File.ReadAllBytes(resolvedPath);

            var familyName = FontNameReader.ReadFamilyName(fontBytes) ?? fontDef.Fallback ?? "sans-serif";

            fontFamilyMap[fontName] = familyName;

            // Also register "default" as "main"
            if (string.Equals(fontName, "default", StringComparison.OrdinalIgnoreCase))
            {
                fontFamilyMap["main"] = familyName;
            }

            // Build @font-face declaration with embedded base64 data
            if (!fontFaces.ContainsKey(familyName))
            {
                var base64 = Convert.ToBase64String(fontBytes);
                var format = GetFontFormat(resolvedPath);

                fontFaces[familyName] = new SvgFontFace(familyName, base64, format);
            }
        }

        return (fontFamilyMap, fontFaces);
    }

    /// <summary>
    /// Resolves a template font name to the actual CSS font family name.
    /// Falls back to the configured default font family or "sans-serif" if the font name is not mapped.
    /// </summary>
    /// <param name="templateFontName">The font name from the template (e.g., "main", "bold").</param>
    /// <param name="fontFamilyMap">The font family map built for the current render call.</param>
    /// <returns>The CSS font family name suitable for use in SVG <c>font-family</c> attributes.</returns>
    private string ResolveFontFamily(string templateFontName, Dictionary<string, string> fontFamilyMap)
    {
        if (fontFamilyMap.TryGetValue(templateFontName, out var familyName))
        {
            return familyName;
        }

        return _options?.DefaultFontFamily ?? "sans-serif";
    }

    /// <summary>
    /// Resolves an image source path to a value suitable for SVG <c>href</c> attributes.
    /// If the source is a local file path, reads the file and returns a base64 data URI.
    /// URLs and existing data URIs are returned unchanged.
    /// </summary>
    /// <param name="src">The image source path or URI.</param>
    /// <returns>A data URI with embedded base64 content, or the original source if it is already a URL or data URI.</returns>
    /// <exception cref="InvalidOperationException">
    /// Thrown when the image file exceeds the maximum allowed size defined by <see cref="ResourceLimits.MaxImageSize"/>.
    /// </exception>
    private string ResolveImageSrc(string src)
    {
        if (string.IsNullOrEmpty(src))
        {
            return src;
        }

        // If it's already a URL or data URI, return as-is
        foreach (var prefix in UrlPrefixes)
        {
            if (src.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            {
                return src;
            }
        }

        // It's a local file path -- resolve and embed as base64
        var resolvedPath = ResolvePath(src);

        if (!File.Exists(resolvedPath))
        {
            // File not found, return original (best effort)
            return src;
        }

        // Validate file size against resource limits
        var fileInfo = new FileInfo(resolvedPath);
        if (fileInfo.Length > _limits.MaxImageSize)
        {
            throw new InvalidOperationException(
                $"Image file '{resolvedPath}' size ({fileInfo.Length} bytes) exceeds maximum allowed size ({_limits.MaxImageSize} bytes).");
        }

        var mimeType = GetMimeType(resolvedPath);
        var bytes = File.ReadAllBytes(resolvedPath);
        var base64 = Convert.ToBase64String(bytes);

        return $"data:{mimeType};base64,{base64}";
    }

    /// <summary>
    /// Resolves a relative or absolute path using the configured base path.
    /// Validates that the resolved path does not escape the base directory via path traversal.
    /// </summary>
    /// <param name="path">The path to resolve.</param>
    /// <returns>The fully resolved absolute path.</returns>
    /// <exception cref="ArgumentException">Thrown when path traversal (e.g., "..") is detected.</exception>
    private string ResolvePath(string path)
    {
        // Reject paths containing ".." to prevent path traversal attacks
        if (path.Contains(".."))
        {
            throw new ArgumentException($"Invalid path (path traversal detected): {path}", nameof(path));
        }

        string resolvedPath;

        if (Path.IsPathRooted(path))
        {
            resolvedPath = Path.GetFullPath(path);
        }
        else if (_options?.BasePath is not null)
        {
            resolvedPath = Path.GetFullPath(Path.Combine(_options.BasePath, path));
        }
        else
        {
            resolvedPath = Path.GetFullPath(path);
        }

        // Additional safety: verify the resolved path is under the base path after canonicalization
        if (_options?.BasePath is not null)
        {
            var canonicalBase = Path.GetFullPath(_options.BasePath);
            if (!resolvedPath.StartsWith(canonicalBase, StringComparison.Ordinal))
            {
                throw new ArgumentException(
                    $"Invalid path (resolved path escapes base directory): {path}", nameof(path));
            }
        }

        return resolvedPath;
    }

    /// <summary>
    /// Determines the MIME type of an image file based on its file extension.
    /// </summary>
    /// <param name="filePath">The file path to check.</param>
    /// <returns>The MIME type string (e.g., "image/png").</returns>
    private static string GetMimeType(string filePath)
    {
        var extension = Path.GetExtension(filePath);
        if (extension.Equals(".png", StringComparison.OrdinalIgnoreCase)) return "image/png";
        if (extension.Equals(".jpg", StringComparison.OrdinalIgnoreCase) ||
            extension.Equals(".jpeg", StringComparison.OrdinalIgnoreCase)) return "image/jpeg";
        if (extension.Equals(".gif", StringComparison.OrdinalIgnoreCase)) return "image/gif";
        if (extension.Equals(".webp", StringComparison.OrdinalIgnoreCase)) return "image/webp";
        if (extension.Equals(".svg", StringComparison.OrdinalIgnoreCase)) return "image/svg+xml";
        if (extension.Equals(".bmp", StringComparison.OrdinalIgnoreCase)) return "image/bmp";
        return "application/octet-stream";
    }

    /// <summary>
    /// Determines the font format string for <c>@font-face</c> declarations based on file extension.
    /// </summary>
    /// <param name="filePath">The font file path.</param>
    /// <returns>The font format identifier (e.g., "truetype", "opentype", "woff2").</returns>
    private static string GetFontFormat(string filePath)
    {
        var extension = Path.GetExtension(filePath);
        if (extension.Equals(".ttf", StringComparison.OrdinalIgnoreCase)) return "truetype";
        if (extension.Equals(".otf", StringComparison.OrdinalIgnoreCase)) return "opentype";
        if (extension.Equals(".woff", StringComparison.OrdinalIgnoreCase)) return "woff";
        if (extension.Equals(".woff2", StringComparison.OrdinalIgnoreCase)) return "woff2";
        return "truetype";
    }

    /// <summary>
    /// Represents an embedded font face declaration for SVG output.
    /// </summary>
    /// <param name="FamilyName">The CSS font family name.</param>
    /// <param name="Base64Data">The base64-encoded font data.</param>
    /// <param name="Format">The font format identifier (e.g., "truetype", "opentype").</param>
    private sealed record SvgFontFace(string FamilyName, string Base64Data, string Format);
}
