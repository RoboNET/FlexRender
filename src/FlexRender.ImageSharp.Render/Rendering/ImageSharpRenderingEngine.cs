using FlexRender.Configuration;
using FlexRender.ImageSharp.Providers;
using FlexRender.Layout;
using FlexRender.Layout.Units;
using FlexRender.Parsing.Ast;
using FlexRender.Providers;
using FlexRender.Rendering;
using FlexRender.TemplateEngine;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Drawing;
using SixLabors.ImageSharp.Drawing.Processing;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

namespace FlexRender.ImageSharp.Rendering;

/// <summary>
/// Core rendering pipeline: renders layout trees to ImageSharp images.
/// Handles recursive node traversal, element drawing, and template processing.
/// </summary>
internal sealed class ImageSharpRenderingEngine
{
    private readonly ImageSharpTextRenderer _textRenderer;
    private readonly ImageSharpFontManager _fontManager;
    private readonly ImageSharpPreprocessor _preprocessor;
    private readonly ResourceLimits _limits;
    private readonly FlexRenderOptions? _options;
    private readonly float _baseFontSize;
    private readonly IImageSharpContentProvider<QrElement>? _qrProvider;
    private readonly IImageSharpContentProvider<BarcodeElement>? _barcodeProvider;

    /// <summary>
    /// Initializes a new instance of the rendering engine.
    /// </summary>
    /// <param name="textRenderer">The text renderer for drawing text elements.</param>
    /// <param name="fontManager">The font manager for font loading and registration.</param>
    /// <param name="limits">The resource limits to enforce.</param>
    /// <param name="baseFontSize">The base font size in pixels.</param>
    /// <param name="qrProvider">Optional QR code provider for ImageSharp rendering.</param>
    /// <param name="barcodeProvider">Optional barcode provider for ImageSharp rendering.</param>
    /// <param name="options">Optional rendering configuration options for path resolution.</param>
    internal ImageSharpRenderingEngine(
        ImageSharpTextRenderer textRenderer,
        ImageSharpFontManager fontManager,
        ResourceLimits limits,
        float baseFontSize,
        IImageSharpContentProvider<QrElement>? qrProvider = null,
        IImageSharpContentProvider<BarcodeElement>? barcodeProvider = null,
        FlexRenderOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(textRenderer);
        ArgumentNullException.ThrowIfNull(fontManager);
        ArgumentNullException.ThrowIfNull(limits);
        _textRenderer = textRenderer;
        _fontManager = fontManager;
        _preprocessor = new ImageSharpPreprocessor(fontManager, options);
        _limits = limits;
        _options = options;
        _baseFontSize = baseFontSize;
        _qrProvider = qrProvider;
        _barcodeProvider = barcodeProvider;
    }

