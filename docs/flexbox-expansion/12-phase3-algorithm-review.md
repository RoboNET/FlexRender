# Phase 3 Algorithm Review: Wrapping Correctness vs Yoga Reference

**Reviewer**: yoga-c-researcher
**Scope**: Tasks 3.1-3.6 from IMPLEMENTATION-PLAN.md (Phase 3 -- Wrapping)
**Reference**: facebook/yoga `FlexLine.cpp`, `CalculateLayout.cpp` (Steps 4-9), `Align.h`, `FlexDirection.h`

---

## 1. Line Breaking -- CalculateFlexLines (Task 3.5)

### Plan (lines 1043-1093)

```csharp
private List<FlexLine> CalculateFlexLines(
    IReadOnlyList<LayoutNode> children,
    float availableMainSize,
    bool isColumn,
    float gap)
{
    // ...
    if (itemsInLine > 0 && lineMainSize + gapBefore + childMainSize > availableMainSize)
    {
        lines.Add(new FlexLine(startIndex, itemsInLine, lineMainSize, lineCrossSize));
        // ... start new line
    }
    else
    {
        lineMainSize += gapBefore + childMainSize;
        lineCrossSize = Math.Max(lineCrossSize, childCrossSize);
        itemsInLine++;
    }
}
```

### Yoga Reference: `calculateFlexLine()` (FlexLine.cpp:16-122)

```cpp
const float flexBasisWithMinAndMaxConstraints =
    boundAxisWithinMinAndMax(child, mainAxis,
        child->getLayout().computedFlexBasis, ...);

if (sizeConsumedIncludingMinConstraint + flexBasisWithMinAndMaxConstraints +
        childMarginMainAxis + childLeadingGapMainAxis >
    availableInnerMainDim && isNodeFlexWrap && !itemsInFlow.empty()) {
    break;  // Start new line
}

sizeConsumed += flexBasisWithMinAndMaxConstraints + childMarginMainAxis +
    childLeadingGapMainAxis;
```

### Verdict: CORRECT with issues

The greedy line-breaking approach matches Yoga. Both use the same condition: accumulated size + new item > available -> break.

**Issues found**:

| # | Severity | Issue | Yoga Reference |
|---|----------|-------|---------------|
| 1 | **HIGH** | Plan uses `childMainSize` (computed layout size) instead of `flexBasisWithMinAndMaxConstraints` | Yoga uses `boundAxisWithinMinAndMax(computedFlexBasis)` for line-breaking -- NOT the final layout size. The flex basis (clamped by min/max) determines whether an item fits on the current line. The plan uses `child.Height`/`child.Width` which is the intrinsic or explicit size, not the resolved flex basis. For items with explicit `flex-basis`, these may differ. If `flex-basis: 200px` but intrinsic width is 50px, the plan would use 50px for line breaking, while Yoga uses 200px. |
| 2 | **HIGH** | Plan does NOT include child margins in line-breaking calculation | Yoga: `flexBasisWithMinAndMaxConstraints + childMarginMainAxis + childLeadingGapMainAxis`. The plan only adds `gapBefore + childMainSize` but NOT the child's margin on the main axis. A child with `margin: 20px` and `width: 45px` should consume 85px (45 + 20 + 20) on the main axis, but the plan counts it as 45px. |
| 3 | **MEDIUM** | Plan skips `display:none` children but does NOT skip `position:absolute` children | Yoga (FlexLine.cpp:46-49): skips both `display: none` AND `positionType == Absolute`. The plan has `if (child.Element.Display == Display.None) continue;` but no check for absolute positioning. Absolute children should not participate in line breaking. |
| 4 | **MEDIUM** | Plan uses `child.Width`/`child.Height` for cross-axis line size instead of considering margins | Yoga includes margins when computing cross-axis line size: `child->dimensionWithMargin(crossAxis, ...)`. The plan uses `child.Width`/`child.Height` without adding cross-axis margins. |
| 5 | **LOW** | Gap handling is correct | Plan: `var gapBefore = itemsInLine > 0 ? gap : 0f;` matches Yoga: `child == firstElementInLine ? 0.0f : gap`. No gap before first item. Correct. |
| 6 | **LOW** | FlexLine struct stores StartIndex+Count | Yoga stores `vector<Node*> itemsInFlow`. The plan's index-based approach is equivalent and more memory-efficient. Correct. |
| 7 | **LOW** | Plan does not accumulate totalGrowFactors/totalShrinkScaledFactors during line breaking | Yoga computes these in `calculateFlexLine` and stores them in `FlexLineRunningLayout`. The plan's `FlexLine` struct has `MainAxisSize` and `CrossAxisSize` but no flex factor totals. These must be computed later during per-line flex resolution. Not a correctness issue if computed correctly in the per-line pass. |

