using FlexRender.Abstractions;
using FlexRender.Configuration;
using FlexRender.Layout;
using FlexRender.Parsing.Ast;
using FlexRender.Providers;
using FlexRender.TemplateEngine;
using SkiaSharp;

namespace FlexRender.Rendering;

/// <summary>
/// Renders templates to SkiaSharp canvases and bitmaps.
/// </summary>
internal sealed class SkiaRenderer : IDisposable, IAsyncDisposable
{
    /// <summary>
    /// Default maximum render depth.
    /// </summary>
    /// <remarks>
    /// This constant is preserved for backward compatibility. The actual limit
    /// at runtime comes from <see cref="ResourceLimits.MaxRenderDepth"/>.
    /// </remarks>
    private const int DefaultMaxRenderDepth = 100;

    private readonly FontManager _fontManager;
    private readonly TextRenderer _textRenderer;
    private readonly LayoutEngine _layoutEngine;
    private readonly IContentProvider<QrElement>? _qrProvider;
    private readonly IContentProvider<BarcodeElement>? _barcodeProvider;
    private readonly IImageLoader? _imageLoader;
    private readonly TemplateProcessor _templateProcessor;
    private readonly TemplateExpander _expander;
    private readonly ResourceLimits _limits;
    private readonly FlexRenderOptions? _options;
    private bool _disposed;

    /// <summary>
    /// Default base font size in pixels.
    /// </summary>
    public float BaseFontSize { get; set; } = 12f;

    /// <summary>
    /// Creates a new renderer instance with default resource limits.
    /// </summary>
    public SkiaRenderer() : this(new ResourceLimits())
    {
    }

    /// <summary>
    /// Creates a new renderer instance with custom resource limits.
    /// </summary>
    /// <param name="limits">The resource limits to apply.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="limits"/> is null.</exception>
    public SkiaRenderer(ResourceLimits limits) : this(limits, null, null)
    {
    }

    /// <summary>
    /// Creates a new renderer instance with custom resource limits and optional content providers.
    /// </summary>
    /// <param name="limits">The resource limits to apply.</param>
    /// <param name="qrProvider">Optional QR code content provider.</param>
    /// <param name="barcodeProvider">Optional barcode content provider.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="limits"/> is null.</exception>
    public SkiaRenderer(
        ResourceLimits limits,
        IContentProvider<QrElement>? qrProvider,
        IContentProvider<BarcodeElement>? barcodeProvider)
        : this(limits, qrProvider, barcodeProvider, null)
    {
    }

    /// <summary>
    /// Creates a new renderer instance with custom resource limits, optional content providers,
    /// and optional image loader for async image pre-loading (HTTP, embedded, etc.).
    /// </summary>
    /// <param name="limits">The resource limits to apply.</param>
    /// <param name="qrProvider">Optional QR code content provider.</param>
    /// <param name="barcodeProvider">Optional barcode content provider.</param>
    /// <param name="imageLoader">Optional image loader for async pre-loading of images from various sources.</param>
    /// <param name="deterministicRendering">
    /// When <c>true</c>, disables font hinting and subpixel rendering for cross-platform consistency.
    /// Default is <c>false</c>.
    /// </param>
    /// <param name="options">Optional configuration options for path resolution and other settings.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="limits"/> is null.</exception>
    public SkiaRenderer(
        ResourceLimits limits,
        IContentProvider<QrElement>? qrProvider,
        IContentProvider<BarcodeElement>? barcodeProvider,
        IImageLoader? imageLoader,
        bool deterministicRendering = false,
        FlexRenderOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(limits);
        _limits = limits;
        _options = options;
        _qrProvider = qrProvider;
        _barcodeProvider = barcodeProvider;
        _imageLoader = imageLoader;
        _templateProcessor = new TemplateProcessor(limits);
        _expander = new TemplateExpander(limits);
        _fontManager = new FontManager();
        _textRenderer = new TextRenderer(_fontManager, deterministicRendering);
        _layoutEngine = new LayoutEngine(_limits);
        _layoutEngine.TextMeasurer = (element, fontSize, maxWidth) =>
        {
            var measured = _textRenderer.MeasureText(element, maxWidth, BaseFontSize);
            return new LayoutSize(measured.Width, measured.Height);
        };
        _layoutEngine.BaseFontSize = BaseFontSize;
    }

