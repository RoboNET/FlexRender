# Phase 1 Review Checklist: Correctness vs Yoga Reference

**Reviewer**: yoga-c-researcher
**Scope**: Tasks 1.1-1.9 from IMPLEMENTATION-PLAN.md (Phase 1 -- Quick Wins)
**Reference**: facebook/yoga `CalculateLayout.cpp`, `Align.h`, `FlexDirection.h`

---

## 1. Align-Self (Tasks 1.1-1.2)

### What to verify

The plan introduces `GetEffectiveAlign()` which maps `AlignSelf` -> `AlignItems`, then uses per-child alignment instead of container-level `flex.Align`.

### Yoga Reference: `resolveChildAlignment()` (Align.h:17-27)

```cpp
inline Align resolveChildAlignment(const Node* node, const Node* child) {
    const Align align = child->style().alignSelf() == Align::Auto
        ? node->style().alignItems()
        : child->style().alignSelf();
    if (align == Align::Baseline && isColumn(node->style().flexDirection())) {
        return Align::FlexStart;
    }
    return align;
}
```

### Checklist

- [ ] **Auto fallback**: `AlignSelf.Auto` -> parent's `AlignItems` (plan line 281: `AlignSelf.Auto => parentAlign`)
- [ ] **Direct mapping**: Start/Center/End/Stretch map to corresponding AlignItems values
- [ ] **Baseline fallback in column**: When direction is Column, Baseline -> FlexStart. The plan does NOT include `AlignSelf.Baseline` in the switch, which will fall to the default `_ => parentAlign`. This is WRONG if `parentAlign` is not FlexStart. Should explicitly handle: `AlignSelf.Baseline => isColumn ? AlignItems.Start : AlignItems.Baseline`
- [ ] **Stretch conditions**: Yoga requires ALL of: (1) alignItem == Stretch, (2) cross-start margin is NOT auto, (3) cross-end margin is NOT auto, (4) child does NOT have definite cross size. The plan checks conditions (1) and (4) via `HasExplicitWidth`/`HasExplicitHeight` but does NOT check (2)/(3) because auto margins are Phase 5. This is acceptable for Phase 1 but should be noted as TODO.
- [ ] **Per-child alignment in BOTH axes**: Plan modifies both `LayoutColumnFlex` (cross = X) and `LayoutRowFlex` (cross = Y). Verify both are updated consistently.
- [ ] **All 8 tests cover the matrix**: Row+Column x Start/Center/End/Stretch + Auto + Multiple children

### Known Issues to Flag

| # | Severity | Issue | Expected in Code |
|---|----------|-------|-----------------|
| 1 | **MEDIUM** | Missing Baseline handling | Plan's switch has no `AlignSelf.Baseline` case. In Yoga, Baseline in column direction falls back to FlexStart. The implementation should add `AlignSelf.Baseline => AlignItems.Start` for column, or at minimum handle the enum value. |
| 2 | **LOW** | Stretch + auto margins interaction | Yoga: stretch does not apply if either cross margin is auto. Not relevant until Phase 5 (auto margins), but the stretch code should be structured to accept this future condition. |

---

## 2. Margin 4-Side (Tasks 1.3-1.4)

### What to verify

Current: margin is uniform (single float via `UnitParser`). Plan changes to 4-side via `PaddingParser`.

### Yoga Reference

Yoga resolves each margin edge independently:
```cpp
child->style().computeFlexStartMargin(mainAxis, direction, availableInnerWidth)
child->style().computeFlexEndMargin(mainAxis, direction, availableInnerWidth)
```

### Checklist

- [ ] **Parse as PaddingValues**: `PaddingParser.Parse(element.Margin, ...)` produces 4-side values (Top, Right, Bottom, Left)
- [ ] **Margin in flex layout**: In LayoutColumnFlex, y-advance should be `child.Height + gap + margin.Top + margin.Bottom` (not `+ childMarginY` which is only top)
- [ ] **Margin in flex layout (row)**: In LayoutRowFlex, x-advance should be `child.Width + gap + margin.Left + margin.Right`
- [ ] **Cross-axis margin**: In column, child X should include `margin.Left`. In row, child Y should include `margin.Top`.
- [ ] **Intrinsic sizing**: `IntrinsicSize.WithMargin(PaddingValues)` adds Horizontal (Left+Right) to width, Vertical (Top+Bottom) to height
- [ ] **No negative margins**: `ClampNegatives()` on parsed margins (Yoga uses `max(0, margin)` for layout purposes)
- [ ] **Layout*Element zero-based**: Plan changes Layout*Element to return X=0, Y=0. Margin is applied in flex pass only. Verify this doesn't break non-flex element rendering.
- [ ] **Backward compatibility**: Existing tests with uniform margin (`margin: "10"`) should produce same results as before