**Critical Fix Required**: Use flex basis (clamped by min/max) for line-breaking, not layout size:

```csharp
// Instead of:
var childMainSize = isColumn ? child.Height : child.Width;

// Use:
var childBasis = ResolveFlexBasis(child.Element, child, isColumn, context);
var (childMin, childMax) = ResolveMinMax(child.Element, isColumn, context);
var childMainSize = ClampSize(childBasis, childMin, childMax);
// Also add margin:
var childMargin = PaddingParser.Parse(child.Element.Margin, ...).ClampNegatives();
var childMarginMain = isColumn ? childMargin.Vertical : childMargin.Horizontal;
var totalChildMain = childMainSize + childMarginMain;
```

**Critical Fix Required**: Add margin to line-breaking threshold:

```csharp
if (itemsInLine > 0 && lineMainSize + gapBefore + totalChildMain > availableMainSize)
```

---

## 2. Per-Line Flex Resolution (Task 3.6)

### Plan (lines 1125-1127)

```csharp
// Per-line: apply flex grow/shrink, justify-content, and align-items
// ... (apply per-line flex resolution)
```

The plan is skeletal here -- it says "apply per-line flex resolution" without concrete code.

### Yoga Reference: Main Loop (CalculateLayout.cpp:1470-1608)

Yoga's main loop iterates over lines and for each line:
1. **Step 5**: `resolveFlexibleLength(flexLine, ...)` -- applies the two-pass flex distribution (first pass freezes min/max violators, second pass distributes remaining)
2. **Step 6**: `justifyMainAxis(flexLine, ...)` -- applies justify-content for that line
3. **Step 7**: Cross-axis alignment per child within the line

### Verdict: CORRECT in concept, but needs concrete implementation

The plan correctly states that each line should be processed independently for grow/shrink. This matches Yoga, which calls `resolveFlexibleLength()` and `justifyMainAxis()` per `FlexLine`.

**Issues found**:

| # | Severity | Issue | Yoga Reference |
|---|----------|-------|---------------|
| 1 | **HIGH** | No concrete per-line flex resolution code | The plan leaves per-line resolution as a comment. The implementation must: (a) compute `remainingFreeSpace = availableMainSize - line.sizeConsumed` per line, (b) apply the iterative freeze loop from Phase 2 per line, (c) apply justify-content per line. If the implementer reuses the existing single-line code path, this is fine. But the plan should specify this. |
| 2 | **HIGH** | Per-line `remainingFreeSpace` must use line's `sizeConsumed`, not total | Yoga (line 1553): `flexLine.layout.remainingFreeSpace = availableInnerMainDim - flexLine.sizeConsumed`. Each line has its own consumed space and own remaining free space. The plan must ensure this per-line scope. |
| 3 | **MEDIUM** | Factor flooring must be per-line | Yoga applies factor flooring (`totalGrowFactors > 0 && < 1 -> 1`) in `calculateFlexLine()`. If FlexRender computes factors during per-line resolution instead, the flooring must still be applied per-line, not globally. |
| 4 | **MEDIUM** | Cross-axis line size needs re-computation after flex resolution | Yoga recomputes `crossDim` in `justifyMainAxis()` (line 1144: `flexLine.layout.crossDim = max(crossDim, child->dimensionWithMargin(crossAxis, ...))`). After flex resolution changes main-axis sizes, children with aspect ratios could change cross-axis sizes. The plan should account for this. |

**Recommendation**: The plan should explicitly state that the per-line code path reuses the Phase 2 flex resolution algorithm (ResolveFlexBasis, two-pass freeze loop, ClampSize) on a per-line subset of children.

---

## 3. Align-Content (Task 3.6)

### Plan (lines 1129-1137)

