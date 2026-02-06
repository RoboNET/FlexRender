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
            Rotate = template.Canvas.Rotate
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
    /// Processes a single template element by resolving expressions in its properties.
    /// </summary>
    /// <param name="element">The element to process.</param>
    /// <param name="data">The data context for expression evaluation.</param>
    /// <returns>A new element with expressions resolved, or null if the element should be excluded.</returns>
    internal TemplateElement? ProcessElement(TemplateElement element, ObjectValue data)
    {
        return element switch
        {
            TextElement text => new TextElement
            {
                Content = ProcessExpression(text.Content, data),
                Font = ProcessExpression(text.Font, data),
                Size = ProcessExpression(text.Size, data),
                Color = ProcessExpression(text.Color, data),
                Align = text.Align,
                Wrap = text.Wrap,
                Overflow = text.Overflow,
                MaxLines = text.MaxLines,
                Rotate = text.Rotate,
                Width = text.Width,
                Height = text.Height,
                Grow = text.Grow,
                Background = ProcessExpression(text.Background, data),
                Padding = text.Padding,
                Margin = text.Margin,
                LineHeight = text.LineHeight,
                Shrink = text.Shrink,
                Basis = text.Basis,
                AlignSelf = text.AlignSelf,
                Order = text.Order,
                Display = text.Display,
                Position = text.Position,
                Top = text.Top,
                Right = text.Right,
                Bottom = text.Bottom,
                Left = text.Left,
                AspectRatio = text.AspectRatio
            },

            QrElement qr => new QrElement
            {
                Data = ProcessExpression(qr.Data, data),
                Size = qr.Size,
                ErrorCorrection = qr.ErrorCorrection,
                Foreground = ProcessExpression(qr.Foreground, data),
                Rotate = qr.Rotate,
                Width = qr.Width,
                Height = qr.Height,
                Grow = qr.Grow,
                Background = ProcessExpression(qr.Background, data),
                Padding = qr.Padding,
                Margin = qr.Margin,
                Shrink = qr.Shrink,
                Basis = qr.Basis,
                AlignSelf = qr.AlignSelf,
                Order = qr.Order,
                Display = qr.Display,
                Position = qr.Position,
                Top = qr.Top,
                Right = qr.Right,
                Bottom = qr.Bottom,
                Left = qr.Left,
                AspectRatio = qr.AspectRatio
            },

            BarcodeElement barcode => new BarcodeElement
            {
                Data = ProcessExpression(barcode.Data, data),
                Format = barcode.Format,
                BarcodeWidth = barcode.BarcodeWidth,
                BarcodeHeight = barcode.BarcodeHeight,
                ShowText = barcode.ShowText,
                Foreground = ProcessExpression(barcode.Foreground, data),
                Rotate = barcode.Rotate,
                Width = barcode.Width,
                Height = barcode.Height,
                Grow = barcode.Grow,
                Background = ProcessExpression(barcode.Background, data),
                Padding = barcode.Padding,
                Margin = barcode.Margin,
                Shrink = barcode.Shrink,
                Basis = barcode.Basis,
                AlignSelf = barcode.AlignSelf,
                Order = barcode.Order,
                Display = barcode.Display,
                Position = barcode.Position,
                Top = barcode.Top,
                Right = barcode.Right,
                Bottom = barcode.Bottom,
                Left = barcode.Left,
                AspectRatio = barcode.AspectRatio
            },

            ImageElement image => new ImageElement
            {
                Src = ProcessExpression(image.Src, data),
                ImageWidth = image.ImageWidth,
                ImageHeight = image.ImageHeight,
                Fit = image.Fit,
                Rotate = image.Rotate,
                Width = image.Width,
                Height = image.Height,
                Grow = image.Grow,
                Background = ProcessExpression(image.Background, data),
                Padding = image.Padding,
                Margin = image.Margin,
                Shrink = image.Shrink,
                Basis = image.Basis,
                AlignSelf = image.AlignSelf,
                Order = image.Order,
                Display = image.Display,
                Position = image.Position,
                Top = image.Top,
                Right = image.Right,
                Bottom = image.Bottom,
                Left = image.Left,
                AspectRatio = image.AspectRatio
            },

            FlexElement flex => ProcessFlexElement(flex, data),

            SeparatorElement separator => new SeparatorElement
            {
                Orientation = separator.Orientation,
                Style = separator.Style,
                Thickness = separator.Thickness,
                Color = ProcessExpression(separator.Color, data),
                Width = separator.Width,
                Height = separator.Height,
                Grow = separator.Grow,
                Shrink = separator.Shrink,
                Basis = separator.Basis,
                AlignSelf = separator.AlignSelf,
                Order = separator.Order,
                Rotate = separator.Rotate,
                // NOTE: Padding/Margin expressions are not processed here. This is
                // consistent with the existing pattern for other element types where
                // Padding and Margin are passed through as-is (they are resolved by
                // the layout engine, not by the template processor).
                Background = ProcessExpression(separator.Background, data),
                Padding = separator.Padding,
                Margin = separator.Margin,
                Display = separator.Display,
                Position = separator.Position,
                Top = separator.Top,
                Right = separator.Right,
                Bottom = separator.Bottom,
                Left = separator.Left,
                AspectRatio = separator.AspectRatio
            },

            _ => element
        };
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
            Direction = flex.Direction,
            Wrap = flex.Wrap,
            Justify = flex.Justify,
            Align = flex.Align,
            AlignContent = flex.AlignContent,
            Gap = flex.Gap,
            Padding = flex.Padding,
            Margin = flex.Margin,
            Background = ProcessExpression(flex.Background, data),
            Width = flex.Width,
            Height = flex.Height,
            Grow = flex.Grow,
            Shrink = flex.Shrink,
            Basis = flex.Basis,
            AlignSelf = flex.AlignSelf,
            Order = flex.Order,
            Rotate = flex.Rotate,
            Display = flex.Display,
            Position = flex.Position,
            Top = flex.Top,
            Right = flex.Right,
            Bottom = flex.Bottom,
            Left = flex.Left,
            AspectRatio = flex.AspectRatio,
            Overflow = flex.Overflow
        };

        foreach (var child in flex.Children)
        {
            var processedChild = ProcessElement(child, data);
            if (processedChild != null)
                processed.AddChild(processedChild);
        }

        return processed;
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
