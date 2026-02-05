using FlexRender.Layout.Units;
using FlexRender.Parsing.Ast;

namespace FlexRender.Layout;

/// <summary>
/// Computes layout positions and sizes for template elements.
/// </summary>
public sealed class LayoutEngine
{
    private const float DefaultFontSize = 16f;
    private const float DefaultTextHeight = 20f;
    private const float LineHeightMultiplier = 1.4f;

    /// <summary>
    /// Optional delegate for measuring text dimensions.
    /// When set, text elements use measured content width instead of container width.
    /// The delegate receives a <see cref="TextElement"/>, its computed font size, and
    /// a maximum width constraint, and returns the measured size of the text content.
    /// </summary>
    public Func<TextElement, float, float, LayoutSize>? TextMeasurer { get; set; }

    /// <summary>
    /// Base font size in pixels used for em resolution and as fallback when text elements
    /// don't specify an explicit size. Must match the renderer's base font size for
    /// consistent layout-to-render results.
    /// Defaults to <see cref="DefaultFontSize"/> (16px).
    /// </summary>
    public float BaseFontSize { get; set; } = DefaultFontSize;

    /// <summary>
    /// Computes the layout for a template.
    /// </summary>
    /// <param name="template">The template to lay out.</param>
    /// <returns>The root layout node with computed positions and sizes.</returns>
    public LayoutNode ComputeLayout(Template template)
    {
        var canvas = template.Canvas;
        // When dimension is not fixed, use a large value for layout calculations
        // to allow content to determine its natural size
        const float UnconstrainedSize = 10000f;

        // Pass 1: Measure intrinsic sizes bottom-up
        // Measure each top-level element independently to avoid mutating template.Elements
        var allSizes = new Dictionary<TemplateElement, IntrinsicSize>(ReferenceEqualityComparer.Instance);
        var childIntrinsics = new List<IntrinsicSize>(template.Elements.Count);
        foreach (var element in template.Elements)
        {
            var elementSizes = MeasureAllIntrinsics(element);
            foreach (var kvp in elementSizes)
                allSizes[kvp.Key] = kvp.Value;
            if (elementSizes.TryGetValue(element, out var intrinsic))
                childIntrinsics.Add(intrinsic);
        }

        // Aggregate root intrinsic as column layout
        IntrinsicSize rootIntrinsic;
        if (childIntrinsics.Count > 0)
        {
            var maxMinWidth = 0f;
            var maxMaxWidth = 0f;
            var sumMinHeight = 0f;
            var sumMaxHeight = 0f;
            foreach (var c in childIntrinsics)
            {
                if (c.MinWidth > maxMinWidth) maxMinWidth = c.MinWidth;
                if (c.MaxWidth > maxMaxWidth) maxMaxWidth = c.MaxWidth;
                sumMinHeight += c.MinHeight;
                sumMaxHeight += c.MaxHeight;
            }
            rootIntrinsic = new IntrinsicSize(maxMinWidth, maxMaxWidth, sumMinHeight, sumMaxHeight);
        }
        else
        {
            rootIntrinsic = new IntrinsicSize();
        }
        var intrinsicSizes = allSizes;

        var width = canvas.Fixed switch
        {
            FixedDimension.Width => canvas.Width,
            FixedDimension.Both => canvas.Width,
            FixedDimension.Height => rootIntrinsic.MaxWidth > 0 ? rootIntrinsic.MaxWidth : UnconstrainedSize,
            FixedDimension.None => rootIntrinsic.MaxWidth > 0 ? rootIntrinsic.MaxWidth : UnconstrainedSize,
            _ => canvas.Width
        };

        var height = canvas.Fixed switch
        {
            FixedDimension.Height => canvas.Height,
            FixedDimension.Both => canvas.Height,
            _ => 0f // Will be calculated from content
        };

        var contextHeight = canvas.Fixed switch
        {
            FixedDimension.Height => canvas.Height,
            FixedDimension.Both => canvas.Height,
            _ => UnconstrainedSize // Keep existing behavior for width-only and none
        };

        if (canvas.Fixed is FixedDimension.Both or FixedDimension.Height && canvas.Height <= 0)
        {
            throw new InvalidOperationException(
                "Canvas height must be specified when fixed dimension includes height. " +
                "Set 'height' in the canvas section.");
        }

        // Create root node representing the canvas
        var rootElement = new FlexElement { Direction = FlexDirection.Column };
        var root = new LayoutNode(rootElement, 0, 0, width, height);

        var context = new LayoutContext(width, contextHeight, BaseFontSize, intrinsicSizes);

        // Process top-level elements
        foreach (var element in template.Elements)
        {
            var childNode = LayoutElement(element, context);
            root.AddChild(childNode);
        }

        // Apply full column flex layout to root (stretch, alignment, gaps)
        LayoutColumnFlex(root, rootElement, context, PaddingValues.Zero, 0f);

        // Calculate dimensions based on which dimension is fixed
        if (canvas.Fixed == FixedDimension.Width)
        {
            // Height is flexible - calculate from content
            root.Height = CalculateTotalHeight(root);
        }
        else if (canvas.Fixed == FixedDimension.Height)
        {
            // Width is flexible - calculate from content
            root.Width = CalculateTotalWidth(root);
        }
        else if (canvas.Fixed == FixedDimension.None)
        {
            // Both dimensions are flexible - calculate from content
            root.Width = CalculateTotalWidth(root);
            root.Height = CalculateTotalHeight(root);
        }
        // For FixedDimension.Both, keep both dimensions as set

        return root;
    }