```csharp
var totalLinesSize = lines.Sum(l => l.CrossAxisSize);
var totalCrossGaps = crossGap * Math.Max(0, lines.Count - 1);
var availableCrossSize = isColumn
    ? node.Width - padding.Horizontal
    : (node.Height > 0 ? node.Height - padding.Vertical : totalLinesSize + totalCrossGaps);
var crossFreeSpace = availableCrossSize - totalLinesSize - totalCrossGaps;

// Distribute cross free space per align-content
// ... (Start, Center, End, Stretch, SpaceBetween, SpaceAround, SpaceEvenly)
```

### Yoga Reference: Step 8 (CalculateLayout.cpp:1768-1987)

```cpp
const float remainingAlignContentDim = innerCrossDim - totalLineCrossDim;

const auto alignContent = remainingAlignContentDim >= 0
    ? node->style().alignContent()
    : fallbackAlignment(node->style().alignContent());

switch (alignContent) {
    case FlexEnd:      currentLead += remainingAlignContentDim; break;
    case Center:       currentLead += remainingAlignContentDim / 2; break;
    case Stretch:      extraSpacePerLine = remaining / lineCount; break;
    case SpaceAround:  currentLead += remaining / (2 * lineCount);
                       leadPerLine = remaining / lineCount; break;
    case SpaceEvenly:  currentLead += remaining / (lineCount + 1);
                       leadPerLine = remaining / (lineCount + 1); break;
    case SpaceBetween: if (lineCount > 1) leadPerLine = remaining / (lineCount - 1); break;
    case FlexStart:    break;
}

// Then iterate lines, adding currentLead + crossGap between lines
for (size_t i = 0; i < lineCount; i++) {
    currentLead += i != 0 ? crossAxisGap : 0;  // Gap between lines
    lineHeight += extraSpacePerLine;             // Stretch adds to line height
    // ... position children within line
    currentLead = currentLead + leadPerLine + lineHeight;
}
```

### Verdict: CORRECT in approach, multiple issues in details

The plan's overall approach (compute cross free space, distribute per align-content) matches Yoga. However, several critical details are missing.

**Issues found**:

| # | Severity | Issue | Yoga Reference |
|---|----------|-------|---------------|
| 1 | **HIGH** | Missing align-content overflow fallback | Yoga (line 1797-1799): `const auto alignContent = remainingAlignContentDim >= 0 ? ... : fallbackAlignment(...)`. When `remainingAlignContentDim < 0`, SpaceBetween/Stretch -> FlexStart, SpaceAround/SpaceEvenly -> FlexStart. The plan's comment lists "Start, Center, End, Stretch, SpaceBetween, SpaceAround, SpaceEvenly" but does NOT mention overflow fallback. This is the same pattern as justify-content overflow fallback from Phase 1. |
| 2 | **HIGH** | Stretch adds to line height, does not reposition | Yoga: `extraSpacePerLine = remainingAlignContentDim / lineCount` is added to each line's height: `lineHeight += extraSpacePerLine`. This means stretched lines are taller, and children within are stretched to the new line height. The plan does not describe this behavior. |
| 3 | **HIGH** | Missing per-line child positioning after align-content | Yoga Step 8 iterates lines and re-positions children based on `currentLead`. After align-content distribution, children need their cross-axis positions updated: `child.position = currentLead + alignment_offset_within_line`. The plan says "distribute cross free space" but does not describe updating child positions. |
| 4 | **MEDIUM** | `totalLinesSize` uses `lines.Sum()` -- LINQ in hot path | Per AGENTS.md coding conventions, LINQ should be avoided in hot paths (`LayoutEngine` is listed as a hot path). Should use a `foreach` loop. |
| 5 | **MEDIUM** | Cross gap between lines (`crossGap`) must NOT be applied before the first line | Yoga (line 1879): `currentLead += i != 0 ? crossAxisGap : 0`. The plan computes `totalCrossGaps = crossGap * Math.Max(0, lines.Count - 1)` which is correct for free space calculation, but the per-line positioning must also skip gap before the first line. |
| 6 | **MEDIUM** | Missing Stretch re-layout of children | Yoga (lines 1916-1962): When align-content is Stretch, children within a stretched line need re-layout with the new line height as their cross-axis constraint. Children with align-self: stretch (and no definite cross size) should be re-measured. The plan doesn't describe this child re-layout. |
| 7 | **LOW** | Plan uses `.Sum()` and `.Count` on `List<FlexLine>` | `List<T>.Count` is O(1) and fine. `.Sum()` should be replaced with manual loop per convention. |

