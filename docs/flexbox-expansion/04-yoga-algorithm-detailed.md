# Yoga Layout Algorithm -- Detailed Reference

Source: `facebook/yoga` (cloned to `/tmp/yoga-ref`), primary file: `yoga/algorithm/CalculateLayout.cpp` (~2200 lines).

This document contains exact code references, formulas, and edge cases from the Yoga reference implementation for use during algorithmic correctness review of FlexRender.

---

## 1. calculateFlexLine() -- Line Breaking (Step 4)

**File**: `yoga/algorithm/FlexLine.cpp:16-122`

### Algorithm

```cpp
// FlexLine.cpp:28-34 -- initialization
float sizeConsumed = 0.0f;
float totalFlexGrowFactors = 0.0f;
float totalFlexShrinkScaledFactors = 0.0f;
size_t numberOfAutoMargins = 0;
```

**Iteration** (FlexLine.cpp:44-102):
- Skips `display: none` and `position: absolute` children
- For each child in flow:
  1. Count auto margins on main axis (start + end)
  2. Compute `flexBasisWithMinAndMaxConstraints = boundAxisWithinMinAndMax(child, computedFlexBasis)`
  3. **Line break condition**: if wrap enabled AND `sizeConsumedIncludingMinConstraint + flexBasis + margin + gap > availableInnerMainDim` AND items already exist -> break
  4. Accumulate `sizeConsumed += flexBasis + margin + gap`
  5. For flexible items: accumulate grow factors and **scaled** shrink factors

### Key Detail: Shrink Factor Scaling

```cpp
// FlexLine.cpp:97-98
totalFlexShrinkScaledFactors += -child->resolveFlexShrink() *
    child->getLayout().computedFlexBasis.unwrap();
```

**Shrink factor is negated and scaled by flex basis.** This is critical -- the `totalFlexShrinkScaledFactors` is always negative (or zero).

### Key Detail: Factor Flooring

```cpp
// FlexLine.cpp:104-112
if (totalFlexGrowFactors > 0 && totalFlexGrowFactors < 1) {
    totalFlexGrowFactors = 1;
}
if (totalFlexShrinkScaledFactors > 0 && totalFlexShrinkScaledFactors < 1) {
    totalFlexShrinkScaledFactors = 1;
}
```

**When total flex factors are between 0 and 1, they are floored to 1.** This prevents fractional grow factors (e.g., `flex-grow: 0.2`) from distributing more than 100% of free space when sum < 1. Per CSS spec, when sum < 1, items receive `freeSpace * factor` (not `freeSpace * factor / totalFactors`).

### Key Detail: Gap Handling

```cpp
// FlexLine.cpp:39-40
const float gap = node->style().computeGapForAxis(mainAxis, availableInnerMainDim);
// FlexLine.cpp:65-66
const float childLeadingGapMainAxis = child == firstElementInLine ? 0.0f : gap;
```

Gap is NOT applied before the first item. Only between items.

### FlexLine struct (FlexLine.h:40-57)

```cpp
struct FlexLine {
    const std::vector<yoga::Node*> itemsInFlow{};  // non-absolute, non-display:none
    const float sizeConsumed{0.0f};                 // total basis + margins + gaps
    const size_t numberOfAutoMargins{0};            // auto margin count
    FlexLineRunningLayout layout{};                 // mutable running state
};

struct FlexLineRunningLayout {
    float totalFlexGrowFactors{0.0f};
    float totalFlexShrinkScaledFactors{0.0f};
    float remainingFreeSpace{0.0f};
    float mainDim{0.0f};
    float crossDim{0.0f};
};
```

---

## 2. Flex Basis Resolution (Step 3)

**File**: `CalculateLayout.cpp:66-264`, function `computeFlexBasisForChild()`

### Priority Order (lines 97-262)

1. **flexBasis defined AND mainAxisSize defined** (line 97):
   ```cpp
   child->setLayoutComputedFlexBasis(
       yoga::maxOrDefined(resolvedFlexBasis, paddingAndBorder));
   ```
   Flex basis is clamped to at least `padding + border`.

