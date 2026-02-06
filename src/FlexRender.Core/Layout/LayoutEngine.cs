using FlexRender.Configuration;
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

    private readonly ResourceLimits _limits;
    private readonly IntrinsicMeasurer _intrinsicMeasurer;
    private readonly WrappedFlexLayoutStrategy _wrappedStrategy;

    /// <summary>
    /// Initializes a new instance of the <see cref="LayoutEngine"/> class with default resource limits.
    /// </summary>
    public LayoutEngine() : this(new ResourceLimits())
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="LayoutEngine"/> class with specified resource limits.
    /// </summary>
    /// <param name="limits">The resource limits to enforce during layout computation.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="limits"/> is null.</exception>
    public LayoutEngine(ResourceLimits limits)
    {
        ArgumentNullException.ThrowIfNull(limits);
        _limits = limits;
        _intrinsicMeasurer = new IntrinsicMeasurer(this);
        _wrappedStrategy = new WrappedFlexLayoutStrategy(limits);
    }

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
        ColumnFlexLayoutStrategy.LayoutColumnFlex(root, rootElement, context, PaddingValues.Zero, 0f);

        // Calculate dimensions based on which dimension is fixed
        if (canvas.Fixed == FixedDimension.Width)
        {
            // Height is flexible - calculate from content
            root.Height = LayoutHelpers.CalculateTotalHeight(root);
        }
        else if (canvas.Fixed == FixedDimension.Height)
        {
            // Width is flexible - calculate from content
            root.Width = LayoutHelpers.CalculateTotalWidth(root);
        }
        else if (canvas.Fixed == FixedDimension.None)
        {
            // Both dimensions are flexible - calculate from content
            root.Width = LayoutHelpers.CalculateTotalWidth(root);
            root.Height = LayoutHelpers.CalculateTotalHeight(root);
        }
        // For FixedDimension.Both, keep both dimensions as set

        return root;
    }

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
        return _intrinsicMeasurer.MeasureAll(root);
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

        var isColumn = flex.Direction is FlexDirection.Column or FlexDirection.ColumnReverse;

        // Layout children
        foreach (var child in flex.Children)
        {
            if (child.Display == Display.None)
            {
                // Add a zero-sized node for display:none children to preserve indexing
                node.AddChild(new LayoutNode(child, 0, 0, 0, 0));
                continue;
            }

            // Absolute children are removed from flex flow but still need layout
            if (child.Position == Position.Absolute)
            {
                var childNode = LayoutElement(child, innerContext);

                // Inset-based sizing: compute width from left+right when no explicit width
                var leftInset = innerContext.ResolveWidth(child.Left);
                var rightInset = innerContext.ResolveWidth(child.Right);
                var topInset = innerContext.ResolveHeight(child.Top);
                var bottomInset = innerContext.ResolveHeight(child.Bottom);

                if (!LayoutHelpers.HasExplicitWidth(child) && leftInset.HasValue && rightInset.HasValue)
                {
                    childNode.Width = Math.Max(0f, width - padding.Left - padding.Right - leftInset.Value - rightInset.Value);
                }

                if (!LayoutHelpers.HasExplicitHeight(child) && topInset.HasValue && bottomInset.HasValue)
                {
                    var containerHeight = height > 0 ? height : node.Height;
                    childNode.Height = Math.Max(0f, containerHeight - padding.Top - padding.Bottom - topInset.Value - bottomInset.Value);
                }

                // Apply aspect ratio for absolute children
                if (child.AspectRatio.HasValue && child.AspectRatio.Value > 0f)
                {
                    var ratio = child.AspectRatio.Value;
                    if (LayoutHelpers.HasExplicitWidth(child) && !LayoutHelpers.HasExplicitHeight(child))
                        childNode.Height = childNode.Width / ratio;
                    else if (LayoutHelpers.HasExplicitHeight(child) && !LayoutHelpers.HasExplicitWidth(child))
                        childNode.Width = childNode.Height * ratio;
                }

                node.AddChild(childNode);
                continue; // Skip flex flow
            }

            var childContext = innerContext;

            // For row flex, children without explicit width should use intrinsic content width
            // instead of full container width. This enables correct justify-content distribution.
            if (!isColumn && !LayoutHelpers.HasExplicitWidth(child))
            {
                var childIntrinsic = context.GetIntrinsicSize(child);
                if (childIntrinsic.HasValue && childIntrinsic.Value.MaxWidth > 0)
                {
                    childContext = innerContext.WithSize(
                        Math.Min(childIntrinsic.Value.MaxWidth, innerContext.ContainerWidth),
                        innerContext.ContainerHeight);
                }
            }

            var flowChildNode = LayoutElement(child, childContext);

            // Apply aspect ratio for flow children (only when one dimension is explicitly set)
            if (child.AspectRatio.HasValue && child.AspectRatio.Value > 0f)
            {
                var ratio = child.AspectRatio.Value;
                if (LayoutHelpers.HasExplicitWidth(child) && !LayoutHelpers.HasExplicitHeight(child))
                    flowChildNode.Height = flowChildNode.Width / ratio;
                else if (LayoutHelpers.HasExplicitHeight(child) && !LayoutHelpers.HasExplicitWidth(child))
                    flowChildNode.Width = flowChildNode.Height * ratio;
            }

            node.AddChild(flowChildNode);
        }

        // Apply flex layout
        if (flex.Wrap != FlexWrap.NoWrap)
        {
            // gap shorthand applies to both axes; RowGap/ColumnGap override individually
            var mainGap = gap;
            var crossGap = gap;
            if (!string.IsNullOrEmpty(flex.RowGap))
            {
                var rowGapValue = UnitParser.Parse(flex.RowGap).Resolve(width, context.FontSize) ?? 0f;
                if (isColumn)
                    mainGap = rowGapValue;
                else
                    crossGap = rowGapValue;
            }
            if (!string.IsNullOrEmpty(flex.ColumnGap))
            {
                var colGapValue = UnitParser.Parse(flex.ColumnGap).Resolve(width, context.FontSize) ?? 0f;
                if (isColumn)
                    crossGap = colGapValue;
                else
                    mainGap = colGapValue;
            }
            _wrappedStrategy.LayoutWrappedFlex(node, flex, innerContext, padding, mainGap, crossGap);
        }
        else if (isColumn)
        {
            ColumnFlexLayoutStrategy.LayoutColumnFlex(node, flex, innerContext, padding, gap);
        }
        else
        {
            RowFlexLayoutStrategy.LayoutRowFlex(node, flex, innerContext, padding, gap);
        }

        // Calculate height if not specified (skip for wrapped containers — they set height in LayoutWrappedFlex)
        if (height == 0f && node.Children.Count > 0 && flex.Wrap == FlexWrap.NoWrap)
        {
            node.Height = LayoutHelpers.CalculateTotalHeight(node) + padding.Bottom;
        }

        // Position absolute children after flex layout is complete
        foreach (var child in node.Children)
        {
            if (child.Element.Position != Position.Absolute) continue;

            var leftInset = innerContext.ResolveWidth(child.Element.Left);
            var topInset = innerContext.ResolveHeight(child.Element.Top);
            var rightInset = innerContext.ResolveWidth(child.Element.Right);
            var bottomInset = innerContext.ResolveHeight(child.Element.Bottom);

            // X positioning: left takes priority, then right, then justify/align fallback
            if (leftInset.HasValue)
                child.X = padding.Left + leftInset.Value;
            else if (rightInset.HasValue)
                child.X = node.Width - padding.Right - child.Width - rightInset.Value;
            else
            {
                // No horizontal insets — use justify-content (row) or align-items (column)
                var isColumnDir = flex.Direction is FlexDirection.Column or FlexDirection.ColumnReverse;
                if (!isColumnDir)
                {
                    // Row: main axis = X, use justify-content
                    child.X = flex.Justify switch
                    {
                        JustifyContent.End => node.Width - padding.Right - child.Width,
                        JustifyContent.Center or JustifyContent.SpaceAround or JustifyContent.SpaceEvenly
                            => padding.Left + (node.Width - padding.Left - padding.Right - child.Width) / 2f,
                        _ => padding.Left // Start, SpaceBetween
                    };
                }
                else
                {
                    // Column: cross axis = X, use align-items
                    var align = LayoutHelpers.GetEffectiveAlign(child.Element, flex.Align);
                    child.X = align switch
                    {
                        AlignItems.End => node.Width - padding.Right - child.Width,
                        AlignItems.Center => padding.Left + (node.Width - padding.Left - padding.Right - child.Width) / 2f,
                        _ => padding.Left // Start, Stretch, Baseline
                    };
                }
            }

            // Y positioning: top takes priority, then bottom, then justify/align fallback
            if (topInset.HasValue)
                child.Y = padding.Top + topInset.Value;
            else if (bottomInset.HasValue)
                child.Y = node.Height - padding.Bottom - child.Height - bottomInset.Value;
            else
            {
                var isColumnDir = flex.Direction is FlexDirection.Column or FlexDirection.ColumnReverse;
                if (isColumnDir)
                {
                    // Column: main axis = Y, use justify-content
                    child.Y = flex.Justify switch
                    {
                        JustifyContent.End => node.Height - padding.Bottom - child.Height,
                        JustifyContent.Center or JustifyContent.SpaceAround or JustifyContent.SpaceEvenly
                            => padding.Top + (node.Height - padding.Top - padding.Bottom - child.Height) / 2f,
                        _ => padding.Top
                    };
                }
                else
                {
                    // Row: cross axis = Y, use align-items
                    var align = LayoutHelpers.GetEffectiveAlign(child.Element, flex.Align);
                    child.Y = align switch
                    {
                        AlignItems.End => node.Height - padding.Bottom - child.Height,
                        AlignItems.Center => padding.Top + (node.Height - padding.Top - padding.Bottom - child.Height) / 2f,
                        _ => padding.Top
                    };
                }
            }
        }

        return node;
    }

    private LayoutNode LayoutTextElement(TextElement text, LayoutContext context)
    {
        var padding = PaddingParser.Parse(text.Padding, context.ContainerWidth, context.FontSize).ClampNegatives();

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

        // Total size includes padding (margin is applied in flex layout pass)
        var totalWidth = contentWidth + padding.Horizontal;
        var totalHeight = contentHeight + padding.Vertical;

        return new LayoutNode(text, 0, 0, totalWidth, totalHeight);
    }

    /// <summary>
    /// Determines whether a text element may produce multiple lines due to wrapping
    /// or embedded newline characters.
    /// </summary>
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
    private static LayoutNode LayoutQrElement(QrElement qr, LayoutContext context)
    {
        var padding = PaddingParser.Parse(qr.Padding, context.ContainerWidth, context.FontSize).ClampNegatives();

        // QR code is square, size is both width and height
        // Check if explicit width/height are provided via flex properties
        // Priority: flex Width/Height > Size > container defaults
        var contentWidth = context.ResolveWidth(qr.Width) ?? (float?)qr.Size ?? context.ContainerWidth;
        var contentHeight = context.ResolveHeight(qr.Height) ?? (float?)qr.Size ?? context.ContainerHeight;

        // Total size includes padding (margin is applied in flex layout pass)
        var totalWidth = contentWidth + padding.Horizontal;
        var totalHeight = contentHeight + padding.Vertical;

        return new LayoutNode(qr, 0, 0, totalWidth, totalHeight);
    }

    /// <summary>
    /// Lays out a barcode element.
    /// </summary>
    private static LayoutNode LayoutBarcodeElement(BarcodeElement barcode, LayoutContext context)
    {
        var padding = PaddingParser.Parse(barcode.Padding, context.ContainerWidth, context.FontSize).ClampNegatives();

        // Use explicit flex dimensions if provided, otherwise fall back to barcode-specific dimensions
        // Priority: flex Width/Height > BarcodeWidth/BarcodeHeight > container defaults
        var contentWidth = context.ResolveWidth(barcode.Width) ?? (float?)barcode.BarcodeWidth ?? context.ContainerWidth;
        var contentHeight = context.ResolveHeight(barcode.Height) ?? (float?)barcode.BarcodeHeight ?? context.ContainerHeight;

        // Total size includes padding (margin is applied in flex layout pass)
        var totalWidth = contentWidth + padding.Horizontal;
        var totalHeight = contentHeight + padding.Vertical;

        return new LayoutNode(barcode, 0, 0, totalWidth, totalHeight);
    }

    /// <summary>
    /// Lays out an image element.
    /// </summary>
    private static LayoutNode LayoutImageElement(ImageElement image, LayoutContext context)
    {
        var padding = PaddingParser.Parse(image.Padding, context.ContainerWidth, context.FontSize).ClampNegatives();

        // Priority: flex Width/Height > ImageWidth/ImageHeight > container defaults
        var contentWidth = context.ResolveWidth(image.Width) ?? image.ImageWidth ?? context.ContainerWidth;
        var contentHeight = context.ResolveHeight(image.Height) ?? image.ImageHeight ?? context.ContainerHeight;

        // Total size includes padding (margin is applied in flex layout pass)
        var totalWidth = contentWidth + padding.Horizontal;
        var totalHeight = contentHeight + padding.Vertical;

        return new LayoutNode(image, 0, 0, totalWidth, totalHeight);
    }

    /// <summary>
    /// Lays out a separator element.
    /// </summary>
    private static LayoutNode LayoutSeparatorElement(SeparatorElement separator, LayoutContext context)
    {
        var padding = PaddingParser.Parse(separator.Padding, context.ContainerWidth, context.FontSize).ClampNegatives();

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
            contentHeight = context.ResolveHeight(separator.Height) ?? separator.Thickness;
        }

        var totalWidth = contentWidth + padding.Horizontal;
        var totalHeight = contentHeight + padding.Vertical;

        return new LayoutNode(separator, 0, 0, totalWidth, totalHeight);
    }
}
