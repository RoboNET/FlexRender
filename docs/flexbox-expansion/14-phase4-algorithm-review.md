# Phase 4 Algorithm Review: Advanced Features vs Yoga Reference

**Reviewer**: yoga-c-researcher
**Scope**: Tasks 4.1-4.5 from IMPLEMENTATION-PLAN.md (Phase 4 -- Advanced Features)
**Reference**: facebook/yoga `AbsoluteLayout.cpp` (563 lines), `CalculateLayout.cpp` (Steps 3, 7, 11), `Node.cpp` (`relativePosition`, `setPosition`)

---

## Summary of Findings

| Severity | Count | Areas |
|----------|-------|-------|
| **HIGH** | 4 | Relative positioning, absolute sizing, absolute alignment, aspect ratio with flex |
| **MEDIUM** | 5 | Containing block, margin handling, inset-based sizing, overflow scroll, display:none |
| **LOW** | 2 | Errata flags, alwaysFormsContainingBlock |

---

## 1. Position Absolute -- Exclusion from Flex Flow (Task 4.3)

### Plan (lines 1241-1251)

```csharp
// Separate absolute children from flow
foreach (var child in flex.Children)
{
    if (child.Position == Position.Absolute)
        continue; // Skip in flex flow
    // ... normal layout
}
```

### Yoga Reference: `CalculateLayout.cpp:588`

```cpp
// After setPosition is called for every child:
if (child->style().positionType() == PositionType::Absolute) {
    continue;
}
```

### Verdict: CORRECT

The plan correctly skips absolute children in the flex flow. Yoga does exactly the same -- after computing initial position (margin + relativePosition), absolute children are excluded from flex basis computation, line breaking, and flex resolution. The plan mirrors this by using `continue` in both `LayoutFlexElement` and `LayoutColumnFlex`/`LayoutRowFlex`.

**One concern**: Absolute children must still be included for intrinsic measurement of the container in some cases. However, Yoga also skips absolute children for intrinsic sizing of the container (`calculateFlexLine` skips them), so the plan is consistent.

---

## 2. Position Absolute -- Inset-Based Positioning (Task 4.3)

### Plan (lines 1253-1271)

```csharp
// Position absolute children
if (left.HasValue) child.X = padding.Left + left.Value;
else if (right.HasValue) child.X = node.Width - padding.Right - child.Width - right.Value;
else child.X = padding.Left;
```

### Yoga Reference: `positionAbsoluteChild()` (AbsoluteLayout.cpp:164-224)

```cpp
if (child->style().isInlineStartPositionDefined(axis, direction) &&
    !child->style().isInlineStartPositionAuto(axis, direction)) {
    // Position from flex-start edge: inset + border + margin
    const float positionRelativeToInlineStart =
        child->style().computeInlineStartPosition(axis, direction, containingBlockSize) +
        containingNode->style().computeInlineStartBorder(axis, direction) +
        child->style().computeInlineStartMargin(axis, direction, containingBlockSize);
    child->setLayoutPosition(positionRelativeToFlexStart, flexStartEdge(axis));
} else if (child->style().isInlineEndPositionDefined(axis, direction)) {
    // Position from flex-end edge: containerSize - childSize - border - margin - inset
    const float positionRelativeToInlineStart =
        containingNode->getLayout().measuredDimension(dimension(axis)) -
        child->getLayout().measuredDimension(dimension(axis)) -
        containingNode->style().computeInlineEndBorder(axis, direction) -
        child->style().computeInlineEndMargin(axis, direction, containingBlockSize) -
        child->style().computeInlineEndPosition(axis, direction, containingBlockSize);
    child->setLayoutPosition(positionRelativeToFlexStart, flexStartEdge(axis));
} else {
    // No insets -> use justify/align to position
    isMainAxis ? justifyAbsoluteChild(...) : alignAbsoluteChild(...);
}
```

### Verdict: CORRECT with issues

The basic approach matches: left inset -> position from start, right inset -> position from end, no inset -> default.