2. **Main axis = row AND width defined** (line 107):
   ```cpp
   child->setLayoutComputedFlexBasis(
       yoga::maxOrDefined(child->getResolvedDimension(Width), paddingAndBorder));
   ```

3. **Main axis = column AND height defined** (line 118):
   ```cpp
   child->setLayoutComputedFlexBasis(
       yoga::maxOrDefined(child->getResolvedDimension(Height), paddingAndBorder));
   ```

4. **Otherwise** (line 128): Measure the child -- call `calculateLayoutInternal()` recursively, then:
   ```cpp
   child->setLayoutComputedFlexBasis(FloatOptional(
       yoga::maxOrDefined(
           child->getLayout().measuredDimension(dimension(mainAxis)),
           paddingAndBorderForAxis(child, mainAxis))));
   ```

### Aspect Ratio in Basis (lines 176-187)

```cpp
if (childStyle.aspectRatio().isDefined()) {
    if (!isMainAxisRow && childWidthSizingMode == SizingMode::StretchFit) {
        childHeight = marginColumn + (childWidth - marginRow) / aspectRatio;
    } else if (isMainAxisRow && childHeightSizingMode == SizingMode::StretchFit) {
        childWidth = marginRow + (childHeight - marginColumn) * aspectRatio;
    }
}
```

**Aspect ratio formula**: `width = height * aspectRatio`, `height = width / aspectRatio`.

---

## 3. Two-Pass Flex Resolution (Step 5)

### resolveFlexibleLength() (CalculateLayout.cpp:930-980)

Orchestration function that calls both passes sequentially:

```cpp
const float originalFreeSpace = flexLine.layout.remainingFreeSpace;
distributeFreeSpaceFirstPass(...);     // freeze min/max violators
distributeFreeSpaceSecondPass(...);    // distribute to remaining
flexLine.layout.remainingFreeSpace = originalFreeSpace - distributedFreeSpace;
```

### distributeFreeSpaceFirstPass() (CalculateLayout.cpp:821-906)

**Purpose**: Find items whose min/max constraints trigger, freeze them, and exclude from remaining.

**Shrink path** (lines 845-873):
```cpp
flexShrinkScaledFactor = -currentLineChild->resolveFlexShrink() * childFlexBasis;

baseMainSize = childFlexBasis +
    flexLine.layout.remainingFreeSpace /
    flexLine.layout.totalFlexShrinkScaledFactors *
    flexShrinkScaledFactor;

boundMainSize = boundAxis(child, mainAxis, baseMainSize);

if (baseMainSize != boundMainSize) {
    // Item is clamped -- freeze it
    deltaFreeSpace += boundMainSize - childFlexBasis;
    flexLine.layout.totalFlexShrinkScaledFactors -=
        (-child->resolveFlexShrink() * child->computedFlexBasis);
}
```

**Grow path** (lines 875-903):
```cpp
flexGrowFactor = currentLineChild->resolveFlexGrow();

baseMainSize = childFlexBasis +
    flexLine.layout.remainingFreeSpace /
    flexLine.layout.totalFlexGrowFactors * flexGrowFactor;

boundMainSize = boundAxis(child, mainAxis, baseMainSize);

if (baseMainSize != boundMainSize) {
    deltaFreeSpace += boundMainSize - childFlexBasis;
    flexLine.layout.totalFlexGrowFactors -= flexGrowFactor;
}
```

**End**: `flexLine.layout.remainingFreeSpace -= deltaFreeSpace;`

### distributeFreeSpaceSecondPass() (CalculateLayout.cpp:622-816)

**Purpose**: Actually compute sizes for all items (both frozen and unfrozen).

