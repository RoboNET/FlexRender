using FlexRender.Configuration;
using FlexRender.Layout.Units;
using FlexRender.Parsing.Ast;

namespace FlexRender.Layout;

/// <summary>
/// Implements wrapped flex layout for containers with <see cref="FlexWrap.Wrap"/>
/// or <see cref="FlexWrap.WrapReverse"/>.
/// </summary>
internal sealed class WrappedFlexLayoutStrategy
{
    private readonly ResourceLimits _limits;

    /// <summary>
    /// Initializes a new instance of the <see cref="WrappedFlexLayoutStrategy"/> class.
    /// </summary>
    /// <param name="limits">Resource limits for enforcing maximum flex lines.</param>
    internal WrappedFlexLayoutStrategy(ResourceLimits limits)
    {
        _limits = limits;
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
    /// Lays out children in a wrapped flex container.
    /// Handles line breaking, per-line flex resolution, align-content distribution,
    /// and wrap-reverse.
    /// </summary>
    internal void LayoutWrappedFlex(LayoutNode node, FlexElement flex, LayoutContext context, PaddingValues padding, float mainGap, float crossGap)
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
                    var effectiveAlign = LayoutHelpers.GetEffectiveAlign(child.Element, flex.Align);
                    child.X = childMargin.Left + effectiveAlign switch
                    {
                        AlignItems.Start => crossLead,
                        AlignItems.Center => crossLead + (lineHeight - child.Width - childMargin.Left - childMargin.Right) / 2,
                        AlignItems.End => crossLead + lineHeight - child.Width - childMargin.Left - childMargin.Right,
                        AlignItems.Stretch => crossLead,
                        _ => crossLead
                    };

                    if (effectiveAlign == AlignItems.Stretch && !LayoutHelpers.HasExplicitWidth(child.Element) && lineHeight > 0)
                    {
                        child.Width = lineHeight - childMargin.Left - childMargin.Right;
                    }
                }
                else
                {
                    // Row: cross axis is Y
                    var effectiveAlign = LayoutHelpers.GetEffectiveAlign(child.Element, flex.Align);
                    child.Y = childMargin.Top + effectiveAlign switch
                    {
                        AlignItems.Start => crossLead,
                        AlignItems.Center => crossLead + (lineHeight - child.Height - childMargin.Top - childMargin.Bottom) / 2,
                        AlignItems.End => crossLead + lineHeight - child.Height - childMargin.Top - childMargin.Bottom,
                        AlignItems.Stretch => crossLead,
                        _ => crossLead
                    };

                    if (effectiveAlign == AlignItems.Stretch && !LayoutHelpers.HasExplicitHeight(child.Element) && lineHeight > 0)
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

        LayoutHelpers.ApplyRelativePositioning(node, context);
    }

    /// <summary>
    /// Breaks children into flex lines using greedy line-breaking algorithm.
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
            var childBasis = LayoutHelpers.ResolveFlexBasis(child.Element, child, isColumn, context);
            var (childMin, childMax) = LayoutHelpers.ResolveMinMax(child.Element, isColumn, context);
            var childMainSize = LayoutHelpers.ClampSize(childBasis, childMin, childMax);

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
            bases[i] = LayoutHelpers.ResolveFlexBasis(child.Element, child, isColumn, context);
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
            if (isGrowing && LayoutHelpers.GetFlexGrow(child.Element) == 0)
                frozen[i] = true;
            else if (!isGrowing && LayoutHelpers.GetFlexShrink(child.Element) == 0)
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
                    totalGrowFactors += LayoutHelpers.GetFlexGrow(lineChildren[i].Element);
                    totalShrinkScaled += LayoutHelpers.GetFlexShrink(lineChildren[i].Element) * bases[i];
                }

                // Factor flooring
                if (totalGrowFactors > 0 && totalGrowFactors < 1) totalGrowFactors = 1;
                if (totalShrinkScaled > 0 && totalShrinkScaled < 1) totalShrinkScaled = 1;

                var anyNewlyFrozen = false;
                for (var i = 0; i < itemCount; i++)
                {
                    if (frozen[i]) continue;
                    var child = lineChildren[i];
                    var (minSize, maxSize) = LayoutHelpers.ResolveMinMax(child.Element, isColumn, context);

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
}