    /// <summary>
    /// Gets the font manager for font registration.
    /// </summary>
    public FontManager FontManager => _fontManager;

    /// <summary>
    /// Computes the layout tree for a template with data.
    /// Uses the same layout engine configuration as rendering (including text measurement).
    /// </summary>
    /// <param name="template">The template to lay out.</param>
    /// <param name="data">The data for variable substitution.</param>
    /// <returns>The root layout node with computed positions and sizes.</returns>
    public LayoutNode ComputeLayout(Template template, ObjectValue data)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentNullException.ThrowIfNull(template);
        ArgumentNullException.ThrowIfNull(data);

        var expandedTemplate = _expander.Expand(template, data);
        var processedTemplate = ProcessTemplate(expandedTemplate, data);
        return _layoutEngine.ComputeLayout(processedTemplate);
    }

    /// <summary>
    /// Measures the size required to render the template.
    /// Takes into account canvas rotation which may swap width and height.
    /// </summary>
    /// <param name="template">The template to measure.</param>
    /// <param name="data">The data for variable substitution.</param>
    /// <returns>The required size after rotation is applied.</returns>
    public SKSize Measure(Template template, ObjectValue data)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentNullException.ThrowIfNull(template);
        ArgumentNullException.ThrowIfNull(data);

        var expandedTemplate = _expander.Expand(template, data);
        var processedTemplate = ProcessTemplate(expandedTemplate, data);

        // Use LayoutEngine to compute accurate sizes
        var rootNode = _layoutEngine.ComputeLayout(processedTemplate);

        var width = rootNode.Width;
        var height = rootNode.Height;

        // Check if canvas rotation swaps dimensions
        var rotationDegrees = RotationHelper.ParseRotation(processedTemplate.Canvas.Rotate);
        if (RotationHelper.SwapsDimensions(rotationDegrees))
        {
            return new SKSize(height, width);
        }

        return new SKSize(width, height);
    }

    /// <summary>
    /// Renders the template to a canvas.
    /// </summary>
    /// <param name="canvas">The canvas to render to.</param>
    /// <param name="template">The template to render.</param>
    /// <param name="data">The data for variable substitution.</param>
    /// <param name="offset">Optional offset for rendering position.</param>
    public void Render(SKCanvas canvas, Template template, ObjectValue data, SKPoint offset = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentNullException.ThrowIfNull(canvas);
        ArgumentNullException.ThrowIfNull(template);
        ArgumentNullException.ThrowIfNull(data);

        RenderToCanvas(canvas, template, data, offset, imageCache: null);
    }

    /// <summary>
    /// Core canvas rendering logic. Accepts an optional pre-loaded image cache
    /// so that async render paths can pass it through without storing mutable state
    /// on the renderer instance (thread safety).
    /// </summary>
    private void RenderToCanvas(
        SKCanvas canvas,
        Template template,
        ObjectValue data,
        SKPoint offset,
        IReadOnlyDictionary<string, SKBitmap>? imageCache)
    {
        var expandedTemplate = _expander.Expand(template, data);
        var processedTemplate = ProcessTemplate(expandedTemplate, data);
        var rootNode = _layoutEngine.ComputeLayout(processedTemplate);

        // Save canvas state
        canvas.Save();

        // Apply offset
        if (offset != default)
            canvas.Translate(offset.X, offset.Y);

        // Draw background
        var backgroundColor = ColorParser.Parse(processedTemplate.Canvas.Background);
        using var backgroundPaint = new SKPaint { Color = backgroundColor };
        canvas.DrawRect(0, 0, rootNode.Width, rootNode.Height, backgroundPaint);

        // Render the layout tree
        foreach (var child in rootNode.Children)
        {
            RenderNode(canvas, child, 0, 0, imageCache);
        }

        // Restore canvas state
        canvas.Restore();
    }

    /// <summary>
    /// Renders the template to a bitmap.
    /// Applies canvas rotation after rendering if specified in template settings.
    /// </summary>
    /// <param name="bitmap">The bitmap to render to.</param>
    /// <param name="template">The template to render.</param>
    /// <param name="data">The data for variable substitution.</param>
    /// <param name="offset">Optional offset for rendering position.</param>
    public void Render(SKBitmap bitmap, Template template, ObjectValue data, SKPoint offset = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentNullException.ThrowIfNull(bitmap);
        ArgumentNullException.ThrowIfNull(template);
        ArgumentNullException.ThrowIfNull(data);

        RenderToBitmapCore(bitmap, template, data, offset, imageCache: null);
    }

    /// <summary>
    /// Core bitmap rendering logic. Accepts an optional pre-loaded image cache
    /// so that async render paths can pass it through without storing mutable state
    /// on the renderer instance (thread safety).
    /// </summary>
    private void RenderToBitmapCore(
        SKBitmap bitmap,
        Template template,
        ObjectValue data,
        SKPoint offset,
        IReadOnlyDictionary<string, SKBitmap>? imageCache)
    {
        var rotationDegrees = RotationHelper.ParseRotation(template.Canvas.Rotate);

        // If no rotation needed, render directly to bitmap
        if (!RotationHelper.HasRotation(rotationDegrees))
        {
            using var canvas = new SKCanvas(bitmap);
            RenderToCanvas(canvas, template, data, offset, imageCache);
            return;
        }

        // Render to temporary bitmap first, then rotate
        var expandedTemplate = _expander.Expand(template, data);
        var processedTemplate = ProcessTemplate(expandedTemplate, data);
        var rootNode = _layoutEngine.ComputeLayout(processedTemplate);
        var originalWidth = (int)rootNode.Width;
        var originalHeight = (int)rootNode.Height;

        using var tempBitmap = new SKBitmap(originalWidth, originalHeight);
        using (var tempCanvas = new SKCanvas(tempBitmap))
        {
            RenderToCanvas(tempCanvas, template, data, offset, imageCache);
        }

        // Rotate the bitmap
        using var rotatedBitmap = RotateBitmap(tempBitmap, rotationDegrees);

        // Draw rotated result onto the target bitmap
        using var targetCanvas = new SKCanvas(bitmap);
        targetCanvas.DrawBitmap(rotatedBitmap, 0, 0);
    }

    /// <summary>
    /// Renders the template using typed data.
    /// </summary>
    /// <typeparam name="T">The data type implementing ITemplateData.</typeparam>
    /// <param name="canvas">The canvas to render to.</param>
    /// <param name="template">The template to render.</param>
    /// <param name="data">The typed data.</param>
    /// <param name="offset">Optional offset for rendering position.</param>
    public void Render<T>(SKCanvas canvas, Template template, T data, SKPoint offset = default)
        where T : ITemplateData
    {
        Render(canvas, template, data.ToTemplateValue(), offset);
    }

    /// <summary>
    /// Renders the template using typed data to a bitmap.
    /// </summary>
    /// <typeparam name="T">The data type implementing ITemplateData.</typeparam>
    /// <param name="bitmap">The bitmap to render to.</param>
    /// <param name="template">The template to render.</param>
    /// <param name="data">The typed data.</param>
    /// <param name="offset">Optional offset for rendering position.</param>
    public void Render<T>(SKBitmap bitmap, Template template, T data, SKPoint offset = default)
        where T : ITemplateData
    {
        Render(bitmap, template, data.ToTemplateValue(), offset);
    }

    /// <summary>
    /// Recursively renders a layout node and its children.
    /// </summary>
    /// <param name="canvas">The canvas to render to.</param>
    /// <param name="node">The layout node to render.</param>
    /// <param name="offsetX">X offset from parent.</param>
    /// <param name="offsetY">Y offset from parent.</param>
    /// <param name="imageCache">Optional pre-loaded image cache for thread-safe rendering.</param>
    /// <param name="depth">Current recursion depth.</param>
    /// <exception cref="InvalidOperationException">Thrown when maximum render depth is exceeded.</exception>
    private void RenderNode(
        SKCanvas canvas,
        LayoutNode node,
        float offsetX,
        float offsetY,
        IReadOnlyDictionary<string, SKBitmap>? imageCache,
        int depth = 0)
    {
        if (depth > _limits.MaxRenderDepth)
        {
            throw new InvalidOperationException(
                $"Maximum render depth ({_limits.MaxRenderDepth}) exceeded. Template may be too deeply nested.");
        }

        // Skip elements with display:none
        if (node.Element.Display == Display.None)
            return;

        var x = node.X + offsetX;
        var y = node.Y + offsetY;

        // Draw current element
        DrawElement(canvas, node.Element, x, y, node.Width, node.Height, imageCache);

        // Apply overflow:hidden clipping for flex containers
        var needsClip = node.Element is FlexElement { Overflow: Overflow.Hidden };
        if (needsClip)
        {
            canvas.Save();
            canvas.ClipRect(new SKRect(x, y, x + node.Width, y + node.Height));
        }

        // Recursively render children
        foreach (var child in node.Children)
        {
            RenderNode(canvas, child, x, y, imageCache, depth + 1);
        }

        if (needsClip)
        {
            canvas.Restore();
        }
    }

    /// <summary>
    /// Draws a single element at the specified position.
    /// </summary>
    /// <param name="canvas">The canvas to draw on.</param>
    /// <param name="element">The element to draw.</param>
    /// <param name="x">X position.</param>
    /// <param name="y">Y position.</param>
    /// <param name="width">Available width.</param>
    /// <param name="height">Available height.</param>
    /// <param name="imageCache">Optional pre-loaded image cache for thread-safe rendering.</param>
    private void DrawElement(
        SKCanvas canvas,
        TemplateElement element,
        float x,
        float y,
        float width,
        float height,
        IReadOnlyDictionary<string, SKBitmap>? imageCache)
    {
        // Draw background if specified
        if (!string.IsNullOrEmpty(element.Background))
        {
            var bgColor = ColorParser.Parse(element.Background);
            using var bgPaint = new SKPaint { Color = bgColor };
            canvas.DrawRect(x, y, width, height, bgPaint);
        }

        switch (element)
        {
            case TextElement text:
                var bounds = new SKRect(x, y, x + width, y + height);
                _textRenderer.DrawText(canvas, text, bounds, BaseFontSize);
                break;

            case QrElement qr when _qrProvider is not null:
                using (var bitmap = _qrProvider.Generate(qr))
                {
                    canvas.DrawBitmap(bitmap, x, y);
                }
                break;

            case BarcodeElement barcode when _barcodeProvider is not null:
                using (var bitmap = _barcodeProvider.Generate(barcode))
                {
                    canvas.DrawBitmap(bitmap, x, y);
                }
                break;

            case ImageElement image:
                using (var bitmap = ImageProvider.Generate(image, imageCache))
                {
                    canvas.DrawBitmap(bitmap, x, y);
                }
                break;

            case SeparatorElement separator:
                DrawSeparator(canvas, separator, x, y, width, height);
                break;

            case FlexElement:
                // FlexElement is a container, children are rendered via RenderNode recursion
                break;
        }
    }

    /// <summary>
    /// Draws a separator element (horizontal or vertical line) with the configured style.
    /// </summary>
    /// <param name="canvas">The canvas to draw on.</param>
    /// <param name="separator">The separator element to draw.</param>
    /// <param name="x">X position.</param>
    /// <param name="y">Y position.</param>
    /// <param name="width">Available width.</param>
    /// <param name="height">Available height.</param>
    private static void DrawSeparator(SKCanvas canvas, SeparatorElement separator, float x, float y, float width, float height)
    {
        var color = ColorParser.Parse(separator.Color);

        using var paint = new SKPaint
        {
            Color = color,
            StrokeWidth = separator.Thickness,
            Style = SKPaintStyle.Stroke,
            IsAntialias = true
        };

        // Apply dash/dot pattern if not solid
        if (separator.Style != SeparatorStyle.Solid)
        {
            var intervals = separator.Style == SeparatorStyle.Dotted
                ? new[] { separator.Thickness, separator.Thickness * 2 }
                : new[] { separator.Thickness * 4, separator.Thickness * 2 };

            using var pathEffect = SKPathEffect.CreateDash(intervals, 0);
            paint.PathEffect = pathEffect;
        }

        if (separator.Orientation == SeparatorOrientation.Horizontal)
        {
            var lineY = y + height / 2f;
            canvas.DrawLine(x, lineY, x + width, lineY, paint);
        }
        else
        {
            var lineX = x + width / 2f;
            canvas.DrawLine(lineX, y, lineX, y + height, paint);
        }
    }

    /// <summary>
    /// Rotates a bitmap by the specified degrees.
    /// Supports 90, 180, 270 degree rotations (and their negatives).
    /// </summary>
    /// <param name="source">The source bitmap to rotate.</param>
    /// <param name="degrees">The rotation angle in degrees.</param>
    /// <returns>A new rotated bitmap. Caller is responsible for disposal.</returns>
    private static SKBitmap RotateBitmap(SKBitmap source, float degrees)
    {
        // Normalize degrees to 0-360 range
        var normalizedDegrees = ((degrees % 360) + 360) % 360;

        // If no rotation needed, return a copy
        if (normalizedDegrees < 0.1f || normalizedDegrees > 359.9f)
        {
            var copy = new SKBitmap(source.Width, source.Height);
            using var copyCanvas = new SKCanvas(copy);
            copyCanvas.DrawBitmap(source, 0, 0);
            return copy;
        }

        // Determine output dimensions
        var swapDimensions = RotationHelper.SwapsDimensions(normalizedDegrees);
        var outputWidth = swapDimensions ? source.Height : source.Width;
        var outputHeight = swapDimensions ? source.Width : source.Height;

        var rotatedBitmap = new SKBitmap(outputWidth, outputHeight);
        using var canvas = new SKCanvas(rotatedBitmap);

        // Clear with transparent
        canvas.Clear(SKColors.Transparent);

        // Apply rotation transformation
        // Move origin to center of output, rotate, then draw centered
        canvas.Translate(outputWidth / 2f, outputHeight / 2f);
        canvas.RotateDegrees(normalizedDegrees);
        canvas.Translate(-source.Width / 2f, -source.Height / 2f);

        canvas.DrawBitmap(source, 0, 0);

        return rotatedBitmap;
    }

    private Template ProcessTemplate(Template template, ObjectValue data)
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

    private TemplateElement? ProcessElement(TemplateElement element, ObjectValue data)
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
    private string? ProcessExpression(string? value, ObjectValue data)
    {
        if (string.IsNullOrEmpty(value) || !value.Contains("{{"))
            return value;

        return _templateProcessor.Process(value, data);
    }

    /// <summary>
    /// Renders a template to a new bitmap.
    /// </summary>
    /// <param name="layoutTemplate">The template to render.</param>
    /// <param name="data">The data context for template expressions.</param>
    /// <param name="cancellationToken">Cancellation token for async operation.</param>
    /// <returns>A new bitmap containing the rendered template.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="layoutTemplate"/> or <paramref name="data"/> is null.</exception>
    /// <exception cref="OperationCanceledException">Thrown when the operation is cancelled.</exception>
    public async Task<SKBitmap> Render(
        Template layoutTemplate,
        ObjectValue data,
        CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentNullException.ThrowIfNull(layoutTemplate);
        ArgumentNullException.ThrowIfNull(data);
        cancellationToken.ThrowIfCancellationRequested();

        var imageCache = _imageLoader is not null
            ? await PreloadImagesAsync(layoutTemplate, data, cancellationToken).ConfigureAwait(false)
            : null;

        try
        {
            // Measure returns the final size after rotation
            var size = Measure(layoutTemplate, data);
            var bitmap = new SKBitmap((int)size.Width, (int)size.Height);
            try
            {
                RenderToBitmapCore(bitmap, layoutTemplate, data, default, imageCache);
                return bitmap;
            }
            catch
            {
                bitmap.Dispose();
                throw;
            }
        }
        finally
        {
            if (imageCache is not null)
            {
                foreach (var bmp in imageCache.Values)
                    bmp.Dispose();
            }
        }
    }

    /// <summary>
    /// Renders a template to an existing bitmap asynchronously.
    /// </summary>
    /// <param name="bitmap">The target bitmap to render onto.</param>
    /// <param name="layoutTemplate">The template to render.</param>
    /// <param name="data">The data context for template expressions.</param>
    /// <param name="offset">Optional offset for rendering position.</param>
    /// <param name="cancellationToken">Cancellation token for async operation.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="bitmap"/>, <paramref name="layoutTemplate"/>, or <paramref name="data"/> is null.</exception>
    /// <exception cref="OperationCanceledException">Thrown when the operation is cancelled.</exception>
    public async Task Render(
        SKBitmap bitmap,
        Template layoutTemplate,
        ObjectValue data,
        SKPoint offset,
        CancellationToken cancellationToken)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentNullException.ThrowIfNull(bitmap);
        ArgumentNullException.ThrowIfNull(layoutTemplate);
        ArgumentNullException.ThrowIfNull(data);
        cancellationToken.ThrowIfCancellationRequested();

        var imageCache = _imageLoader is not null
            ? await PreloadImagesAsync(layoutTemplate, data, cancellationToken).ConfigureAwait(false)
            : null;

        try
        {
            RenderToBitmapCore(bitmap, layoutTemplate, data, offset, imageCache);
        }
        finally
        {
            if (imageCache is not null)
            {
                foreach (var bmp in imageCache.Values)
                    bmp.Dispose();
            }
        }
    }

    /// <summary>
    /// Renders a template to a PNG stream.
    /// </summary>
    /// <param name="output">The output stream to write PNG data to.</param>
    /// <param name="layoutTemplate">The template to render.</param>
    /// <param name="data">The data context for template expressions.</param>
    /// <param name="cancellationToken">Cancellation token for async operation.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="output"/>, <paramref name="layoutTemplate"/>, or <paramref name="data"/> is null.</exception>
    /// <exception cref="OperationCanceledException">Thrown when the operation is cancelled.</exception>
    public async Task RenderToPng(
        Stream output,
        Template layoutTemplate,
        ObjectValue data,
        CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentNullException.ThrowIfNull(output);
        ArgumentNullException.ThrowIfNull(layoutTemplate);
        ArgumentNullException.ThrowIfNull(data);
        cancellationToken.ThrowIfCancellationRequested();

        var imageCache = _imageLoader is not null
            ? await PreloadImagesAsync(layoutTemplate, data, cancellationToken).ConfigureAwait(false)
            : null;

        try
        {
            // Measure returns the final size after rotation
            var size = Measure(layoutTemplate, data);
            using var bitmap = new SKBitmap((int)size.Width, (int)size.Height);

            RenderToBitmapCore(bitmap, layoutTemplate, data, default, imageCache);

            using var image = SKImage.FromBitmap(bitmap);
            using var encodedData = image.Encode(SKEncodedImageFormat.Png, 100);
            encodedData.SaveTo(output);
        }
        finally
        {
            if (imageCache is not null)
            {
                foreach (var bmp in imageCache.Values)
                    bmp.Dispose();
            }
        }
    }

    /// <summary>
    /// Renders a template to a JPEG stream.
    /// </summary>
    /// <param name="output">The output stream to write JPEG data to.</param>
    /// <param name="layoutTemplate">The template to render.</param>
    /// <param name="data">The data context for template expressions.</param>
    /// <param name="quality">JPEG quality (1-100, default 90).</param>
    /// <param name="cancellationToken">Cancellation token for async operation.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="output"/>, <paramref name="layoutTemplate"/>, or <paramref name="data"/> is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="quality"/> is not between 1 and 100.</exception>
    /// <exception cref="OperationCanceledException">Thrown when the operation is cancelled.</exception>
    public async Task RenderToJpeg(
        Stream output,
        Template layoutTemplate,
        ObjectValue data,
        int quality = 90,
        CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentNullException.ThrowIfNull(output);
        ArgumentNullException.ThrowIfNull(layoutTemplate);
        ArgumentNullException.ThrowIfNull(data);

        if (quality < 1 || quality > 100)
        {
            throw new ArgumentOutOfRangeException(nameof(quality), quality, "Quality must be between 1 and 100.");
        }

        cancellationToken.ThrowIfCancellationRequested();

        var imageCache = _imageLoader is not null
            ? await PreloadImagesAsync(layoutTemplate, data, cancellationToken).ConfigureAwait(false)
            : null;

        try
        {
            // Measure returns the final size after rotation
            var size = Measure(layoutTemplate, data);
            using var bitmap = new SKBitmap((int)size.Width, (int)size.Height);

            RenderToBitmapCore(bitmap, layoutTemplate, data, default, imageCache);

            using var image = SKImage.FromBitmap(bitmap);
            using var encodedData = image.Encode(SKEncodedImageFormat.Jpeg, quality);
            encodedData.SaveTo(output);
        }
        finally
        {
            if (imageCache is not null)
            {
                foreach (var bmp in imageCache.Values)
                    bmp.Dispose();
            }
        }
    }

    /// <summary>
    /// Renders a template to a BMP stream.
    /// </summary>
    /// <param name="output">The output stream to write BMP data to.</param>
    /// <param name="layoutTemplate">The template to render.</param>
    /// <param name="data">The data context for template expressions.</param>
    /// <param name="colorMode">The BMP color depth mode to use for encoding.</param>
    /// <param name="cancellationToken">Cancellation token for async operation.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="output"/>, <paramref name="layoutTemplate"/>, or <paramref name="data"/> is null.</exception>
    /// <exception cref="OperationCanceledException">Thrown when the operation is cancelled.</exception>
    public async Task RenderToBmp(
        Stream output,
        Template layoutTemplate,
        ObjectValue data,
        BmpColorMode colorMode = BmpColorMode.Bgra32,
        CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentNullException.ThrowIfNull(output);
        ArgumentNullException.ThrowIfNull(layoutTemplate);
        ArgumentNullException.ThrowIfNull(data);
        cancellationToken.ThrowIfCancellationRequested();

        var imageCache = _imageLoader is not null
            ? await PreloadImagesAsync(layoutTemplate, data, cancellationToken).ConfigureAwait(false)
            : null;

        try
        {
            // Measure returns the final size after rotation
            var size = Measure(layoutTemplate, data);
            using var bitmap = new SKBitmap((int)size.Width, (int)size.Height);

            RenderToBitmapCore(bitmap, layoutTemplate, data, default, imageCache);

            BmpEncoder.Encode(bitmap, output, colorMode);
        }
        finally
        {
            if (imageCache is not null)
            {
                foreach (var bmp in imageCache.Values)
                    bmp.Dispose();
            }
        }
    }

    /// <summary>
    /// Renders a template to raw pixel data in BGRA8888 format.
    /// </summary>
    /// <param name="output">The output stream to write raw pixel data to.</param>
    /// <param name="layoutTemplate">The template to render.</param>
    /// <param name="data">The data context for template expressions.</param>
    /// <param name="cancellationToken">Cancellation token for async operation.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="output"/>, <paramref name="layoutTemplate"/>, or <paramref name="data"/> is null.</exception>
    /// <exception cref="OperationCanceledException">Thrown when the operation is cancelled.</exception>
    public async Task RenderToRaw(
        Stream output,
        Template layoutTemplate,
        ObjectValue data,
        CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentNullException.ThrowIfNull(output);
        ArgumentNullException.ThrowIfNull(layoutTemplate);
        ArgumentNullException.ThrowIfNull(data);
        cancellationToken.ThrowIfCancellationRequested();

        var imageCache = _imageLoader is not null
            ? await PreloadImagesAsync(layoutTemplate, data, cancellationToken).ConfigureAwait(false)
            : null;

        try
        {
            // Measure returns the final size after rotation
            var size = Measure(layoutTemplate, data);
            using var bitmap = new SKBitmap((int)size.Width, (int)size.Height);

            RenderToBitmapCore(bitmap, layoutTemplate, data, default, imageCache);

            // Copy raw pixel bytes directly from the bitmap
            var pixels = bitmap.Bytes;
            await output.WriteAsync(pixels, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            if (imageCache is not null)
            {
                foreach (var bmp in imageCache.Values)
                    bmp.Dispose();
            }
        }
    }

    /// <summary>
    /// Measures template size without rendering.
    /// </summary>
    /// <param name="layoutTemplate">The template to measure.</param>
    /// <param name="data">The data context for template expressions.</param>
    /// <param name="cancellationToken">Cancellation token for async operation.</param>
    /// <returns>The size of the template in pixels.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="layoutTemplate"/> or <paramref name="data"/> is null.</exception>
    /// <exception cref="OperationCanceledException">Thrown when the operation is cancelled.</exception>
    public Task<SKSize> Measure(
        Template layoutTemplate,
        ObjectValue data,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(Measure(layoutTemplate, data));
    }

    /// <summary>
    /// Collects all unique image URIs from the processed template element tree.
    /// </summary>
    private static HashSet<string> CollectImageUris(Template template)
    {
        var uris = new HashSet<string>(StringComparer.Ordinal);
        CollectImageUrisFromElements(template.Elements, uris);
        return uris;
    }

    private static void CollectImageUrisFromElements(IReadOnlyList<TemplateElement> elements, HashSet<string> uris)
    {
        foreach (var element in elements)
        {
            if (element is ImageElement image && !string.IsNullOrEmpty(image.Src))
            {
                uris.Add(image.Src);
            }
            else if (element is FlexElement flex)
            {
                CollectImageUrisFromElements(flex.Children, uris);
            }
        }
    }

    /// <summary>
    /// Pre-loads all images from the template asynchronously using the image loader.
    /// </summary>
    private async Task<Dictionary<string, SKBitmap>> PreloadImagesAsync(
        Template template,
        ObjectValue data,
        CancellationToken cancellationToken)
    {
        var expandedTemplate = _expander.Expand(template, data);
        var processedTemplate = ProcessTemplate(expandedTemplate, data);
        var uris = CollectImageUris(processedTemplate);
        var cache = new Dictionary<string, SKBitmap>(uris.Count, StringComparer.Ordinal);

        foreach (var uri in uris)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var bitmap = await _imageLoader!.Load(uri, cancellationToken).ConfigureAwait(false);
            if (bitmap is not null)
            {
                cache[uri] = bitmap;
            }
        }

        return cache;
    }

    /// <summary>
    /// Disposes the renderer and releases resources.
    /// </summary>
    public void Dispose()
    {
        if (_disposed)
            return;

        _fontManager.Dispose();
        _disposed = true;
    }

    /// <summary>
    /// Asynchronously disposes the renderer and releases resources.
    /// </summary>
    /// <returns>A value task representing the asynchronous dispose operation.</returns>
    public ValueTask DisposeAsync()
    {
        Dispose();
        return ValueTask.CompletedTask;
    }
}