**Shrink formula** (lines 658-674):
```cpp
if (remainingFreeSpace < 0) {
    flexShrinkScaledFactor = -child->resolveFlexShrink() * childFlexBasis;
    if (flexShrinkScaledFactor != 0) {
        float childSize;
        if (totalFlexShrinkScaledFactors == 0) {
            childSize = childFlexBasis + flexShrinkScaledFactor;
        } else {
            childSize = childFlexBasis +
                (remainingFreeSpace / totalFlexShrinkScaledFactors) *
                flexShrinkScaledFactor;
        }
        updatedMainSize = boundAxis(child, mainAxis, childSize);
    }
}
```

**CRITICAL**: When `totalFlexShrinkScaledFactors == 0` (edge case: all items frozen in first pass), the formula degrades to `childFlexBasis + flexShrinkScaledFactor`. This is a safety fallback.

**Grow formula** (lines 684-701):
```cpp
if (remainingFreeSpace > 0) {
    flexGrowFactor = child->resolveFlexGrow();
    if (flexGrowFactor != 0) {
        updatedMainSize = boundAxis(child, mainAxis,
            childFlexBasis + remainingFreeSpace / totalFlexGrowFactors * flexGrowFactor);
    }
}
```

**After sizing**: `deltaFreeSpace += updatedMainSize - childFlexBasis;`

### Aspect Ratio in Second Pass (lines 716-722)

```cpp
if (childStyle.aspectRatio().isDefined()) {
    childCrossSize = isMainAxisRow
        ? (childMainSize - marginMain) / aspectRatio
        : (childMainSize - marginMain) * aspectRatio;
    childCrossSize += marginCross;
}
```

---

## 4. boundAxisWithinMinAndMax() and boundAxis()

**File**: `yoga/algorithm/BoundAxis.h`

### boundAxisWithinMinAndMax (lines 30-61)

```cpp
// Clamp value to min/max. Order: if max >= 0 and value > max -> max.
// Then: if min >= 0 and value < min -> min.
// This means min wins over max (if min > max, min prevails).
if (max >= 0 && value > max) return max;
if (min >= 0 && value < min) return min;
return value;
```

### boundAxis (lines 65-77)

```cpp
// Like boundAxisWithinMinAndMax but also ensures value >= padding + border.
return max(
    boundAxisWithinMinAndMax(node, axis, value).unwrap(),
    paddingAndBorderForAxis(node, axis));
```

**Key**: An item can never be smaller than its padding + border.

---

## 5. justifyMainAxis() -- Justify Content + Auto Margins (Step 6)

**File**: `CalculateLayout.cpp:982-1156`

### Overflow Fallback (lines 1043-1045)

```cpp
const Justify justifyContent = flexLine.layout.remainingFreeSpace >= 0
    ? node->style().justifyContent()
    : fallbackAlignment(node->style().justifyContent());
```

`fallbackAlignment()` (from `Align.h:54-70`):
```cpp
constexpr Justify fallbackAlignment(Justify align) {
    switch (align) {
        case Justify::SpaceBetween:   return Justify::FlexStart;
        case Justify::SpaceAround:    return Justify::FlexStart;
        case Justify::SpaceEvenly:    return Justify::FlexStart;
        default:                      return align;
    }
}
```

**Note**: Center and FlexEnd do NOT fallback -- they retain their behavior with negative free space.

### Auto Margins Override (lines 1047, 1086-1107)

```cpp
if (flexLine.numberOfAutoMargins == 0) {
    // Apply justify-content normally
} // else: auto margins consume free space

// In the per-child loop:
if (child->flexStartMarginIsAuto(mainAxis) && remainingFreeSpace > 0.0f) {
    mainDim += remainingFreeSpace / numberOfAutoMargins;
}
// ... position child ...
if (child->flexEndMarginIsAuto(mainAxis) && remainingFreeSpace > 0.0f) {
    mainDim += remainingFreeSpace / numberOfAutoMargins;
}
```

**Key behaviors**:
1. Auto margins completely override justify-content (when `numberOfAutoMargins > 0`)
2. Auto margin space = `remainingFreeSpace / numberOfAutoMargins` (divided equally among ALL auto margins)
3. Auto margins only apply when `remainingFreeSpace > 0` (negative space -> auto margins become 0)

