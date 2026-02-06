using FlexRender.Layout.Units;
using FlexRender.Parsing.Ast;

namespace FlexRender.Layout;

/// <summary>
/// Pass 1 intrinsic measurement — bottom-up traversal that computes
/// <see cref="IntrinsicSize"/> for every element in the tree.
/// </summary>
internal sealed class IntrinsicMeasurer
{
    private const float DefaultFontSize = 16f;
    private const float LineHeightMultiplier = 1.4f;

    private readonly LayoutEngine _engine;

    /// <summary>
    /// Initializes a new instance of the <see cref="IntrinsicMeasurer"/> class.
    /// </summary>
    /// <param name="engine">The owning layout engine providing TextMeasurer and BaseFontSize.</param>
    internal IntrinsicMeasurer(LayoutEngine engine)
    {
        _engine = engine;
    }

    /// <summary>
    /// Measures intrinsic sizes for all elements in the tree bottom-up.
    /// </summary>
    /// <param name="root">The root element to measure.</param>
    /// <returns>
    /// A dictionary mapping each element (by reference identity) to its computed
    /// <see cref="IntrinsicSize"/>.
    /// </returns>
    internal Dictionary<TemplateElement, IntrinsicSize> MeasureAll(TemplateElement root)
    {
        var sizes = new Dictionary<TemplateElement, IntrinsicSize>(ReferenceEqualityComparer.Instance);
        MeasureIntrinsic(root, sizes);
        return sizes;
    }

    /// <summary>
    /// Dispatches intrinsic measurement to the appropriate element-specific method.
    /// </summary>
    internal IntrinsicSize MeasureIntrinsic(TemplateElement element, Dictionary<TemplateElement, IntrinsicSize> sizes)
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
    /// </summary>
    private IntrinsicSize MeasureTextIntrinsic(TextElement text)
    {
        var fontSize = FontSizeResolver.Resolve(text.Size, _engine.BaseFontSize);

        float contentWidth;
        float contentHeight;

        if (_engine.TextMeasurer != null && !string.IsNullOrEmpty(text.Content))
        {
            var measured = _engine.TextMeasurer(text, fontSize, float.MaxValue);
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
    /// </summary>
    private static IntrinsicSize MeasureQrIntrinsic(QrElement qr)
    {
        // Default to 100px if Size not specified (will be overridden by container dimensions in layout)
        float size = qr.Size ?? 100f;
        var intrinsic = new IntrinsicSize(size, size, size, size);

        return ApplyPaddingAndMargin(intrinsic, qr.Padding, qr.Margin);
    }

    /// <summary>
    /// Measures intrinsic size for a barcode element.
    /// </summary>
    private static IntrinsicSize MeasureBarcodeIntrinsic(BarcodeElement barcode)
    {
        // Default to 200×80 if dimensions not specified (will be overridden by container dimensions in layout)
        float width = barcode.BarcodeWidth ?? 200f;
        float height = barcode.BarcodeHeight ?? 80f;
        var intrinsic = new IntrinsicSize(width, width, height, height);

        return ApplyPaddingAndMargin(intrinsic, barcode.Padding, barcode.Margin);
    }

    /// <summary>
    /// Measures intrinsic size for an image element.
    /// </summary>
    private static IntrinsicSize MeasureImageIntrinsic(ImageElement image)
    {
        float width = image.ImageWidth ?? 0f;
        float height = image.ImageHeight ?? 0f;
        var intrinsic = new IntrinsicSize(width, width, height, height);

        return ApplyPaddingAndMargin(intrinsic, image.Padding, image.Margin);
    }

    /// <summary>
    /// Measures intrinsic size for a separator element.
    /// </summary>
    private static IntrinsicSize MeasureSeparatorIntrinsic(SeparatorElement separator)
    {
        var intrinsic = separator.Orientation == SeparatorOrientation.Horizontal
            ? new IntrinsicSize(0f, 0f, separator.Thickness, separator.Thickness)
            : new IntrinsicSize(separator.Thickness, separator.Thickness, 0f, 0f);

        return ApplyPaddingAndMargin(intrinsic, separator.Padding, separator.Margin);
    }

    /// <summary>
    /// Measures intrinsic size for a flex container element.
    /// </summary>
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
    /// </summary>
    internal static float ParseAbsolutePixelValue(string? value, float defaultValue)
    {
        if (string.IsNullOrWhiteSpace(value))
            return defaultValue;

        var unit = UnitParser.Parse(value);
        return unit.Resolve(0f, DefaultFontSize) ?? defaultValue;
    }

    /// <summary>
    /// Applies padding and margin to an intrinsic size measurement.
    /// </summary>
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
}