**Issues found**:

| # | Severity | Issue | Yoga Reference |
|---|----------|-------|---------------|
| 1 | **MEDIUM** | Plan uses `padding.Left + left.Value` but Yoga uses **border** (not padding) for inset positioning: `containingNode->style().computeInlineStartBorder()`. Since FlexRender doesn't have borders, padding is the closest equivalent. However, the plan should document this design decision. | `AbsoluteLayout.cpp:190` |
| 2 | **MEDIUM** | Plan does NOT include child **margin** in the positioning formula. Yoga adds `child->style().computeInlineStartMargin()` to the start-inset position and subtracts `computeInlineEndMargin()` from the end-inset position. If an absolute child has a margin, it should offset the inset position. | `AbsoluteLayout.cpp:191-192, 208-209` |
| 3 | **MEDIUM** | Plan's default (no insets) falls back to `padding.Left` / `padding.Top`. Yoga falls back to `justifyAbsoluteChild()` / `alignAbsoluteChild()` which use the parent's `justify-content` and `align-items` to position the child. The plan ignores these properties for absolute children without insets. | `AbsoluteLayout.cpp:219-222` |

**Recommended fix for issue 3** (justify/align fallback):

```csharp
// When no insets are defined, use justify-content for main axis:
// FlexStart/SpaceBetween -> padding start
// FlexEnd -> container end - child size - padding end
// Center/SpaceAround/SpaceEvenly -> centered in content box

// Use align-items for cross axis:
// FlexStart/Baseline/Stretch -> padding start
// FlexEnd -> container end - child size - padding end
// Center -> centered in content box
```

---

## 3. Position Absolute -- Inset-Based Sizing (Task 4.3)

### Plan

The plan does NOT implement inset-based sizing. When an absolute child has no explicit width but both `left` and `right` insets are defined, the width should be computed from the insets.

### Yoga Reference: `layoutAbsoluteChild()` (AbsoluteLayout.cpp:252-288)

```cpp
// If the child doesn't have a specified width, compute the width based on
// the left/right offsets if they're defined.
if (child->style().isFlexStartPositionDefined(FlexDirection::Row, direction) &&
    child->style().isFlexEndPositionDefined(FlexDirection::Row, direction) &&
    !child->style().isFlexStartPositionAuto(FlexDirection::Row, direction) &&
    !child->style().isFlexEndPositionAuto(FlexDirection::Row, direction)) {
    childWidth =
        containingNode->getLayout().measuredDimension(Dimension::Width) -
        (containingNode->style().computeFlexStartBorder(FlexDirection::Row, direction) +
         containingNode->style().computeFlexEndBorder(FlexDirection::Row, direction)) -
        (child->style().computeFlexStartPosition(FlexDirection::Row, direction, containingBlockWidth) +
         child->style().computeFlexEndPosition(FlexDirection::Row, direction, containingBlockWidth));
    childWidth = boundAxis(child, FlexDirection::Row, ...);
}
```

### Verdict: MISSING

| # | Severity | Issue | Yoga Reference |
|---|----------|-------|---------------|
| 4 | **HIGH** | **Inset-based sizing is completely missing from the plan.** When an absolute child has no explicit width/height but has both left+right or top+bottom insets defined, the child's size should be computed as: `containerSize - borderStart - borderEnd - insetStart - insetEnd`. This is a critical CSS feature (e.g., `position: absolute; left: 10px; right: 10px` creates a child that fills the container minus 20px). | `AbsoluteLayout.cpp:264-288, 303-328` |

**Recommended addition to Task 4.3**:

```csharp
// Before laying out the absolute child, determine its size:
// If both left and right are defined and width is not explicit:
if (!HasExplicitWidth(child.Element) && left.HasValue && right.HasValue)
{
    childWidth = node.Width - padding.Left - padding.Right - left.Value - right.Value;
    childWidth = Math.Max(0, childWidth); // Floor at 0
}
// Same for top/bottom -> height
```