### Justify-Content Values (lines 1048-1075)

```
FlexStart:     leadingMainDim = 0
Center:        leadingMainDim = remainingFreeSpace / 2
FlexEnd:       leadingMainDim = remainingFreeSpace
SpaceBetween:  betweenMainDim += remainingFreeSpace / (itemCount - 1)  [if itemCount > 1]
SpaceAround:   leadingMainDim = 0.5 * remainingFreeSpace / itemCount
               betweenMainDim += leadingMainDim * 2
SpaceEvenly:   leadingMainDim = remainingFreeSpace / (itemCount + 1)
               betweenMainDim += leadingMainDim
```

**Note**: `betweenMainDim` is initialized to `gap`, not 0. So the final between-dim = gap + distributed space.

### Cross Dimension Calculation (lines 1118-1155)

In `justifyMainAxis()`, the crossDim is also computed:
- Baseline layout: crossDim = maxAscent + maxDescent
- Non-baseline: crossDim = max of children's cross dimensions

---

## 6. Cross-Axis Alignment (Step 7)

**File**: `CalculateLayout.cpp:1646-1760`

### resolveChildAlignment() (Align.h:17-27)

```cpp
inline Align resolveChildAlignment(const Node* node, const Node* child) {
    const Align align = child->style().alignSelf() == Align::Auto
        ? node->style().alignItems()
        : child->style().alignSelf();
    // Baseline in column direction falls back to FlexStart
    if (align == Align::Baseline && isColumn(node->style().flexDirection())) {
        return Align::FlexStart;
    }
    return align;
}
```

**Key**: Baseline alignment in column direction -> FlexStart fallback.

### Stretch (lines 1660-1733)

Stretch requires ALL of:
- alignItem == Stretch
- cross axis start margin is NOT auto
- cross axis end margin is NOT auto
- child does NOT have definite cross size

When stretching: re-layout child with `crossDim` of the line.

### Auto Margins on Cross Axis (lines 1734-1752)

```cpp
const float remainingCrossDim = containerCrossAxis -
    child->dimensionWithMargin(crossAxis);

if (startMarginIsAuto && endMarginIsAuto) {
    leadingCrossDim += max(0, remainingCrossDim / 2);  // center
} else if (endMarginIsAuto) {
    // no-op -> stays at start
} else if (startMarginIsAuto) {
    leadingCrossDim += max(0, remainingCrossDim);       // push to end
} else if (alignItem == FlexStart) {
    // no-op
} else if (alignItem == Center) {
    leadingCrossDim += remainingCrossDim / 2;
} else {
    leadingCrossDim += remainingCrossDim;               // FlexEnd
}
```

**Key**: Auto margins have priority over align-items. Both auto -> center. Start auto -> push to end. End auto -> stay at start.

---

## 7. Align Content (Step 8)

**File**: `CalculateLayout.cpp:1768-1987`

### Overflow Fallback for Align Content (line 1797-1799)

```cpp
const auto alignContent = remainingAlignContentDim >= 0
    ? node->style().alignContent()
    : fallbackAlignment(node->style().alignContent());
```

`fallbackAlignment(Align)`:
```cpp
SpaceBetween, Stretch  -> FlexStart
SpaceAround, SpaceEvenly -> FlexStart  // TODO: should be Start, not FlexStart
```

### Distribution (lines 1801-1833)

```
FlexEnd:      currentLead += remainingAlignContentDim
Center:       currentLead += remainingAlignContentDim / 2
Stretch:      extraSpacePerLine = remainingAlignContentDim / lineCount
SpaceAround:  currentLead += remaining / (2 * lineCount)
              leadPerLine = remaining / lineCount
SpaceEvenly:  currentLead += remaining / (lineCount + 1)
              leadPerLine = remaining / (lineCount + 1)
SpaceBetween: if (lineCount > 1) leadPerLine = remaining / (lineCount - 1)
FlexStart:    (no-op)
```

### Cross gap between lines (line 1879)