**Critical Fix Required**: Add overflow fallback for align-content:

```csharp
var effectiveAlignContent = crossFreeSpace < 0 ? flex.AlignContent switch
{
    AlignContent.SpaceBetween => AlignContent.Start,
    AlignContent.Stretch => AlignContent.Start,
    AlignContent.SpaceAround => AlignContent.Start,
    AlignContent.SpaceEvenly => AlignContent.Start,
    _ => flex.AlignContent
} : flex.AlignContent;
```

**Critical Fix Required**: Stretch adds to line height:

```csharp
if (effectiveAlignContent == AlignContent.Stretch && lines.Count > 0)
{
    var extraPerLine = crossFreeSpace / lines.Count;
    for (var i = 0; i < lines.Count; i++)
    {
        lines[i] = lines[i] with { CrossAxisSize = lines[i].CrossAxisSize + extraPerLine };
    }
}
```

---

## 4. WrapReverse (Task 3.6)

### Plan (lines 1140-1152)

```csharp
if (flex.Wrap == FlexWrap.WrapReverse)
{
    var containerCrossSize = isColumn
        ? node.Width - padding.Horizontal
        : (node.Height > 0 ? node.Height - padding.Vertical : totalLinesSize + totalCrossGaps);
    foreach (var child in node.Children)
    {
        if (isColumn)
            child.X = containerCrossSize - child.X - child.Width + padding.Left;
        else
            child.Y = containerCrossSize - child.Y - child.Height + padding.Top;
    }
}
```

### Yoga Reference (CalculateLayout.cpp:2083-2095)

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

### Verdict: PARTIALLY CORRECT

The formula concept is right (`newPos = containerSize - oldPos - childSize`), but there are critical issues.

**Issues found**:

| # | Severity | Issue | Yoga Reference |
|---|----------|-------|---------------|
| 1 | **HIGH** | Plan's formula includes padding offset (`+ padding.Left`/`+ padding.Top`) but Yoga's does NOT | Yoga formula: `containerCrossSize - childPosition - childCrossSize`. No padding adjustment because positions already include padding. The plan adds `padding.Left`/`padding.Top` which double-counts padding if child positions already include padding offset. |
| 2 | **HIGH** | Plan uses `containerCrossSize` excluding padding, but Yoga uses full measured dimension | Yoga: `node->getLayout().measuredDimension(dimension(crossAxis))` -- this is the FULL container cross dimension INCLUDING padding. The plan subtracts padding: `node.Width - padding.Horizontal`. This creates a mismatch. |
| 3 | **MEDIUM** | Plan does NOT skip absolute children | Yoga (line 2087): `if (child->style().positionType() != PositionType::Absolute)`. Absolute children are NOT affected by wrap-reverse. The plan iterates ALL children without this filter. |
| 4 | **LOW** | Timing of WrapReverse application | Yoga applies wrap-reverse AFTER Step 9 (final dimensions) and AFTER Step 8 (align-content). The plan places it at the end of `LayoutWrappedFlex` which should be equivalent if align-content is already applied. Verify execution order. |

**Critical Fix Required**: The wrap-reverse formula should use FULL container size and NOT add padding:

```csharp
if (flex.Wrap == FlexWrap.WrapReverse)
{
    // Use full container cross dimension (including padding)
    var containerCrossSize = isColumn ? node.Width : node.Height;
    foreach (var child in node.Children)
    {
        if (child.Element.Position == Position.Absolute) continue;
        if (isColumn)
            child.X = containerCrossSize - child.X - child.Width;
        else
            child.Y = containerCrossSize - child.Y - child.Height;
    }
}
```

This works because child positions already include padding (e.g., `child.X = padding.Left + ...`). The flip formula `containerCrossSize - (padding.Left + offset) - childWidth` correctly places the child at the symmetric position relative to the container.

---

## 5. MaxFlexLines = 1000 (Task 3.4)

### Yoga Reference