    // ============================================
    // Intrinsic Measurement Pass
    // ============================================

    /// <summary>
    /// Measures intrinsic sizes for all elements in the tree bottom-up.
    /// This pass computes minimum and maximum content sizes without a layout context,
    /// enabling content-based sizing for containers.
    /// </summary>
    /// <param name="root">The root element to measure.</param>
    /// <returns>
    /// A dictionary mapping each element (by reference identity) to its computed
    /// <see cref="IntrinsicSize"/>.
    /// </returns>
    public Dictionary<TemplateElement, IntrinsicSize> MeasureAllIntrinsics(TemplateElement root)
    {
        var sizes = new Dictionary<TemplateElement, IntrinsicSize>(ReferenceEqualityComparer.Instance);
        MeasureIntrinsic(root, sizes);
        return sizes;
    }

    /// <summary>
    /// Dispatches intrinsic measurement to the appropriate element-specific method.
    /// </summary>
    /// <param name="element">The element to measure.</param>
    /// <param name="sizes">The accumulating dictionary of intrinsic sizes.</param>
    /// <returns>The computed intrinsic size for the element.</returns>
    private IntrinsicSize MeasureIntrinsic(TemplateElement element, Dictionary<TemplateElement, IntrinsicSize> sizes)
    {
        var intrinsic = element switch
        {
            TextElement text => MeasureTextIntrinsic(text),
            QrElement qr => MeasureQrIntrinsic(qr),
            BarcodeElement barcode => MeasureBarcodeIntrinsic(barcode),
            ImageElement image => MeasureImageIntrinsic(image),
            SeparatorElement separator => MeasureSeparatorIntrinsic(separator),
            FlexElement flex => MeasureFlexIntrinsic(flex, sizes),
            _ => new IntrinsicSize(0f, 0f, 0f, 0f)
        };

        sizes[element] = intrinsic;
        return intrinsic;
    }

    /// <summary>
    /// Measures intrinsic size for a text element.
    /// When <see cref="TextMeasurer"/> is set and the element has content, the delegate
    /// is used to compute content width and height from actual font metrics.
    /// Otherwise, width defaults to 0 (unknown) and height to fontSize * <see cref="LineHeightMultiplier"/>.
    /// Padding and margin are added to the result.
    /// </summary>
    /// <param name="text">The text element to measure.</param>
    /// <returns>The computed intrinsic size.</returns>
    private IntrinsicSize MeasureTextIntrinsic(TextElement text)
    {
        var fontSize = FontSizeResolver.Resolve(text.Size, BaseFontSize);

        float contentWidth;
        float contentHeight;

        if (TextMeasurer != null && !string.IsNullOrEmpty(text.Content))
        {
            var measured = TextMeasurer(text, fontSize, float.MaxValue);
            contentWidth = !string.IsNullOrEmpty(text.Width)
                ? ParseAbsolutePixelValue(text.Width, measured.Width)
                : measured.Width;
            contentHeight = !string.IsNullOrEmpty(text.Height)
                ? ParseAbsolutePixelValue(text.Height, measured.Height)
                : measured.Height;
        }
        else
        {
            contentWidth = ParseAbsolutePixelValue(text.Width, 0f);
            var lineHeight = LineHeightResolver.Resolve(text.LineHeight, fontSize, fontSize * LineHeightMultiplier);
            contentHeight = !string.IsNullOrEmpty(text.Height)
                ? ParseAbsolutePixelValue(text.Height, lineHeight)
                : lineHeight;
        }

        var intrinsic = new IntrinsicSize(contentWidth, contentWidth, contentHeight, contentHeight);

        return ApplyPaddingAndMargin(intrinsic, text.Padding, text.Margin);
    }

    /// <summary>
    /// Measures intrinsic size for a QR code element.
    /// Uses <see cref="QrElement.Size"/> for both width and height (square).
    /// Padding and margin are added to the result.
    /// </summary>
    /// <param name="qr">The QR element to measure.</param>
    /// <returns>The computed intrinsic size.</returns>
    private static IntrinsicSize MeasureQrIntrinsic(QrElement qr)
    {
        float size = qr.Size;
        var intrinsic = new IntrinsicSize(size, size, size, size);

        return ApplyPaddingAndMargin(intrinsic, qr.Padding, qr.Margin);
    }

    /// <summary>
    /// Measures intrinsic size for a barcode element.
    /// Uses <see cref="BarcodeElement.BarcodeWidth"/> and <see cref="BarcodeElement.BarcodeHeight"/>.
    /// Padding and margin are added to the result.
    /// </summary>
    /// <param name="barcode">The barcode element to measure.</param>
    /// <returns>The computed intrinsic size.</returns>
    private static IntrinsicSize MeasureBarcodeIntrinsic(BarcodeElement barcode)
    {
        float width = barcode.BarcodeWidth;
        float height = barcode.BarcodeHeight;
        var intrinsic = new IntrinsicSize(width, width, height, height);

        return ApplyPaddingAndMargin(intrinsic, barcode.Padding, barcode.Margin);
    }

