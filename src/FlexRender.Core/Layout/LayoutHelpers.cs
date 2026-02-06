using FlexRender.Layout.Units;
using FlexRender.Parsing.Ast;

namespace FlexRender.Layout;

/// <summary>
/// Shared utility methods for flex layout algorithms.
/// </summary>
internal static class LayoutHelpers
{
    /// <summary>
    /// Gets the flex-grow value for a template element.
    /// </summary>
    /// <param name="element">The template element to query.</param>
    /// <returns>The flex-grow value (defaults to 0).</returns>
    /// <exception cref="ArgumentException">Thrown when the grow value is negative.</exception>
    internal static float GetFlexGrow(TemplateElement element)
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
    internal static float GetFlexShrink(TemplateElement element)
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
    internal static AlignItems GetEffectiveAlign(TemplateElement element, AlignItems parentAlign)
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
    internal static float ClampSize(float value, float min, float max)
    {
        var effectiveMax = Math.Max(min, max);
        return Math.Clamp(value, min, effectiveMax);
    }

    /// <summary>
    /// Resolves the flex basis for a child element.
    /// Priority: explicit basis > main-axis dimension > intrinsic size.
    /// Result is clamped to at least the element's padding on the main axis.
    /// </summary>
    internal static float ResolveFlexBasis(TemplateElement element, LayoutNode childNode,
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
    internal static (float min, float max) ResolveMinMax(TemplateElement element,
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
    /// Applies relative positioning offsets to children.
    /// Left takes priority over right, top takes priority over bottom per CSS spec.
    /// </summary>
    /// <param name="node">The parent layout node whose children may be relatively positioned.</param>
    /// <param name="context">The layout context for resolving unit values.</param>
    internal static void ApplyRelativePositioning(LayoutNode node, LayoutContext context)
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

    /// <summary>
    /// Checks whether a template element has an explicit height.
    /// </summary>
    internal static bool HasExplicitHeight(TemplateElement element)
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

    /// <summary>
    /// Checks whether a template element has an explicit width.
    /// </summary>
    internal static bool HasExplicitWidth(TemplateElement element)
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

    /// <summary>
    /// Calculates the total height needed to contain all children.
    /// </summary>
    /// <param name="node">The parent layout node.</param>
    /// <returns>The maximum bottom edge of all children.</returns>
    internal static float CalculateTotalHeight(LayoutNode node)
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
    internal static float CalculateTotalWidth(LayoutNode node)
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