    /// <summary>
    /// Renders a template to a new Image&lt;Rgba32&gt;.
    /// </summary>
    /// <param name="template">The template to render.</param>
    /// <param name="data">The data context for expression evaluation.</param>
    /// <param name="filterRegistry">Optional filter registry.</param>
    /// <param name="imageCache">
    /// Optional pre-loaded image cache for HTTP and other async sources.
    /// When provided, images are resolved from the cache before falling back to
    /// inline loading (file and base64 only).
    /// </param>
    /// <param name="preprocessedTemplate">
    /// Optional pre-processed template from image preloading. When provided, the
    /// expand+preprocess steps are skipped to avoid redundant work.
    /// </param>
    /// <returns>A new image containing the rendered template. Caller owns disposal.</returns>
    internal Image<Rgba32> RenderToImage(
        Template template,
        ObjectValue data,
        FilterRegistry? filterRegistry = null,
        IReadOnlyDictionary<string, Image<Rgba32>>? imageCache = null,
        Template? preprocessedTemplate = null)
    {
        ArgumentNullException.ThrowIfNull(template);
        ArgumentNullException.ThrowIfNull(data);

        Template processedTemplate;

        if (preprocessedTemplate is not null)
        {
            processedTemplate = preprocessedTemplate;
        }
        else
        {
            // Expand, resolve, and materialize template via the Core pipeline
            var expander = filterRegistry is not null
                ? new TemplateExpander(_limits, filterRegistry)
                : new TemplateExpander(_limits);
            var templateProcessor = filterRegistry is not null
                ? new TemplateProcessor(_limits, filterRegistry)
                : new TemplateProcessor(_limits);

            var pipeline = new TemplatePipeline(expander, templateProcessor);
            processedTemplate = pipeline.Process(template, data);
        }

        // Register fonts from the processed template (backend-specific)
        _preprocessor.RegisterFonts(processedTemplate);

        // Compute layout
        var layoutEngine = new LayoutEngine(_limits);
        var textShaper = new ImageSharpTextShaper(_fontManager);
        layoutEngine.TextShaper = textShaper;
        layoutEngine.BaseFontSize = _baseFontSize;
        var rootNode = layoutEngine.ComputeLayout(processedTemplate);

        // Create image
        var width = Math.Max(1, (int)rootNode.Width);
        var height = Math.Max(1, (int)rootNode.Height);
        var image = new Image<Rgba32>(width, height);

        image.Mutate(ctx =>
        {
            // Draw canvas background
            var bgColor = ImageSharpColorParser.Parse(processedTemplate.Canvas.Background.Value);
            ctx.Fill(bgColor, new RectangleF(0, 0, width, height));

            // Render layout tree
            foreach (var child in rootNode.Children)
            {
                RenderNode(ctx, child, 0, 0, imageCache, depth: 0);
            }
        });

        return image;
    }

    /// <summary>
    /// Collects all unique image URIs from a template's element tree.
    /// Used for pre-loading images asynchronously before synchronous rendering.
    /// </summary>
    /// <param name="template">The template to collect URIs from.</param>
    /// <returns>A set of unique image URIs found in the template.</returns>
    internal static HashSet<string> CollectImageUris(Template template)
    {
        ArgumentNullException.ThrowIfNull(template);
        var uris = new HashSet<string>(StringComparer.Ordinal);
        CollectImageUrisFromElements(template.Elements, uris);
        return uris;
    }

    /// <summary>
    /// Recursively collects image URIs from a list of template elements.
    /// </summary>
    /// <param name="elements">The elements to scan for image sources.</param>
    /// <param name="uris">The set to add discovered URIs to.</param>
    private static void CollectImageUrisFromElements(
        IReadOnlyList<TemplateElement> elements,
        HashSet<string> uris)
    {
        foreach (var element in elements)
        {
            if (element is ImageElement image && !string.IsNullOrEmpty(image.Src.Value))
            {
                uris.Add(image.Src.Value);
            }
            else if (element is FlexElement flex)
            {
                CollectImageUrisFromElements(flex.Children, uris);
            }
        }
    }

    private void RenderNode(
        IImageProcessingContext ctx,
        LayoutNode node,
        float offsetX,
        float offsetY,
        IReadOnlyDictionary<string, Image<Rgba32>>? imageCache,
        int depth)
    {
        if (depth > _limits.MaxRenderDepth)
        {
            throw new InvalidOperationException(
                $"Maximum render depth ({_limits.MaxRenderDepth}) exceeded. Template may be too deeply nested.");
        }

        if (node.Element.Display.Value == Display.None)
            return;

        var x = node.X + offsetX;
        var y = node.Y + offsetY;

        // Draw background
        if (!string.IsNullOrEmpty(node.Element.Background.Value))
        {
            DrawBackground(ctx, node.Element.Background.Value, x, y, node.Width, node.Height, node.Element);
        }

        // Draw element-specific content
        DrawElement(ctx, node.Element, x, y, node.Width, node.Height, imageCache);

        // Recursively render children
        foreach (var child in node.Children)
        {
            RenderNode(ctx, child, x, y, imageCache, depth + 1);
        }
    }

    private void DrawElement(
        IImageProcessingContext ctx,
        TemplateElement element,
        float x,
        float y,
        float width,
        float height,
        IReadOnlyDictionary<string, Image<Rgba32>>? imageCache)
    {
        var rotation = RotationHelper.ParseRotation(element.Rotate.Value);

        if (RotationHelper.HasRotation(rotation))
        {
            DrawWithRotation(ctx, x, y, width, height, rotation, bufferCtx =>
            {
                DrawElementContent(bufferCtx, element, 0, 0, width, height, imageCache);
            });
        }
        else
        {
            DrawElementContent(ctx, element, x, y, width, height, imageCache);
        }
    }