    /// <summary>
    /// Measures intrinsic size for an image element.
    /// Uses <see cref="ImageElement.ImageWidth"/> and <see cref="ImageElement.ImageHeight"/> if set;
    /// otherwise defaults to 0 (unknown image dimensions at layout time).
    /// Padding and margin are added to the result.
    /// </summary>
    /// <param name="image">The image element to measure.</param>
    /// <returns>The computed intrinsic size.</returns>
    private static IntrinsicSize MeasureImageIntrinsic(ImageElement image)
    {
        float width = image.ImageWidth ?? 0f;
        float height = image.ImageHeight ?? 0f;
        var intrinsic = new IntrinsicSize(width, width, height, height);

        return ApplyPaddingAndMargin(intrinsic, image.Padding, image.Margin);
    }

    /// <summary>
    /// Measures intrinsic size for a separator element.
    /// Horizontal separators have zero intrinsic width (stretch to parent) and
    /// height equal to thickness. Vertical separators have the inverse.
    /// Padding and margin are added to the result.
    /// </summary>
    /// <param name="separator">The separator element to measure.</param>
    /// <returns>The computed intrinsic size.</returns>
    private static IntrinsicSize MeasureSeparatorIntrinsic(SeparatorElement separator)
    {
        var intrinsic = separator.Orientation == SeparatorOrientation.Horizontal
            ? new IntrinsicSize(0f, 0f, separator.Thickness, separator.Thickness)
            : new IntrinsicSize(separator.Thickness, separator.Thickness, 0f, 0f);

        return ApplyPaddingAndMargin(intrinsic, separator.Padding, separator.Margin);
    }

    /// <summary>
    /// Measures intrinsic size for a flex container element.
    /// Aggregates children sizes based on flex direction:
    /// Column layout sums heights and takes max width; Row layout sums widths and takes max height.
    /// Padding, margin, gap, and explicit dimension overrides are applied.
    /// </summary>
    /// <param name="flex">The flex container element to measure.</param>
    /// <param name="sizes">The accumulating dictionary of intrinsic sizes.</param>
    /// <returns>The computed intrinsic size for the flex container.</returns>
    private IntrinsicSize MeasureFlexIntrinsic(FlexElement flex, Dictionary<TemplateElement, IntrinsicSize> sizes)
    {
        var padding = PaddingParser.ParseAbsolute(flex.Padding).ClampNegatives();
        var margin = ParseAbsolutePixelValue(flex.Margin, 0f);
        if (margin < 0) margin = 0;
        var gap = ParseAbsolutePixelValue(flex.Gap, 0f);

        // Measure all children first (bottom-up)
        var childCount = flex.Children.Count;
        if (childCount == 0)
        {
            var empty = new IntrinsicSize();
            empty = empty.WithPadding(padding);
            empty = empty.WithMargin(margin);

            if (!string.IsNullOrEmpty(flex.Width))
            {
                var w = ParseAbsolutePixelValue(flex.Width, 0f);
                empty = new IntrinsicSize(w, w, empty.MinHeight, empty.MaxHeight);
            }
            if (!string.IsNullOrEmpty(flex.Height))
            {
                var h = ParseAbsolutePixelValue(flex.Height, 0f);
                empty = new IntrinsicSize(empty.MinWidth, empty.MaxWidth, h, h);
            }

            sizes[flex] = empty;
            return empty;
        }

        var childSizes = new IntrinsicSize[childCount];
        for (var i = 0; i < childCount; i++)
        {
            childSizes[i] = MeasureIntrinsic(flex.Children[i], sizes);
        }

        var totalGaps = gap * Math.Max(0, childCount - 1);

        float minWidth, maxWidth, minHeight, maxHeight;

        if (flex.Direction == FlexDirection.Column)
        {
            // Column: maxWidth = max of children, minWidth = max of children (container must fit widest child), height = sum + gaps
            minWidth = 0f;
            maxWidth = 0f;
            minHeight = totalGaps;
            maxHeight = totalGaps;
            foreach (var c in childSizes)
            {
                if (c.MinWidth > minWidth) minWidth = c.MinWidth;
                if (c.MaxWidth > maxWidth) maxWidth = c.MaxWidth;
                minHeight += c.MinHeight;
                maxHeight += c.MaxHeight;
            }
        }
        else
        {
            // Row: width = sum of children + gaps, height = max of children
            minWidth = totalGaps;
            maxWidth = totalGaps;
            minHeight = 0f;
            maxHeight = 0f;
            foreach (var c in childSizes)
            {
                minWidth += c.MinWidth;
                maxWidth += c.MaxWidth;
                if (c.MinHeight > minHeight) minHeight = c.MinHeight;
                if (c.MaxHeight > maxHeight) maxHeight = c.MaxHeight;
            }
        }

        var result = new IntrinsicSize(minWidth, maxWidth, minHeight, maxHeight);
        result = result.WithPadding(padding);
        result = result.WithMargin(margin);

        // Override with explicit dimensions
        if (!string.IsNullOrEmpty(flex.Width))
        {
            var w = ParseAbsolutePixelValue(flex.Width, result.MaxWidth);
            result = new IntrinsicSize(w, w, result.MinHeight, result.MaxHeight);
        }
        if (!string.IsNullOrEmpty(flex.Height))
        {
            var h = ParseAbsolutePixelValue(flex.Height, result.MaxHeight);
            result = new IntrinsicSize(result.MinWidth, result.MaxWidth, h, h);
        }

        sizes[flex] = result;
        return result;
    }