---

## 4. Position Relative (Task 4.3)

### Plan (lines 1274-1283)

```csharp
if (child.Element.Position == Position.Relative)
{
    var offsetX = context.ResolveWidth(child.Element.Left) ?? 0f;
    var offsetY = context.ResolveHeight(child.Element.Top) ?? 0f;
    child.X += offsetX;
    child.Y += offsetY;
}
```

### Yoga Reference: `Node::relativePosition()` (Node.cpp:267-280)

```cpp
float Node::relativePosition(FlexDirection axis, Direction direction, float axisSize) const {
    if (style_.positionType() == PositionType::Static) {
        return 0;
    }
    if (style_.isInlineStartPositionDefined(axis, direction) &&
        !style_.isInlineStartPositionAuto(axis, direction)) {
        return style_.computeInlineStartPosition(axis, direction, axisSize);
    }
    return -1 * style_.computeInlineEndPosition(axis, direction, axisSize);
}
```

And in `Node::setPosition()` (Node.cpp:282-326):
```cpp
// Position includes margin + relativePosition for BOTH leading and trailing edges
setLayoutPosition(
    (style_.computeInlineStartMargin(mainAxis, direction, ownerWidth) +
     relativePositionMain),
    mainAxisLeadingEdge);
```

### Verdict: PARTIALLY CORRECT with issues

| # | Severity | Issue | Yoga Reference |
|---|----------|-------|---------------|
| 5 | **HIGH** | **Plan only handles `left` and `top` insets for relative positioning.** Yoga handles the opposite insets as well: if `left` is NOT defined but `right` IS defined, then `relativePosition = -1 * right`. So `right: 10px` means the element moves 10px to the LEFT. The plan's code ignores `right` and `bottom` offsets entirely. | `Node.cpp:279` |
| 6 | **MEDIUM** | Plan uses `ResolveWidth(child.Element.Left)` for X offset regardless of flex direction. In Yoga, `relativePosition` is computed per-axis (main and cross), not per physical dimension. For a row container, main axis offset comes from `left`/`right`; for a column container, main axis offset comes from `top`/`bottom`. However, since the plan uses physical Left/Top directly (not logical start/end), this is functionally correct for LTR layouts. For RTL support in the future this would need revisiting. | `Node.cpp:297-304` |

**Recommended fix for issue 5**:

```csharp
if (child.Element.Position == Position.Relative)
{
    var left = context.ResolveWidth(child.Element.Left);
    var right = context.ResolveWidth(child.Element.Right);
    var top = context.ResolveHeight(child.Element.Top);
    var bottom = context.ResolveHeight(child.Element.Bottom);

    // Left takes priority over right (CSS spec)
    var offsetX = left ?? (right.HasValue ? -right.Value : 0f);
    var offsetY = top ?? (bottom.HasValue ? -bottom.Value : 0f);

    child.X += offsetX;
    child.Y += offsetY;
}
```

---

## 5. Position Absolute -- Justify/Align Fallback (Task 4.3)

### Plan

The plan defaults absolute children without insets to `padding.Left` / `padding.Top` (equivalent to FlexStart).

### Yoga Reference: `justifyAbsoluteChild()` (AbsoluteLayout.cpp:84-108)

```cpp
switch (parentJustifyContent) {
    case Justify::FlexStart:
    case Justify::SpaceBetween:
        setFlexStartLayoutPosition(...);  // Start
        break;
    case Justify::FlexEnd:
        setFlexEndLayoutPosition(...);    // End
        break;
    case Justify::Center:
    case Justify::SpaceAround:
    case Justify::SpaceEvenly:
        setCenterLayoutPosition(...);     // Center
        break;
}
```

### Yoga Reference: `alignAbsoluteChild()` (AbsoluteLayout.cpp:110-146)

