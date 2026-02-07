using FlexRender.Configuration;
using FlexRender.Parsing.Ast;
using FlexRender.TemplateEngine;

namespace FlexRender.Rendering;

/// <summary>
/// Handles template preprocessing: expression resolution, font registration,
/// and element processing before layout and rendering.
/// </summary>
internal sealed class TemplatePreprocessor
{
    private readonly FontManager _fontManager;
    private readonly TemplateProcessor _templateProcessor;
    private readonly FlexRenderOptions? _options;

    /// <summary>
    /// Initializes a new instance of the <see cref="TemplatePreprocessor"/> class.
    /// </summary>
    /// <param name="fontManager">The font manager for font registration.</param>
    /// <param name="templateProcessor">The template processor for expression evaluation.</param>
    /// <param name="options">Optional configuration options for path resolution.</param>
    internal TemplatePreprocessor(
        FontManager fontManager,
        TemplateProcessor templateProcessor,
        FlexRenderOptions? options)
    {
        ArgumentNullException.ThrowIfNull(fontManager);
        ArgumentNullException.ThrowIfNull(templateProcessor);
        _fontManager = fontManager;
        _templateProcessor = templateProcessor;
        _options = options;
    }

    /// <summary>
    /// Processes a template by resolving expressions and registering fonts.
    /// </summary>
    /// <param name="template">The expanded template to process.</param>
    /// <param name="data">The data context for expression evaluation.</param>
    /// <returns>A new template with all expressions resolved.</returns>
    internal Template Process(Template template, ObjectValue data)
    {
        // Register fonts from template
        RegisterTemplateFonts(template);

        var processedCanvas = new CanvasSettings
        {
            Fixed = template.Canvas.Fixed,
            Width = template.Canvas.Width,
            Height = template.Canvas.Height,
            Background = ProcessExpression(template.Canvas.Background, data),
            Rotate = template.Canvas.Rotate,
            TextDirection = template.Canvas.TextDirection
        };

        var processed = new Template
        {
            Name = template.Name,
            Version = template.Version,
            Canvas = processedCanvas,
            Fonts = template.Fonts
        };

        foreach (var element in template.Elements)
        {
            var processedElement = ProcessElement(element, data);
            if (processedElement != null)
                processed.AddElement(processedElement);
        }

        return processed;
    }

    /// <summary>
    /// Registers all fonts defined in the template with the font manager.
    /// If a font named "default" is defined, it is also registered as "main"
    /// to serve as the default font for elements without an explicit font specification.
    /// </summary>
    /// <param name="template">The template containing font definitions.</param>
    private void RegisterTemplateFonts(Template template)
    {
        foreach (var (fontName, fontDef) in template.Fonts)
        {
            var resolvedPath = ResolveFontPath(fontDef.Path);
            _fontManager.RegisterFont(fontName, resolvedPath, fontDef.Fallback);

            // Register "default" font also as "main" for elements without explicit font
            if (string.Equals(fontName, "default", StringComparison.OrdinalIgnoreCase))
            {
                _fontManager.RegisterFont("main", resolvedPath, fontDef.Fallback);
            }
        }
    }

    /// <summary>
    /// Resolves a font path, applying the base path if the path is relative.
    /// </summary>
    /// <param name="path">The font path from the template.</param>
    /// <returns>The resolved absolute path.</returns>
    private string ResolveFontPath(string path)
    {
        if (Path.IsPathRooted(path))
        {
            return Path.GetFullPath(path);
        }

        if (_options?.BasePath is not null)
        {
            return Path.GetFullPath(Path.Combine(_options.BasePath, path));
        }

        return Path.GetFullPath(path);
    }