    /// <summary>
    /// Parses a string unit value into an absolute pixel value.
    /// For percentage and em units, resolves against a parent size of 0 and <see cref="DefaultFontSize"/>
    /// respectively, since no layout context is available during the intrinsic measure pass.
    /// </summary>
    /// <param name="value">The string value to parse (e.g., "100", "16px", "1.5em").</param>
    /// <param name="defaultValue">The value to return if parsing fails or the value is null/empty.</param>
    /// <returns>The resolved pixel value.</returns>
    private static float ParseAbsolutePixelValue(string? value, float defaultValue)
    {
        if (string.IsNullOrWhiteSpace(value))
            return defaultValue;

        var unit = UnitParser.Parse(value);
        return unit.Resolve(0f, DefaultFontSize) ?? defaultValue;
    }

    /// <summary>
    /// Applies padding and margin to an intrinsic size measurement.
    /// Negative values are clamped to zero. Supports non-uniform padding via
    /// <see cref="PaddingParser.ParseAbsolute"/>.
    /// </summary>
    /// <param name="intrinsic">The base intrinsic size.</param>
    /// <param name="padding">The padding value string to parse.</param>
    /// <param name="margin">The margin value string to parse.</param>
    /// <returns>The intrinsic size with padding and margin applied.</returns>
    private static IntrinsicSize ApplyPaddingAndMargin(IntrinsicSize intrinsic, string? padding, string? margin)
    {
        var p = PaddingParser.ParseAbsolute(padding);
        if (p.Horizontal > 0f || p.Vertical > 0f)
        {
            intrinsic = intrinsic.WithPadding(p);
        }
        var m = ParseAbsolutePixelValue(margin, 0f);
        if (m > 0f) intrinsic = intrinsic.WithMargin(m);
        return intrinsic;
    }

    private LayoutNode LayoutElement(TemplateElement element, LayoutContext context)
    {
        return element switch
        {
            FlexElement flex => LayoutFlexElement(flex, context),
            TextElement text => LayoutTextElement(text, context),
            QrElement qr => LayoutQrElement(qr, context),
            BarcodeElement barcode => LayoutBarcodeElement(barcode, context),
            ImageElement image => LayoutImageElement(image, context),
            SeparatorElement separator => LayoutSeparatorElement(separator, context),
            _ => new LayoutNode(element, 0, 0, context.ContainerWidth, DefaultTextHeight)
        };
    }

    private LayoutNode LayoutFlexElement(FlexElement flex, LayoutContext context)
    {
        var width = context.ResolveWidth(flex.Width) ?? context.ContainerWidth;
        var height = context.ResolveHeight(flex.Height) ?? 0f;

        var node = new LayoutNode(flex, 0, 0, width, height);

        var padding = PaddingParser.Parse(flex.Padding, width, context.FontSize).ClampNegatives();
        var gap = UnitParser.Parse(flex.Gap).Resolve(width, context.FontSize) ?? 0f;

        // For inner context height: use explicit height minus padding, or 0 to indicate auto-sizing
        // This prevents propagating large "unconstrained" heights down to children
        var innerHeight = height > 0 ? height - padding.Vertical : 0f;
        var innerContext = context.WithSize(width - padding.Horizontal, innerHeight);

        // Layout children
        foreach (var child in flex.Children)
        {
            var childContext = innerContext;

            // For row flex, children without explicit width should use intrinsic content width
            // instead of full container width. This enables correct justify-content distribution.
            if (flex.Direction == FlexDirection.Row && !HasExplicitWidth(child))
            {
                var childIntrinsic = context.GetIntrinsicSize(child);
                if (childIntrinsic.HasValue && childIntrinsic.Value.MaxWidth > 0)
                {
                    childContext = innerContext.WithSize(
                        Math.Min(childIntrinsic.Value.MaxWidth, innerContext.ContainerWidth),
                        innerContext.ContainerHeight);
                }
            }

            var childNode = LayoutElement(child, childContext);
            node.AddChild(childNode);
        }

        // Apply flex layout
        if (flex.Direction == FlexDirection.Column)
        {
            LayoutColumnFlex(node, flex, innerContext, padding, gap);
        }
        else
        {
            LayoutRowFlex(node, flex, innerContext, padding, gap);
        }

        // Calculate height if not specified
        if (height == 0f && node.Children.Count > 0)
        {
            node.Height = CalculateTotalHeight(node) + padding.Bottom;
        }

        return node;
    }