```cpp
Align itemAlign = resolveChildAlignment(parent, child);
// WrapReverse inverts the alignment:
if (parentWrap == Wrap::WrapReverse) {
    if (itemAlign == Align::FlexEnd) itemAlign = Align::FlexStart;
    else if (itemAlign != Align::Center) itemAlign = Align::FlexEnd;
}

switch (itemAlign) {
    case Align::Auto:
    case Align::FlexStart:
    case Align::Baseline:
    case Align::SpaceAround:
    case Align::SpaceBetween:
    case Align::Stretch:
    case Align::SpaceEvenly:
        setFlexStartLayoutPosition(...);
        break;
    case Align::FlexEnd:
        setFlexEndLayoutPosition(...);
        break;
    case Align::Center:
        setCenterLayoutPosition(...);
        break;
}
```

### Verdict: MISSING

| # | Severity | Issue | Yoga Reference |
|---|----------|-------|---------------|
| 7 | **HIGH** | **Justify/align fallback for absolute children without insets is completely missing.** The plan always positions to `padding.Left`/`padding.Top`, effectively hardcoding `FlexStart`. Yoga uses `justify-content` for the main axis and `align-items` (with `align-self` override) for the cross axis. This means `justify-content: center` should center absolute children without insets, `justify-content: flex-end` should push them to the end, etc. | `AbsoluteLayout.cpp:84-146, 219-222` |
| 8 | **MEDIUM** | For cross-axis alignment of absolute children, Yoga applies WrapReverse inversion (FlexEnd <-> FlexStart swap). If FlexRender supports `flex-wrap: wrap-reverse`, this inversion must be applied to absolute child alignment as well. | `AbsoluteLayout.cpp:118-124` |

**Recommended addition**:

```csharp
// Main axis (no insets case):
private static float JustifyAbsoluteChild(
    float containerSize, float childSize, float paddingStart, float paddingEnd,
    JustifyContent justify)
{
    return justify switch
    {
        JustifyContent.FlexStart or JustifyContent.SpaceBetween => paddingStart,
        JustifyContent.FlexEnd => containerSize - paddingEnd - childSize,
        JustifyContent.Center or JustifyContent.SpaceAround or JustifyContent.SpaceEvenly
            => paddingStart + (containerSize - paddingStart - paddingEnd - childSize) / 2f,
        _ => paddingStart
    };
}

// Cross axis (no insets case):
// Use resolveChildAlignment(parent, child) then position accordingly
// Apply WrapReverse inversion before the switch
```

---

## 6. Overflow:Hidden (Task 4.4)

### Plan (lines 1324-1342)

```csharp
var needsClip = node.Element is FlexElement { Overflow: Overflow.Hidden };
if (needsClip)
{
    canvas.Save();
    canvas.ClipRect(new SKRect(x, y, x + node.Width, y + node.Height));
}
// ... render children ...
if (needsClip) canvas.Restore();
```

### Yoga Reference

Yoga has `Overflow::Hidden`, `Overflow::Visible`, and `Overflow::Scroll`. In the layout algorithm, `Overflow::Scroll` affects sizing mode decisions (CalculateLayout.cpp:159-168), but **overflow does not affect layout positioning at all** -- it is purely a rendering concern.

Yoga also uses `overflow` in one layout decision: when `overflow == Scroll` and `sizingMode == FitContent`, the container is clamped to available size (CalculateLayout.cpp:2031-2080).

### Verdict: CORRECT

| # | Severity | Issue | Yoga Reference |
|---|----------|-------|---------------|
| 9 | **LOW** | The plan correctly treats `overflow:hidden` as a renderer concern (clipping in SkiaRenderer). This matches Yoga's approach where overflow does not change layout positions. | N/A |
| 10 | **MEDIUM** | The plan does not implement `Overflow::Scroll` sizing behavior. In Yoga, `overflow: scroll` affects how auto-sized containers are clamped (they clamp to available size rather than expanding to fit content). This is a separate feature and can be deferred, but should be documented as a known gap. | `CalculateLayout.cpp:2029-2080` |

---

## 7. Aspect Ratio (Task 4.5)