Yoga does NOT have a line count limit. It processes all lines until children are exhausted.

### Analysis

| Factor | Assessment |
|--------|-----------|
| **Typical use case** | Receipt/label rendering. Most wrapping layouts have 2-10 lines. 1000 is more than adequate. |
| **DoS vector** | An attacker could create a template with thousands of tiny children in a wrapping container, causing O(N * lines) computation. The limit prevents this. |
| **Memory impact** | Each FlexLine is a `readonly record struct` with 4 floats (~16 bytes). 1000 lines = ~16KB. Minimal. |
| **Comparison to other limits** | `MaxRenderDepth = 100` limits nesting. `MaxFlexLines = 1000` limits wrapping. Both serve the same purpose. |
| **Edge cases** | A 1000x1000px image with 1px items could legitimately need 1000 lines. This is extreme but possible. |

### Verdict: APPROPRIATE

1000 is a reasonable default. The limit is configurable via `ResourceLimits.MaxFlexLines`, so users with legitimate need for more lines can increase it. The exception type (`InvalidOperationException`) is appropriate for resource exhaustion.

**One issue**: The limit check is inside the line-breaking loop:

```csharp
if (lines.Count >= _limits.MaxFlexLines)
    throw new InvalidOperationException(
        $"Maximum flex lines ({_limits.MaxFlexLines}) exceeded.");
```

This throws after adding the line, so the actual count could be `MaxFlexLines + 1` (the line is added, then the check fires before the next iteration). Should check BEFORE adding:

```csharp
// Before adding a new line (not the first):
if (lines.Count >= _limits.MaxFlexLines)
    throw new InvalidOperationException(...);
lines.Add(new FlexLine(startIndex, itemsInLine, lineMainSize, lineCrossSize));
```

Wait -- reading the code more carefully, the check is AFTER `lines.Add()`, which means the thrown exception is when we're about to create line N+1. The current line (N) is already added. This is actually correct behavior: the limit says "max N lines", and we throw when trying to create line N+1. The `>=` comparison after adding means: if we just added the Nth line, stop before creating more. This is correct.

**Actually**: Re-reading more carefully, the sequence is: `lines.Add(...)` then `if (lines.Count >= _limits.MaxFlexLines) throw`. So if MaxFlexLines=1000 and we add line #1000, lines.Count becomes 1000, and 1000 >= 1000 is true -- we throw. But we just added line 1000, which should be allowed. The throw should be `>` not `>=`, OR the check should be moved to before the Add.

| Fix Option | Code |
|------------|------|
| Option A: Use `>` | `if (lines.Count > _limits.MaxFlexLines)` |
| Option B: Check before Add | Move check before `lines.Add(...)` and use `>=` |

**Recommendation**: Option A is simpler. Allow exactly MaxFlexLines lines.

---

## 6. Row-Gap / Column-Gap (Task 3.1)

### Plan (lines 897-923)

```csharp
// On FlexElement:
public string? ColumnGap { get; set; }
public string? RowGap { get; set; }
```

### Yoga Reference

Yoga supports `gap`, `rowGap`, `columnGap` via `computeGapForAxis(axis, availableSize)`:

```cpp
// FlexLine.cpp:39-40 (main-axis gap)
const float gap = node->style().computeGapForAxis(mainAxis, availableInnerMainDim);

// CalculateLayout.cpp:1878-1879 (cross-axis gap between lines)
currentLead += i != 0 ? crossAxisGap : 0;
```

### Verdict: CORRECT

The plan correctly separates gap into row-gap (between items in column direction / between lines in row direction) and column-gap (between items in row direction / between columns in column wrapping).

**Issues found**:

| # | Severity | Issue | Yoga Reference |
|---|----------|-------|---------------|
| 1 | **MEDIUM** | Gap resolution semantics: which gap applies where? | For `direction: row`, `columnGap` is the main-axis gap (between items), and `rowGap` is the cross-axis gap (between lines). For `direction: column`, `rowGap` is the main-axis gap, and `columnGap` is the cross-axis gap. The plan should explicitly document this mapping. Yoga uses `computeGapForAxis(mainAxis, ...)` which handles this mapping internally. |
| 2 | **LOW** | Fallback from gap to rowGap/columnGap | The plan's test `ComputeLayout_GapFallback_UsedWhenRowColumnGapNotSet` implies that `gap` is the fallback. This matches CSS spec: `gap` sets both `row-gap` and `column-gap`. |