    private LayoutNode LayoutTextElement(TextElement text, LayoutContext context)
    {
        var padding = PaddingParser.Parse(text.Padding, context.ContainerWidth, context.FontSize).ClampNegatives();
        var margin = UnitParser.Parse(text.Margin).Resolve(context.ContainerWidth, context.FontSize) ?? 0f;
        if (margin < 0) margin = 0;

        float contentWidth;
        if (!string.IsNullOrEmpty(text.Width))
        {
            contentWidth = context.ResolveWidth(text.Width) ?? context.ContainerWidth;
        }
        else if (context.IntrinsicSizes != null
            && context.IntrinsicSizes.TryGetValue(text, out var intrinsic)
            && intrinsic.MaxWidth > 0)
        {
            contentWidth = intrinsic.MaxWidth;
        }
        else
        {
            contentWidth = context.ContainerWidth;
        }

        float contentHeight;
        // If height is explicitly specified, use it
        if (!string.IsNullOrEmpty(text.Height))
        {
            contentHeight = context.ResolveHeight(text.Height) ?? DefaultTextHeight;
        }
        else if (TextMeasurer != null && !string.IsNullOrEmpty(text.Content)
            && MayWrapOrContainsNewlines(text, contentWidth, context))
        {
            // Re-measure with real container width to account for text wrapping
            var fontSize = FontSizeResolver.Resolve(text.Size, context.FontSize);
            var measureWidth = Math.Min(contentWidth, context.ContainerWidth);
            var measured = TextMeasurer(text, fontSize, measureWidth);
            contentHeight = measured.Height;
        }
        else
        {
            // Fallback: single line height
            var fontSize = FontSizeResolver.Resolve(text.Size, context.FontSize);
            contentHeight = LineHeightResolver.Resolve(text.LineHeight, fontSize, fontSize * LineHeightMultiplier);
        }

        // Total size includes padding (margin is outside, affects position not size)
        var totalWidth = contentWidth + padding.Horizontal;
        var totalHeight = contentHeight + padding.Vertical;

        return new LayoutNode(text, margin, margin, totalWidth, totalHeight);
    }

    /// <summary>
    /// Determines whether a text element may produce multiple lines due to wrapping
    /// or embedded newline characters. This avoids calling <see cref="TextMeasurer"/>
    /// for simple single-line text, which would change the height calculation and
    /// break backward compatibility with existing layouts.
    /// </summary>
    /// <param name="text">The text element to check.</param>
    /// <param name="contentWidth">The resolved content width for the element.</param>
    /// <param name="context">The layout context providing container dimensions.</param>
    /// <returns><c>true</c> if the text may wrap or contains newlines; otherwise <c>false</c>.</returns>
    private static bool MayWrapOrContainsNewlines(TextElement text, float contentWidth, LayoutContext context)
    {
        // Text with embedded newlines always needs multi-line measurement
        if (text.Content.Contains('\n'))
            return true;

        // If wrapping is disabled, it will never wrap
        if (!text.Wrap)
            return false;

        // Check if intrinsic (unwrapped) width exceeds the available space,
        // which means the text will wrap to multiple lines
        var availableWidth = Math.Min(contentWidth, context.ContainerWidth);
        if (context.IntrinsicSizes != null
            && context.IntrinsicSizes.TryGetValue(text, out var intrinsic))
        {
            return intrinsic.MaxWidth > availableWidth;
        }

        // No intrinsic data available -- conservatively assume wrapping may occur
        return true;
    }

    /// <summary>
    /// Lays out a QR code element.
    /// </summary>
    /// <param name="qr">The QR element to lay out.</param>
    /// <param name="context">The layout context.</param>
    /// <returns>A layout node with computed dimensions.</returns>
    private static LayoutNode LayoutQrElement(QrElement qr, LayoutContext context)
    {
        var padding = PaddingParser.Parse(qr.Padding, context.ContainerWidth, context.FontSize).ClampNegatives();
        var margin = UnitParser.Parse(qr.Margin).Resolve(context.ContainerWidth, context.FontSize) ?? 0f;
        if (margin < 0) margin = 0;

        // QR code is square, size is both width and height
        // Check if explicit width/height are provided via flex properties
        var contentWidth = context.ResolveWidth(qr.Width) ?? qr.Size;
        var contentHeight = context.ResolveHeight(qr.Height) ?? qr.Size;

        // Total size includes padding
        var totalWidth = contentWidth + padding.Horizontal;
        var totalHeight = contentHeight + padding.Vertical;

        return new LayoutNode(qr, margin, margin, totalWidth, totalHeight);
    }