    /// <summary>
    /// Draws the element-specific content (text, image, QR, barcode, separator) at the given position.
    /// </summary>
    /// <param name="ctx">The image processing context to draw on.</param>
    /// <param name="element">The template element to draw.</param>
    /// <param name="x">X position of the element.</param>
    /// <param name="y">Y position of the element.</param>
    /// <param name="width">Width of the element.</param>
    /// <param name="height">Height of the element.</param>
    /// <param name="imageCache">Optional pre-loaded image cache.</param>
    private void DrawElementContent(
        IImageProcessingContext ctx,
        TemplateElement element,
        float x,
        float y,
        float width,
        float height,
        IReadOnlyDictionary<string, Image<Rgba32>>? imageCache)
    {
        switch (element)
        {
            case TextElement text:
                _textRenderer.DrawText(ctx, text, x, y, width, height, _baseFontSize);
                break;

            case SeparatorElement separator:
                DrawSeparator(ctx, separator, x, y, width, height);
                break;

            case FlexElement:
                // Container -- children are rendered by RenderNode recursion
                break;

            case ImageElement image:
                DrawImage(ctx, image, x, y, width, height, imageCache);
                break;

            case QrElement:
                if (_qrProvider is not null)
                {
                    DrawProviderImage(ctx, _qrProvider, (QrElement)element, x, y, width, height);
                }
                break;

            case BarcodeElement:
                if (_barcodeProvider is not null)
                {
                    DrawProviderImage(ctx, _barcodeProvider, (BarcodeElement)element, x, y, width, height);
                }
                break;
        }
    }

    /// <summary>
    /// Renders content to a temporary offscreen buffer, applies rotation, and composites onto the main context.
    /// ImageSharp does not support save/restore transform stacks like Skia, so rotation is achieved by
    /// drawing into a buffer, rotating the buffer image, and then compositing it at the correct position.
    /// </summary>
    /// <param name="ctx">The main image processing context to composite onto.</param>
    /// <param name="x">X position of the element on the main canvas.</param>
    /// <param name="y">Y position of the element on the main canvas.</param>
    /// <param name="width">Width of the element before rotation.</param>
    /// <param name="height">Height of the element before rotation.</param>
    /// <param name="rotationDegrees">Rotation angle in degrees.</param>
    /// <param name="drawContent">Action that draws the element content into the buffer context at (0, 0).</param>
    private static void DrawWithRotation(
        IImageProcessingContext ctx,
        float x,
        float y,
        float width,
        float height,
        float rotationDegrees,
        Action<IImageProcessingContext> drawContent)
    {
        var w = Math.Max(1, (int)MathF.Ceiling(width));
        var h = Math.Max(1, (int)MathF.Ceiling(height));

        using var buffer = new Image<Rgba32>(w, h);
        buffer.Mutate(drawContent);
        buffer.Mutate(bctx => bctx.Rotate(rotationDegrees));

        // Center the rotated image at the original element center.
        // After rotation the buffer dimensions may change (e.g. 90 degrees swaps width/height,
        // arbitrary angles grow both dimensions). We place the rotated image so its center
        // coincides with the original element center.
        var centerX = x + width / 2f;
        var centerY = y + height / 2f;
        var drawX = centerX - buffer.Width / 2f;
        var drawY = centerY - buffer.Height / 2f;

        ctx.DrawImage(buffer, new Point((int)MathF.Round(drawX), (int)MathF.Round(drawY)), 1f);
    }

    private static void DrawProviderImage<TElement>(
        IImageProcessingContext ctx,
        IImageSharpContentProvider<TElement> provider,
        TElement element,
        float x,
        float y,
        float width,
        float height)
    {
        var targetWidth = Math.Max(1, (int)MathF.Round(width));
        var targetHeight = Math.Max(1, (int)MathF.Round(height));

        using var image = provider.GenerateImage(element, targetWidth, targetHeight);
        ctx.DrawImage(
            image,
            new Point((int)MathF.Round(x), (int)MathF.Round(y)),
            1f);
    }

