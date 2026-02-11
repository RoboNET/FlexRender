using FlexRender.Layout.Units;
using FlexRender.Parsing.Ast;

namespace FlexRender.Layout;

/// <summary>
/// Implements column flex layout (non-wrapped) for flex containers with
/// <see cref="FlexDirection.Column"/> or <see cref="FlexDirection.ColumnReverse"/>.
/// </summary>
internal sealed class ColumnFlexLayoutStrategy
{
    /// <summary>
    /// Performs column flex layout on the given node.
    /// </summary>
    internal static void LayoutColumnFlex(LayoutNode node, FlexElement flex, LayoutContext context, PaddingValues padding, float gap)
    {
        var crossAxisSize = node.Width - padding.Horizontal;

        // Count visible children and compute margins
        var totalMarginHeight = 0f;
        var visibleCount = 0;
        foreach (var child in node.Children)
        {
            if (child.Element.Display.Value == Display.None) continue;
            if (child.Element.Position.Value == Position.Absolute) continue;
            var childMargin = PaddingParser.Parse(child.Element.Margin.Value, context.ContainerWidth, context.FontSize).ClampNegatives();
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
            if (child.Element.Display.Value == Display.None || child.Element.Position.Value == Position.Absolute)
            {
                bases[i] = 0f;
                sizes[i] = 0f;
                frozen[i] = true;
                continue;
            }
            bases[i] = LayoutHelpers.ResolveFlexBasis(child.Element, child, isColumn: true, context);
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
            if (isGrowing && LayoutHelpers.GetFlexGrow(child.Element) == 0)
            {
                frozen[i] = true;
            }
            else if (!isGrowing && LayoutHelpers.GetFlexShrink(child.Element) == 0)
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
                    totalGrowFactors += LayoutHelpers.GetFlexGrow(node.Children[i].Element);
                    totalShrinkScaled += LayoutHelpers.GetFlexShrink(node.Children[i].Element) * bases[i];
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
                    var (minSize, maxSize) = LayoutHelpers.ResolveMinMax(child.Element, isColumn: true, context);

                    float hypothetical;
                    if (isGrowing)
                    {
                        var grow = LayoutHelpers.GetFlexGrow(child.Element);
                        hypothetical = grow > 0 && totalGrowFactors > 0
                            ? bases[i] + unfrozenFreeSpace * grow / totalGrowFactors
                            : bases[i];
                    }
                    else
                    {
                        var shrink = LayoutHelpers.GetFlexShrink(child.Element);
                        var scaledFactor = shrink * bases[i];
                        hypothetical = totalShrinkScaled > 0 && scaledFactor > 0
                            ? bases[i] + unfrozenFreeSpace * scaledFactor / totalShrinkScaled
                            : bases[i];
                    }

                    var clamped = LayoutHelpers.ClampSize(hypothetical, minSize, maxSize);
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

        // Apply resolved sizes to child nodes, tracking which ones changed
        Span<bool> heightChanged = itemCount <= 32 ? stackalloc bool[itemCount] : new bool[itemCount];
        for (var i = 0; i < itemCount; i++)
        {
            if (node.Children[i].Element.Display.Value == Display.None) continue;
            if (node.Children[i].Element.Position.Value == Position.Absolute) continue;
            var newHeight = Math.Max(0, sizes[i]);
            heightChanged[i] = Math.Abs(node.Children[i].Height - newHeight) > 0.01f;
            node.Children[i].Height = newHeight;
        }

        // After flex grow/shrink changes row container heights, their children's
        // cross-axis (vertical) positions are stale. Re-align grandchildren so that
        // align-items: center, margin: auto 0, etc. use the NEW row height.
        RealignRowChildren(node, heightChanged, context);

        // Re-apply aspect ratio after flex resolution changed main-axis size (column: main=height)
        foreach (var child in node.Children)
        {
            if (child.Element.Position.Value == Position.Absolute) continue;
            if (child.Element.Display.Value == Display.None) continue;
            if (!child.Element.AspectRatio.Value.HasValue || child.Element.AspectRatio.Value.Value <= 0f) continue;
            var ratio = child.Element.AspectRatio.Value.Value;
            child.Width = child.Height * ratio;
        }

        // Recalculate freeSpace after flex resolution for justify-content
        var totalSized = 0f;
        for (var i = 0; i < itemCount; i++)
        {
            if (node.Children[i].Element.Display.Value == Display.None) continue;
            if (node.Children[i].Element.Position.Value == Position.Absolute) continue;
            totalSized += sizes[i];
        }
        var freeSpace = availableHeight - totalSized - totalGaps - totalMarginHeight;

        // Check for auto margins on the main axis (vertical for column)
        var hasMainAutoMargins = false;
        var totalMainAutoCount = 0;
        foreach (var child in node.Children)
        {
            if (child.Element.Display.Value == Display.None) continue;
            if (child.Element.Position.Value == Position.Absolute) continue;
            var m = PaddingParser.ParseMargin(child.Element.Margin.Value, context.ContainerWidth, context.FontSize);
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
                if (child.Element.Display.Value == Display.None) continue;
                if (child.Element.Position.Value == Position.Absolute) continue;
                if (!first) pos += gap;
                first = false;

                var m = PaddingParser.ParseMargin(child.Element.Margin.Value, context.ContainerWidth, context.FontSize);
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
            var effectiveJustify = freeSpace < 0 ? flex.Justify.Value switch
            {
                JustifyContent.SpaceBetween => JustifyContent.Start,
                JustifyContent.SpaceAround => JustifyContent.Start,
                JustifyContent.SpaceEvenly => JustifyContent.Start,
                _ => flex.Justify.Value
            } : flex.Justify.Value;

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
                if (child.Element.Display.Value == Display.None) continue;
                if (child.Element.Position.Value == Position.Absolute) continue;

                // Parse child margin once (supports auto margins, clamp negatives)
                var m = PaddingParser.ParseMargin(child.Element.Margin.Value, context.ContainerWidth, context.FontSize);
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
                    var effectiveAlign = LayoutHelpers.GetEffectiveAlign(child.Element, flex.Align.Value);
                    child.X = mLeft + effectiveAlign switch
                    {
                        AlignItems.Start => padding.Left,
                        AlignItems.Center => padding.Left + (crossAxisSize - child.Width - mLeft - mRight) / 2,
                        AlignItems.End => padding.Left + crossAxisSize - child.Width - mLeft - mRight,
                        AlignItems.Stretch => padding.Left,
                        _ => padding.Left
                    };

                    // Stretch width if align is stretch and no explicit width
                    var hasAspectWidth = child.Element.AspectRatio.Value.HasValue && child.Element.AspectRatio.Value.Value > 0f
                        && LayoutHelpers.HasExplicitHeight(child.Element) && !LayoutHelpers.HasExplicitWidth(child.Element);
                    if (effectiveAlign == AlignItems.Stretch && !LayoutHelpers.HasExplicitWidth(child.Element) && !hasAspectWidth && crossAxisSize > 0)
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
        if (flex.Direction.Value == FlexDirection.ColumnReverse)
        {
            var containerHeight = node.Height > 0 ? node.Height : yEnd;
            foreach (var child in node.Children)
            {
                if (child.Element.Display.Value == Display.None) continue;
                if (child.Element.Position.Value == Position.Absolute) continue;
                child.Y = containerHeight - child.Y - child.Height;
            }
        }

        LayoutHelpers.ApplyRelativePositioning(node, context);
    }

    /// <summary>
    /// Applies cross-axis auto margin positioning for a child in a column container.
    /// </summary>
    internal static void ApplyColumnCrossAxisMargins(
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
            var effectiveAlign = LayoutHelpers.GetEffectiveAlign(child.Element, flex.Align.Value);
            child.X = margin.Left.ResolvedPixels + effectiveAlign switch
            {
                AlignItems.Start => padding.Left,
                AlignItems.Center => padding.Left + (crossAxisSize - child.Width) / 2,
                AlignItems.End => padding.Left + crossAxisSize - child.Width,
                AlignItems.Stretch => padding.Left,
                _ => padding.Left
            };

            // Stretch width if align is stretch and no explicit width
            var hasAspectWidth = child.Element.AspectRatio.Value.HasValue && child.Element.AspectRatio.Value.Value > 0f
                && LayoutHelpers.HasExplicitHeight(child.Element) && !LayoutHelpers.HasExplicitWidth(child.Element);
            if (effectiveAlign == AlignItems.Stretch && !LayoutHelpers.HasExplicitWidth(child.Element) && !hasAspectWidth && crossAxisSize > 0)
            {
                child.Width = crossAxisSize;
            }
        }
    }

    /// <summary>
    /// After flex grow/shrink changes row container heights, re-aligns
    /// grandchildren along the cross axis (vertical for row containers).
    /// Only processes children whose height actually changed during flex resolution.
    /// This replicates the cross-axis alignment logic from
    /// <see cref="RowFlexLayoutStrategy"/> so that align-items: center,
    /// margin: auto 0, and similar properties use the updated row height.
    /// </summary>
    /// <param name="node">The column container whose children may be row flex containers.</param>
    /// <param name="heightChanged">Per-child flags indicating which heights changed during flex resolution.</param>
    /// <param name="context">The layout context for resolving units.</param>
    private static void RealignRowChildren(LayoutNode node, Span<bool> heightChanged, LayoutContext context)
    {
        for (var i = 0; i < node.Children.Count; i++)
        {
            if (!heightChanged[i]) continue;

            var child = node.Children[i];
            if (child.Element.Display.Value == Display.None) continue;
            if (child.Element.Position.Value == Position.Absolute) continue;
            if (child.Element is not FlexElement childFlex) continue;
            if (childFlex.Direction.Value is not (FlexDirection.Row or FlexDirection.RowReverse)) continue;

            var childPadding = PaddingParser.Parse(childFlex.Padding.Value, context.ContainerWidth, context.FontSize).ClampNegatives();
            var newCrossAxisSize = child.Height - childPadding.Vertical;
            if (newCrossAxisSize <= 0) continue;

            // For row containers, hasExplicitHeight is true since the height was set by flex grow/shrink
            foreach (var grandchild in child.Children)
            {
                if (grandchild.Element.Display.Value == Display.None) continue;
                if (grandchild.Element.Position.Value == Position.Absolute) continue;

                var m = PaddingParser.ParseMargin(grandchild.Element.Margin.Value, context.ContainerWidth, context.FontSize);

                if (m.CrossAxisAutoCount(isColumn: false) > 0)
                {
                    RowFlexLayoutStrategy.ApplyRowCrossAxisMargins(
                        grandchild, m, childFlex, childPadding, newCrossAxisSize, hasExplicitHeight: true);
                }
                else
                {
                    var mTop = Math.Max(0f, m.Top.ResolvedPixels);
                    var mBottom = Math.Max(0f, m.Bottom.ResolvedPixels);

                    var effectiveAlign = LayoutHelpers.GetEffectiveAlign(grandchild.Element, childFlex.Align.Value);
                    grandchild.Y = mTop + (newCrossAxisSize > 0 ? effectiveAlign switch
                    {
                        AlignItems.Start => childPadding.Top,
                        AlignItems.Center => childPadding.Top + (newCrossAxisSize - grandchild.Height - mTop - mBottom) / 2,
                        AlignItems.End => childPadding.Top + newCrossAxisSize - grandchild.Height - mTop - mBottom,
                        AlignItems.Stretch => childPadding.Top,
                        _ => childPadding.Top
                    } : childPadding.Top);

                    // Stretch height if align is stretch and no explicit height on grandchild
                    var hasAspectHeight = grandchild.Element.AspectRatio.Value.HasValue && grandchild.Element.AspectRatio.Value.Value > 0f
                        && LayoutHelpers.HasExplicitWidth(grandchild.Element) && !LayoutHelpers.HasExplicitHeight(grandchild.Element);
                    if (effectiveAlign == AlignItems.Stretch && !LayoutHelpers.HasExplicitHeight(grandchild.Element) && !hasAspectHeight && newCrossAxisSize > 0)
                    {
                        grandchild.Height = newCrossAxisSize - mTop - mBottom;
                    }
                }
            }
        }
    }
}