### Plan (lines 1372-1386)

```csharp
if (child.Element.AspectRatio.HasValue)
{
    var ratio = child.Element.AspectRatio.Value;
    if (ratio > 0)
    {
        if (HasExplicitWidth(child.Element) && !HasExplicitHeight(child.Element))
            child.Height = child.Width / ratio;
        else if (HasExplicitHeight(child.Element) && !HasExplicitWidth(child.Element))
            child.Width = child.Height * ratio;
    }
}
```

### Yoga Reference: Multiple locations in `CalculateLayout.cpp`

**At flex basis computation** (CalculateLayout.cpp:176-187):
```cpp
if (childStyle.aspectRatio().isDefined()) {
    if (!isMainAxisRow && childWidthSizingMode == SizingMode::StretchFit) {
        childHeight = marginColumn + (childWidth - marginRow) / childStyle.aspectRatio().unwrap();
        childHeightSizingMode = SizingMode::StretchFit;
    } else if (isMainAxisRow && childHeightSizingMode == SizingMode::StretchFit) {
        childWidth = marginRow + (childHeight - marginColumn) * childStyle.aspectRatio().unwrap();
        childWidthSizingMode = SizingMode::StretchFit;
    }
}
```

**At flex resolution** (CalculateLayout.cpp:716-719):
```cpp
if (childStyle.aspectRatio().isDefined()) {
    childCrossSize = isMainAxisRow
        ? (childMainSize - marginMain) / childStyle.aspectRatio().unwrap()
        : (childMainSize - marginMain) * childStyle.aspectRatio().unwrap();
    childCrossSize += marginCross;
}
```

**At stretch** (CalculateLayout.cpp:201-205):
```cpp
if (childStyle.aspectRatio().isDefined()) {
    childHeight = (childWidth - marginRow) / childStyle.aspectRatio().unwrap();
}
```

**At absolute child sizing** (AbsoluteLayout.cpp:335-345):
```cpp
if (yoga::isUndefined(childWidth) ^ yoga::isUndefined(childHeight)) {
    if (childStyle.aspectRatio().isDefined()) {
        if (yoga::isUndefined(childWidth)) {
            childWidth = marginRow + (childHeight - marginColumn) * childStyle.aspectRatio().unwrap();
        } else if (yoga::isUndefined(childHeight)) {
            childHeight = marginColumn + (childWidth - marginRow) / childStyle.aspectRatio().unwrap();
        }
    }
}
```

### Verdict: PARTIALLY CORRECT with issues

| # | Severity | Issue | Yoga Reference |
|---|----------|-------|---------------|
| 11 | **HIGH** | **Plan applies aspect ratio only when explicit dimensions are set.** Yoga applies aspect ratio in MULTIPLE contexts: (a) at flex basis computation, (b) after flex resolution (main size known -> compute cross size), (c) at stretch (cross size known -> compute main size), (d) for absolute children. The plan's simple check `HasExplicitWidth && !HasExplicitHeight` misses the most important case: when the main axis size is computed via flex-grow/shrink, the cross axis size should be computed via aspect ratio. | `CalculateLayout.cpp:176-187, 716-719` |
| 12 | **MEDIUM** | **Margin subtraction missing.** Yoga computes aspect ratio on the CONTENT size (excluding margins): `(childWidth - marginRow) / ratio`. The plan uses the full `child.Width` including margins. For children with non-zero margins, this produces incorrect results. | `CalculateLayout.cpp:179, 718` |

**Recommended fix for issue 11**:

The plan should apply aspect ratio at three points:
1. During flex basis resolution (if one axis is explicit, compute the other)
2. After flex main-axis resolution (main size determined by grow/shrink -> compute cross size)
3. After cross-axis stretch (cross size determined by container -> compute main size if aspect ratio set)