```cpp
currentLead += i != 0 ? crossAxisGap : 0;
```

Gap is applied between lines, not before the first line.

---

## 8. Wrap-Reverse (Step 9, post)

**File**: `CalculateLayout.cpp:2083-2095`

```cpp
if (performLayout && node->style().flexWrap() == Wrap::WrapReverse) {
    for (auto child : node->getLayoutChildren()) {
        if (child->style().positionType() != PositionType::Absolute) {
            child->setLayoutPosition(
                node->getLayout().measuredDimension(dimension(crossAxis)) -
                    child->getLayout().position(flexStartEdge(crossAxis)) -
                    child->getLayout().measuredDimension(dimension(crossAxis)),
                flexStartEdge(crossAxis));
        }
    }
}
```

**Formula**: `newPosition = containerCrossSize - currentPosition - childCrossSize`

**Key**: Absolute children are NOT affected by wrap-reverse.

---

## 9. Absolute Positioning (Step 11)

**File**: `yoga/algorithm/AbsoluteLayout.cpp`

### positionAbsoluteChild() (lines 164-224)

Two cases per axis:
1. **Insets defined** (e.g., `left: 10px`): Position = border + margin + inset
2. **No insets**: Use `justifyAbsoluteChild()` (main axis) or `alignAbsoluteChild()` (cross axis)

### justifyAbsoluteChild() (lines 84-108)

```
FlexStart, SpaceBetween     -> setFlexStartLayoutPosition()
FlexEnd                     -> setFlexEndLayoutPosition()
Center, SpaceAround, SpaceEvenly -> setCenterLayoutPosition()
```

### alignAbsoluteChild() (lines 110-146)

```
FlexStart, Baseline, Stretch, SpaceBetween, SpaceAround, SpaceEvenly -> FlexStart
FlexEnd                      -> FlexEnd
Center                       -> Center
```

**Key**: When parent has WrapReverse, alignment is inverted:
```cpp
if (parentWrap == Wrap::WrapReverse) {
    if (itemAlign == FlexEnd) itemAlign = FlexStart;
    else if (itemAlign != Center) itemAlign = FlexEnd;
}
```

### Width/Height from insets (lines 264-329)

If child has no explicit width but has both left AND right insets defined:
```cpp
childWidth = containingNode->measuredWidth - startBorder - endBorder - startInset - endInset;
childWidth = boundAxis(child, Row, childWidth);
```

Same pattern for height with top/bottom insets.

### layoutAbsoluteDescendants() (lines 424-562)

Recursive traversal:
- For absolute children: layout + position relative to containing block
- For static children without `alwaysFormsContainingBlock`: recurse to find deeper absolute descendants
- Containing block = nearest ancestor with `positionType != Static` or `alwaysFormsContainingBlock`

---

## 10. Enums Reference

### FlexDirection (yoga/enums/FlexDirection.h)
```
Column = 0, ColumnReverse = 1, Row = 2, RowReverse = 3
```

### Justify (yoga/enums/Justify.h)
```
FlexStart = 0, Center = 1, FlexEnd = 2, SpaceBetween = 3, SpaceAround = 4, SpaceEvenly = 5
```

### Align (yoga/enums/Align.h) -- used for align-items, align-self, align-content
```
Auto = 0, FlexStart = 1, Center = 2, FlexEnd = 3, Stretch = 4,
Baseline = 5, SpaceBetween = 6, SpaceAround = 7, SpaceEvenly = 8
```

### Wrap (yoga/enums/Wrap.h)
```
NoWrap = 0, Wrap = 1, WrapReverse = 2
```

### Display (yoga/enums/Display.h)
```
Flex = 0, None = 1, Contents = 2
```

### PositionType (yoga/enums/PositionType.h)
```
Static = 0, Relative = 1, Absolute = 2
```

### Overflow (yoga/enums/Overflow.h)
```
Visible = 0, Hidden = 1, Scroll = 2
```

---

## 11. Documented Limitations of Yoga