### Known Issues to Flag

| # | Severity | Issue | Expected in Code |
|---|----------|-------|-----------------|
| 1 | **MEDIUM** | Margin affects available cross-axis space | Yoga: when computing cross-axis alignment, margin is subtracted from available space (`child->dimensionWithMargin(crossAxis, ...)`). The plan should reduce `crossAxisSize` by left+right margin (column) or top+bottom margin (row) when computing alignment offset. |
| 2 | **MEDIUM** | Free space calculation must include margins | When computing `freeSpace = available - totalChildSizes - gaps`, `totalChildSizes` should include margins: `sum(childSize + marginStart + marginEnd)`. Currently: `totalChildHeight += child.Height` without margin. |
| 3 | **LOW** | Non-flex elements (standalone text) still need margin | If a text element is rendered outside a flex container, its margin should still be applied. Verify the Layout*Element -> flex pass delegation doesn't lose margin for root-level elements. |

---

## 3. Display:None (Tasks 1.5-1.6)

### What to verify

Elements with `Display.None` should be completely excluded from layout.

### Yoga Reference: `computeFlexBasisForChildren()` (CalculateLayout.cpp:575-589)

```cpp
// In computeFlexBasisForChildren:
if (child->style().display() == Display::None) {
    zeroOutLayoutRecursively(child);
    child->setHasNewLayout(true);
    child->setDirty(false);
    continue;
}
```

And in `calculateFlexLine()` (FlexLine.cpp:48-50):
```cpp
if (child->style().display() == Display::None ||
    child->style().positionType() == PositionType::Absolute) {
    continue;
}
```

### Checklist

- [ ] **MeasureIntrinsic**: Return `IntrinsicSize(0,0,0,0)` for `Display.None` elements
- [ ] **MeasureFlexIntrinsic**: Skip children with `Display.None` when aggregating sizes
- [ ] **LayoutFlexElement**: Skip children with `Display.None` in child layout pass
- [ ] **LayoutColumnFlex/LayoutRowFlex**: Skip children with `Display.None` in positioning and size distribution
- [ ] **Gap handling**: Gap should NOT be applied adjacent to a `Display.None` child. The plan's test `ComputeLayout_DisplayNone_GapNotAppliedForHiddenChild` verifies this. Implementation must track "visible child count" for gap calculation.
- [ ] **Flex grow/shrink**: Hidden children should NOT participate in grow/shrink factor totals
- [ ] **Justify-content**: Item count for SpaceBetween/SpaceAround/SpaceEvenly should exclude hidden children
- [ ] **SkiaRenderer**: Skip rendering nodes with `Display.None`
- [ ] **YAML parsing**: Parse `display: none` / `display: flex` from template
- [ ] **Recursive zeroing**: Yoga zeros out layout for display:none children recursively. FlexRender should at minimum not create LayoutNodes for hidden children, or create them with zero size.

### Known Issues to Flag

| # | Severity | Issue | Expected in Code |
|---|----------|-------|-----------------|
| 1 | **MEDIUM** | Children of display:none should also be hidden | If a FlexElement has `display: none`, its children should not be laid out. The plan's `LayoutFlexElement` skip handles this implicitly (no layout pass for hidden container = children aren't laid out), but verify the skip is early enough. |
| 2 | **LOW** | node.Children list should exclude hidden children | If hidden children are still in `node.Children`, the gap/justify calculations need to filter them. Better to exclude them from the list entirely during layout. |

---

## 4. Reverse Directions (Tasks 1.7-1.8)

### What to verify

The plan adds `RowReverse` and `ColumnReverse` to the FlexDirection enum and reverses child positions after normal layout.

### Yoga Reference: `flexStartEdge()` (FlexDirection.h:53-66)

Yoga handles reverse directions natively through edge mapping:
```
Column:        flexStartEdge = Top,    flexEndEdge = Bottom
ColumnReverse: flexStartEdge = Bottom, flexEndEdge = Top
Row:           flexStartEdge = Left,   flexEndEdge = Right
RowReverse:    flexStartEdge = Right,  flexEndEdge = Left
```

Children are positioned from `flexStartEdge` with `leadingPaddingAndBorder`. In reverse, this means positioning starts from Bottom/Right.

### Plan Approach: Post-Processing Flip

```csharp
if (flex.Direction == FlexDirection.ColumnReverse)
{
    var containerHeight = node.Height > 0 ? node.Height : y;
    foreach (var child in node.Children)
    {
        child.Y = containerHeight - child.Y - child.Height;
    }
}
```