```csharp
// After flex resolution sets childMainSize:
if (child.Element.AspectRatio is > 0 aspectRatio)
{
    var marginMain = GetMarginMain(child.Element, isColumn);
    var marginCross = GetMarginCross(child.Element, isColumn);

    if (isColumn)
    {
        // Main = height, Cross = width
        child.Width = (child.Height - marginMain) * aspectRatio + marginCross;
    }
    else
    {
        // Main = width, Cross = height
        child.Height = (child.Width - marginMain) / aspectRatio + marginCross;
    }
}
```

---

## 8. Containing Block Resolution (Context for Task 4.3)

### Yoga Reference: `layoutAbsoluteDescendants()` (AbsoluteLayout.cpp:424-562)

Yoga has a sophisticated containing block resolution mechanism:
- The containing block for an absolute child is the **nearest ancestor with `positionType != Static`** (or `alwaysFormsContainingBlock`, or tree root at `depth == 1`).
- `layoutAbsoluteDescendants()` recursively traverses the tree, accumulating position offsets from the containing block.
- When a non-absolute, non-containing-block node (Static without `alwaysFormsContainingBlock`) is encountered, it recurses into that node's children, carrying the accumulated offset.

### Plan (Task 4.3)

The plan positions absolute children relative to their **direct parent** only. There is no containing block resolution.

### Verdict: ACCEPTABLE simplification

| # | Severity | Issue | Yoga Reference |
|---|----------|-------|---------------|
| 13 | **LOW** | The plan always uses the direct parent as the containing block. This is a simplification that works correctly in the common case (absolute child inside a non-static parent). It only breaks when an absolute child is nested inside a static grandparent but should be positioned relative to a non-static great-grandparent. For FlexRender's use case (document rendering, not web layout), this simplification is reasonable. Document as a known limitation. | `AbsoluteLayout.cpp:424-562` |

---

## 9. Absolute Children and Intrinsic Measurement

### Yoga Reference

In Yoga, absolute children are **completely excluded** from:
- Flex line calculation (`calculateFlexLine` skips `PositionType::Absolute`, FlexLine.cpp)
- Flex basis computation (skipped after `setPosition`, CalculateLayout.cpp:588)
- Container intrinsic sizing (they don't contribute to container's auto size)

Absolute children ARE included in:
- `layoutAbsoluteDescendants()` for their own layout (after the parent is fully sized)

### Plan (Task 4.3)

The plan correctly skips absolute children in the flex flow loop. However, it does not explicitly address intrinsic measurement.

### Verdict: NEEDS VERIFICATION

| # | Severity | Issue | Yoga Reference |
|---|----------|-------|---------------|
| 14 | **MEDIUM** | Verify that `MeasureAllIntrinsics` / `MeasureFlexIntrinsic` also skips absolute children when computing the container's intrinsic size. If absolute children contribute to the container's MinWidth/MaxWidth, the container may be sized larger than expected. Yoga completely excludes absolute children from parent sizing. | `FlexLine.cpp` skip logic, `CalculateLayout.cpp:588` |

---

## Consolidated Issue Table

| # | Severity | Task | Issue Summary |
|---|----------|------|---------------|
| 1 | MEDIUM | 4.3 | Padding vs border for inset positioning (no borders in FlexRender -- document decision) |
| 2 | MEDIUM | 4.3 | Child margin missing from absolute positioning formula |
| 3 | MEDIUM | 4.3 | Default position uses hardcoded FlexStart instead of justify/align |
| 4 | **HIGH** | 4.3 | **Inset-based sizing (left+right -> width) completely missing** |
| 5 | **HIGH** | 4.3 | **Relative positioning ignores right/bottom insets (should negate)** |
| 6 | MEDIUM | 4.3 | Relative positioning uses physical dims, not logical axes (RTL concern) |
| 7 | **HIGH** | 4.3 | **Justify/align fallback for absolute children without insets missing** |
| 8 | MEDIUM | 4.3 | WrapReverse inversion for absolute child alignment missing |
| 9 | LOW | 4.4 | Overflow:hidden correctly treated as renderer concern |
| 10 | MEDIUM | 4.4 | Overflow:scroll sizing behavior not implemented (defer is OK) |
| 11 | **HIGH** | 4.5 | **Aspect ratio only at explicit dimensions; misses flex-resolved and stretch cases** |
| 12 | MEDIUM | 4.5 | Aspect ratio formula doesn't subtract margins |
| 13 | LOW | 4.3 | Containing block simplified to direct parent (acceptable for FlexRender) |
| 14 | MEDIUM | 4.3 | Verify absolute children excluded from intrinsic measurement |