    private static void DrawImage(
        IImageProcessingContext ctx,
        ImageElement image,
        float x,
        float y,
        float width,
        float height,
        IReadOnlyDictionary<string, Image<Rgba32>>? imageCache)
    {
        if (string.IsNullOrEmpty(image.Src.Value))
            return;

        try
        {
            using var generated = ImageSharpImageProvider.Generate(
                image,
                imageCache,
                layoutWidth: (int)MathF.Round(width),
                layoutHeight: (int)MathF.Round(height));

            ctx.DrawImage(
                generated,
                new Point((int)MathF.Round(x), (int)MathF.Round(y)),
                1f);
        }
        catch (Exception ex) when (
            ex is FileNotFoundException
            or ArgumentException
            or InvalidOperationException
            or IOException)
        {
            // Silently skip images that cannot be loaded, matching Skia behavior
        }
    }

    private static void DrawBackground(
        IImageProcessingContext ctx,
        string background,
        float x,
        float y,
        float width,
        float height,
        TemplateElement element)
    {
        var color = ImageSharpColorParser.Parse(background);
        var borderRadius = ResolveBorderRadius(element, width, height, 12f);

        if (borderRadius > 0f)
        {
            var path = BuildRoundedRectPath(x, y, width, height, borderRadius);
            ctx.Fill(color, path);
        }
        else
        {
            ctx.Fill(color, new RectangleF(x, y, width, height));
        }
    }

    private static void DrawSeparator(
        IImageProcessingContext ctx,
        SeparatorElement separator,
        float x,
        float y,
        float width,
        float height)
    {
        var color = ImageSharpColorParser.Parse(separator.Color.Value);

        if (separator.Orientation.Value == SeparatorOrientation.Horizontal)
        {
            var lineY = y + height / 2f;
            ctx.DrawLine(color, separator.Thickness.Value, new PointF(x, lineY), new PointF(x + width, lineY));
        }
        else
        {
            var lineX = x + width / 2f;
            ctx.DrawLine(color, separator.Thickness.Value, new PointF(lineX, y), new PointF(lineX, y + height));
        }
    }

    private static IPath BuildRoundedRectPath(float x, float y, float width, float height, float radius)
    {
        // Clamp radius to half the smallest dimension
        radius = Math.Min(radius, Math.Min(width, height) / 2f);

        var builder = new PathBuilder();
        builder.SetOrigin(new PointF(0, 0));

        // Top-left arc
        builder.AddArc(new PointF(x + radius, y + radius), radius, radius, 0, 180, 90);
        // Top edge
        builder.AddLine(new PointF(x + radius, y), new PointF(x + width - radius, y));
        // Top-right arc
        builder.AddArc(new PointF(x + width - radius, y + radius), radius, radius, 0, 270, 90);
        // Right edge
        builder.AddLine(new PointF(x + width, y + radius), new PointF(x + width, y + height - radius));
        // Bottom-right arc
        builder.AddArc(new PointF(x + width - radius, y + height - radius), radius, radius, 0, 0, 90);
        // Bottom edge
        builder.AddLine(new PointF(x + width - radius, y + height), new PointF(x + radius, y + height));
        // Bottom-left arc
        builder.AddArc(new PointF(x + radius, y + height - radius), radius, radius, 0, 90, 90);
        // Left edge
        builder.AddLine(new PointF(x, y + height - radius), new PointF(x, y + radius));

        builder.CloseFigure();
        return builder.Build();
    }

    private static float ResolveBorderRadius(TemplateElement element, float width, float height, float fontSize)
    {
        if (string.IsNullOrEmpty(element.BorderRadius.Value))
            return 0f;

        var unit = UnitParser.Parse(element.BorderRadius.Value);
        var resolved = unit.Resolve(Math.Min(width, height), fontSize) ?? 0f;
        return Math.Max(0f, Math.Min(resolved, Math.Min(width, height) / 2f));
    }

}