    /// <summary>
    /// Lays out a barcode element.
    /// </summary>
    /// <param name="barcode">The barcode element to lay out.</param>
    /// <param name="context">The layout context.</param>
    /// <returns>A layout node with computed dimensions.</returns>
    private static LayoutNode LayoutBarcodeElement(BarcodeElement barcode, LayoutContext context)
    {
        var padding = PaddingParser.Parse(barcode.Padding, context.ContainerWidth, context.FontSize).ClampNegatives();
        var margin = UnitParser.Parse(barcode.Margin).Resolve(context.ContainerWidth, context.FontSize) ?? 0f;
        if (margin < 0) margin = 0;

        // Use explicit flex dimensions if provided, otherwise fall back to barcode-specific dimensions
        var contentWidth = context.ResolveWidth(barcode.Width) ?? barcode.BarcodeWidth;
        var contentHeight = context.ResolveHeight(barcode.Height) ?? barcode.BarcodeHeight;

        // Total size includes padding
        var totalWidth = contentWidth + padding.Horizontal;
        var totalHeight = contentHeight + padding.Vertical;

        return new LayoutNode(barcode, margin, margin, totalWidth, totalHeight);
    }

    /// <summary>
    /// Lays out an image element.
    /// </summary>
    /// <param name="image">The image element to lay out.</param>
    /// <param name="context">The layout context.</param>
    /// <returns>A layout node with computed dimensions.</returns>
    private static LayoutNode LayoutImageElement(ImageElement image, LayoutContext context)
    {
        var padding = PaddingParser.Parse(image.Padding, context.ContainerWidth, context.FontSize).ClampNegatives();
        var margin = UnitParser.Parse(image.Margin).Resolve(context.ContainerWidth, context.FontSize) ?? 0f;
        if (margin < 0) margin = 0;

        // Priority: flex Width/Height > ImageWidth/ImageHeight > container defaults
        var contentWidth = context.ResolveWidth(image.Width) ?? image.ImageWidth ?? context.ContainerWidth;
        var contentHeight = context.ResolveHeight(image.Height) ?? image.ImageHeight ?? DefaultTextHeight;

        // Total size includes padding
        var totalWidth = contentWidth + padding.Horizontal;
        var totalHeight = contentHeight + padding.Vertical;

        return new LayoutNode(image, margin, margin, totalWidth, totalHeight);
    }

    /// <summary>
    /// Lays out a separator element.
    /// Horizontal separators stretch to container width; vertical separators use thickness as width.
    /// </summary>
    /// <param name="separator">The separator element to lay out.</param>
    /// <param name="context">The layout context.</param>
    /// <returns>A layout node with computed dimensions.</returns>
    private static LayoutNode LayoutSeparatorElement(SeparatorElement separator, LayoutContext context)
    {
        var padding = PaddingParser.Parse(separator.Padding, context.ContainerWidth, context.FontSize).ClampNegatives();
        var margin = UnitParser.Parse(separator.Margin).Resolve(context.ContainerWidth, context.FontSize) ?? 0f;
        if (margin < 0) margin = 0;

        float contentWidth;
        float contentHeight;

        if (separator.Orientation == SeparatorOrientation.Horizontal)
        {
            contentWidth = context.ResolveWidth(separator.Width) ?? context.ContainerWidth;
            contentHeight = context.ResolveHeight(separator.Height) ?? separator.Thickness;
        }
        else
        {
            contentWidth = context.ResolveWidth(separator.Width) ?? separator.Thickness;
            // Use thickness as fallback instead of context.ContainerHeight to avoid
            // producing a 10000px tall separator when the container is unconstrained.
            // In practice, vertical separators inside a row flex will be stretched by
            // align-items: stretch to match their siblings' height.
            contentHeight = context.ResolveHeight(separator.Height) ?? separator.Thickness;
        }

        var totalWidth = contentWidth + padding.Horizontal;
        var totalHeight = contentHeight + padding.Vertical;

        return new LayoutNode(separator, margin, margin, totalWidth, totalHeight);
    }