### Checklist

- [ ] **Flip formula**: `newPos = containerSize - currentPos - childSize` is correct: mirrors position around center axis
- [ ] **Container size for auto-height**: `containerHeight = node.Height > 0 ? node.Height : y` -- when container has no explicit height, `y` is the total content height. Verify `y` includes trailing padding. If `y = lastChildBottom + gap(remainder)` but not `+ padding.Bottom`, the flip will be incorrect.
- [ ] **Container size for auto-width (row)**: Same concern: `containerWidth = node.Width` -- for row, width is always explicit (or from parent). Verify this is always > 0.
- [ ] **Padding handling**: After flip, children should be positioned symmetrically. If original: `child.Y = padding.Top + offset`, then flipped: `child.Y = containerHeight - (padding.Top + offset) - child.Height`. For this to equal `padding.Bottom + reverseOffset`, we need `containerHeight = contentHeight + padding.Top + padding.Bottom`. Verify this.
- [ ] **Margin handling**: Child margins should flip correctly. If margin.Top = 10, after ColumnReverse the child should have effective margin.Bottom = 10 at its new position. The post-processing flip handles this automatically if margins are already baked into Y positions.
- [ ] **Justify-content interaction**: For ColumnReverse with JustifyContent.Start, items should be at the bottom. JustifyContent.End should place items at the top. The post-flip approach achieves this: Start positions items at Y=padding.Top in normal layout, then flip moves them to bottom. Correct.
- [ ] **Grow/shrink with reverse**: Flex grow/shrink distribution should be identical -- only positions flip, not sizes. Verify sizes are not affected by the flip.
- [ ] **isColumn helper**: Plan line 545: `var isColumn = flex.Direction is FlexDirection.Column or FlexDirection.ColumnReverse;` -- correct, both Column and ColumnReverse use vertical main axis.
- [ ] **MeasureFlexIntrinsic**: Plan line 554: same isColumn check for measurement. Correct.
- [ ] **YAML parsing**: `row-reverse` and `column-reverse` string values must be parsed

### Known Issues to Flag

| # | Severity | Issue | Expected in Code |
|---|----------|-------|-----------------|
| 1 | **HIGH** | Yoga positions natively, plan post-processes | Yoga uses `flexStartEdge` to position from Bottom/Right directly. The plan's post-processing flip is equivalent IF and ONLY IF the container size is computed correctly (including both padding edges). If `containerHeight` is wrong, ALL child positions will be incorrect. This is the highest risk item in Phase 1. |
| 2 | **MEDIUM** | Gap direction in reverse | In Yoga, gap is always between items in flow order. For ColumnReverse, the first child in document order is at the bottom, and gap separates items upward. The post-flip approach preserves gap correctly because gaps are computed between adjacent children in document order, then the whole arrangement is flipped. However, verify that gap is not applied after the last child (which becomes the first after flip). |
| 3 | **MEDIUM** | Auto-sized container with reverse | When container has no explicit height and direction is ColumnReverse, the container should size to fit content and items should be at the bottom. But `containerHeight = y` (content height) means `newPos = y - oldPos - childSize`, and the first child (at top, Y=padding.Top) becomes `Y = y - padding.Top - childHeight`. If `y = padding.Top + totalContent + totalGaps`, this places the first child at the bottom correctly. Verify edge cases. |
| 4 | **LOW** | Reverse does not affect cross-axis | RowReverse only flips X positions, not Y. ColumnReverse only flips Y, not X. The plan handles this correctly by having separate flip blocks. |

### Critical Verification: Reverse + Justify

Test these combinations:
- ColumnReverse + JustifyContent.Start -> items packed at bottom
- ColumnReverse + JustifyContent.End -> items packed at top
- ColumnReverse + JustifyContent.Center -> items centered
- ColumnReverse + JustifyContent.SpaceBetween -> first item at bottom, last at top
- RowReverse + JustifyContent.Start -> items packed at right
- RowReverse + JustifyContent.End -> items packed at left

The plan's 6 tests do NOT cover justify-content + reverse combinations. These should be added.

---

## 5. Overflow Fallback (Task 1.9)

### What to verify

SpaceBetween, SpaceAround, SpaceEvenly fall back to Start when `freeSpace < 0`.

### Yoga Reference: `fallbackAlignment(Justify)` (Align.h:54-70)

```cpp
constexpr Justify fallbackAlignment(Justify align) {
    switch (align) {
        case Justify::SpaceBetween: return Justify::FlexStart;
        case Justify::SpaceAround:  return Justify::FlexStart;
        case Justify::SpaceEvenly:  return Justify::FlexStart;
        default: return align;
    }
}
```

