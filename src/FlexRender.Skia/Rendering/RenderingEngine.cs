using FlexRender.Abstractions;
using FlexRender.Configuration;
using FlexRender.Layout;
using FlexRender.Layout.Units;
using FlexRender.Parsing.Ast;
using FlexRender.Providers;
using FlexRender.TemplateEngine;
using SkiaSharp;

namespace FlexRender.Rendering;

/// <summary>
/// Core rendering pipeline: renders layout trees to SkiaSharp canvases and bitmaps.
/// Handles recursive node traversal, element drawing, rotation, and image preloading.
/// </summary>
internal sealed class RenderingEngine
{
    private readonly TextRenderer _textRenderer;
    private readonly IContentProvider<QrElement>? _qrProvider;
    private readonly IContentProvider<BarcodeElement>? _barcodeProvider;
    private readonly IImageLoader? _imageLoader;
    private readonly TemplateExpander _expander;
    private readonly TemplatePreprocessor _preprocessor;
    private readonly LayoutEngine _layoutEngine;
    private readonly ResourceLimits _limits;
    private readonly float _baseFontSize;

    /// <summary>
    /// Initializes a new instance of the <see cref="RenderingEngine"/> class.
    /// </summary>
    /// <param name="textRenderer">The text renderer for drawing text elements.</param>
    /// <param name="qrProvider">Optional QR code content provider.</param>
    /// <param name="barcodeProvider">Optional barcode content provider.</param>
    /// <param name="imageLoader">Optional image loader for async pre-loading.</param>
    /// <param name="expander">The template expander for control flow expansion.</param>
    /// <param name="preprocessor">The template preprocessor for expression resolution.</param>
    /// <param name="layoutEngine">The layout engine for computing element positions.</param>
    /// <param name="limits">The resource limits to enforce.</param>
    /// <param name="baseFontSize">The base font size in pixels.</param>
    internal RenderingEngine(
        TextRenderer textRenderer,
        IContentProvider<QrElement>? qrProvider,
        IContentProvider<BarcodeElement>? barcodeProvider,
        IImageLoader? imageLoader,
        TemplateExpander expander,
        TemplatePreprocessor preprocessor,
        LayoutEngine layoutEngine,
        ResourceLimits limits,
        float baseFontSize)
    {
        ArgumentNullException.ThrowIfNull(textRenderer);
        ArgumentNullException.ThrowIfNull(expander);
        ArgumentNullException.ThrowIfNull(preprocessor);
        ArgumentNullException.ThrowIfNull(layoutEngine);
        ArgumentNullException.ThrowIfNull(limits);
        _textRenderer = textRenderer;
        _qrProvider = qrProvider;
        _barcodeProvider = barcodeProvider;
        _imageLoader = imageLoader;
        _expander = expander;
        _preprocessor = preprocessor;
        _layoutEngine = layoutEngine;
        _limits = limits;
        _baseFontSize = baseFontSize;
    }

    /// <summary>
    /// Core canvas rendering logic. Accepts an optional pre-loaded image cache
    /// so that async render paths can pass it through without storing mutable state
    /// on the renderer instance (thread safety).
    /// </summary>
    /// <param name="canvas">The canvas to render to.</param>
    /// <param name="template">The template to render.</param>
    /// <param name="data">The data for variable substitution.</param>
    /// <param name="offset">Optional offset for rendering position.</param>
    /// <param name="imageCache">Optional pre-loaded image cache.</param>
    /// <param name="renderOptions">Per-call rendering options.</param>
    internal void RenderToCanvas(
        SKCanvas canvas,
        Template template,
        ObjectValue data,
        SKPoint offset,
        IReadOnlyDictionary<string, SKBitmap>? imageCache,
        RenderOptions? renderOptions = null)
    {
        var effectiveRenderOptions = renderOptions ?? RenderOptions.Default;
        var expandedTemplate = _expander.Expand(template, data);
        var processedTemplate = _preprocessor.Process(expandedTemplate, data);
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
            RenderNode(canvas, child, 0, 0, imageCache, effectiveRenderOptions);
        }