---

## Answers to Team-Lead's Specific Questions

### Q1: Containing block resolution + inset-based sizing (left+right -> width)?

**Containing block**: Yoga resolves to nearest `positionType != Static` ancestor via recursive `layoutAbsoluteDescendants()`. The plan simplifies to direct parent, which is acceptable for FlexRender. **Inset-based sizing**: This is a critical gap (**HIGH #4**). When both opposing insets are defined (left+right or top+bottom) and no explicit dimension is set, the child's size should be computed as `containerSize - paddingStart - paddingEnd - insetStart - insetEnd`. The plan doesn't implement this at all.

### Q2: Relative positioning edge cases?

Two issues found. **HIGH #5**: The plan only handles `left`/`top` offsets. Yoga also handles `right`/`bottom` -- if `left` is undefined but `right` is set, the element moves LEFTWARD by `right` amount (`relativePosition = -1 * right`). **MEDIUM #6**: The plan uses physical Left/Top directly. Yoga resolves per-axis (main/cross) based on flex direction, which matters for RTL but is OK for LTR-only.

### Q3: Absolute children exclusion from flex flow and intrinsic measurement?

Flex flow exclusion is correct. Intrinsic measurement exclusion needs verification (**MEDIUM #14**). The plan must ensure `MeasureFlexIntrinsic` skips `Position == Absolute` children, otherwise the container's auto-size calculations will include absolute children's dimensions, which is incorrect per Yoga.

### Q4: Absolute + justify/align for no-inset absolute children?

This is completely missing (**HIGH #7**). The plan hardcodes FlexStart (top-left corner) for absolute children without insets. Yoga uses `justify-content` on the main axis and `align-items`/`align-self` on the cross axis to position these children. This means `justify-content: center` should center an absolute child with no insets.

### Q5: Overflow:hidden -- layout vs renderer concern?

The plan correctly treats it as a renderer concern (**LOW #9**). In Yoga, `overflow:hidden` does not affect layout positions -- only rendering clipping. The plan's `canvas.Save()` / `canvas.ClipRect()` / `canvas.Restore()` approach in SkiaRenderer is correct. The only layout-affecting overflow in Yoga is `overflow: scroll`, which affects auto-sizing behavior (the container clamps to available size). This can be deferred (**MEDIUM #10**).

---

## Recommendations

### Must-fix before implementation (4 HIGH issues):

1. **Add inset-based sizing** (HIGH #4): When both left+right or top+bottom are defined, compute child size from insets
2. **Add right/bottom for relative** (HIGH #5): `offsetX = left ?? -right ?? 0`, `offsetY = top ?? -bottom ?? 0`
3. **Add justify/align fallback for absolute** (HIGH #7): Use parent's justify-content/align-items when no insets defined
4. **Expand aspect ratio application** (HIGH #11): Apply after flex resolution and stretch, not just for explicit dimensions

### Should-fix (5 MEDIUM issues):

5. **Add child margin to absolute positioning** (MEDIUM #2)
6. **Skip absolute children in intrinsic measurement** (MEDIUM #14)
7. **Subtract margins in aspect ratio formula** (MEDIUM #12)
8. **WrapReverse inversion for absolute alignment** (MEDIUM #8)
9. **Document border/padding design decision** (MEDIUM #1)

### Can defer:

10. Overflow:scroll sizing behavior (MEDIUM #10)
11. Containing block resolution beyond direct parent (LOW #13)