From `CalculateLayout.cpp:1158-1184`:

> **Not supported:**
> - `zIndex` (z ordering)
> - `order` property -- items always in document order
> - `visibility: collapse` or `hidden`
> - Forced breaks
> - Vertical inline directions
>
> **Deviations from CSS spec:**
> - Default minimum main size is 0 (spec says widest word for text)
> - Min/max sizes NOT honored during flexible length resolution (simplified two-pass)
> - Default flexDirection is `column` (spec says `row`)

---

## 12. Edge Cases for Review Checklist

### From Test Fixtures

1. **flex_grow_less_than_factor_one**: `flex-grow: 0.2, 0.2, 0.4` -- total < 1, factor flooring applies
2. **flex_shrink_to_zero**: Item with `flex-shrink: 0` should not shrink
3. **flex_basis_overrides_main_size**: `flex-basis` takes priority over explicit height/width
4. **flex_wrap_children_with_min_main_overriding_flex_basis**: `min-width: 55px` overrides `flex-basis: 50px` -- causes line break at different point
5. **wrap_reverse_row_align_content_***: Various align-content values with wrap-reverse
6. **wrapped_column_max_height**: `max-height` on child causes it to wrap to next column
7. **auto_margin**: `margin-left: auto` pushes child to flex-end
8. **justify_content_overflow_min_max**: Overflow fallback when children exceed min/max constraints
9. **flex_grow_within_constrained_min_max_column**: flex-grow bounded by min-height / max-height
10. **nowrap_expands_flexline_box_to_min_cross vs wrap_does_not_impose_min_cross_onto_single_flexline**: No-wrap imposes min-cross to line, wrap does not

### Critical Edge Cases to Verify in FlexRender

1. **Shrink scaled by basis**: Shrink amount is `overflow * (-shrink * basis) / totalScaledShrinkFactors`, NOT `overflow * shrink / totalShrink`
2. **Factor flooring**: When `0 < totalGrowFactors < 1`, floor to 1 (same for shrink)
3. **Auto margins vs justify**: Auto margins completely override justify-content
4. **Auto margin with negative space**: When `remainingFreeSpace <= 0`, auto margins = 0
5. **Baseline in column**: Falls back to FlexStart
6. **Stretch vs auto margins**: Stretch does NOT apply if either cross margin is auto
7. **Min wins over max**: `boundAxisWithinMinAndMax` checks max first, then min -- so min prevails
8. **FlexBasis minimum**: Flex basis can never go below padding + border
9. **Gap not before first item**: Gap only between items, not before first or after last
10. **Wrap-reverse excludes absolute children**: Only non-absolute positions are inverted

---

## 13. Mapping to FlexRender Architecture

| Yoga Concept | FlexRender Current | Phase |
|---|---|---|
| `computeFlexBasisForChild` | Implicit in `LayoutElement()` | N/A |
| `calculateFlexLine` | Not implemented (no wrap) | Phase 3a |
| `distributeFreeSpaceFirstPass` | Not implemented (single pass) | Phase 2a |
| `distributeFreeSpaceSecondPass` | `LayoutRowFlex`/`LayoutColumnFlex` shrink/grow blocks | Phase 2a |
| `justifyMainAxis` | `switch (flex.Justify)` blocks | Phase 1e (fallback), Phase 5 (auto margins) |
| `resolveChildAlignment` | Not implemented (ignores AlignSelf) | Phase 1a |
| `fallbackAlignment` | Not implemented | Phase 1e |
| `boundAxisWithinMinAndMax` | Not implemented | Phase 2b |
| `boundAxis` | Not implemented | Phase 2b |
| `align-content` | Not implemented | Phase 3a |
| `wrap-reverse` | Not implemented | Phase 3a |
| `layoutAbsoluteChild` | Not implemented | Phase 4a |
| `display: none` | Not implemented | Phase 1c |
| `RowReverse/ColumnReverse` | Not implemented | Phase 1d |
| Factor flooring (grow < 1) | Not implemented | Phase 2a |