**Recommendation**: Document gap-to-axis mapping:

```
direction: row    -> mainGap = columnGap ?? gap, crossGap = rowGap ?? gap
direction: column -> mainGap = rowGap ?? gap,    crossGap = columnGap ?? gap
```

---

## 7. Test Coverage Review

### Task 3.2: Wrap Tests (10 tests)

Coverage is good for basic wrapping. **Missing test cases from Yoga fixtures**:

| # | Missing Test | Yoga Fixture |
|---|-------------|-------------|
| 1 | `flex_wrap_children_with_min_main_overriding_flex_basis` | Line break at different point due to min-width overriding flex-basis (YGFlexWrapTest.html:29-32) |
| 2 | `wrap_nodes_with_content_sizing_overflowing_margin` | Margin causes wrapping even though content fits (YGFlexWrapTest.html:129-138) |
| 3 | `wrapped_column_max_height` | max-height on child causes column wrapping (YGFlexWrapTest.html:117-121) |
| 4 | `nowrap_expands_flexline_box_to_min_cross` vs `wrap_does_not_impose_min_cross_onto_single_flexline` | No-wrap imposes min-cross to line, wrap does not (YGFlexWrapTest.html:161-167) |
| 5 | `flex_wrap_align_stretch_fits_one_row` | Single-line wrap with stretch -- line height should match container (YGFlexWrapTest.html:43-46) |
| 6 | Wrap with `align-items: flex-end` / `center` | Children aligned within multi-height lines (YGFlexWrapTest.html:15-27) |

### Task 3.3: Align-Content Tests (7 tests)

Coverage of align-content values is comprehensive (all 7 values). **Missing test cases**:

| # | Missing Test | Why Important |
|---|-------------|--------------|
| 1 | Align-content overflow fallback | SpaceBetween/Stretch -> Start when cross space is negative |
| 2 | Align-content + wrap-reverse | All values with WrapReverse -- positions should be inverted |
| 3 | Align-content stretch + child re-layout | Stretched line height should cause children to re-measure |
| 4 | Single-line wrap + align-content | With only 1 line, SpaceBetween should have no effect |

---

## 8. Summary of Findings

### CRITICAL Issues (must fix before implementation)

| # | Issue | Severity | Location | Fix |
|---|-------|----------|----------|-----|
| 1 | **Line breaking uses layout size instead of flex basis** | HIGH | `CalculateFlexLines` | Use `boundAxisWithinMinAndMax(ResolveFlexBasis(...))` for line break decisions, not `child.Width`/`child.Height` |
| 2 | **Line breaking ignores child margins on main axis** | HIGH | `CalculateFlexLines` | Add `childMarginMain` to the threshold: `lineMainSize + gapBefore + childMainSize + childMarginMain > available` |
| 3 | **Missing align-content overflow fallback** | HIGH | `LayoutWrappedFlex` | Add `fallbackAlignment()` for align-content when `crossFreeSpace < 0`: SpaceBetween/Stretch/SpaceAround/SpaceEvenly -> Start |
| 4 | **WrapReverse formula incorrectly adjusts for padding** | HIGH | `LayoutWrappedFlex` | Use full container dimension without subtracting padding, and do not add padding back. Formula: `containerCross - childPos - childSize` |
| 5 | **No concrete per-line flex resolution code** | HIGH | Task 3.6 | Specify that per-line flex resolution reuses Phase 2 algorithm on line's children subset |

### WARNINGS (should fix, not blocking)

