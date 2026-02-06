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
        // Elements with display:none have zero intrinsic size
        if (element.Display == Display.None)
        {
            var zero = new IntrinsicSize(0f, 0f, 0f, 0f);
            sizes[element] = zero;
            return zero;
        }

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
        var margin = PaddingParser.ParseAbsolute(flex.Margin).ClampNegatives();
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
        var visibleCount = 0;
        for (var i = 0; i < childCount; i++)
        {
            if (flex.Children[i].Position == Position.Absolute)
            {
                childSizes[i] = new IntrinsicSize();
                // Still measure the absolute child so it gets an entry in sizes dict
                MeasureIntrinsic(flex.Children[i], sizes);
                continue;
            }
            childSizes[i] = MeasureIntrinsic(flex.Children[i], sizes);
            if (flex.Children[i].Display != Display.None)
                visibleCount++;
        }

        var totalGaps = gap * Math.Max(0, visibleCount - 1);

        float minWidth, maxWidth, minHeight, maxHeight;

        var isColumn = flex.Direction is FlexDirection.Column or FlexDirection.ColumnReverse;
        if (isColumn)
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
        var m = PaddingParser.ParseAbsolute(margin).ClampNegatives();
        if (m.Horizontal > 0f || m.Vertical > 0f)
        {
            intrinsic = intrinsic.WithMargin(m);
        }
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

                if (!HasExplicitWidth(child) && leftInset.HasValue && rightInset.HasValue)
                {
                    childNode.Width = Math.Max(0f, width - padding.Left - padding.Right - leftInset.Value - rightInset.Value);
                }

                if (!HasExplicitHeight(child) && topInset.HasValue && bottomInset.HasValue)
                {
                    var containerHeight = height > 0 ? height : node.Height;
                    childNode.Height = Math.Max(0f, containerHeight - padding.Top - padding.Bottom - topInset.Value - bottomInset.Value);
                }

                // Apply aspect ratio for absolute children
                if (child.AspectRatio.HasValue && child.AspectRatio.Value > 0f)
                {
                    var ratio = child.AspectRatio.Value;
                    if (HasExplicitWidth(child) && !HasExplicitHeight(child))
                        childNode.Height = childNode.Width / ratio;
                    else if (HasExplicitHeight(child) && !HasExplicitWidth(child))
                        childNode.Width = childNode.Height * ratio;
                }

                node.AddChild(childNode);
                continue; // Skip flex flow
            }

            var childContext = innerContext;

            // For row flex, children without explicit width should use intrinsic content width
            // instead of full container width. This enables correct justify-content distribution.
            if (!isColumn && !HasExplicitWidth(child))
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
                if (HasExplicitWidth(child) && !HasExplicitHeight(child))
                    flowChildNode.Height = flowChildNode.Width / ratio;
                else if (HasExplicitHeight(child) && !HasExplicitWidth(child))
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
            LayoutWrappedFlex(node, flex, innerContext, padding, mainGap, crossGap);
        }
        else if (isColumn)
        {
            LayoutColumnFlex(node, flex, innerContext, padding, gap);
        }
        else
        {
            LayoutRowFlex(node, flex, innerContext, padding, gap);
        }

        // Calculate height if not specified (skip for wrapped containers — they set height in LayoutWrappedFlex)
        if (height == 0f && node.Children.Count > 0 && flex.Wrap == FlexWrap.NoWrap)
        {
            node.Height = CalculateTotalHeight(node) + padding.Bottom;
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
                    var align = GetEffectiveAlign(child.Element, flex.Align);
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
                    var align = GetEffectiveAlign(child.Element, flex.Align);
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

        // QR code is square, size is both width and height
        // Check if explicit width/height are provided via flex properties
        var contentWidth = context.ResolveWidth(qr.Width) ?? qr.Size;
        var contentHeight = context.ResolveHeight(qr.Height) ?? qr.Size;

        // Total size includes padding (margin is applied in flex layout pass)
        var totalWidth = contentWidth + padding.Horizontal;
        var totalHeight = contentHeight + padding.Vertical;

        return new LayoutNode(qr, 0, 0, totalWidth, totalHeight);
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

        // Use explicit flex dimensions if provided, otherwise fall back to barcode-specific dimensions
        var contentWidth = context.ResolveWidth(barcode.Width) ?? barcode.BarcodeWidth;
        var contentHeight = context.ResolveHeight(barcode.Height) ?? barcode.BarcodeHeight;

        // Total size includes padding (margin is applied in flex layout pass)
        var totalWidth = contentWidth + padding.Horizontal;
        var totalHeight = contentHeight + padding.Vertical;

        return new LayoutNode(barcode, 0, 0, totalWidth, totalHeight);
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

        // Priority: flex Width/Height > ImageWidth/ImageHeight > container defaults
        var contentWidth = context.ResolveWidth(image.Width) ?? image.ImageWidth ?? context.ContainerWidth;
        var contentHeight = context.ResolveHeight(image.Height) ?? image.ImageHeight ?? DefaultTextHeight;

        // Total size includes padding (margin is applied in flex layout pass)
        var totalWidth = contentWidth + padding.Horizontal;
        var totalHeight = contentHeight + padding.Vertical;

        return new LayoutNode(image, 0, 0, totalWidth, totalHeight);
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

        return new LayoutNode(separator, 0, 0, totalWidth, totalHeight);
    }

    private static void LayoutColumnFlex(LayoutNode node, FlexElement flex, LayoutContext context, PaddingValues padding, float gap)
    {
        var crossAxisSize = node.Width - padding.Horizontal;

        // Count visible children and compute margins
        var totalMarginHeight = 0f;
        var visibleCount = 0;
        foreach (var child in node.Children)
        {
            if (child.Element.Display == Display.None) continue;
            if (child.Element.Position == Position.Absolute) continue;
            var childMargin = PaddingParser.Parse(child.Element.Margin, context.ContainerWidth, context.FontSize).ClampNegatives();
            totalMarginHeight += childMargin.Top + childMargin.Bottom;
            visibleCount++;
        }
        var totalGaps = gap * Math.Max(0, visibleCount - 1);
        // When auto-sizing (no explicit height), the container sizes to fit content.
        var isAutoHeight = node.Height == 0;

        // Two-pass flex resolution algorithm (CSS spec Section 9.7)
        // Step 1: Determine flex basis for each child
        var itemCount = node.Children.Count;
        Span<float> bases = itemCount <= 32 ? stackalloc float[itemCount] : new float[itemCount];
        Span<float> sizes = itemCount <= 32 ? stackalloc float[itemCount] : new float[itemCount];
        Span<bool> frozen = itemCount <= 32 ? stackalloc bool[itemCount] : new bool[itemCount];

        var totalBases = 0f;
        for (var i = 0; i < itemCount; i++)
        {
            var child = node.Children[i];
            if (child.Element.Display == Display.None || child.Element.Position == Position.Absolute)
            {
                bases[i] = 0f;
                sizes[i] = 0f;
                frozen[i] = true;
                continue;
            }
            bases[i] = ResolveFlexBasis(child.Element, child, isColumn: true, context);
            sizes[i] = bases[i];
            frozen[i] = false;
            totalBases += bases[i];
        }

        var availableHeight = isAutoHeight
            ? totalBases + totalGaps + totalMarginHeight
            : node.Height - padding.Vertical;

        // Freeze items with grow=0 (when growing) or shrink=0 (when shrinking) immediately
        var initialFreeSpace = availableHeight - totalBases - totalGaps - totalMarginHeight;
        var isGrowing = initialFreeSpace > 0;
        for (var i = 0; i < itemCount; i++)
        {
            if (frozen[i]) continue;
            var child = node.Children[i];
            if (isGrowing && GetFlexGrow(child.Element) == 0)
            {
                frozen[i] = true;
            }
            else if (!isGrowing && GetFlexShrink(child.Element) == 0)
            {
                frozen[i] = true;
            }
        }

        // Step 2: Iterative freeze loop
        if (!isAutoHeight)
        {
            for (var iteration = 0; iteration < itemCount + 1; iteration++)
            {
                // Calculate unfrozen free space and total factors
                var unfrozenFreeSpace = availableHeight - totalGaps - totalMarginHeight;
                for (var i = 0; i < itemCount; i++)
                {
                    unfrozenFreeSpace -= frozen[i] ? sizes[i] : bases[i];
                }

                var totalGrowFactors = 0f;
                var totalShrinkScaled = 0f;
                for (var i = 0; i < itemCount; i++)
                {
                    if (frozen[i]) continue;
                    totalGrowFactors += GetFlexGrow(node.Children[i].Element);
                    totalShrinkScaled += GetFlexShrink(node.Children[i].Element) * bases[i];
                }

                // Factor flooring: prevent fractional total factors from under-distributing space
                // Per CSS spec and Yoga FlexLine.cpp:104-112
                if (totalGrowFactors > 0 && totalGrowFactors < 1)
                    totalGrowFactors = 1;
                if (totalShrinkScaled > 0 && totalShrinkScaled < 1)
                    totalShrinkScaled = 1;

                var anyNewlyFrozen = false;

                for (var i = 0; i < itemCount; i++)
                {
                    if (frozen[i]) continue;
                    var child = node.Children[i];
                    var (minSize, maxSize) = ResolveMinMax(child.Element, isColumn: true, context);

                    float hypothetical;
                    if (isGrowing)
                    {
                        var grow = GetFlexGrow(child.Element);
                        hypothetical = grow > 0 && totalGrowFactors > 0
                            ? bases[i] + unfrozenFreeSpace * grow / totalGrowFactors
                            : bases[i];
                    }
                    else
                    {
                        var shrink = GetFlexShrink(child.Element);
                        var scaledFactor = shrink * bases[i];
                        hypothetical = totalShrinkScaled > 0 && scaledFactor > 0
                            ? bases[i] + unfrozenFreeSpace * scaledFactor / totalShrinkScaled
                            : bases[i];
                    }

                    var clamped = ClampSize(hypothetical, minSize, maxSize);
                    if (Math.Abs(clamped - hypothetical) > 0.01f)
                    {
                        sizes[i] = clamped;
                        frozen[i] = true;
                        anyNewlyFrozen = true;
                    }
                    else
                    {
                        sizes[i] = hypothetical;
                    }
                }

                if (!anyNewlyFrozen)
                    break;
            }
        }

        // Apply resolved sizes to child nodes
        for (var i = 0; i < itemCount; i++)
        {
            if (node.Children[i].Element.Display == Display.None) continue;
            if (node.Children[i].Element.Position == Position.Absolute) continue;
            node.Children[i].Height = Math.Max(0, sizes[i]);
        }

        // Re-apply aspect ratio after flex resolution changed main-axis size (column: main=height)
        foreach (var child in node.Children)
        {
            if (child.Element.Position == Position.Absolute) continue;
            if (child.Element.Display == Display.None) continue;
            if (!child.Element.AspectRatio.HasValue || child.Element.AspectRatio.Value <= 0f) continue;
            var ratio = child.Element.AspectRatio.Value;
            child.Width = child.Height * ratio;
        }

        // Recalculate freeSpace after flex resolution for justify-content
        var totalSized = 0f;
        for (var i = 0; i < itemCount; i++)
        {
            if (node.Children[i].Element.Display == Display.None) continue;
            if (node.Children[i].Element.Position == Position.Absolute) continue;
            totalSized += sizes[i];
        }
        var freeSpace = availableHeight - totalSized - totalGaps - totalMarginHeight;

        // Check for auto margins on the main axis (vertical for column)
        var hasMainAutoMargins = false;
        var totalMainAutoCount = 0;
        foreach (var child in node.Children)
        {
            if (child.Element.Display == Display.None) continue;
            if (child.Element.Position == Position.Absolute) continue;
            var m = PaddingParser.ParseMargin(child.Element.Margin, context.ContainerWidth, context.FontSize);
            var ac = m.MainAxisAutoCount(isColumn: true);
            if (ac > 0) { hasMainAutoMargins = true; totalMainAutoCount += ac; }
        }

        // Track the end-of-content Y position for ColumnReverse fallback
        float yEnd = padding.Top;

        if (hasMainAutoMargins && freeSpace > 0)
        {
            // Auto margins consume free space BEFORE justify-content
            var spacePerAuto = freeSpace / totalMainAutoCount;
            var pos = padding.Top;
            var first = true;
            foreach (var child in node.Children)
            {
                if (child.Element.Display == Display.None) continue;
                if (child.Element.Position == Position.Absolute) continue;
                if (!first) pos += gap;
                first = false;

                var m = PaddingParser.ParseMargin(child.Element.Margin, context.ContainerWidth, context.FontSize);
                pos += m.Top.IsAuto ? spacePerAuto : m.Top.ResolvedPixels;
                child.Y = pos;
                pos += child.Height;
                pos += m.Bottom.IsAuto ? spacePerAuto : m.Bottom.ResolvedPixels;

                // Cross axis auto margins override align-items (horizontal for column)
                ApplyColumnCrossAxisMargins(child, m, flex, padding, crossAxisSize);
            }
            yEnd = pos;
        }
        else
        {
            float y = padding.Top;

            // Overflow fallback: space-distribution modes fall back to Start when freeSpace < 0
            var effectiveJustify = freeSpace < 0 ? flex.Justify switch
            {
                JustifyContent.SpaceBetween => JustifyContent.Start,
                JustifyContent.SpaceAround => JustifyContent.Start,
                JustifyContent.SpaceEvenly => JustifyContent.Start,
                _ => flex.Justify
            } : flex.Justify;

            // Apply justify-content
            switch (effectiveJustify)
            {
                case JustifyContent.Center:
                    y += freeSpace / 2;
                    break;
                case JustifyContent.End:
                    y += freeSpace;
                    break;
                case JustifyContent.SpaceBetween:
                    if (visibleCount > 1)
                    {
                        gap += freeSpace / (visibleCount - 1);
                    }
                    break;
                case JustifyContent.SpaceAround:
                    if (visibleCount > 0)
                    {
                        var space = freeSpace / visibleCount;
                        y += space / 2;
                        gap += space;
                    }
                    break;
                case JustifyContent.SpaceEvenly:
                    if (visibleCount > 0)
                    {
                        var space = freeSpace / (visibleCount + 1);
                        y += space;
                        gap += space;
                    }
                    break;
            }

            foreach (var child in node.Children)
            {
                if (child.Element.Display == Display.None) continue;
                if (child.Element.Position == Position.Absolute) continue;

                // Parse child margin once (supports auto margins, clamp negatives)
                var m = PaddingParser.ParseMargin(child.Element.Margin, context.ContainerWidth, context.FontSize);
                var mTop = Math.Max(0f, m.Top.ResolvedPixels);
                var mBottom = Math.Max(0f, m.Bottom.ResolvedPixels);
                var mLeft = Math.Max(0f, m.Left.ResolvedPixels);
                var mRight = Math.Max(0f, m.Right.ResolvedPixels);

                // Check for cross axis auto margins even when main axis has no auto margins
                if (m.CrossAxisAutoCount(isColumn: true) > 0)
                {
                    ApplyColumnCrossAxisMargins(child, m, flex, padding, crossAxisSize);
                }
                else
                {
                    // Original align-items / align-self logic (cross axis is horizontal for column)
                    var effectiveAlign = GetEffectiveAlign(child.Element, flex.Align);
                    child.X = mLeft + effectiveAlign switch
                    {
                        AlignItems.Start => padding.Left,
                        AlignItems.Center => padding.Left + (crossAxisSize - child.Width - mLeft - mRight) / 2,
                        AlignItems.End => padding.Left + crossAxisSize - child.Width - mLeft - mRight,
                        AlignItems.Stretch => padding.Left,
                        _ => padding.Left
                    };

                    // Stretch width if align is stretch and no explicit width
                    var hasAspectWidth = child.Element.AspectRatio.HasValue && child.Element.AspectRatio.Value > 0f
                        && HasExplicitHeight(child.Element) && !HasExplicitWidth(child.Element);
                    if (effectiveAlign == AlignItems.Stretch && !HasExplicitWidth(child.Element) && !hasAspectWidth && crossAxisSize > 0)
                    {
                        child.Width = crossAxisSize - mLeft - mRight;
                    }
                }

                // Add child margin to Y position
                child.Y = y + mTop;
                y += child.Height + gap + mTop + mBottom;
            }
            yEnd = y;
        }

        // Reverse positions for ColumnReverse
        if (flex.Direction == FlexDirection.ColumnReverse)
        {
            var containerHeight = node.Height > 0 ? node.Height : yEnd;
            foreach (var child in node.Children)
            {
                if (child.Element.Display == Display.None) continue;
                if (child.Element.Position == Position.Absolute) continue;
                child.Y = containerHeight - child.Y - child.Height;
            }
        }

        ApplyRelativePositioning(node, context);
    }

    private static void LayoutRowFlex(LayoutNode node, FlexElement flex, LayoutContext context, PaddingValues padding, float gap)
    {
        // For row flex, cross axis is height.
        // Only use explicit crossAxisSize when node has explicit height.
        // Otherwise, don't stretch children - let them keep their natural heights.
        var hasExplicitHeight = node.Height > 0;
        var crossAxisSize = hasExplicitHeight ? node.Height - padding.Vertical : 0f;

        // Count visible children and compute margins
        var totalMarginWidth = 0f;
        var visibleCount = 0;
        foreach (var child in node.Children)
        {
            if (child.Element.Display == Display.None) continue;
            if (child.Element.Position == Position.Absolute) continue;
            var childMargin = PaddingParser.Parse(child.Element.Margin, context.ContainerWidth, context.FontSize).ClampNegatives();
            totalMarginWidth += childMargin.Left + childMargin.Right;
            visibleCount++;
        }
        var totalGaps = gap * Math.Max(0, visibleCount - 1);
        var availableWidth = node.Width - padding.Horizontal;

        // Two-pass flex resolution algorithm (CSS spec Section 9.7)
        var itemCount = node.Children.Count;
        Span<float> bases = itemCount <= 32 ? stackalloc float[itemCount] : new float[itemCount];
        Span<float> sizes = itemCount <= 32 ? stackalloc float[itemCount] : new float[itemCount];
        Span<bool> frozen = itemCount <= 32 ? stackalloc bool[itemCount] : new bool[itemCount];

        var totalBases = 0f;
        for (var i = 0; i < itemCount; i++)
        {
            var child = node.Children[i];
            if (child.Element.Display == Display.None || child.Element.Position == Position.Absolute)
            {
                bases[i] = 0f;
                sizes[i] = 0f;
                frozen[i] = true;
                continue;
            }
            bases[i] = ResolveFlexBasis(child.Element, child, isColumn: false, context);
            sizes[i] = bases[i];
            frozen[i] = false;
            totalBases += bases[i];
        }

        // Freeze items with grow=0 (when growing) or shrink=0 (when shrinking) immediately
        var initialFreeSpace = availableWidth - totalBases - totalGaps - totalMarginWidth;
        var isGrowing = initialFreeSpace > 0;
        for (var i = 0; i < itemCount; i++)
        {
            if (frozen[i]) continue;
            var child = node.Children[i];
            if (isGrowing && GetFlexGrow(child.Element) == 0)
            {
                frozen[i] = true;
            }
            else if (!isGrowing && GetFlexShrink(child.Element) == 0)
            {
                frozen[i] = true;
            }
        }

        // Iterative freeze loop
        for (var iteration = 0; iteration < itemCount + 1; iteration++)
        {
            var unfrozenFreeSpace = availableWidth - totalGaps - totalMarginWidth;
            for (var i = 0; i < itemCount; i++)
            {
                unfrozenFreeSpace -= frozen[i] ? sizes[i] : bases[i];
            }

            var totalGrowFactors = 0f;
            var totalShrinkScaled = 0f;
            for (var i = 0; i < itemCount; i++)
            {
                if (frozen[i]) continue;
                totalGrowFactors += GetFlexGrow(node.Children[i].Element);
                totalShrinkScaled += GetFlexShrink(node.Children[i].Element) * bases[i];
            }

            // Factor flooring
            if (totalGrowFactors > 0 && totalGrowFactors < 1)
                totalGrowFactors = 1;
            if (totalShrinkScaled > 0 && totalShrinkScaled < 1)
                totalShrinkScaled = 1;

            var anyNewlyFrozen = false;

            for (var i = 0; i < itemCount; i++)
            {
                if (frozen[i]) continue;
                var child = node.Children[i];
                var (minSize, maxSize) = ResolveMinMax(child.Element, isColumn: false, context);

                float hypothetical;
                if (isGrowing)
                {
                    var grow = GetFlexGrow(child.Element);
                    hypothetical = grow > 0 && totalGrowFactors > 0
                        ? bases[i] + unfrozenFreeSpace * grow / totalGrowFactors
                        : bases[i];
                }
                else
                {
                    var shrink = GetFlexShrink(child.Element);
                    var scaledFactor = shrink * bases[i];
                    hypothetical = totalShrinkScaled > 0 && scaledFactor > 0
                        ? bases[i] + unfrozenFreeSpace * scaledFactor / totalShrinkScaled
                        : bases[i];
                }

                var clamped = ClampSize(hypothetical, minSize, maxSize);
                if (Math.Abs(clamped - hypothetical) > 0.01f)
                {
                    sizes[i] = clamped;
                    frozen[i] = true;
                    anyNewlyFrozen = true;
                }
                else
                {
                    sizes[i] = hypothetical;
                }
            }

            if (!anyNewlyFrozen)
                break;
        }

        // Apply resolved sizes to child nodes
        for (var i = 0; i < itemCount; i++)
        {
            if (node.Children[i].Element.Display == Display.None) continue;
            if (node.Children[i].Element.Position == Position.Absolute) continue;
            node.Children[i].Width = Math.Max(0, sizes[i]);
        }

        // Re-apply aspect ratio after flex resolution changed main-axis size (row: main=width)
        foreach (var child in node.Children)
        {
            if (child.Element.Position == Position.Absolute) continue;
            if (child.Element.Display == Display.None) continue;
            if (!child.Element.AspectRatio.HasValue || child.Element.AspectRatio.Value <= 0f) continue;
            var ratio = child.Element.AspectRatio.Value;
            child.Height = child.Width / ratio;
        }

        // Recalculate freeSpace after flex resolution for justify-content
        var totalSized = 0f;
        for (var i = 0; i < itemCount; i++)
        {
            if (node.Children[i].Element.Display == Display.None) continue;
            if (node.Children[i].Element.Position == Position.Absolute) continue;
            totalSized += sizes[i];
        }
        var freeSpace = availableWidth - totalSized - totalGaps - totalMarginWidth;

        // Check for auto margins on the main axis (horizontal for row)
        var hasMainAutoMargins = false;
        var totalMainAutoCount = 0;
        foreach (var child in node.Children)
        {
            if (child.Element.Display == Display.None) continue;
            if (child.Element.Position == Position.Absolute) continue;
            var m = PaddingParser.ParseMargin(child.Element.Margin, context.ContainerWidth, context.FontSize);
            var ac = m.MainAxisAutoCount(isColumn: false);
            if (ac > 0) { hasMainAutoMargins = true; totalMainAutoCount += ac; }
        }

        if (hasMainAutoMargins && freeSpace > 0)
        {
            // Auto margins consume free space BEFORE justify-content
            var spacePerAuto = freeSpace / totalMainAutoCount;
            var pos = padding.Left;
            var first = true;
            foreach (var child in node.Children)
            {
                if (child.Element.Display == Display.None) continue;
                if (child.Element.Position == Position.Absolute) continue;
                if (!first) pos += gap;
                first = false;

                var m = PaddingParser.ParseMargin(child.Element.Margin, context.ContainerWidth, context.FontSize);
                pos += m.Left.IsAuto ? spacePerAuto : m.Left.ResolvedPixels;
                child.X = pos;
                pos += child.Width;
                pos += m.Right.IsAuto ? spacePerAuto : m.Right.ResolvedPixels;

                // Cross axis auto margins override align-items (vertical for row)
                ApplyRowCrossAxisMargins(child, m, flex, padding, crossAxisSize, hasExplicitHeight);
            }
        }
        else
        {
            float x = padding.Left;

            // Overflow fallback: space-distribution modes fall back to Start when freeSpace < 0
            var effectiveJustify = freeSpace < 0 ? flex.Justify switch
            {
                JustifyContent.SpaceBetween => JustifyContent.Start,
                JustifyContent.SpaceAround => JustifyContent.Start,
                JustifyContent.SpaceEvenly => JustifyContent.Start,
                _ => flex.Justify
            } : flex.Justify;

            // Apply justify-content
            switch (effectiveJustify)
            {
                case JustifyContent.Center:
                    x += freeSpace / 2;
                    break;
                case JustifyContent.End:
                    x += freeSpace;
                    break;
                case JustifyContent.SpaceBetween:
                    if (visibleCount > 1)
                    {
                        gap += freeSpace / (visibleCount - 1);
                    }
                    break;
                case JustifyContent.SpaceAround:
                    if (visibleCount > 0)
                    {
                        var space = freeSpace / visibleCount;
                        x += space / 2;
                        gap += space;
                    }
                    break;
                case JustifyContent.SpaceEvenly:
                    if (visibleCount > 0)
                    {
                        var space = freeSpace / (visibleCount + 1);
                        x += space;
                        gap += space;
                    }
                    break;
            }

            foreach (var child in node.Children)
            {
                if (child.Element.Display == Display.None) continue;
                if (child.Element.Position == Position.Absolute) continue;

                // Parse child margin once (supports auto margins, clamp negatives)
                var m = PaddingParser.ParseMargin(child.Element.Margin, context.ContainerWidth, context.FontSize);
                var mLeft = Math.Max(0f, m.Left.ResolvedPixels);
                var mRight = Math.Max(0f, m.Right.ResolvedPixels);
                var mTop = Math.Max(0f, m.Top.ResolvedPixels);
                var mBottom = Math.Max(0f, m.Bottom.ResolvedPixels);

                // Add child margin to X position
                child.X = x + mLeft;

                // Check for cross axis auto margins even when main axis has no auto margins
                if (m.CrossAxisAutoCount(isColumn: false) > 0)
                {
                    ApplyRowCrossAxisMargins(child, m, flex, padding, crossAxisSize, hasExplicitHeight);
                }
                else
                {
                    // Original align-items / align-self logic
                    var effectiveAlign = GetEffectiveAlign(child.Element, flex.Align);
                    child.Y = mTop + ((hasExplicitHeight && crossAxisSize > 0) ? effectiveAlign switch
                    {
                        AlignItems.Start => padding.Top,
                        AlignItems.Center => padding.Top + (crossAxisSize - child.Height - mTop - mBottom) / 2,
                        AlignItems.End => padding.Top + crossAxisSize - child.Height - mTop - mBottom,
                        AlignItems.Stretch => padding.Top,
                        _ => padding.Top
                    } : padding.Top);

                    // Stretch height if align is stretch and no explicit height on child
                    var hasAspectHeight = child.Element.AspectRatio.HasValue && child.Element.AspectRatio.Value > 0f
                        && HasExplicitWidth(child.Element) && !HasExplicitHeight(child.Element);
                    if (hasExplicitHeight && effectiveAlign == AlignItems.Stretch && !HasExplicitHeight(child.Element) && !hasAspectHeight && crossAxisSize > 0)
                    {
                        child.Height = crossAxisSize - mTop - mBottom;
                    }
                }

                x += child.Width + gap + mLeft + mRight;
            }
        }

        // Reverse positions for RowReverse
        if (flex.Direction == FlexDirection.RowReverse)
        {
            var containerWidth = node.Width;
            foreach (var child in node.Children)
            {
                if (child.Element.Display == Display.None) continue;
                if (child.Element.Position == Position.Absolute) continue;
                child.X = containerWidth - child.X - child.Width;
            }
        }

        ApplyRelativePositioning(node, context);
    }

    /// <summary>
    /// Applies cross-axis auto margin positioning for a child in a row container.
    /// Auto margins on the cross axis (Top/Bottom for row) override align-items/align-self.
    /// When no auto margins exist, falls back to normal align-items/align-self behavior.
    /// </summary>
    private static void ApplyRowCrossAxisMargins(
        LayoutNode child, MarginValues margin, FlexElement flex,
        PaddingValues padding, float crossAxisSize, bool hasExplicitHeight)
    {
        var crossAutoCount = margin.CrossAxisAutoCount(isColumn: false);
        var crossFreeSpace = crossAxisSize - child.Height;

        if (crossAutoCount > 0 && crossFreeSpace > 0 && hasExplicitHeight)
        {
            if (crossAutoCount == 2)
            {
                // Both top and bottom auto: center vertically
                child.Y = padding.Top + crossFreeSpace / 2;
            }
            else if (margin.Top.IsAuto)
            {
                // Top auto only: push to bottom
                child.Y = padding.Top + crossFreeSpace;
            }
            else
            {
                // Bottom auto only: stays at top (default position + fixed top margin)
                child.Y = padding.Top + margin.Top.ResolvedPixels;
            }
        }
        else
        {
            // Normal align-items / align-self logic
            var effectiveAlign = GetEffectiveAlign(child.Element, flex.Align);
            child.Y = margin.Top.ResolvedPixels + ((hasExplicitHeight && crossAxisSize > 0) ? effectiveAlign switch
            {
                AlignItems.Start => padding.Top,
                AlignItems.Center => padding.Top + (crossAxisSize - child.Height) / 2,
                AlignItems.End => padding.Top + crossAxisSize - child.Height,
                AlignItems.Stretch => padding.Top,
                _ => padding.Top
            } : padding.Top);

            // Stretch height if align is stretch and no explicit height on child
            var hasAspectHeight = child.Element.AspectRatio.HasValue && child.Element.AspectRatio.Value > 0f
                && HasExplicitWidth(child.Element) && !HasExplicitHeight(child.Element);
            if (hasExplicitHeight && effectiveAlign == AlignItems.Stretch && !HasExplicitHeight(child.Element) && !hasAspectHeight && crossAxisSize > 0)
            {
                child.Height = crossAxisSize;
            }
        }
    }

    /// <summary>
    /// Applies cross-axis auto margin positioning for a child in a column container.
    /// Auto margins on the cross axis (Left/Right for column) override align-items/align-self.
    /// When no auto margins exist, falls back to normal align-items/align-self behavior.
    /// </summary>
    private static void ApplyColumnCrossAxisMargins(
        LayoutNode child, MarginValues margin, FlexElement flex,
        PaddingValues padding, float crossAxisSize)
    {
        var crossAutoCount = margin.CrossAxisAutoCount(isColumn: true);
        var crossFreeSpace = crossAxisSize - child.Width;

        if (crossAutoCount > 0 && crossFreeSpace > 0)
        {
            if (crossAutoCount == 2)
            {
                // Both left and right auto: center horizontally
                child.X = padding.Left + crossFreeSpace / 2;
            }
            else if (margin.Left.IsAuto)
            {
                // Left auto only: push to right
                child.X = padding.Left + crossFreeSpace;
            }
            else
            {
                // Right auto only: stays at left (default position + fixed left margin)
                child.X = padding.Left + margin.Left.ResolvedPixels;
            }
        }
        else
        {
            // Normal align-items / align-self logic
            var effectiveAlign = GetEffectiveAlign(child.Element, flex.Align);
            child.X = margin.Left.ResolvedPixels + effectiveAlign switch
            {
                AlignItems.Start => padding.Left,
                AlignItems.Center => padding.Left + (crossAxisSize - child.Width) / 2,
                AlignItems.End => padding.Left + crossAxisSize - child.Width,
                AlignItems.Stretch => padding.Left,
                _ => padding.Left
            };

            // Stretch width if align is stretch and no explicit width
            var hasAspectWidth = child.Element.AspectRatio.HasValue && child.Element.AspectRatio.Value > 0f
                && HasExplicitHeight(child.Element) && !HasExplicitWidth(child.Element);
            if (effectiveAlign == AlignItems.Stretch && !HasExplicitWidth(child.Element) && !hasAspectWidth && crossAxisSize > 0)
            {
                child.Width = crossAxisSize;
            }
        }
    }

    private static float GetFlexGrow(TemplateElement element)
    {
        if (element.Grow < 0f)
        {
            throw new ArgumentException(
                $"Flex grow value cannot be negative. Got {element.Grow} for element of type {element.Type}.",
                nameof(element));
        }

        return element.Grow;
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
        if (element.Shrink < 0f)
        {
            throw new ArgumentException(
                $"Flex shrink value cannot be negative. Got {element.Shrink} for element of type {element.Type}.",
                nameof(element));
        }

        return element.Shrink;
    }

    /// <summary>
    /// Resolves the effective cross-axis alignment for a child element,
    /// considering align-self override vs parent's align-items.
    /// </summary>
    /// <param name="element">The child element to resolve alignment for.</param>
    /// <param name="parentAlign">The parent container's align-items value.</param>
    /// <returns>The effective alignment to use for this child.</returns>
    private static AlignItems GetEffectiveAlign(TemplateElement element, AlignItems parentAlign)
    {
        return element.AlignSelf switch
        {
            AlignSelf.Auto => parentAlign,
            AlignSelf.Start => AlignItems.Start,
            AlignSelf.Center => AlignItems.Center,
            AlignSelf.End => AlignItems.End,
            AlignSelf.Stretch => AlignItems.Stretch,
            _ => parentAlign
        };
    }

    /// <summary>
    /// Clamps a value between min and max, where min wins over max per CSS spec.
    /// </summary>
    private static float ClampSize(float value, float min, float max)
    {
        var effectiveMax = Math.Max(min, max);
        return Math.Clamp(value, min, effectiveMax);
    }

    /// <summary>
    /// Resolves the flex basis for a child element.
    /// Priority: explicit basis > main-axis dimension > intrinsic size.
    /// Result is clamped to at least the element's padding on the main axis.
    /// </summary>
    private static float ResolveFlexBasis(TemplateElement element, LayoutNode childNode,
        bool isColumn, LayoutContext context)
    {
        float resolved;
        var basisStr = element.Basis;
        if (!string.IsNullOrEmpty(basisStr) && basisStr != "auto")
        {
            var unit = UnitParser.Parse(basisStr);
            var unitResolved = isColumn
                ? unit.Resolve(context.ContainerHeight, context.FontSize)
                : unit.Resolve(context.ContainerWidth, context.FontSize);
            resolved = unitResolved ?? (isColumn ? childNode.Height : childNode.Width);
        }
        else
        {
            // auto: use main-axis dimension (Width for row, Height for column)
            resolved = isColumn ? childNode.Height : childNode.Width;
        }

        // Flex basis minimum: can never go below padding (Yoga: maxOrDefined(basis, paddingAndBorder))
        var paddingValues = PaddingParser.ParseAbsolute(element.Padding).ClampNegatives();
        var paddingFloor = isColumn ? paddingValues.Vertical : paddingValues.Horizontal;
        return Math.Max(resolved, paddingFloor);
    }

    /// <summary>
    /// Resolves min/max constraints for a child on the main axis.
    /// </summary>
    private static (float min, float max) ResolveMinMax(TemplateElement element,
        bool isColumn, LayoutContext context)
    {
        float min = 0f;
        float max = float.MaxValue;

        var minStr = isColumn ? element.MinHeight : element.MinWidth;
        var maxStr = isColumn ? element.MaxHeight : element.MaxWidth;

        if (!string.IsNullOrEmpty(minStr))
        {
            var unit = UnitParser.Parse(minStr);
            var parentSize = isColumn ? context.ContainerHeight : context.ContainerWidth;
            min = unit.Resolve(parentSize, context.FontSize) ?? 0f;
        }

        if (!string.IsNullOrEmpty(maxStr))
        {
            var unit = UnitParser.Parse(maxStr);
            var parentSize = isColumn ? context.ContainerHeight : context.ContainerWidth;
            max = unit.Resolve(parentSize, context.FontSize) ?? float.MaxValue;
        }

        return (min, max);
    }

    /// <summary>
    /// Represents a single flex line in a wrapped flex container.
    /// </summary>
    /// <param name="StartIndex">Index of first child in this line.</param>
    /// <param name="Count">Number of children in this line.</param>
    /// <param name="MainAxisSize">Total consumed size on the main axis.</param>
    /// <param name="CrossAxisSize">Maximum cross-axis size of children in this line.</param>
    private readonly record struct FlexLine(int StartIndex, int Count, float MainAxisSize, float CrossAxisSize);

    /// <summary>
    /// Breaks children into flex lines using greedy line-breaking algorithm.
    /// Uses flex basis (clamped by min/max) for line-breaking decisions, per Yoga FlexLine.cpp.
    /// </summary>
    private List<FlexLine> CalculateFlexLines(
        IReadOnlyList<LayoutNode> children,
        float availableMainSize,
        bool isColumn,
        float mainGap,
        LayoutContext context)
    {
        var lines = new List<FlexLine>();
        var startIndex = 0;
        var itemsInLine = 0;
        var lineMainSize = 0f;
        var lineCrossSize = 0f;

        for (var i = 0; i < children.Count; i++)
        {
            var child = children[i];
            if (child.Element.Display == Display.None) continue;
            if (child.Element.Position == Position.Absolute) continue;

            // Use flex basis clamped by min/max for line-breaking (Yoga: boundAxisWithinMinAndMax)
            var childBasis = ResolveFlexBasis(child.Element, child, isColumn, context);
            var (childMin, childMax) = ResolveMinMax(child.Element, isColumn, context);
            var childMainSize = ClampSize(childBasis, childMin, childMax);

            // Include main-axis margins
            var childMargin = PaddingParser.Parse(child.Element.Margin, context.ContainerWidth, context.FontSize).ClampNegatives();
            var childMarginMain = isColumn ? childMargin.Top + childMargin.Bottom : childMargin.Left + childMargin.Right;
            var childMarginCross = isColumn ? childMargin.Left + childMargin.Right : childMargin.Top + childMargin.Bottom;

            var totalChildMain = childMainSize + childMarginMain;
            var gapBefore = itemsInLine > 0 ? mainGap : 0f;

            // Check if child exceeds remaining space on current line
            if (itemsInLine > 0 && lineMainSize + gapBefore + totalChildMain > availableMainSize)
            {
                // Finalize current line
                lines.Add(new FlexLine(startIndex, itemsInLine, lineMainSize, lineCrossSize));
                if (lines.Count >= _limits.MaxFlexLines)
                    throw new InvalidOperationException($"Maximum flex lines ({_limits.MaxFlexLines}) exceeded.");

                // Start new line with this child
                startIndex = i;
                itemsInLine = 1;
                lineMainSize = totalChildMain;
                var childCrossSize = (isColumn ? child.Width : child.Height) + childMarginCross;
                lineCrossSize = childCrossSize;
            }
            else
            {
                lineMainSize += gapBefore + totalChildMain;
                var childCrossSize = (isColumn ? child.Width : child.Height) + childMarginCross;
                lineCrossSize = Math.Max(lineCrossSize, childCrossSize);
                itemsInLine++;
            }
        }

        // Don't forget the last line
        if (itemsInLine > 0)
        {
            if (lines.Count >= _limits.MaxFlexLines)
                throw new InvalidOperationException($"Maximum flex lines ({_limits.MaxFlexLines}) exceeded.");
            lines.Add(new FlexLine(startIndex, itemsInLine, lineMainSize, lineCrossSize));
        }

        return lines;
    }

    /// <summary>
    /// Resolves flex sizes for a single line and positions children on the main axis.
    /// Reuses the Phase 2 iterative freeze algorithm on a subset of children.
    /// </summary>
    private static void ResolveFlexForLine(LayoutNode node, FlexElement flex, LayoutContext context,
        PaddingValues padding, float mainGap, FlexLine line, bool isColumn, float availableMainSize)
    {
        var lineChildren = new List<LayoutNode>();
        for (var i = line.StartIndex; i < line.StartIndex + line.Count; i++)
        {
            if (node.Children[i].Element.Display == Display.None) continue;
            lineChildren.Add(node.Children[i]);
        }

        if (lineChildren.Count == 0) return;

        var itemCount = lineChildren.Count;
        Span<float> bases = itemCount <= 32 ? stackalloc float[itemCount] : new float[itemCount];
        Span<float> sizes = itemCount <= 32 ? stackalloc float[itemCount] : new float[itemCount];
        Span<bool> frozen = itemCount <= 32 ? stackalloc bool[itemCount] : new bool[itemCount];

        // Compute margins
        var totalMarginMain = 0f;
        for (var i = 0; i < itemCount; i++)
        {
            var childMargin = PaddingParser.Parse(lineChildren[i].Element.Margin, context.ContainerWidth, context.FontSize).ClampNegatives();
            totalMarginMain += isColumn ? childMargin.Top + childMargin.Bottom : childMargin.Left + childMargin.Right;
        }

        var totalGaps = mainGap * Math.Max(0, itemCount - 1);

        // Resolve flex basis for each item in line
        var totalBases = 0f;
        for (var i = 0; i < itemCount; i++)
        {
            var child = lineChildren[i];
            bases[i] = ResolveFlexBasis(child.Element, child, isColumn, context);
            sizes[i] = bases[i];
            frozen[i] = false;
            totalBases += bases[i];
        }

        // Determine grow vs shrink
        var isAutoSize = isColumn && node.Height == 0;
        var effectiveAvailable = isAutoSize ? totalBases + totalGaps + totalMarginMain : availableMainSize;
        var initialFreeSpace = effectiveAvailable - totalBases - totalGaps - totalMarginMain;
        var isGrowing = initialFreeSpace > 0;

        // Freeze zero-factor items
        for (var i = 0; i < itemCount; i++)
        {
            if (frozen[i]) continue;
            var child = lineChildren[i];
            if (isGrowing && GetFlexGrow(child.Element) == 0)
                frozen[i] = true;
            else if (!isGrowing && GetFlexShrink(child.Element) == 0)
                frozen[i] = true;
        }

        // Iterative freeze loop (only when constrained)
        if (!isAutoSize)
        {
            for (var iteration = 0; iteration < itemCount + 1; iteration++)
            {
                var unfrozenFreeSpace = effectiveAvailable - totalGaps - totalMarginMain;
                for (var i = 0; i < itemCount; i++)
                    unfrozenFreeSpace -= frozen[i] ? sizes[i] : bases[i];

                var totalGrowFactors = 0f;
                var totalShrinkScaled = 0f;
                for (var i = 0; i < itemCount; i++)
                {
                    if (frozen[i]) continue;
                    totalGrowFactors += GetFlexGrow(lineChildren[i].Element);
                    totalShrinkScaled += GetFlexShrink(lineChildren[i].Element) * bases[i];
                }

                // Factor flooring
                if (totalGrowFactors > 0 && totalGrowFactors < 1) totalGrowFactors = 1;
                if (totalShrinkScaled > 0 && totalShrinkScaled < 1) totalShrinkScaled = 1;

                var anyNewlyFrozen = false;
                for (var i = 0; i < itemCount; i++)
                {
                    if (frozen[i]) continue;
                    var child = lineChildren[i];
                    var (minSize, maxSize) = ResolveMinMax(child.Element, isColumn, context);

                    float hypothetical;
                    if (isGrowing)
                    {
                        var grow = GetFlexGrow(child.Element);
                        hypothetical = grow > 0 && totalGrowFactors > 0
                            ? bases[i] + unfrozenFreeSpace * grow / totalGrowFactors
                            : bases[i];
                    }
                    else
                    {
                        var shrink = GetFlexShrink(child.Element);
                        var scaledFactor = shrink * bases[i];
                        hypothetical = totalShrinkScaled > 0 && scaledFactor > 0
                            ? bases[i] + unfrozenFreeSpace * scaledFactor / totalShrinkScaled
                            : bases[i];
                    }

                    var clamped = ClampSize(hypothetical, minSize, maxSize);
                    if (Math.Abs(clamped - hypothetical) > 0.01f)
                    {
                        sizes[i] = clamped;
                        frozen[i] = true;
                        anyNewlyFrozen = true;
                    }
                    else
                    {
                        sizes[i] = hypothetical;
                    }
                }

                if (!anyNewlyFrozen) break;
            }
        }

        // Apply resolved sizes
        for (var i = 0; i < itemCount; i++)
        {
            if (isColumn)
                lineChildren[i].Height = Math.Max(0, sizes[i]);
            else
                lineChildren[i].Width = Math.Max(0, sizes[i]);
        }

        // Recalculate freeSpace for justify-content
        var totalSized = 0f;
        for (var i = 0; i < itemCount; i++)
            totalSized += sizes[i];
        var freeSpace = effectiveAvailable - totalSized - totalGaps - totalMarginMain;

        // Main-axis positioning with justify-content
        float mainStart = isColumn ? padding.Top : padding.Left;
        var lineGap = mainGap;

        var effectiveJustify = freeSpace < 0 ? flex.Justify switch
        {
            JustifyContent.SpaceBetween => JustifyContent.Start,
            JustifyContent.SpaceAround => JustifyContent.Start,
            JustifyContent.SpaceEvenly => JustifyContent.Start,
            _ => flex.Justify
        } : flex.Justify;

        switch (effectiveJustify)
        {
            case JustifyContent.Center:
                mainStart += freeSpace / 2;
                break;
            case JustifyContent.End:
                mainStart += freeSpace;
                break;
            case JustifyContent.SpaceBetween:
                if (itemCount > 1) lineGap += freeSpace / (itemCount - 1);
                break;
            case JustifyContent.SpaceAround:
                if (itemCount > 0)
                {
                    var space = freeSpace / itemCount;
                    mainStart += space / 2;
                    lineGap += space;
                }
                break;
            case JustifyContent.SpaceEvenly:
                if (itemCount > 0)
                {
                    var space = freeSpace / (itemCount + 1);
                    mainStart += space;
                    lineGap += space;
                }
                break;
        }

        // Position children on main axis
        var pos = mainStart;
        for (var i = 0; i < itemCount; i++)
        {
            var child = lineChildren[i];
            var childMargin = PaddingParser.Parse(child.Element.Margin, context.ContainerWidth, context.FontSize).ClampNegatives();

            if (isColumn)
            {
                child.Y = pos + childMargin.Top;
                pos += sizes[i] + lineGap + childMargin.Top + childMargin.Bottom;
            }
            else
            {
                child.X = pos + childMargin.Left;
                pos += sizes[i] + lineGap + childMargin.Left + childMargin.Right;
            }
        }
    }

    /// <summary>
    /// Lays out children in a wrapped flex container.
    /// Handles line breaking, per-line flex resolution, align-content distribution,
    /// and wrap-reverse.
    /// </summary>
    private void LayoutWrappedFlex(LayoutNode node, FlexElement flex, LayoutContext context, PaddingValues padding, float mainGap, float crossGap)
    {
        var isColumn = flex.Direction is FlexDirection.Column or FlexDirection.ColumnReverse;
        var availableMainSize = isColumn
            ? (node.Height > 0 ? node.Height - padding.Vertical : float.MaxValue)
            : node.Width - padding.Horizontal;

        // Step 1: Break children into flex lines
        var lines = CalculateFlexLines(node.Children, availableMainSize, isColumn, mainGap, context);

        if (lines.Count == 0) return;

        // Step 2: Per-line flex resolution and main-axis positioning
        for (var lineIdx = 0; lineIdx < lines.Count; lineIdx++)
        {
            var line = lines[lineIdx];
            ResolveFlexForLine(node, flex, context, padding, mainGap, line, isColumn, availableMainSize);
        }

        // Step 3: Calculate total lines cross-axis size
        var totalLinesCrossSize = 0f;
        for (var i = 0; i < lines.Count; i++)
            totalLinesCrossSize += lines[i].CrossAxisSize;

        var totalCrossGaps = crossGap * Math.Max(0, lines.Count - 1);

        // Available cross-axis size
        float availableCrossSize;
        if (isColumn)
        {
            var hasExplicitWidth = !string.IsNullOrEmpty(flex.Width);
            availableCrossSize = hasExplicitWidth
                ? node.Width - padding.Horizontal
                : totalLinesCrossSize + totalCrossGaps; // auto-width
        }
        else
        {
            availableCrossSize = node.Height > 0
                ? node.Height - padding.Vertical
                : totalLinesCrossSize + totalCrossGaps; // auto-height
        }

        var crossFreeSpace = availableCrossSize - totalLinesCrossSize - totalCrossGaps;

        // Step 4: Align-content distribution
        // Overflow fallback: space-distribution modes -> Start when crossFreeSpace < 0
        var effectiveAlignContent = crossFreeSpace < 0 ? flex.AlignContent switch
        {
            AlignContent.SpaceBetween => AlignContent.Start,
            AlignContent.Stretch => AlignContent.Start,
            AlignContent.SpaceAround => AlignContent.Start,
            AlignContent.SpaceEvenly => AlignContent.Start,
            _ => flex.AlignContent
        } : flex.AlignContent;

        // WrapReverse reverses line stacking direction. Stretch distributes extra space
        // between lines which conflicts with the post-hoc position flip.
        // Fallback to Start to ensure correct flipped positions.
        if (flex.Wrap == FlexWrap.WrapReverse && effectiveAlignContent == AlignContent.Stretch)
            effectiveAlignContent = AlignContent.Start;

        var leadingCrossDim = 0f;
        var betweenCrossDim = 0f;
        var extraPerLine = 0f;

        switch (effectiveAlignContent)
        {
            case AlignContent.Start:
                break;
            case AlignContent.Center:
                leadingCrossDim = crossFreeSpace / 2;
                break;
            case AlignContent.End:
                leadingCrossDim = crossFreeSpace;
                break;
            case AlignContent.Stretch:
                if (lines.Count > 0)
                    extraPerLine = crossFreeSpace / lines.Count;
                break;
            case AlignContent.SpaceBetween:
                if (lines.Count > 1)
                    betweenCrossDim = crossFreeSpace / (lines.Count - 1);
                break;
            case AlignContent.SpaceAround:
                if (lines.Count > 0)
                {
                    leadingCrossDim = crossFreeSpace / (2 * lines.Count);
                    betweenCrossDim = crossFreeSpace / lines.Count;
                }
                break;
            case AlignContent.SpaceEvenly:
                if (lines.Count > 0)
                {
                    leadingCrossDim = crossFreeSpace / (lines.Count + 1);
                    betweenCrossDim = leadingCrossDim;
                }
                break;
        }

        // Step 5: Position lines on cross-axis
        var crossLead = (isColumn ? padding.Left : padding.Top) + leadingCrossDim;

        for (var lineIdx = 0; lineIdx < lines.Count; lineIdx++)
        {
            var line = lines[lineIdx];
            var lineHeight = line.CrossAxisSize + extraPerLine;

            if (lineIdx > 0)
                crossLead += crossGap;

            // Position children within this line on the cross axis
            for (var i = line.StartIndex; i < line.StartIndex + line.Count; i++)
            {
                var child = node.Children[i];
                if (child.Element.Display == Display.None) continue;
                if (child.Element.Position == Position.Absolute) continue;

                var childMargin = PaddingParser.Parse(child.Element.Margin, context.ContainerWidth, context.FontSize).ClampNegatives();

                if (isColumn)
                {
                    // Column: cross axis is X
                    var effectiveAlign = GetEffectiveAlign(child.Element, flex.Align);
                    child.X = childMargin.Left + effectiveAlign switch
                    {
                        AlignItems.Start => crossLead,
                        AlignItems.Center => crossLead + (lineHeight - child.Width - childMargin.Left - childMargin.Right) / 2,
                        AlignItems.End => crossLead + lineHeight - child.Width - childMargin.Left - childMargin.Right,
                        AlignItems.Stretch => crossLead,
                        _ => crossLead
                    };

                    if (effectiveAlign == AlignItems.Stretch && !HasExplicitWidth(child.Element) && lineHeight > 0)
                    {
                        child.Width = lineHeight - childMargin.Left - childMargin.Right;
                    }
                }
                else
                {
                    // Row: cross axis is Y
                    var effectiveAlign = GetEffectiveAlign(child.Element, flex.Align);
                    child.Y = childMargin.Top + effectiveAlign switch
                    {
                        AlignItems.Start => crossLead,
                        AlignItems.Center => crossLead + (lineHeight - child.Height - childMargin.Top - childMargin.Bottom) / 2,
                        AlignItems.End => crossLead + lineHeight - child.Height - childMargin.Top - childMargin.Bottom,
                        AlignItems.Stretch => crossLead,
                        _ => crossLead
                    };

                    if (effectiveAlign == AlignItems.Stretch && !HasExplicitHeight(child.Element) && lineHeight > 0)
                    {
                        child.Height = lineHeight - childMargin.Top - childMargin.Bottom;
                    }
                }
            }

            crossLead += betweenCrossDim + lineHeight;
        }

        // Step 6: Update container auto-dimensions
        if (isColumn)
        {
            // Column wrap: auto-width = total cross-axis size
            if (string.IsNullOrEmpty(flex.Width))
            {
                node.Width = crossLead + padding.Right;
            }
        }
        else
        {
            // Row wrap: auto-height = total cross-axis size
            if (node.Height == 0)
            {
                node.Height = crossLead + padding.Bottom;
            }
        }

        // Step 7: WrapReverse - flip cross-axis positions
        if (flex.Wrap == FlexWrap.WrapReverse)
        {
            // Use FULL container cross dimension (Yoga: node.measuredDimension(crossAxis))
            var containerCrossSize = isColumn ? node.Width : node.Height;
            foreach (var child in node.Children)
            {
                if (child.Element.Display == Display.None) continue;
                if (child.Element.Position == Position.Absolute) continue;
                if (isColumn)
                    child.X = containerCrossSize - child.X - child.Width;
                else
                    child.Y = containerCrossSize - child.Y - child.Height;
            }
        }

        // Step 8: ColumnReverse / RowReverse - flip main-axis positions
        if (flex.Direction == FlexDirection.ColumnReverse)
        {
            var containerMainSize = node.Height > 0 ? node.Height : crossLead;
            foreach (var child in node.Children)
            {
                if (child.Element.Display == Display.None) continue;
                if (child.Element.Position == Position.Absolute) continue;
                child.Y = containerMainSize - child.Y - child.Height;
            }
        }
        else if (flex.Direction == FlexDirection.RowReverse)
        {
            var containerMainSize = node.Width;
            foreach (var child in node.Children)
            {
                if (child.Element.Display == Display.None) continue;
                if (child.Element.Position == Position.Absolute) continue;
                child.X = containerMainSize - child.X - child.Width;
            }
        }

        ApplyRelativePositioning(node, context);
    }

    /// <summary>
    /// Applies relative positioning offsets to children.
    /// Left takes priority over right, top takes priority over bottom per CSS spec.
    /// </summary>
    /// <param name="node">The parent layout node whose children may be relatively positioned.</param>
    /// <param name="context">The layout context for resolving unit values.</param>
    private static void ApplyRelativePositioning(LayoutNode node, LayoutContext context)
    {
        foreach (var child in node.Children)
        {
            if (child.Element.Position != Position.Relative) continue;

            var left = context.ResolveWidth(child.Element.Left);
            var right = context.ResolveWidth(child.Element.Right);
            var top = context.ResolveHeight(child.Element.Top);
            var bottom = context.ResolveHeight(child.Element.Bottom);

            var offsetX = left ?? (right.HasValue ? -right.Value : 0f);
            var offsetY = top ?? (bottom.HasValue ? -bottom.Value : 0f);

            child.X += offsetX;
            child.Y += offsetY;
        }
    }

    private static bool HasExplicitHeight(TemplateElement element)
    {
        return element switch
        {
            // Row wrap containers compute their own height from line cross-axis sizes
            FlexElement f => !string.IsNullOrEmpty(f.Height)
                || (f.Wrap != FlexWrap.NoWrap && f.Direction is FlexDirection.Row or FlexDirection.RowReverse),
            QrElement q => !string.IsNullOrEmpty(q.Height) || q.Size > 0,
            BarcodeElement b => !string.IsNullOrEmpty(b.Height) || b.BarcodeHeight > 0,
            ImageElement i => !string.IsNullOrEmpty(i.Height) || i.ImageHeight.HasValue,
            _ => !string.IsNullOrEmpty(element.Height)
        };
    }

    private static bool HasExplicitWidth(TemplateElement element)
    {
        return element switch
        {
            // Column wrap containers compute their own width from line cross-axis sizes
            FlexElement f => !string.IsNullOrEmpty(f.Width)
                || (f.Wrap != FlexWrap.NoWrap && f.Direction is FlexDirection.Column or FlexDirection.ColumnReverse),
            QrElement q => !string.IsNullOrEmpty(q.Width) || q.Size > 0,
            BarcodeElement b => !string.IsNullOrEmpty(b.Width) || b.BarcodeWidth > 0,
            ImageElement i => !string.IsNullOrEmpty(i.Width) || i.ImageWidth.HasValue,
            _ => !string.IsNullOrEmpty(element.Width)
        };
    }

    private static void StackChildrenVertically(LayoutNode parent, LayoutContext context)
    {
        var y = 0f;
        foreach (var child in parent.Children)
        {
            child.Y = y;
            y = child.Y + child.Height;
        }
    }

    /// <summary>
    /// Calculates the total height needed to contain all children.
    /// </summary>
    /// <param name="node">The parent layout node.</param>
    /// <returns>The maximum bottom edge of all children.</returns>
    private static float CalculateTotalHeight(LayoutNode node)
    {
        if (node.Children.Count == 0)
            return 0f;

        var maxBottom = 0f;
        foreach (var child in node.Children)
        {
            if (child.Element.Position == Position.Absolute) continue;
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
            if (child.Element.Position == Position.Absolute) continue;
            var right = child.X + child.Width;
            if (right > maxRight)
                maxRight = right;
        }
        return maxRight;
    }
}
