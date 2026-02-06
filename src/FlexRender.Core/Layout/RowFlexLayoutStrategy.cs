using FlexRender.Layout.Units;
using FlexRender.Parsing.Ast;

namespace FlexRender.Layout;

/// <summary>
/// Implements row flex layout (non-wrapped) for flex containers with
/// <see cref="FlexDirection.Row"/> or <see cref="FlexDirection.RowReverse"/>.
/// </summary>
internal sealed class RowFlexLayoutStrategy
{
    /// <summary>
    /// Performs row flex layout on the given node.
    /// </summary>
    internal static void LayoutRowFlex(LayoutNode node, FlexElement flex, LayoutContext context, PaddingValues padding, float gap)
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
            bases[i] = LayoutHelpers.ResolveFlexBasis(child.Element, child, isColumn: false, context);
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
            if (isGrowing && LayoutHelpers.GetFlexGrow(child.Element) == 0)
            {
                frozen[i] = true;
            }
            else if (!isGrowing && LayoutHelpers.GetFlexShrink(child.Element) == 0)
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
                totalGrowFactors += LayoutHelpers.GetFlexGrow(node.Children[i].Element);
                totalShrinkScaled += LayoutHelpers.GetFlexShrink(node.Children[i].Element) * bases[i];
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
                var (minSize, maxSize) = LayoutHelpers.ResolveMinMax(child.Element, isColumn: false, context);

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

        // When container has no explicit height, compute cross-axis size from tallest child.
        // This enables align-items to work even with auto-sized containers.
        if (!hasExplicitHeight)
        {
            var maxChildBottom = 0f;
            foreach (var child in node.Children)
            {
                if (child.Element.Display == Display.None) continue;
                if (child.Element.Position == Position.Absolute) continue;
                var m = PaddingParser.Parse(child.Element.Margin, context.ContainerWidth, context.FontSize).ClampNegatives();
                var totalChildHeight = child.Height + m.Top + m.Bottom;
                if (totalChildHeight > maxChildBottom)
                    maxChildBottom = totalChildHeight;
            }
            crossAxisSize = maxChildBottom;
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
                    var effectiveAlign = LayoutHelpers.GetEffectiveAlign(child.Element, flex.Align);
                    child.Y = mTop + (crossAxisSize > 0 ? effectiveAlign switch
                    {
                        AlignItems.Start => padding.Top,
                        AlignItems.Center => padding.Top + (crossAxisSize - child.Height - mTop - mBottom) / 2,
                        AlignItems.End => padding.Top + crossAxisSize - child.Height - mTop - mBottom,
                        AlignItems.Stretch => padding.Top,
                        _ => padding.Top
                    } : padding.Top);

                    // Stretch height if align is stretch and no explicit height on child
                    var hasAspectHeight = child.Element.AspectRatio.HasValue && child.Element.AspectRatio.Value > 0f
                        && LayoutHelpers.HasExplicitWidth(child.Element) && !LayoutHelpers.HasExplicitHeight(child.Element);
                    if (effectiveAlign == AlignItems.Stretch && !LayoutHelpers.HasExplicitHeight(child.Element) && !hasAspectHeight && crossAxisSize > 0)
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

        LayoutHelpers.ApplyRelativePositioning(node, context);
    }

    /// <summary>
    /// Applies cross-axis auto margin positioning for a child in a row container.
    /// </summary>
    internal static void ApplyRowCrossAxisMargins(
        LayoutNode child, MarginValues margin, FlexElement flex,
        PaddingValues padding, float crossAxisSize, bool hasExplicitHeight)
    {
        var crossAutoCount = margin.CrossAxisAutoCount(isColumn: false);
        var crossFreeSpace = crossAxisSize - child.Height;

        if (crossAutoCount > 0 && crossFreeSpace > 0)
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
            var effectiveAlign = LayoutHelpers.GetEffectiveAlign(child.Element, flex.Align);
            child.Y = margin.Top.ResolvedPixels + (crossAxisSize > 0 ? effectiveAlign switch
            {
                AlignItems.Start => padding.Top,
                AlignItems.Center => padding.Top + (crossAxisSize - child.Height) / 2,
                AlignItems.End => padding.Top + crossAxisSize - child.Height,
                AlignItems.Stretch => padding.Top,
                _ => padding.Top
            } : padding.Top);

            // Stretch height if align is stretch and no explicit height on child
            var hasAspectHeight = child.Element.AspectRatio.HasValue && child.Element.AspectRatio.Value > 0f
                && LayoutHelpers.HasExplicitWidth(child.Element) && !LayoutHelpers.HasExplicitHeight(child.Element);
            if (effectiveAlign == AlignItems.Stretch && !LayoutHelpers.HasExplicitHeight(child.Element) && !hasAspectHeight && crossAxisSize > 0)
            {
                child.Height = crossAxisSize;
            }
        }
    }
}