| # | Issue | Severity | Location | Fix |
|---|-------|----------|----------|-----|
| 6 | Line breaking does not skip absolute children | MEDIUM | `CalculateFlexLines` | Add `if (child.Element.Position == Position.Absolute) continue;` |
| 7 | Cross-axis line size ignores child margins | MEDIUM | `CalculateFlexLines` | Use `childCrossSize + crossAxisMargin` for `lineCrossSize = Math.Max(...)` |
| 8 | Stretch does not describe child re-layout | MEDIUM | Align-content Stretch | After stretching line height, children with align-items: stretch need their cross dimension updated |
| 9 | LINQ `.Sum()` in hot path | MEDIUM | `LayoutWrappedFlex` | Replace with `foreach` loop per AGENTS.md convention |
| 10 | WrapReverse does not skip absolute children | MEDIUM | `LayoutWrappedFlex` | Add position check before flipping |
| 11 | MaxFlexLines off-by-one: `>=` should be `>` | LOW | `CalculateFlexLines` | Change `lines.Count >= _limits.MaxFlexLines` to `lines.Count > _limits.MaxFlexLines` |
| 12 | Gap-to-axis mapping not documented | LOW | Task 3.1 | Add explicit mapping: row direction -> columnGap=main, rowGap=cross |
| 13 | Missing test cases for margin-induced wrapping | LOW | Task 3.2 | Add tests from Yoga fixtures |

### APPROVED Items (correct as-is)

- Greedy line-breaking approach -- matches Yoga
- Gap not applied before first item -- correct
- FlexLine as `readonly record struct` -- good design
- MaxFlexLines = 1000 default -- appropriate for the use case
- Per-line flex resolution concept -- correct (each line resolved independently)
- Gap fallback semantics (`gap` sets both `rowGap` and `columnGap`) -- matches CSS spec
- All 7 align-content values enumerated -- complete

---

## 9. Recommended Changes to IMPLEMENTATION-PLAN.md

### Task 3.5 CalculateFlexLines: Fix line-breaking input

```csharp
// Method signature should include LayoutContext for basis/minmax resolution:
private List<FlexLine> CalculateFlexLines(
    IReadOnlyList<LayoutNode> children,
    float availableMainSize,
    bool isColumn,
    float gap,
    LayoutContext context)  // ADD: needed for ResolveFlexBasis + ResolveMinMax

// Inside loop:
if (child.Element.Display == Display.None) continue;
if (child.Element.Position == Position.Absolute) continue;  // ADD

var childBasis = ResolveFlexBasis(child.Element, child, isColumn, context);
var (childMin, childMax) = ResolveMinMax(child.Element, isColumn, context);
var childMainSize = ClampSize(childBasis, childMin, childMax);

// ADD: include margins
var childMargin = PaddingParser.Parse(child.Element.Margin, ...).ClampNegatives();
var childMarginMain = isColumn ? childMargin.Vertical : childMargin.Horizontal;
var childMarginCross = isColumn ? childMargin.Horizontal : childMargin.Vertical;

var totalChildMain = childMainSize + childMarginMain;
var gapBefore = itemsInLine > 0 ? gap : 0f;

if (itemsInLine > 0 && lineMainSize + gapBefore + totalChildMain > availableMainSize)
{
    // Start new line
}

var childCrossSize = (isColumn ? child.Width : child.Height) + childMarginCross;
lineCrossSize = Math.Max(lineCrossSize, childCrossSize);
```

### Task 3.6 LayoutWrappedFlex: Add align-content overflow fallback

```csharp
// Before align-content switch:
var effectiveAlignContent = crossFreeSpace >= 0 ? flex.AlignContent : flex.AlignContent switch
{
    AlignContent.SpaceBetween => AlignContent.Start,
    AlignContent.Stretch => AlignContent.Start,
    AlignContent.SpaceAround => AlignContent.Start,
    AlignContent.SpaceEvenly => AlignContent.Start,
    _ => flex.AlignContent
};
```

### Task 3.6 WrapReverse: Fix formula

```csharp
if (flex.Wrap == FlexWrap.WrapReverse)
{
    var containerCrossSize = isColumn ? node.Width : node.Height;
    foreach (var child in node.Children)
    {
        if (child.Element.Position == Position.Absolute) continue;
        if (isColumn)
            child.X = containerCrossSize - child.X - child.Width;
        else
            child.Y = containerCrossSize - child.Y - child.Height;
    }
}
```

### Task 3.2: Add test cases

```
ComputeLayout_RowWrap_ChildMarginCausesWrap
ComputeLayout_RowWrap_MinWidthOverridesFlexBasis_AffectsLineBreak
ComputeLayout_RowWrap_AlignItemsFlexEnd_WithinLine
ComputeLayout_RowWrap_SingleLine_AlignContentHasNoEffect
```