    /// <summary>
    /// Copies all base flex-item and positioning properties from source to target element.
    /// This avoids duplication across all element processing branches.
    /// Properties that require per-element transformation (Background via ProcessExpression,
    /// Rotate, Padding, Margin) are intentionally excluded and must be set in each caller.
    /// </summary>
    /// <param name="source">The source element to copy properties from.</param>
    /// <param name="target">The target element to copy properties to.</param>
    private static void CopyBaseProperties(TemplateElement source, TemplateElement target)
    {
        // ⚠ SYNC WARNING: This method is duplicated in 3 locations that MUST stay in sync:
        //   1. TemplateExpander.CopyBaseProperties       (FlexRender.Core)
        //   2. TemplatePreprocessor.CopyBaseProperties    (FlexRender.Skia)
        //   3. SvgPreprocessor.CopyBaseProperties         (FlexRender.Svg)
        // When adding a new property to TemplateElement, you MUST add it to ALL THREE methods.
        // Failure to do so causes properties to silently disappear during preprocessing.

        // Flex-item properties
        target.Grow = source.Grow;
        target.Shrink = source.Shrink;
        target.Basis = source.Basis;
        target.AlignSelf = source.AlignSelf;
        target.Order = source.Order;
        target.Width = source.Width;
        target.Height = source.Height;
        target.MinWidth = source.MinWidth;
        target.MaxWidth = source.MaxWidth;
        target.MinHeight = source.MinHeight;
        target.MaxHeight = source.MaxHeight;

        // Position properties
        target.Position = source.Position;
        target.Top = source.Top;
        target.Right = source.Right;
        target.Bottom = source.Bottom;
        target.Left = source.Left;

        // Other base properties
        target.Display = source.Display;
        target.AspectRatio = source.AspectRatio;

        // Visual effects
        target.Opacity = source.Opacity;
        target.BoxShadow = source.BoxShadow;

        // Text direction
        target.TextDirection = source.TextDirection;

        // Border properties
        target.Border = source.Border;
        target.BorderWidth = source.BorderWidth;
        target.BorderColor = source.BorderColor;
        target.BorderStyle = source.BorderStyle;
        target.BorderTop = source.BorderTop;
        target.BorderRight = source.BorderRight;
        target.BorderBottom = source.BorderBottom;
        target.BorderLeft = source.BorderLeft;
        target.BorderRadius = source.BorderRadius;
    }

    /// <summary>
    /// Processes a single template element by resolving expressions in its properties.
    /// </summary>
    /// <param name="element">The element to process.</param>
    /// <param name="data">The data context for expression evaluation.</param>
    /// <returns>A new element with expressions resolved, or null if the element should be excluded.</returns>
    internal TemplateElement? ProcessElement(TemplateElement element, ObjectValue data)
    {
        return element switch
        {
            TextElement text => ProcessTextElement(text, data),
            QrElement qr => ProcessQrElement(qr, data),
            BarcodeElement barcode => ProcessBarcodeElement(barcode, data),
            ImageElement image => ProcessImageElement(image, data),
            SvgElement svg => ProcessSvgElement(svg, data),
            FlexElement flex => ProcessFlexElement(flex, data),
            SeparatorElement separator => ProcessSeparatorElement(separator, data),
            _ => element
        };
    }

    /// <summary>
    /// Processes a text element by resolving expressions in its properties.
    /// </summary>
    /// <param name="text">The text element to process.</param>
    /// <param name="data">The data context for expression evaluation.</param>
    /// <returns>A new text element with expressions resolved.</returns>
    private TextElement ProcessTextElement(TextElement text, ObjectValue data)
    {
        var clone = new TextElement
        {
            Content = ProcessExpression(text.Content, data),
            Font = ProcessExpression(text.Font, data),
            Size = ProcessExpression(text.Size, data),
            Color = ProcessExpression(text.Color, data),
            Align = text.Align,
            Wrap = text.Wrap,
            Overflow = text.Overflow,
            MaxLines = text.MaxLines,
            LineHeight = text.LineHeight,
            Rotate = text.Rotate,
            Background = ProcessExpression(text.Background, data),
            Padding = text.Padding,
            Margin = text.Margin
        };

        CopyBaseProperties(text, clone);
        ProcessBorderExpressions(clone, data);
        return clone;
    }