Applied in `justifyMainAxis()` (CalculateLayout.cpp:1043-1045):
```cpp
const Justify justifyContent = flexLine.layout.remainingFreeSpace >= 0
    ? node->style().justifyContent()
    : fallbackAlignment(node->style().justifyContent());
```

### Checklist

- [ ] **Only three values fallback**: SpaceBetween, SpaceAround, SpaceEvenly -> Start. Center and End do NOT fallback.
- [ ] **Center does NOT fallback**: With negative free space, Center should center children, causing equal overflow on both sides. Verify the plan does NOT include Center in the fallback.
- [ ] **End does NOT fallback**: With negative free space, End should position children so they overflow at the start edge. Verify the plan does NOT include End in the fallback.
- [ ] **Threshold is `< 0`, not `<= 0`**: Yoga uses `>= 0` for non-fallback (meaning exactly 0 keeps original). The plan uses `freeSpace < 0` which is equivalent. Correct.
- [ ] **Applied in BOTH axes**: Plan should add fallback in both `LayoutColumnFlex` and `LayoutRowFlex`.
- [ ] **Fallback is to Start, not FlexStart**: In FlexRender, `JustifyContent.Start` is the equivalent. Verify the enum value.

### Known Issues to Flag

| # | Severity | Issue | Expected in Code |
|---|----------|-------|-----------------|
| 1 | **LOW** | Fallback with reverse direction | For RowReverse + SpaceBetween + negative space: fallback to Start, then flip. This means items overflow on the left side (Start in RowReverse context is Right before flip, but after flip becomes Left). This is correct per CSS spec -- fallback is to flex-start edge, not physical start. |
| 2 | **LOW** | Missing tests for Center/End NOT falling back | The plan has 3 tests (SpaceBetween, SpaceAround, SpaceEvenly) but no negative tests for Center/End. Consider adding tests that verify Center/End behavior is preserved with negative free space. |

---

## 6. Cross-Cutting Concerns

### Margin interaction with other features

When reviewing the implementation, verify these interactions:

| Feature A | Feature B | Expected Behavior |
|-----------|-----------|-------------------|
| align-self | 4-side margin | Margin reduces available cross-axis space for alignment |
| display:none | margin | Hidden children's margins do not affect layout |
| display:none | gap | No gap adjacent to hidden children |
| reverse | margin | Margins flip with children (margin-top becomes effective margin-bottom in ColumnReverse) |
| reverse | justify fallback | Fallback to Start in reverse means flex-start edge (bottom/right), which is correct |
| align-self | reverse | Cross-axis alignment is not affected by main-axis reversal |

### Order of implementation matters

The plan implements features in this order: 1.1-1.2 (align-self) -> 1.3-1.4 (margin) -> 1.5-1.6 (display:none) -> 1.7-1.8 (reverse) -> 1.9 (overflow fallback).

Each implementation may change the structure of `LayoutColumnFlex`/`LayoutRowFlex`. When reviewing, verify that later implementations don't break earlier ones. Specifically:
- Margin changes (1.4) modify how child positions are calculated, which could affect align-self (1.2)
- Display:none (1.6) changes child iteration, which affects all subsequent features
- Reverse (1.8) post-processes positions, which must account for margin changes from 1.4

---

## 7. Summary: Review Priority Matrix

| Task | Yoga Fidelity Risk | Test Coverage Risk | Integration Risk |
|------|-------------------|-------------------|-----------------|
| 1.2 align-self | **MEDIUM** (missing Baseline) | LOW (8 tests) | LOW |
| 1.4 margin 4-side | **MEDIUM** (free space calc) | LOW (6 tests) | **HIGH** (changes positioning) |
| 1.6 display:none | LOW | **MEDIUM** (gap + justify count) | MEDIUM |
| 1.8 reverse | **HIGH** (container size for flip) | **HIGH** (no justify+reverse tests) | **HIGH** (post-processing all positions) |
| 1.9 overflow fallback | LOW | LOW (no negative Center/End tests) | LOW |

### Top 3 Items to Scrutinize During Code Review

1. **Reverse direction container size**: The flip formula `containerSize - pos - childSize` MUST use the correct container size that includes both padding edges. If wrong, all positions break.

2. **Margin integration with free space**: After 4-side margin, `freeSpace` must be calculated as `available - sum(childSize + marginStart + marginEnd) - gaps`. Missing margins = incorrect grow/shrink distribution.

3. **Baseline alignment in column**: The plan's `GetEffectiveAlign` switch misses `AlignSelf.Baseline`. In column direction, Yoga falls back to FlexStart. The plan will fall to default (`parentAlign`), which could be Stretch/Center/End -- producing wrong alignment.
