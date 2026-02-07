using FlexRender.Configuration;
using FlexRender.Parsing.Ast;
using FlexRender.TemplateEngine;

namespace FlexRender.Svg.Rendering;

/// <summary>
/// Lightweight template preprocessor for SVG rendering.
/// Resolves template expressions in element properties without performing
/// font registration (which is a Skia-specific concern).
/// </summary>
internal sealed class SvgPreprocessor
{
    private readonly TemplateProcessor _templateProcessor;

    /// <summary>
    /// Initializes a new instance of the <see cref="SvgPreprocessor"/> class.
    /// </summary>
    /// <param name="templateProcessor">The template processor for expression evaluation.</param>
    internal SvgPreprocessor(TemplateProcessor templateProcessor)
    {
        ArgumentNullException.ThrowIfNull(templateProcessor);
        _templateProcessor = templateProcessor;
    }

    /// <summary>
    /// Processes a template by resolving expressions in all element properties.
    /// </summary>
    /// <param name="template">The expanded template to process.</param>
    /// <param name="data">The data context for expression evaluation.</param>
    /// <returns>A new template with all expressions resolved.</returns>
    internal Template Process(Template template, ObjectValue data)
    {
        ArgumentNullException.ThrowIfNull(template);
        ArgumentNullException.ThrowIfNull(data);

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
            {
                processed.AddElement(processedElement);
            }
        }

        return processed;
    }

    private TemplateElement? ProcessElement(TemplateElement element, ObjectValue data)
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

    private SeparatorElement ProcessSeparatorElement(SeparatorElement separator, ObjectValue data)
    {
        var clone = new SeparatorElement
        {
            Orientation = separator.Orientation,
            Style = separator.Style,
            Thickness = separator.Thickness,
            Color = ProcessExpression(separator.Color, data),
            Rotate = separator.Rotate,
            Background = ProcessExpression(separator.Background, data),
            Padding = separator.Padding,
            Margin = separator.Margin
        };

        TemplateElement.CopyBaseProperties(separator, clone);
        ProcessBorderExpressions(clone, data);
        return clone;
    }

    private FlexElement ProcessFlexElement(FlexElement flex, ObjectValue data)
    {
        var processed = new FlexElement
        {
            Direction = flex.Direction,
            Wrap = flex.Wrap,
            Justify = flex.Justify,
            Align = flex.Align,
            AlignContent = flex.AlignContent,
            Gap = flex.Gap,
            Overflow = flex.Overflow,
            RowGap = flex.RowGap,
            ColumnGap = flex.ColumnGap,
            Rotate = flex.Rotate,
            Background = ProcessExpression(flex.Background, data),
            Padding = flex.Padding,
            Margin = flex.Margin
        };

        TemplateElement.CopyBaseProperties(flex, processed);
        ProcessBorderExpressions(processed, data);

        foreach (var child in flex.Children)
        {
            var processedChild = ProcessElement(child, data);
            if (processedChild != null)
            {
                processed.AddChild(processedChild);
            }
        }

        return processed;
    }

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
        {
            return value;
        }

        return _templateProcessor.Process(value, data);
    }
}