    /// <summary>
    /// Processes a QR element by resolving expressions in its properties.
    /// </summary>
    /// <param name="qr">The QR element to process.</param>
    /// <param name="data">The data context for expression evaluation.</param>
    /// <returns>A new QR element with expressions resolved.</returns>
    private QrElement ProcessQrElement(QrElement qr, ObjectValue data)
    {
        var clone = new QrElement
        {
            Data = ProcessExpression(qr.Data, data),
            Size = qr.Size,
            ErrorCorrection = qr.ErrorCorrection,
            Foreground = ProcessExpression(qr.Foreground, data),
            Rotate = qr.Rotate,
            Background = ProcessExpression(qr.Background, data),
            Padding = qr.Padding,
            Margin = qr.Margin
        };

        CopyBaseProperties(qr, clone);
        ProcessBorderExpressions(clone, data);
        return clone;
    }

    /// <summary>
    /// Processes a barcode element by resolving expressions in its properties.
    /// </summary>
    /// <param name="barcode">The barcode element to process.</param>
    /// <param name="data">The data context for expression evaluation.</param>
    /// <returns>A new barcode element with expressions resolved.</returns>
    private BarcodeElement ProcessBarcodeElement(BarcodeElement barcode, ObjectValue data)
    {
        var clone = new BarcodeElement
        {
            Data = ProcessExpression(barcode.Data, data),
            Format = barcode.Format,
            BarcodeWidth = barcode.BarcodeWidth,
            BarcodeHeight = barcode.BarcodeHeight,
            ShowText = barcode.ShowText,
            Foreground = ProcessExpression(barcode.Foreground, data),
            Rotate = barcode.Rotate,
            Background = ProcessExpression(barcode.Background, data),
            Padding = barcode.Padding,
            Margin = barcode.Margin
        };

        CopyBaseProperties(barcode, clone);
        ProcessBorderExpressions(clone, data);
        return clone;
    }

    /// <summary>
    /// Processes an image element by resolving expressions in its properties.
    /// </summary>
    /// <param name="image">The image element to process.</param>
    /// <param name="data">The data context for expression evaluation.</param>
    /// <returns>A new image element with expressions resolved.</returns>
    private ImageElement ProcessImageElement(ImageElement image, ObjectValue data)
    {
        var clone = new ImageElement
        {
            Src = ProcessExpression(image.Src, data),
            ImageWidth = image.ImageWidth,
            ImageHeight = image.ImageHeight,
            Fit = image.Fit,
            Rotate = image.Rotate,
            Background = ProcessExpression(image.Background, data),
            Padding = image.Padding,
            Margin = image.Margin
        };

        CopyBaseProperties(image, clone);
        ProcessBorderExpressions(clone, data);
        return clone;
    }

    /// <summary>
    /// Processes an SVG element by resolving expressions in its properties.
    /// </summary>
    /// <param name="svg">The SVG element to process.</param>
    /// <param name="data">The data context for expression evaluation.</param>
    /// <returns>A new SVG element with expressions resolved.</returns>
    private SvgElement ProcessSvgElement(SvgElement svg, ObjectValue data)
    {
        var clone = new SvgElement
        {
            Src = ProcessExpression(svg.Src, data),
            Content = ProcessExpression(svg.Content, data),
            SvgWidth = svg.SvgWidth,
            SvgHeight = svg.SvgHeight,
            Fit = svg.Fit,
            Rotate = svg.Rotate,
            Background = ProcessExpression(svg.Background, data),
            Padding = svg.Padding,
            Margin = svg.Margin
        };

        CopyBaseProperties(svg, clone);
        ProcessBorderExpressions(clone, data);
        return clone;
    }

