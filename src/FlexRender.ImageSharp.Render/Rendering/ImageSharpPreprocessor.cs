using FlexRender.Configuration;
using FlexRender.Parsing.Ast;
using FlexRender.TemplateEngine;

namespace FlexRender.ImageSharp.Rendering;

/// <summary>
/// Handles template preprocessing for ImageSharp rendering: expression resolution,
/// font registration, and element processing before layout and rendering.
/// </summary>
internal sealed class ImageSharpPreprocessor
{
    private readonly ImageSharpFontManager _fontManager;
    private readonly TemplateProcessor _templateProcessor;
    private readonly FlexRenderOptions? _options;

    /// <summary>
    /// Initializes a new instance of the <see cref="ImageSharpPreprocessor"/> class.
    /// </summary>
    /// <param name="fontManager">The font manager for font registration.</param>
    /// <param name="templateProcessor">The template processor for expression evaluation.</param>
    /// <param name="options">Optional configuration options for path resolution.</param>
    internal ImageSharpPreprocessor(
        ImageSharpFontManager fontManager,
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

    private void RegisterTemplateFonts(Template template)
    {
        foreach (var (fontName, fontDef) in template.Fonts)
        {
            var resolvedPath = ResolveFontPath(fontDef.Path);
            _fontManager.RegisterFont(fontName, resolvedPath);

            if (string.Equals(fontName, "default", StringComparison.OrdinalIgnoreCase))
            {
                _fontManager.RegisterFont("main", resolvedPath);
            }
        }
    }

    private string ResolveFontPath(string path)
    {
        if (Path.IsPathRooted(path))
            return Path.GetFullPath(path);

        if (_options?.BasePath is not null)
            return Path.GetFullPath(Path.Combine(_options.BasePath, path));

        return Path.GetFullPath(path);
    }

    private TemplateElement? ProcessElement(TemplateElement element, ObjectValue data)
    {
        return element switch
        {
            TextElement text => ProcessTextElement(text, data),
            FlexElement flex => ProcessFlexElement(flex, data),
            SeparatorElement separator => ProcessSeparatorElement(separator, data),
            ImageElement image => ProcessImageElement(image, data),
            QrElement qr => ProcessQrElement(qr, data),
            BarcodeElement barcode => ProcessBarcodeElement(barcode, data),
            SvgElement svg => ProcessSvgElement(svg, data),
            _ => element
        };
    }

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

        TemplateElement.CopyBaseProperties(text, clone);
        ProcessBorderExpressions(clone, data);
        return clone;
    }

    private FlexElement ProcessFlexElement(FlexElement flex, ObjectValue data)
    {
        var clone = new FlexElement
        {
            Direction = flex.Direction,
            Wrap = flex.Wrap,
            Justify = flex.Justify,
            Align = flex.Align,
            AlignContent = flex.AlignContent,
            Gap = flex.Gap,
            ColumnGap = flex.ColumnGap,
            RowGap = flex.RowGap,
            Overflow = flex.Overflow,
            Rotate = flex.Rotate,
            Background = ProcessExpression(flex.Background, data),
            Padding = flex.Padding,
            Margin = flex.Margin
        };

        TemplateElement.CopyBaseProperties(flex, clone);
        ProcessBorderExpressions(clone, data);

        foreach (var child in flex.Children)
        {
            var processedChild = ProcessElement(child, data);
            if (processedChild != null)
                clone.AddChild(processedChild);
        }

        return clone;
    }

    private SeparatorElement ProcessSeparatorElement(SeparatorElement separator, ObjectValue data)
    {
        var clone = new SeparatorElement
        {
            Color = ProcessExpression(separator.Color, data),
            Thickness = separator.Thickness,
            Orientation = separator.Orientation,
            Style = separator.Style,
            Rotate = separator.Rotate,
            Background = ProcessExpression(separator.Background, data),
            Padding = separator.Padding,
            Margin = separator.Margin
        };

        TemplateElement.CopyBaseProperties(separator, clone);
        ProcessBorderExpressions(clone, data);
        return clone;
    }

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

        TemplateElement.CopyBaseProperties(image, clone);
        ProcessBorderExpressions(clone, data);
        return clone;
    }

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

        TemplateElement.CopyBaseProperties(qr, clone);
        ProcessBorderExpressions(clone, data);
        return clone;
    }

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

        TemplateElement.CopyBaseProperties(barcode, clone);
        ProcessBorderExpressions(clone, data);
        return clone;
    }

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

        TemplateElement.CopyBaseProperties(svg, clone);
        ProcessBorderExpressions(clone, data);
        return clone;
    }

    /// <summary>
    /// Processes all border-related properties on an element through expression resolution.
    /// Must be called after <see cref="TemplateElement.CopyBaseProperties"/> which copies border values verbatim.
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

    [return: System.Diagnostics.CodeAnalysis.NotNullIfNotNull(nameof(value))]
    private string? ProcessExpression(string? value, ObjectValue data)
    {
        if (string.IsNullOrEmpty(value) || !value.Contains("{{"))
            return value;

        return _templateProcessor.Process(value, data);
    }
}