        // Restore canvas state
        canvas.Restore();
    }

    /// <summary>
    /// Core bitmap rendering logic. Accepts an optional pre-loaded image cache
    /// so that async render paths can pass it through without storing mutable state
    /// on the renderer instance (thread safety).
    /// </summary>
    /// <param name="bitmap">The bitmap to render to.</param>
    /// <param name="template">The template to render.</param>
    /// <param name="data">The data for variable substitution.</param>
    /// <param name="offset">Optional offset for rendering position.</param>
    /// <param name="imageCache">Optional pre-loaded image cache.</param>
    /// <param name="renderOptions">Per-call rendering options.</param>
    internal void RenderToBitmapCore(
        SKBitmap bitmap,
        Template template,
        ObjectValue data,
        SKPoint offset,
        IReadOnlyDictionary<string, SKBitmap>? imageCache,
        RenderOptions? renderOptions = null)
    {
        var rotationDegrees = RotationHelper.ParseRotation(template.Canvas.Rotate);

        // If no rotation needed, render directly to bitmap
        if (!RotationHelper.HasRotation(rotationDegrees))
        {
            using var canvas = new SKCanvas(bitmap);
            RenderToCanvas(canvas, template, data, offset, imageCache, renderOptions);
            return;
        }

        // Render to temporary bitmap first, then rotate
        var expandedTemplate = _expander.Expand(template, data);
        var processedTemplate = _preprocessor.Process(expandedTemplate, data);
        var rootNode = _layoutEngine.ComputeLayout(processedTemplate);
        var originalWidth = (int)rootNode.Width;
        var originalHeight = (int)rootNode.Height;

        using var tempBitmap = new SKBitmap(originalWidth, originalHeight);
        using (var tempCanvas = new SKCanvas(tempBitmap))
        {
            RenderToCanvas(tempCanvas, template, data, offset, imageCache, renderOptions);
        }

        // Rotate the bitmap
        using var rotatedBitmap = RotateBitmap(tempBitmap, rotationDegrees);

        // Draw rotated result onto the target bitmap
        using var targetCanvas = new SKCanvas(bitmap);
        targetCanvas.DrawBitmap(rotatedBitmap, 0, 0);
    }

    /// <summary>
    /// Recursively renders a layout node and its children.
    /// </summary>
    /// <param name="canvas">The canvas to render to.</param>
    /// <param name="node">The layout node to render.</param>
    /// <param name="offsetX">X offset from parent.</param>
    /// <param name="offsetY">Y offset from parent.</param>
    /// <param name="imageCache">Optional pre-loaded image cache for thread-safe rendering.</param>
    /// <param name="renderOptions">Per-call rendering options.</param>
    /// <param name="depth">Current recursion depth.</param>
    /// <exception cref="InvalidOperationException">Thrown when maximum render depth is exceeded.</exception>
    private void RenderNode(
        SKCanvas canvas,
        LayoutNode node,
        float offsetX,
        float offsetY,
        IReadOnlyDictionary<string, SKBitmap>? imageCache,
        RenderOptions renderOptions,
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
        DrawElement(canvas, node.Element, x, y, node.Width, node.Height, imageCache, renderOptions, node.Direction);

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
            RenderNode(canvas, child, x, y, imageCache, renderOptions, depth + 1);
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
    /// <param name="renderOptions">Per-call rendering options.</param>
    /// <param name="direction">Text direction for this element.</param>
    private void DrawElement(
        SKCanvas canvas,
        TemplateElement element,
        float x,
        float y,
        float width,
        float height,
        IReadOnlyDictionary<string, SKBitmap>? imageCache,
        RenderOptions renderOptions,
        TextDirection direction = TextDirection.Ltr)
    {
        // Resolve border-radius for background clipping and border rendering
        var borderRadius = ResolveBorderRadius(element, width, height, _baseFontSize);

        // Draw background if specified
        if (!string.IsNullOrEmpty(element.Background))
        {
            var bgColor = ColorParser.Parse(element.Background);
            using var bgPaint = new SKPaint { Color = bgColor };
            if (borderRadius > 0f)
            {
                canvas.DrawRoundRect(x, y, width, height, borderRadius, borderRadius, bgPaint);
            }
            else
            {
                canvas.DrawRect(x, y, width, height, bgPaint);
            }
        }

        // Draw borders between background and content
        DrawBorders(canvas, element, x, y, width, height, borderRadius, _baseFontSize);

        switch (element)
        {
            case TextElement text:
                var bounds = new SKRect(x, y, x + width, y + height);
                _textRenderer.DrawText(canvas, text, bounds, _baseFontSize, renderOptions, direction);
                break;

            case QrElement qr when _qrProvider is not null:
                using (var bitmap = _qrProvider.Generate(qr))
                {
                    DrawBitmapWithRotation(canvas, bitmap, element, x, y, width, height);
                }
                break;

            case BarcodeElement barcode when _barcodeProvider is not null:
                using (var bitmap = _barcodeProvider.Generate(barcode))
                {
                    DrawBitmapWithRotation(canvas, bitmap, element, x, y, width, height);
                }
                break;

            case ImageElement image:
                using (var bitmap = ImageProvider.Generate(
                    image,
                    imageCache,
                    renderOptions.Antialiasing,
                    layoutWidth: (int)width,
                    layoutHeight: (int)height))
                {
                    DrawBitmapWithRotation(canvas, bitmap, element, x, y, width, height);
                }
                break;

            case SeparatorElement separator:
                DrawSeparator(canvas, separator, x, y, width, height, renderOptions.Antialiasing);
                break;

            case FlexElement:
                // FlexElement is a container, children are rendered via RenderNode recursion
                break;
        }
    }

    /// <summary>
    /// Draws a bitmap on the canvas, applying rotation from the element's <see cref="TemplateElement.Rotate"/> property.
    /// If no rotation is specified, the bitmap is drawn directly at (<paramref name="x"/>, <paramref name="y"/>).
    /// </summary>
    /// <param name="canvas">The canvas to draw on.</param>
    /// <param name="bitmap">The bitmap to draw.</param>
    /// <param name="element">The element whose rotation property is used.</param>
    /// <param name="x">X position.</param>
    /// <param name="y">Y position.</param>
    /// <param name="width">Element width (used to compute rotation center).</param>
    /// <param name="height">Element height (used to compute rotation center).</param>
    private static void DrawBitmapWithRotation(
        SKCanvas canvas,
        SKBitmap bitmap,
        TemplateElement element,
        float x,
        float y,
        float width,
        float height)
    {
        var rotation = RotationHelper.ParseRotation(element.Rotate);

        if (RotationHelper.HasRotation(rotation))
        {
            canvas.Save();
            var centerX = x + width / 2f;
            var centerY = y + height / 2f;
            canvas.RotateDegrees(rotation, centerX, centerY);
        }

        canvas.DrawBitmap(bitmap, x, y);

        if (RotationHelper.HasRotation(rotation))
        {
            canvas.Restore();
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
    /// <param name="antialiasing">Whether to enable antialiasing for the separator line.</param>
    internal static void DrawSeparator(SKCanvas canvas, SeparatorElement separator, float x, float y, float width, float height, bool antialiasing = true)
    {
        var color = ColorParser.Parse(separator.Color);

        using var paint = new SKPaint
        {
            Color = color,
            StrokeWidth = separator.Thickness,
            Style = SKPaintStyle.Stroke,
            IsAntialias = antialiasing
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
    internal static SKBitmap RotateBitmap(SKBitmap source, float degrees)
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

    /// <summary>
    /// Collects all unique image URIs from the processed template element tree.
    /// </summary>
    /// <param name="template">The template to collect URIs from.</param>
    /// <returns>A set of unique image URIs.</returns>
    internal static HashSet<string> CollectImageUris(Template template)
    {
        var uris = new HashSet<string>(StringComparer.Ordinal);
        CollectImageUrisFromElements(template.Elements, uris);
        return uris;
    }

    /// <summary>
    /// Recursively collects image URIs from a list of elements.
    /// </summary>
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
    /// <param name="template">The template containing image references.</param>
    /// <param name="data">The data context for expression evaluation.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A dictionary mapping image URIs to loaded bitmaps.</returns>
    internal async Task<Dictionary<string, SKBitmap>> PreloadImagesAsync(
        Template template,
        ObjectValue data,
        CancellationToken cancellationToken)
    {
        var expandedTemplate = _expander.Expand(template, data);
        var processedTemplate = _preprocessor.Process(expandedTemplate, data);
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
    /// Resolves the border-radius for an element, clamped to half the smaller dimension.
    /// </summary>
    /// <param name="element">The element whose border-radius to resolve.</param>
    /// <param name="width">The element width for percentage resolution and clamping.</param>
    /// <param name="height">The element height for clamping.</param>
    /// <param name="fontSize">The base font size for em/rem resolution.</param>
    private static float ResolveBorderRadius(TemplateElement element, float width, float height, float fontSize)
    {
        if (string.IsNullOrEmpty(element.BorderRadius))
            return 0f;

        var unit = UnitParser.Parse(element.BorderRadius);
        var resolved = unit.Resolve(Math.Min(width, height), fontSize) ?? 0f;
        // Clamp to half the smaller dimension to avoid rendering artifacts
        return Math.Max(0f, Math.Min(resolved, Math.Min(width, height) / 2f));
    }

    /// <summary>
    /// Draws borders for an element. Called between background and content rendering.
    /// </summary>
    /// <param name="canvas">The canvas to draw on.</param>
    /// <param name="element">The element whose borders to draw.</param>
    /// <param name="x">X position.</param>
    /// <param name="y">Y position.</param>
    /// <param name="width">Element width.</param>
    /// <param name="height">Element height.</param>
    /// <param name="borderRadius">The resolved border-radius value.</param>
    /// <param name="fontSize">The base font size for em/rem resolution.</param>
    private static void DrawBorders(
        SKCanvas canvas,
        TemplateElement element,
        float x,
        float y,
        float width,
        float height,
        float borderRadius,
        float fontSize)
    {
        var border = BorderParser.Resolve(element, width, fontSize);
        if (!border.HasVisibleBorder)
            return;

        if (AllSidesEqual(border))
        {
            DrawUniformBorder(canvas, border.Top, x, y, width, height, borderRadius);
        }
        else
        {
            DrawPerSideBorders(canvas, border, x, y, width, height);
        }
    }

    /// <summary>
    /// Checks whether all four border sides have the same width, style, and color.
    /// </summary>
    private static bool AllSidesEqual(BorderValues border)
    {
        return border.Top == border.Right
            && border.Right == border.Bottom
            && border.Bottom == border.Left;
    }

    /// <summary>
    /// Draws a uniform border (all sides identical) as a single stroked rect.
    /// </summary>
    private static void DrawUniformBorder(
        SKCanvas canvas,
        BorderSide side,
        float x,
        float y,
        float width,
        float height,
        float borderRadius)
    {
        if (!side.IsVisible)
            return;

        using var paint = new SKPaint
        {
            Color = ColorParser.Parse(side.Color),
            StrokeWidth = side.Width,
            Style = SKPaintStyle.Stroke,
            IsAntialias = true
        };

        ApplyBorderDashPattern(paint, side);

        // Inset by half stroke width (SkiaSharp draws stroke centered on path)
        var half = side.Width / 2f;
        if (borderRadius > 0f)
        {
            canvas.DrawRoundRect(x + half, y + half, width - side.Width, height - side.Width, borderRadius, borderRadius, paint);
        }
        else
        {
            canvas.DrawRect(x + half, y + half, width - side.Width, height - side.Width, paint);
        }
    }

    /// <summary>
    /// Draws per-side borders when sides have different styles/widths/colors.
    /// </summary>
    private static void DrawPerSideBorders(
        SKCanvas canvas,
        BorderValues border,
        float x,
        float y,
        float width,
        float height)
    {
        // Top border
        if (border.Top.IsVisible)
        {
            DrawBorderLine(canvas, border.Top,
                x, y + border.Top.Width / 2f,
                x + width, y + border.Top.Width / 2f);
        }

        // Right border
        if (border.Right.IsVisible)
        {
            DrawBorderLine(canvas, border.Right,
                x + width - border.Right.Width / 2f, y,
                x + width - border.Right.Width / 2f, y + height);
        }

        // Bottom border
        if (border.Bottom.IsVisible)
        {
            DrawBorderLine(canvas, border.Bottom,
                x, y + height - border.Bottom.Width / 2f,
                x + width, y + height - border.Bottom.Width / 2f);
        }

        // Left border
        if (border.Left.IsVisible)
        {
            DrawBorderLine(canvas, border.Left,
                x + border.Left.Width / 2f, y,
                x + border.Left.Width / 2f, y + height);
        }
    }

    /// <summary>
    /// Draws a single border line with the given side's style.
    /// </summary>
    private static void DrawBorderLine(SKCanvas canvas, BorderSide side, float x1, float y1, float x2, float y2)
    {
        using var paint = new SKPaint
        {
            Color = ColorParser.Parse(side.Color),
            StrokeWidth = side.Width,
            Style = SKPaintStyle.Stroke,
            IsAntialias = true
        };

        ApplyBorderDashPattern(paint, side);
        canvas.DrawLine(x1, y1, x2, y2, paint);
    }

    /// <summary>
    /// Applies dash/dot pattern to paint based on border line style.
    /// </summary>
    private static void ApplyBorderDashPattern(SKPaint paint, BorderSide side)
    {
        switch (side.Style)
        {
            case BorderLineStyle.Dotted:
                paint.PathEffect = SKPathEffect.CreateDash(
                    new[] { side.Width, side.Width * 2 }, 0);
                break;
            case BorderLineStyle.Dashed:
                paint.PathEffect = SKPathEffect.CreateDash(
                    new[] { side.Width * 4, side.Width * 2 }, 0);
                break;
        }
    }
}
