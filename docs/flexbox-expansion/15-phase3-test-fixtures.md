# Phase 3 Test Fixtures: Exact Numerical Expectations (Wrapping + Align-Content)

Reference: Yoga layout algorithm (`CalculateLayout.cpp` Steps 4-8, `FlexLine.cpp`), IMPLEMENTATION-PLAN.md Tasks 3.2-3.3, yoga-c-researcher review (`12-phase3-algorithm-review.md`).

---

## Key Formulas

### Line Breaking (Greedy)

```
for each child (skip display:none):
  childMainSize = ClampSize(ResolveFlexBasis(child), min, max) + childMarginMain
  gapBefore = (itemsInLine > 0) ? mainGap : 0
  if itemsInLine > 0 AND lineMainSize + gapBefore + childMainSize > availableMainSize:
    finalize current line
    start new line with this child
  else:
    lineMainSize += gapBefore + childMainSize
    lineCrossSize = Max(lineCrossSize, childCrossSize + childMarginCross)
    itemsInLine++
```

**NOTE** (yoga-c-researcher finding HIGH #1): Line breaking must use `flexBasisWithMinAndMaxConstraints` (clamped flex basis), NOT `child.Width`/`child.Height`. For items with explicit `flex-basis`, these may differ.

**NOTE** (yoga-c-researcher finding HIGH #2): Line breaking must include child margins on main axis. A child with `margin: 20px` and `width: 45px` consumes 85px (45 + 20 + 20).

### Align-Content Distribution

```
totalLinesSize = sum of all line CrossAxisSizes
totalCrossGaps = crossGap * (lineCount - 1)
crossFreeSpace = availableCrossSize - totalLinesSize - totalCrossGaps

Start:       leadingCrossDim = 0; betweenCrossDim = 0
Center:      leadingCrossDim = crossFreeSpace / 2; betweenCrossDim = 0
End:         leadingCrossDim = crossFreeSpace; betweenCrossDim = 0
Stretch:     extraPerLine = crossFreeSpace / lineCount (added to each line's height)
SpaceBetween: betweenCrossDim = crossFreeSpace / (lineCount - 1)  [if lineCount > 1]
SpaceAround: leadingCrossDim = crossFreeSpace / (2 * lineCount); betweenCrossDim = leadingCrossDim * 2
SpaceEvenly: leadingCrossDim = crossFreeSpace / (lineCount + 1); betweenCrossDim = leadingCrossDim
```

### Align-Content Overflow Fallback

**NOTE** (yoga-c-researcher finding HIGH #3): When `crossFreeSpace < 0`:
- SpaceBetween -> Start
- Stretch -> Start
- SpaceAround -> Start
- SpaceEvenly -> Start
- Center, End remain unchanged

### WrapReverse

```
For each non-absolute child:
  newCrossPos = containerCrossDimension - childCrossPos - childCrossSize
```

**NOTE** (yoga-c-researcher finding HIGH #4): Use FULL container cross dimension (including padding), NOT `container - padding`. Positions already include padding offsets, so the flip is symmetric over the full container size.

### Gap-to-Axis Mapping

```
direction: row    -> mainGap = columnGap ?? gap, crossGap = rowGap ?? gap
direction: column -> mainGap = rowGap ?? gap,    crossGap = columnGap ?? gap
```

---

## 2. Wrap Tests (Task 3.2) -- 10 tests

File: `tests/FlexRender.Tests/Layout/LayoutEngineWrapTests.cs`

### Test 1: ComputeLayout_RowWrap_ChildrenExceedWidth_WrapsToNewLine

| Parameter | Value |
|-----------|-------|
| Container | Row, W=300, H=auto, Wrap=Wrap, AlignContent=Start (default) |
| Children  | 5 TextElements, each W="80", H="40" |

**Line Breaking Calculation** (mainGap=0, no margins):
- Line 1: child[0]=80, child[1]=80+80=160, child[2]=160+80=240 <= 300 (fits). child[3]: 240+80=320 > 300 -> break.
- Line 1: items 0-2, mainAxisSize=240, crossAxisSize=40
- Line 2: child[3]=80, child[4]=80+80=160 <= 300 (fits).
- Line 2: items 3-4, mainAxisSize=160, crossAxisSize=40

**Container auto-height**: totalLinesSize = 40 + 40 = 80. Container H = 80.

**Align-content=Start**: lines at cross-axis start. Line 1 starts at Y=0, Line 2 starts at Y=40.

**Justify-content=Start (default)**: items at main-axis start.

| Child | X | Y | Width | Height |
|-------|---|---|-------|--------|
| child[0] | 0 | 0 | 80 | 40 |
| child[1] | 80 | 0 | 80 | 40 |
| child[2] | 160 | 0 | 80 | 40 |
| child[3] | 0 | 40 | 80 | 40 |
| child[4] | 80 | 40 | 80 | 40 |

**Container dimensions**: W=300, H=80.

---

### Test 2: ComputeLayout_RowWrap_AllChildrenFit_SingleLine

| Parameter | Value |
|-----------|-------|
| Container | Row, W=300, H=100, Wrap=Wrap |
| Children  | 3 TextElements, each W="80", H="40" |

**Line Breaking**: 80+80+80=240 <= 300. All children fit on one line.
- Line 1: items 0-2, mainAxisSize=240, crossAxisSize=40

**Align-content=Start**: Line 1 at Y=0.

| Child | X | Y | Width | Height |
|-------|---|---|-------|--------|
| child[0] | 0 | 0 | 80 | 40 |
| child[1] | 80 | 0 | 80 | 40 |
| child[2] | 160 | 0 | 80 | 40 |

---

### Test 3: ComputeLayout_RowWrap_ThreeLines_CalculatesHeightCorrectly

| Parameter | Value |
|-----------|-------|
| Container | Row, W=200, H=auto, Wrap=Wrap |
| Children  | 7 TextElements, each W="80", H="30" |

**Line Breaking** (mainGap=0):
- Line 1: 80+80=160 <= 200 (fits). 160+80=240 > 200 -> break.
- Line 1: items 0-1, mainAxisSize=160, crossAxisSize=30
- Line 2: 80+80=160 <= 200 (fits). 160+80=240 > 200 -> break.
- Line 2: items 2-3, mainAxisSize=160, crossAxisSize=30
- Line 3: 80+80=160 <= 200 (fits). 160+80=240 > 200 -> break.
- Line 3: items 4-5, mainAxisSize=160, crossAxisSize=30
- Line 4 (remaining): item 6, mainAxisSize=80, crossAxisSize=30

Wait -- 7 children. Let me recalculate:
- Line 1: child[0]=80, child[1]=80+80=160 <= 200. child[2]: 160+80=240 > 200 -> break.
- Line 1: items 0-1 (2 items), mainAxisSize=160, crossAxisSize=30
- Line 2: child[2]=80, child[3]=80+80=160 <= 200. child[4]: 160+80=240 > 200 -> break.
- Line 2: items 2-3 (2 items), mainAxisSize=160, crossAxisSize=30
- Line 3: child[4]=80, child[5]=80+80=160 <= 200. child[6]: 160+80=240 > 200 -> break.
- Line 3: items 4-5 (2 items), mainAxisSize=160, crossAxisSize=30
- Line 4: child[6]=80.
- Line 4: item 6 (1 item), mainAxisSize=80, crossAxisSize=30

This gives 4 lines which doesn't match the "ThreeLines" test name. Let me adjust: use 6 children instead.

**Revised**: 6 TextElements, each W="80", H="30"
- Line 1: items 0-1, mainAxisSize=160, crossAxisSize=30
- Line 2: items 2-3, mainAxisSize=160, crossAxisSize=30
- Line 3: items 4-5, mainAxisSize=160, crossAxisSize=30

**Container auto-height**: 30 + 30 + 30 = 90. Container H = 90.

| Child | X | Y | Width | Height |
|-------|---|---|-------|--------|
| child[0] | 0 | 0 | 80 | 30 |
| child[1] | 80 | 0 | 80 | 30 |
| child[2] | 0 | 30 | 80 | 30 |
| child[3] | 80 | 30 | 80 | 30 |
| child[4] | 0 | 60 | 80 | 30 |
| child[5] | 80 | 60 | 80 | 30 |

**Container dimensions**: W=200, H=90.

---

### Test 4: ComputeLayout_RowWrap_WithGap_GapAppliedBetweenLinesAndItems

| Parameter | Value |
|-----------|-------|
| Container | Row, W=200, H=auto, Wrap=Wrap, Gap="10" (both row and column gap = 10) |
| Children  | 4 TextElements, each W="80", H="40" |

**Gap mapping** (row direction): mainGap = columnGap = 10, crossGap = rowGap = 10.

**Line Breaking** (mainGap=10):
- Line 1: child[0]=80. child[1]: 80+10+80=170 <= 200 (fits). child[2]: 170+10+80=260 > 200 -> break.
- Line 1: items 0-1, mainAxisSize=170 (80+10+80), crossAxisSize=40
- Line 2: child[2]=80. child[3]: 80+10+80=170 <= 200 (fits).
- Line 2: items 2-3, mainAxisSize=170, crossAxisSize=40

**Container auto-height**: 40 + 10 (crossGap) + 40 = 90. Container H = 90.

**Justify-content=Start (default)**: items placed from X=0 with mainGap between them.

| Child | X | Y | Width | Height |
|-------|---|---|-------|--------|
| child[0] | 0 | 0 | 80 | 40 |
| child[1] | 90 | 0 | 80 | 40 |
| child[2] | 0 | 50 | 80 | 40 |
| child[3] | 90 | 50 | 80 | 40 |

**Container dimensions**: W=200, H=90.

---

### Test 5: ComputeLayout_RowWrapReverse_WrapsInReverse

| Parameter | Value |
|-----------|-------|
| Container | Row, W=200, H=120, Wrap=WrapReverse |
| Children  | 4 TextElements, each W="80", H="40" |

**Line Breaking** (same as Test 4 without gap):
- Line 1: child[0]=80, child[1]=80+80=160 <= 200 (fits). child[2]: 160+80=240 > 200 -> break.
- Line 1: items 0-1, mainAxisSize=160, crossAxisSize=40
- Line 2: child[2]=80, child[3]=80+80=160 <= 200 (fits).
- Line 2: items 2-3, mainAxisSize=160, crossAxisSize=40

**Before WrapReverse** (align-content=Start positions):
- Line 1 at Y=0: child[0]=(0,0,80,40), child[1]=(80,0,80,40)
- Line 2 at Y=40: child[2]=(0,40,80,40), child[3]=(80,40,80,40)

**WrapReverse formula**: `newY = containerHeight - oldY - childHeight` (full container dimension = 120)
- child[0]: newY = 120 - 0 - 40 = 80
- child[1]: newY = 120 - 0 - 40 = 80
- child[2]: newY = 120 - 40 - 40 = 40
- child[3]: newY = 120 - 40 - 40 = 40

| Child | X | Y | Width | Height |
|-------|---|---|-------|--------|
| child[0] | 0 | 80 | 80 | 40 |
| child[1] | 80 | 80 | 80 | 40 |
| child[2] | 0 | 40 | 80 | 40 |
| child[3] | 80 | 40 | 80 | 40 |

**Verification**: Lines are reversed on cross axis. Line 1 (originally at top) now at bottom (Y=80). Line 2 (originally Y=40) now at Y=40. The container has 40px of unused space at the top (Y=0 to Y=40), which is correct -- align-content=Start in WrapReverse pushes lines toward the bottom.

---

### Test 6: ComputeLayout_ColumnWrap_ChildrenExceedHeight_WrapsToNewColumn

| Parameter | Value |
|-----------|-------|
| Container | Column, W=auto, H=200, Wrap=Wrap |
| Children  | 4 TextElements, each W="60", H="80" |

**Line Breaking** (column direction: main=vertical, cross=horizontal, mainGap=0):
- Line 1: child[0]=80, child[1]=80+80=160 <= 200 (fits). child[2]: 160+80=240 > 200 -> break.
- Line 1: items 0-1, mainAxisSize=160, crossAxisSize=60
- Line 2: child[2]=80, child[3]=80+80=160 <= 200 (fits).
- Line 2: items 2-3, mainAxisSize=160, crossAxisSize=60

**Container auto-width**: 60 + 60 = 120. Container W = 120.

**Positions** (main axis = Y, cross axis = X):
- Line 1 at X=0: child[0]=(0,0,60,80), child[1]=(0,80,60,80)
- Line 2 at X=60: child[2]=(60,0,60,80), child[3]=(60,80,60,80)

| Child | X | Y | Width | Height |
|-------|---|---|-------|--------|
| child[0] | 0 | 0 | 60 | 80 |
| child[1] | 0 | 80 | 60 | 80 |
| child[2] | 60 | 0 | 60 | 80 |
| child[3] | 60 | 80 | 60 | 80 |

**Container dimensions**: W=120, H=200.

---

### Test 7: ComputeLayout_ColumnWrapReverse_WrapsInReverse

| Parameter | Value |
|-----------|-------|
| Container | Column, W=200, H=200, Wrap=WrapReverse |
| Children  | 4 TextElements, each W="60", H="80" |

**Line Breaking** (same as Test 6):
- Line 1: items 0-1, crossAxisSize=60
- Line 2: items 2-3, crossAxisSize=60

**Before WrapReverse** (align-content=Start positions):
- Line 1 at X=0: child[0]=(0,0,60,80), child[1]=(0,80,60,80)
- Line 2 at X=60: child[2]=(60,0,60,80), child[3]=(60,80,60,80)

**WrapReverse formula** (column: cross=X): `newX = containerWidth - oldX - childWidth` (containerWidth = 200)
- child[0]: newX = 200 - 0 - 60 = 140
- child[1]: newX = 200 - 0 - 60 = 140
- child[2]: newX = 200 - 60 - 60 = 80
- child[3]: newX = 200 - 60 - 60 = 80

| Child | X | Y | Width | Height |
|-------|---|---|-------|--------|
| child[0] | 140 | 0 | 60 | 80 |
| child[1] | 140 | 80 | 60 | 80 |
| child[2] | 80 | 0 | 60 | 80 |
| child[3] | 80 | 80 | 60 | 80 |

---

### Test 8: ComputeLayout_RowWrap_DifferentHeightChildren_LineHeightIsMaxChild

| Parameter | Value |
|-----------|-------|
| Container | Row, W=200, H=auto, Wrap=Wrap |
| Children  | child[0]: W="80", H="30"; child[1]: W="80", H="60"; child[2]: W="80", H="20"; child[3]: W="80", H="50" |

**Line Breaking** (mainGap=0):
- Line 1: 80+80=160 <= 200. 160+80=240 > 200 -> break.
- Line 1: items 0-1, mainAxisSize=160, crossAxisSize=Max(30,60)=60
- Line 2: 80+80=160 <= 200.
- Line 2: items 2-3, mainAxisSize=160, crossAxisSize=Max(20,50)=50

**Container auto-height**: 60 + 50 = 110.

**Positions** (align-items=Stretch default, but children have explicit heights so they keep them; align at start within line):

| Child | X | Y | Width | Height |
|-------|---|---|-------|--------|
| child[0] | 0 | 0 | 80 | 30 |
| child[1] | 80 | 0 | 80 | 60 |
| child[2] | 0 | 60 | 80 | 20 |
| child[3] | 80 | 60 | 80 | 50 |

**Note**: With default align-items=Stretch, children without explicit height would stretch to line height. Children WITH explicit height keep their height. In FlexRender, TextElements always have explicit height (intrinsic), so they keep their specified height and are positioned at the start of the cross axis within their line.

**Container dimensions**: W=200, H=110.

---

### Test 9: ComputeLayout_RowWrap_SingleChild_NoWrap

| Parameter | Value |
|-----------|-------|
| Container | Row, W=200, H=100, Wrap=Wrap |
| Child     | TextElement, W="80", H="40" |

**Line Breaking**: Single child, 80 <= 200. One line.
- Line 1: item 0, mainAxisSize=80, crossAxisSize=40

| Child | X | Y | Width | Height |
|-------|---|---|-------|--------|
| child[0] | 0 | 0 | 80 | 40 |

---

### Test 10: ComputeLayout_RowWrap_EmptyChildren_NoWrap

| Parameter | Value |
|-----------|-------|
| Container | Row, W=200, H=100, Wrap=Wrap |
| Children  | (none) |

**Expected**: No lines created. Container dimensions: W=200, H=100. No children to assert.

---

## 3. Align-Content Tests (Task 3.3) -- 7 tests

File: `tests/FlexRender.Tests/Layout/LayoutEngineAlignContentTests.cs`

### Common Setup for Tests 11-17

All align-content tests use the same container and children to produce 2 lines:

```
Container: Row, W=140, H=120, Wrap=Wrap
Children: 5 TextElements, each W="50", H="10"
```

**Line Breaking** (mainGap=0):
- child[0]=50. child[1]: 50+50=100 <= 140. child[2]: 100+50=150 > 140 -> break.
- Line 1: items 0-1 (2 items), mainAxisSize=100, crossAxisSize=10
- child[2]=50. child[3]: 50+50=100 <= 140. child[4]: 100+50=150 > 140 -> break.
- Line 2: items 2-3 (2 items), mainAxisSize=100, crossAxisSize=10
- Line 3: item 4 (1 item), mainAxisSize=50, crossAxisSize=10

Wait -- that gives 3 lines. Let me re-examine: the Yoga fixture `align_content_*_wrap` uses W=140 with 5 children of W=50. 50+50=100 <= 140, 100+50=150 > 140. So yes, 3 lines: [0,1], [2,3], [4].

**Revised Common Setup** (3 lines):

```
Container: Row, W=140, H=120, Wrap=Wrap
Children: 5 TextElements, each W="50", H="10"
Line 1: items 0-1, crossAxisSize=10
Line 2: items 2-3, crossAxisSize=10
Line 3: item 4, crossAxisSize=10
lineCount = 3
totalLinesSize = 10 + 10 + 10 = 30
totalCrossGaps = 0 (no cross gap specified)
availableCrossSize = 120 (container H - padding = 120 - 0)
crossFreeSpace = 120 - 30 - 0 = 90
```

### Main-axis positions (same for all align-content tests):

Justify-content=Start (default). No main-axis gap. Within each line, items start at X=0.

```
Line 1: child[0] X=0, child[1] X=50
Line 2: child[2] X=0, child[3] X=50
Line 3: child[4] X=0
```

---

### Test 11: ComputeLayout_RowWrap_AlignContentStart_LinesPackedAtStart

AlignContent=Start: leadingCrossDim=0, betweenCrossDim=0.

**Y positions**: Line 1 at Y=0, Line 2 at Y=0+10=10, Line 3 at Y=10+10=20.

| Child | X | Y | Width | Height |
|-------|---|---|-------|--------|
| child[0] | 0 | 0 | 50 | 10 |
| child[1] | 50 | 0 | 50 | 10 |
| child[2] | 0 | 10 | 50 | 10 |
| child[3] | 50 | 10 | 50 | 10 |
| child[4] | 0 | 20 | 50 | 10 |

---

### Test 12: ComputeLayout_RowWrap_AlignContentCenter_LinesCentered

AlignContent=Center: leadingCrossDim = crossFreeSpace / 2 = 90 / 2 = 45.

**Y positions**: Line 1 at Y=45, Line 2 at Y=45+10=55, Line 3 at Y=55+10=65.

| Child | X | Y | Width | Height |
|-------|---|---|-------|--------|
| child[0] | 0 | 45 | 50 | 10 |
| child[1] | 50 | 45 | 50 | 10 |
| child[2] | 0 | 55 | 50 | 10 |
| child[3] | 50 | 55 | 50 | 10 |
| child[4] | 0 | 65 | 50 | 10 |

---

### Test 13: ComputeLayout_RowWrap_AlignContentEnd_LinesPackedAtEnd

AlignContent=End: leadingCrossDim = crossFreeSpace = 90.

**Y positions**: Line 1 at Y=90, Line 2 at Y=90+10=100, Line 3 at Y=100+10=110.

| Child | X | Y | Width | Height |
|-------|---|---|-------|--------|
| child[0] | 0 | 90 | 50 | 10 |
| child[1] | 50 | 90 | 50 | 10 |
| child[2] | 0 | 100 | 50 | 10 |
| child[3] | 50 | 100 | 50 | 10 |
| child[4] | 0 | 110 | 50 | 10 |

---

### Test 14: ComputeLayout_RowWrap_AlignContentStretch_LinesStretchToFill

AlignContent=Stretch: extraPerLine = crossFreeSpace / lineCount = 90 / 3 = 30.

Each line's new height = 10 + 30 = 40.

**Y positions**: Line 1 at Y=0, Line 2 at Y=0+40=40, Line 3 at Y=40+40=80.

Children within stretched lines: children with explicit height keep their height (H=10). Children without explicit height would stretch to line height (40). Since our TextElements have explicit H="10", they keep H=10 but are positioned at Y=lineStart (align-items=Stretch means they would stretch if they didn't have explicit heights; with explicit heights, they stay at line start).

| Child | X | Y | Width | Height |
|-------|---|---|-------|--------|
| child[0] | 0 | 0 | 50 | 10 |
| child[1] | 50 | 0 | 50 | 10 |
| child[2] | 0 | 40 | 50 | 10 |
| child[3] | 50 | 40 | 50 | 10 |
| child[4] | 0 | 80 | 50 | 10 |

**Note**: If children did NOT have explicit height, they would stretch to H=40. The test should verify the line spacing (Y positions), which is the primary purpose of align-content=Stretch.

**Alternative for verification**: Use children without explicit height and verify they stretch to line height. But TextElements always have intrinsic height. To test Stretch properly, you might need FlexElement children. For this test, verify the Y-position spacing (40px apart).

---

### Test 15: ComputeLayout_RowWrap_AlignContentSpaceBetween_SpaceDistributedBetweenLines

AlignContent=SpaceBetween: lineCount=3 > 1, so betweenCrossDim = crossFreeSpace / (lineCount - 1) = 90 / 2 = 45. leadingCrossDim=0.

**Y positions**: Line 1 at Y=0, Line 2 at Y=0+10+45=55, Line 3 at Y=55+10+45=110.

| Child | X | Y | Width | Height |
|-------|---|---|-------|--------|
| child[0] | 0 | 0 | 50 | 10 |
| child[1] | 50 | 0 | 50 | 10 |
| child[2] | 0 | 55 | 50 | 10 |
| child[3] | 50 | 55 | 50 | 10 |
| child[4] | 0 | 110 | 50 | 10 |

---

### Test 16: ComputeLayout_RowWrap_AlignContentSpaceAround_SpaceDistributedAroundLines

AlignContent=SpaceAround:
- leadingCrossDim = crossFreeSpace / (2 * lineCount) = 90 / 6 = 15
- betweenCrossDim = leadingCrossDim * 2 = 30 (Yoga: `leadPerLine = remaining / lineCount = 30`)

**Y positions** (Yoga Step 8 iteration):
- currentLead starts at 15 (leadingCrossDim)
- Line 1: Y = 15. After line 1: currentLead = 15 + 30 + 10 = 55 (leadPerLine + lineHeight)
- Line 2: Y = 55. After line 2: currentLead = 55 + 30 + 10 = 95
- Line 3: Y = 95. After line 3: currentLead = 95 + 30 + 10 = 135

Wait, I need to re-check the Yoga iteration pattern. From the Yoga reference code:

```cpp
for (size_t i = 0; i < lineCount; i++) {
    currentLead += i != 0 ? crossAxisGap : 0;
    // ... position children at currentLead
    currentLead = currentLead + leadPerLine + lineHeight;
}
```

Actually looking more carefully at the Yoga code structure:

```
Initial: currentLead = leadingCrossDim (for SpaceAround: = remaining / (2*lineCount) = 15)
Line 0: gap = 0 (i==0). position children at currentLead=15. currentLead += leadPerLine + lineHeight = 15 + 30 + 10 = 55.
Line 1: gap = 0 (crossGap=0). position children at currentLead=55. currentLead += 30 + 10 = 95.
Line 2: gap = 0. position children at currentLead=95. currentLead += 30 + 10 = 135.
```

Wait -- that doesn't look right either. Let me reconsider. The Yoga code for SpaceAround:
- `currentLead += remaining / (2 * lineCount)` -- initial offset = 15
- `leadPerLine = remaining / lineCount` -- per-line spacing = 30

Then the iteration:
```
Line 0: children at currentLead=15. currentLead += leadPerLine(=30) + lineHeight(=10) = 55
Line 1: children at currentLead=55. currentLead += 30 + 10 = 95
Line 2: children at currentLead=95.
```

This gives spacing: [15] line(10) [30] line(10) [30] line(10) [15] = 15+10+30+10+30+10+15 = 120. Correct!

| Child | X | Y | Width | Height |
|-------|---|---|-------|--------|
| child[0] | 0 | 15 | 50 | 10 |
| child[1] | 50 | 15 | 50 | 10 |
| child[2] | 0 | 55 | 50 | 10 |
| child[3] | 50 | 55 | 50 | 10 |
| child[4] | 0 | 95 | 50 | 10 |

---

### Test 17: ComputeLayout_RowWrap_AlignContentSpaceEvenly_EvenDistribution

AlignContent=SpaceEvenly:
- leadingCrossDim = crossFreeSpace / (lineCount + 1) = 90 / 4 = 22.5
- betweenCrossDim = leadingCrossDim = 22.5

**Y positions** (Yoga iteration):
```
Initial: currentLead = 22.5
Line 0: children at Y=22.5. currentLead = 22.5 + 22.5 + 10 = 55.
Line 1: children at Y=55. currentLead = 55 + 22.5 + 10 = 87.5.
Line 2: children at Y=87.5.
```

Verification: [22.5] line(10) [22.5] line(10) [22.5] line(10) [22.5] = 22.5+10+22.5+10+22.5+10+22.5 = 120. Correct!

| Child | X | Y | Width | Height |
|-------|---|---|-------|--------|
| child[0] | 0 | 22.5 | 50 | 10 |
| child[1] | 50 | 22.5 | 50 | 10 |
| child[2] | 0 | 55 | 50 | 10 |
| child[3] | 50 | 55 | 50 | 10 |
| child[4] | 0 | 87.5 | 50 | 10 |

**Note**: Float precision. In C# tests, use `Assert.Equal(22.5f, ...)` or `Assert.InRange(childNode.Y, 22.4f, 22.6f)`.

---

## 4. yoga-c-researcher Findings Integration

### Edge Case Tests (recommended additions from yoga-c-researcher review)

The following tests cover critical findings from `12-phase3-algorithm-review.md`:

#### Test 18 (Edge Case): RowWrap_ChildMarginCausesWrap

**Source**: yoga-c-researcher finding HIGH #2 (line breaking must include margins).
**Yoga fixture**: `wrap_nodes_with_content_sizing_overflowing_margin`

| Parameter | Value |
|-----------|-------|
| Container | Row, W=85, H=auto, Wrap=Wrap |
| Children  | child[0]: W="40", H="40", Margin=0; child[1]: W="40", H="40", Margin="5" (uniform) |

**Line Breaking** (mainGap=0, with margins):
- child[0]: mainSize=40 (no margin). lineMainSize=40.
- child[1]: mainSize=40+5+5=50 (includes horizontal margins). 40+50=90 > 85 -> break.
- Line 1: item 0, mainAxisSize=40, crossAxisSize=40
- Line 2: item 1, mainAxisSize=50, crossAxisSize=40+5+5=50 (includes vertical margins for cross-axis)

Without margin consideration (bug): 40+40=80 <= 85, both on one line. With margins: 40+50=90 > 85, wraps correctly.

**Positions**:
- Line 1: child[0] at (0, 0), size 40x40
- Line 2: child[1] at (5, 40+5=45), size 40x40 (margin-left=5 offsets X, margin-top=5 offsets Y from line start which is Y=40 for crossAxisSize of line 1)

Wait -- the line cross-axis size calculation. Line 1 crossAxisSize = Max child cross dim including margins = 40 (child[0] has no margin). Line 2 starts at Y=40 (line 1 cross size). Within line 2, child[1] has margin-top=5, so its content starts at Y=40+5=45.

| Child | X | Y | Width | Height |
|-------|---|---|-------|--------|
| child[0] | 0 | 0 | 40 | 40 |
| child[1] | 5 | 45 | 40 | 40 |

**Container dimensions**: W=85, H=40+50=90 (line1 cross 40 + line2 cross with margins 50).

**Key assertion**: child[1] wraps to new line despite content width (40+40=80) fitting in container (85). Margin (10 horizontal) causes overflow.

---

#### Test 19 (Edge Case): AlignContent_OverflowFallback_SpaceBetweenBecomesStart

**Source**: yoga-c-researcher finding HIGH #3 (align-content overflow fallback).

| Parameter | Value |
|-----------|-------|
| Container | Row, W=100, H=40, Wrap=Wrap, AlignContent=SpaceBetween |
| Children  | child[0]: W="60", H="30"; child[1]: W="60", H="30" |

**Line Breaking**:
- child[0]=60. child[1]: 60+60=120 > 100 -> break.
- Line 1: item 0, crossAxisSize=30
- Line 2: item 1, crossAxisSize=30

**Cross-axis free space**:
- totalLinesSize = 30 + 30 = 60
- availableCrossSize = 40
- crossFreeSpace = 40 - 60 = -20 (NEGATIVE)

**Overflow fallback**: SpaceBetween -> Start (because crossFreeSpace < 0).

**Y positions** (Start behavior): Line 1 at Y=0, Line 2 at Y=30.

| Child | X | Y | Width | Height |
|-------|---|---|-------|--------|
| child[0] | 0 | 0 | 60 | 30 |
| child[1] | 0 | 30 | 60 | 30 |

**Key assertion**: Even though AlignContent=SpaceBetween, the negative free space triggers fallback to Start. Lines are packed at top, overflowing the container (total height 60 > container 40).

---

#### Test 20 (Edge Case): WrapReverse_UsesFullContainerDimension

**Source**: yoga-c-researcher finding HIGH #4 (WrapReverse formula).

| Parameter | Value |
|-----------|-------|
| Container | Row, W=200, H=100, Wrap=WrapReverse, Padding="10" |
| Children  | child[0]: W="80", H="30"; child[1]: W="80", H="30"; child[2]: W="80", H="30" |

**Available main size** (accounting for padding): 200 - 10 - 10 = 180.

**Line Breaking** (mainGap=0, available=180):
- child[0]=80, child[1]=80+80=160 <= 180. child[2]: 160+80=240 > 180 -> break.
- Line 1: items 0-1, crossAxisSize=30
- Line 2: item 2, crossAxisSize=30

**Before WrapReverse** (positions within padding area, align-content=Start):
- Available cross size = 100 - 10 - 10 = 80.
- Line 1 at Y=10 (padding-top): child[0]=(10, 10, 80, 30), child[1]=(90, 10, 80, 30)
- Line 2 at Y=40 (10+30): child[2]=(10, 40, 80, 30)

**WrapReverse formula**: `newY = containerHeight - oldY - childHeight` (containerHeight = 100, FULL dimension)
- child[0]: newY = 100 - 10 - 30 = 60
- child[1]: newY = 100 - 10 - 30 = 60
- child[2]: newY = 100 - 40 - 30 = 30

| Child | X | Y | Width | Height |
|-------|---|---|-------|--------|
| child[0] | 10 | 60 | 80 | 30 |
| child[1] | 90 | 60 | 80 | 30 |
| child[2] | 10 | 30 | 80 | 30 |

**Key assertion**: The flip uses the FULL container height (100), not the inner height (80). This ensures children stay within the padding area symmetrically. If the formula incorrectly subtracted padding (used 80 instead of 100), child[0] would be at Y=80-10-30=40 (wrong).

---

## 5. Yoga Cross-Reference Table

| FlexRender Test | Yoga Fixture | Key Aspect |
|-----------------|-------------|------------|
| Test 1: RowWrap_ChildrenExceedWidth | `wrap_row` | Basic row wrapping, 4 items W=30 in W=100 |
| Test 2: AllChildrenFit_SingleLine | `flex_wrap_align_stretch_fits_one_row` | Single-line wrap |
| Test 3: ThreeLines | (derived from wrap_row pattern) | Multi-line height calculation |
| Test 4: WithGap | (no direct Yoga fixture, CSS gap spec) | Gap between items and lines |
| Test 5: RowWrapReverse | `wrap_reverse_row_align_content_flex_start` | WrapReverse formula |
| Test 6: ColumnWrap | `wrap_column` | Column direction wrapping |
| Test 7: ColumnWrapReverse | `wrap_reverse_column_fixed_size` | Column WrapReverse |
| Test 8: DifferentHeightChildren | `wrap_row_align_items_flex_end` | Line cross-axis sizing |
| Test 9: SingleChild | (degenerate case) | Edge: no wrapping needed |
| Test 10: EmptyChildren | (degenerate case) | Edge: no children |
| Test 11: AlignContentStart | `align_content_flex_start_wrap` | Lines packed at start |
| Test 12: AlignContentCenter | `align_content_center_wrap` | Lines centered |
| Test 13: AlignContentEnd | `align_content_flex_end_wrap` | Lines at end |
| Test 14: AlignContentStretch | `align_content_stretch_row` | Lines stretched |
| Test 15: AlignContentSpaceBetween | `align_content_space_between_wrap` | Space between lines |
| Test 16: AlignContentSpaceAround | `align_content_space_around_wrap` | Space around lines |
| Test 17: AlignContentSpaceEvenly | `align_content_space_evenly_wrap` | Even space distribution |
| Test 18: MarginCausesWrap | `wrap_nodes_with_content_sizing_overflowing_margin` | Margins in line breaking |
| Test 19: OverflowFallback | (align_content_*_wrapped_negative_space fixtures) | Negative space fallback |
| Test 20: WrapReverse_FullContainerDim | (derived from yoga-c finding) | Correct flip formula |

---

## 6. Summary

| Category | Tests | Key Coverage |
|----------|-------|-------------|
| Wrap (Task 3.2) | 10 tests (1-10) | Row wrap, column wrap, WrapReverse, gap, different heights, single child, empty |
| Align-Content (Task 3.3) | 7 tests (11-17) | Start, Center, End, Stretch, SpaceBetween, SpaceAround, SpaceEvenly |
| yoga-c-researcher Edge Cases | 3 tests (18-20) | Margin-induced wrap, overflow fallback, WrapReverse formula |
| **Total** | **20 tests** | |

### Quick Reference: Expected Values Table

| Test | Container (WxH) | Lines | Key Y Values | Notes |
|------|-----------------|-------|-------------|-------|
| 1. RowWrap | 300xauto(80) | 3+2 | 0, 40 | Basic wrap |
| 2. SingleLine | 300x100 | 3 | 0 | No wrapping |
| 3. ThreeLines | 200xauto(90) | 2+2+2 | 0, 30, 60 | Height=3*30 |
| 4. WithGap | 200xauto(90) | 2+2 | 0, 50 | gap=10 both axes |
| 5. WrapReverse | 200x120 | 2+2 | 80, 40 | Lines flipped |
| 6. ColWrap | auto(120)x200 | 2+2 | X=0, X=60 | Column direction |
| 7. ColWrapReverse | 200x200 | 2+2 | X=140, X=80 | Column flipped |
| 8. DiffHeights | 200xauto(110) | 2+2 | 0, 60 | Line H = max child |
| 9. SingleChild | 200x100 | 1 | 0 | Degenerate |
| 10. Empty | 200x100 | 0 | N/A | No children |
| 11. AC-Start | 140x120 | 2+2+1 | 0, 10, 20 | Packed at start |
| 12. AC-Center | 140x120 | 2+2+1 | 45, 55, 65 | Centered |
| 13. AC-End | 140x120 | 2+2+1 | 90, 100, 110 | Packed at end |
| 14. AC-Stretch | 140x120 | 2+2+1 | 0, 40, 80 | +30px per line |
| 15. AC-SpaceBetween | 140x120 | 2+2+1 | 0, 55, 110 | 45px between |
| 16. AC-SpaceAround | 140x120 | 2+2+1 | 15, 55, 95 | 15/30/30/15 |
| 17. AC-SpaceEvenly | 140x120 | 2+2+1 | 22.5, 55, 87.5 | 22.5px slots |
| 18. MarginWrap | 85xauto(90) | 1+1 | 0, 45 | Margin causes break |
| 19. OverflowFallback | 100x40 | 1+1 | 0, 30 | SB->Start fallback |
| 20. WrapReverse+Padding | 200x100 | 2+1 | 60, 30 | Full dim flip |
