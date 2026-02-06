# Yoga Layout Algorithm Reference

Source: facebook/yoga, CalculateLayout.cpp (~2436 lines)

## 11-Step Algorithm

1. **STEP 1**: Calculate values -- determine mainAxis, crossAxis, available sizes
2. **STEP 2**: Determine available size in main and cross directions (with min/max constraints)
3. **STEP 3**: Determine flex basis for each item -- `computeFlexBasisForChildren()`
4. **STEP 4**: Collect flex items into flex lines -- `calculateFlexLine()` (line breaking for wrap)
5. **STEP 5**: Resolving flexible lengths on main axis -- `distributeFreeSpaceFirstPass()` + `distributeFreeSpaceSecondPass()`
6. **STEP 6**: Main-axis justification & cross-axis size determination -- `justifyMainAxis()`
7. **STEP 7**: Cross-axis alignment -- align-items / align-self for each child
8. **STEP 8**: Multi-line content alignment -- align-content for multi-line containers
9. **STEP 9**: Computing final dimensions -- bound to min/max
10. **STEP 10**: Setting trailing positions for children
11. **STEP 11**: Sizing and positioning absolute children -- `layoutAbsoluteDescendants()`

## Flex Basis Resolution (Step 3)

Priority order:
1. If `flexBasis` set explicitly and mainAxisSize defined -> use flexBasis (clamped to padding+border)
2. If main axis = row and width defined -> width as flex basis
3. If main axis = column and height defined -> height as flex basis
4. Otherwise -> measure child for intrinsic size

## Flexible Length Resolution (Step 5) -- Two-Pass

**First pass** (`distributeFreeSpaceFirstPass`):
- Identify items whose min/max constraints trigger
- "Freeze" their sizes
- Exclude from remaining free space

**Second pass** (`distributeFreeSpaceSecondPass`):
- Distribute remaining space among unfrozen items
- Shrink: `childSize = flexBasis + (remainingFreeSpace / totalShrinkScaledFactors) * shrinkScaledFactor`
- Grow: `childSize = flexBasis + (remainingFreeSpace / totalGrowFactors) * growFactor`
- **Shrink factor scaled by basis**: `shrinkScaledFactor = -flexShrink * flexBasis`

NOTE: Yoga uses simplified two-pass approach. CSS spec requires variable iterations.

## Line Breaking (Step 4, flex-wrap)

`calculateFlexLine()`:
- Collect items until `sizeConsumed + itemSize + margin + gap <= availableInnerMainDim`
- Skip `display: none` and `position: absolute` items
- Count auto margins
- Collect totalFlexGrowFactors and totalFlexShrinkScaledFactors

## Justify Content (Step 6)

In `justifyMainAxis()`:
- **flex-start**: `leadingMainDim = 0`
- **center**: `leadingMainDim = remainingFreeSpace / 2`
- **flex-end**: `leadingMainDim = remainingFreeSpace`
- **space-between**: `betweenMainDim = remainingFreeSpace / (itemCount - 1)`
- **space-around**: `leadingMainDim = 0.5 * remainingFreeSpace / itemCount; betweenMainDim = leadingMainDim * 2`
- **space-evenly**: `leadingMainDim = remainingFreeSpace / (itemCount + 1); betweenMainDim = leadingMainDim`

**Auto margins**: consume free space INSTEAD of justify-content.

**Overflow fallback** (negative free space):
- SpaceBetween -> FlexStart
- SpaceAround -> FlexStart
- SpaceEvenly -> FlexStart

## Align Content (Step 8, multi-line)

- **flex-start**: lines at start
- **flex-end**: offset by remaining space
- **center**: offset by half remaining
- **stretch**: extra space per line
- **space-between**: distribute between lines (lineCount > 1)
- **space-around**: distribute around lines
- **space-evenly**: distribute evenly (lineCount + 1 slots)

Same overflow fallback as justify-content.

## Cross-Axis Alignment (Step 7)

- `resolveChildAlignment()` -- if align-self == Auto, use parent's align-items
- **baseline in column direction** -> fallback to FlexStart
- **FlexStart**: position = leadingPaddingAndBorder
- **FlexEnd**: position = lineEnd - childSize - margin
- **Center**: position = (lineHeight - childSize) / 2
- **Stretch**: if no definite cross size -> re-layout with crossDim of line
- **Baseline**: position = maxAscent - childBaseline

Auto margins in cross axis:
- Both auto -> centering (remainingCrossDim / 2)
- Only start auto -> push to end
- Only end auto -> push to start

## Absolute Positioning (Step 11)

- Absolute children skipped in flex flow
- Layouted separately via `layoutAbsoluteDescendants()`
- Containing block = nearest ancestor with `positionType != Static` or `alwaysFormsContainingBlock`
- Supports insets (top/right/bottom/left/start/end)
- justify-content and align-items used to position absolute children WITHOUT insets

## Wrap Reverse

After normal layout, for WrapReverse:
- All non-absolute children positions inverted on cross axis:
  `newPosition = containerCrossSize - currentPosition - childCrossSize`

## Aspect Ratio

- At flex basis computation: if one axis defined, other computed via ratio
- At distribute free space: cross size from main size / ratio
- At stretch: if aspect ratio set, cross size computed from ratio