    private static void LayoutColumnFlex(LayoutNode node, FlexElement flex, LayoutContext context, PaddingValues padding, float gap)
    {
        var crossAxisSize = node.Width - padding.Horizontal;

        // Calculate flex grow and total child height in a single pass
        var totalGrow = 0f;
        var totalChildHeight = 0f;
        foreach (var child in node.Children)
        {
            totalGrow += GetFlexGrow(child.Element);
            totalChildHeight += child.Height;
        }
        var totalGaps = gap * Math.Max(0, node.Children.Count - 1);
        // When auto-sizing (no explicit height), the container sizes to fit content.
        // There is no free space to distribute via justify-content or flex-grow.
        var isAutoHeight = node.Height == 0;
        var availableHeight = isAutoHeight
            ? totalChildHeight + totalGaps
            : node.Height - padding.Vertical;
        var freeSpace = availableHeight - totalChildHeight - totalGaps;

        // Distribute free space according to flex-grow
        if (totalGrow > 0 && freeSpace > 0)
        {
            foreach (var child in node.Children)
            {
                var grow = GetFlexGrow(child.Element);
                if (grow > 0)
                {
                    child.Height += freeSpace * grow / totalGrow;
                }
            }
            freeSpace = 0;
        }

        // Shrink children when overflowing (CSS flex-shrink behavior)
        // Only shrink when the container has an explicit height constraint
        if (freeSpace < 0 && totalGrow == 0 && node.Height > 0)
        {
            var overflow = -freeSpace;
            var totalShrinkBasis = 0f;
            foreach (var child in node.Children)
            {
                totalShrinkBasis += GetFlexShrink(child.Element) * child.Height;
            }
            if (totalShrinkBasis > 0)
            {
                foreach (var child in node.Children)
                {
                    var shrink = GetFlexShrink(child.Element);
                    if (shrink > 0)
                    {
                        var shrinkAmount = overflow * (shrink * child.Height) / totalShrinkBasis;
                        child.Height = Math.Max(0, child.Height - shrinkAmount);
                    }
                }
                freeSpace = 0;
            }
        }

        float y = padding.Top;

        // Apply justify-content
        switch (flex.Justify)
        {
            case JustifyContent.Center:
                y += freeSpace / 2;
                break;
            case JustifyContent.End:
                y += freeSpace;
                break;
            case JustifyContent.SpaceBetween:
                if (node.Children.Count > 1)
                {
                    gap += freeSpace / (node.Children.Count - 1);
                }
                break;
            case JustifyContent.SpaceAround:
                if (node.Children.Count > 0)
                {
                    var space = freeSpace / node.Children.Count;
                    y += space / 2;
                    gap += space;
                }
                break;
            case JustifyContent.SpaceEvenly:
                if (node.Children.Count > 0)
                {
                    var space = freeSpace / (node.Children.Count + 1);
                    y += space;
                    gap += space;
                }
                break;
        }

        foreach (var child in node.Children)
        {
            // Get child's margin (stored in initial X/Y from layout)
            var childMarginX = child.X;
            var childMarginY = child.Y;

            // Apply align-items (cross axis is horizontal for column)
            // Add child margin to the base position
            child.X = childMarginX + flex.Align switch
            {
                AlignItems.Start => padding.Left,
                AlignItems.Center => padding.Left + (crossAxisSize - child.Width) / 2,
                AlignItems.End => padding.Left + crossAxisSize - child.Width,
                AlignItems.Stretch => padding.Left,
                _ => padding.Left
            };

            // Stretch width if align is stretch and no explicit width
            // Only stretch if crossAxisSize is positive to avoid shrinking to 0
            if (flex.Align == AlignItems.Stretch && !HasExplicitWidth(child.Element) && crossAxisSize > 0)
            {
                child.Width = crossAxisSize;
            }

            // Add child margin to Y position
            child.Y = y + childMarginY;
            y += child.Height + gap + childMarginY;
        }
    }

    private static void LayoutRowFlex(LayoutNode node, FlexElement flex, LayoutContext context, PaddingValues padding, float gap)
    {
        // For row flex, cross axis is height.
        // Only use explicit crossAxisSize when node has explicit height.
        // Otherwise, don't stretch children - let them keep their natural heights.
        var hasExplicitHeight = node.Height > 0;
        var crossAxisSize = hasExplicitHeight ? node.Height - padding.Vertical : 0f;

        // Calculate flex grow and total child width in a single pass
        var totalGrow = 0f;
        var totalChildWidth = 0f;
        foreach (var child in node.Children)
        {
            totalGrow += GetFlexGrow(child.Element);
            totalChildWidth += child.Width;
        }
        var totalGaps = gap * Math.Max(0, node.Children.Count - 1);
        var availableWidth = node.Width - padding.Horizontal;
        var freeSpace = availableWidth - totalChildWidth - totalGaps;

        // Distribute free space according to flex-grow
        if (totalGrow > 0 && freeSpace > 0)
        {
            foreach (var child in node.Children)
            {
                var grow = GetFlexGrow(child.Element);
                if (grow > 0)
                {
                    child.Width += freeSpace * grow / totalGrow;
                }
            }
            freeSpace = 0;
        }

        // Shrink children when overflowing (CSS flex-shrink behavior)
        if (freeSpace < 0 && totalGrow == 0)
        {
            var overflow = -freeSpace;
            var totalShrinkBasis = 0f;
            foreach (var child in node.Children)
            {
                totalShrinkBasis += GetFlexShrink(child.Element) * child.Width;
            }
            if (totalShrinkBasis > 0)
            {
                foreach (var child in node.Children)
                {
                    var shrink = GetFlexShrink(child.Element);
                    if (shrink > 0)
                    {
                        var shrinkAmount = overflow * (shrink * child.Width) / totalShrinkBasis;
                        child.Width = Math.Max(0, child.Width - shrinkAmount);
                    }
                }
                freeSpace = 0;
            }
        }

        float x = padding.Left;

        // Apply justify-content
        switch (flex.Justify)
        {
            case JustifyContent.Center:
                x += freeSpace / 2;
                break;
            case JustifyContent.End:
                x += freeSpace;
                break;
            case JustifyContent.SpaceBetween:
                if (node.Children.Count > 1)
                {
                    gap += freeSpace / (node.Children.Count - 1);
                }
                break;
            case JustifyContent.SpaceAround:
                if (node.Children.Count > 0)
                {
                    var space = freeSpace / node.Children.Count;
                    x += space / 2;
                    gap += space;
                }
                break;
            case JustifyContent.SpaceEvenly:
                if (node.Children.Count > 0)
                {
                    var space = freeSpace / (node.Children.Count + 1);
                    x += space;
                    gap += space;
                }
                break;
        }

        foreach (var child in node.Children)
        {
            // Get child's margin (stored in initial X/Y from layout)
            var childMarginX = child.X;
            var childMarginY = child.Y;

            // Add child margin to X position
            child.X = x + childMarginX;

            // Apply align-items
            // When container has no explicit height (crossAxisSize = 0), use padding for all alignments
            // Add child margin to Y position
            child.Y = childMarginY + ((hasExplicitHeight && crossAxisSize > 0) ? flex.Align switch
            {
                AlignItems.Start => padding.Top,
                AlignItems.Center => padding.Top + (crossAxisSize - child.Height) / 2,
                AlignItems.End => padding.Top + crossAxisSize - child.Height,
                AlignItems.Stretch => padding.Top,
                _ => padding.Top
            } : padding.Top);

            // Stretch height if align is stretch and no explicit height on child
            // Only stretch when container has explicit height
            if (hasExplicitHeight && flex.Align == AlignItems.Stretch && !HasExplicitHeight(child.Element) && crossAxisSize > 0)
            {
                child.Height = crossAxisSize;
            }

            x += child.Width + gap + childMarginX;
        }
    }