    /// <summary>
    /// Processes a separator element by resolving expressions in its properties.
    /// </summary>
    /// <param name="separator">The separator element to process.</param>
    /// <param name="data">The data context for expression evaluation.</param>
    /// <returns>A new separator element with expressions resolved.</returns>
    private SeparatorElement ProcessSeparatorElement(SeparatorElement separator, ObjectValue data)
    {
        var clone = new SeparatorElement
        {
            Orientation = separator.Orientation,
            Style = separator.Style,
            Thickness = separator.Thickness,
            Color = ProcessExpression(separator.Color, data),
            Rotate = separator.Rotate,
            // NOTE: Padding/Margin expressions are not processed here. This is
            // consistent with the existing pattern for other element types where
            // Padding and Margin are passed through as-is (they are resolved by
            // the layout engine, not by the template processor).
            Background = ProcessExpression(separator.Background, data),
            Padding = separator.Padding,
            Margin = separator.Margin
        };

        CopyBaseProperties(separator, clone);
        ProcessBorderExpressions(clone, data);
        return clone;
    }

    /// <summary>
    /// Processes a flex element and its children recursively.
    /// </summary>
    /// <param name="flex">The flex element to process.</param>
    /// <param name="data">The data context for expression evaluation.</param>
    /// <returns>A new flex element with expressions resolved.</returns>
    private FlexElement ProcessFlexElement(FlexElement flex, ObjectValue data)
    {
        var processed = new FlexElement
        {
            // Flex container-specific properties
            Direction = flex.Direction,
            Wrap = flex.Wrap,
            Justify = flex.Justify,
            Align = flex.Align,
            AlignContent = flex.AlignContent,
            Gap = flex.Gap,
            Overflow = flex.Overflow,
            RowGap = flex.RowGap,
            ColumnGap = flex.ColumnGap,

            // Base element properties requiring per-element handling
            Rotate = flex.Rotate,
            Background = ProcessExpression(flex.Background, data),
            Padding = flex.Padding,
            Margin = flex.Margin
        };

        CopyBaseProperties(flex, processed);
        ProcessBorderExpressions(processed, data);

        foreach (var child in flex.Children)
        {
            var processedChild = ProcessElement(child, data);
            if (processedChild != null)
                processed.AddChild(processedChild);
        }

        return processed;
    }

    /// <summary>
    /// Processes all border-related properties on an element through expression resolution.
    /// Must be called after <see cref="CopyBaseProperties"/> which copies border values verbatim.
    /// </summary>
    /// <param name="element">The element whose border properties to process.</param>
    /// <param name="data">The data context for expression evaluation.</param>
    private void ProcessBorderExpressions(TemplateElement element, ObjectValue data)
    {
        element.Border = ProcessExpression(element.Border, data);
        element.BorderWidth = ProcessExpression(element.BorderWidth, data);
        element.BorderColor = ProcessExpression(element.BorderColor, data);
        element.BorderStyle = ProcessExpression(element.BorderStyle, data);
        element.BorderTop = ProcessExpression(element.BorderTop, data);
        element.BorderRight = ProcessExpression(element.BorderRight, data);
        element.BorderBottom = ProcessExpression(element.BorderBottom, data);
        element.BorderLeft = ProcessExpression(element.BorderLeft, data);
        element.BorderRadius = ProcessExpression(element.BorderRadius, data);
        element.BoxShadow = ProcessExpression(element.BoxShadow, data);
    }

    /// <summary>
    /// Processes a template expression string using the template engine.
    /// Handles <c>{{variable}}</c>, <c>{{#if}}</c>, <c>{{#each}}</c>, and other expressions.
    /// Returns the original value unchanged if it is null, empty, or contains no expressions.
    /// Preserves null values to maintain nullable property semantics.
    /// </summary>
    /// <param name="value">The string that may contain template expressions.</param>
    /// <param name="data">The data context for expression evaluation.</param>
    /// <returns>The processed string with all expressions resolved, or the original value if no processing is needed.</returns>
    [return: System.Diagnostics.CodeAnalysis.NotNullIfNotNull(nameof(value))]
    internal string? ProcessExpression(string? value, ObjectValue data)
    {
        if (string.IsNullOrEmpty(value) || !value.Contains("{{"))
            return value;

        return _templateProcessor.Process(value, data);
    }
}