    private static float GetFlexGrow(TemplateElement element)
    {
        var grow = element switch
        {
            FlexElement f => f.Grow,
            TextElement t => t.Grow,
            QrElement q => q.Grow,
            BarcodeElement b => b.Grow,
            ImageElement i => i.Grow,
            SeparatorElement s => s.Grow,
            _ => 0f
        };

        if (grow < 0f)
        {
            throw new ArgumentException(
                $"Flex grow value cannot be negative. Got {grow} for element of type {element.Type}.",
                nameof(element));
        }

        return grow;
    }

    /// <summary>
    /// Gets the flex-shrink value for a template element.
    /// Flex-shrink determines how much an element shrinks relative to its siblings
    /// when the container overflows (CSS flex-shrink behavior).
    /// </summary>
    /// <param name="element">The template element to query.</param>
    /// <returns>The flex-shrink value (defaults to 1).</returns>
    /// <exception cref="ArgumentException">Thrown when the shrink value is negative.</exception>
    private static float GetFlexShrink(TemplateElement element)
    {
        var shrink = element switch
        {
            FlexElement f => f.Shrink,
            TextElement t => t.Shrink,
            QrElement q => q.Shrink,
            BarcodeElement b => b.Shrink,
            ImageElement i => i.Shrink,
            SeparatorElement s => s.Shrink,
            _ => 1f
        };

        if (shrink < 0f)
        {
            throw new ArgumentException(
                $"Flex shrink value cannot be negative. Got {shrink} for element of type {element.Type}.",
                nameof(element));
        }

        return shrink;
    }

    private static bool HasExplicitHeight(TemplateElement element)
    {
        return element switch
        {
            FlexElement f => !string.IsNullOrEmpty(f.Height),
            TextElement t => !string.IsNullOrEmpty(t.Height),
            QrElement q => !string.IsNullOrEmpty(q.Height),
            BarcodeElement b => !string.IsNullOrEmpty(b.Height),
            ImageElement i => !string.IsNullOrEmpty(i.Height),
            SeparatorElement s => !string.IsNullOrEmpty(s.Height),
            _ => false
        };
    }

    private static bool HasExplicitWidth(TemplateElement element)
    {
        return element switch
        {
            FlexElement f => !string.IsNullOrEmpty(f.Width),
            TextElement t => !string.IsNullOrEmpty(t.Width),
            QrElement q => !string.IsNullOrEmpty(q.Width) || q.Size > 0,
            BarcodeElement b => !string.IsNullOrEmpty(b.Width) || b.BarcodeWidth > 0,
            ImageElement i => !string.IsNullOrEmpty(i.Width) || i.ImageWidth.HasValue,
            SeparatorElement s => !string.IsNullOrEmpty(s.Width),
            _ => false
        };
    }

    private static void StackChildrenVertically(LayoutNode parent, LayoutContext context)
    {
        var y = 0f;
        foreach (var child in parent.Children)
        {
            // Preserve the child's X position (which includes margin from layout)
            // Add Y margin to the stacking position
            var childMarginY = child.Y; // Y was set to margin in Layout*Element methods
            child.Y = y + childMarginY;
            y = child.Y + child.Height;
        }
    }

    private static float CalculateTotalHeight(LayoutNode node)
    {
        if (node.Children.Count == 0)
            return 0f;

        var maxBottom = 0f;
        foreach (var child in node.Children)
        {
            if (child.Bottom > maxBottom)
                maxBottom = child.Bottom;
        }
        return maxBottom;
    }

    /// <summary>
    /// Calculates the total width needed to contain all children.
    /// </summary>
    /// <param name="node">The parent layout node.</param>
    /// <returns>The maximum right edge of all children.</returns>
    private static float CalculateTotalWidth(LayoutNode node)
    {
        if (node.Children.Count == 0)
            return 0f;

        var maxRight = 0f;
        foreach (var child in node.Children)
        {
            var right = child.X + child.Width;
            if (right > maxRight)
                maxRight = right;
        }
        return maxRight;
    }
}
