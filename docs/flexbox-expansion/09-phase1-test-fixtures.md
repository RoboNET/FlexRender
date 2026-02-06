# Phase 1 Test Fixtures: Exact Numerical Expectations

Reference: Yoga layout algorithm, existing FlexRender test patterns, IMPLEMENTATION-PLAN.md Tasks 1.1, 1.7, 1.9.

---

## 1. align-self Tests (Task 1.1) -- 8 tests

File: `tests/FlexRender.Tests/Layout/LayoutEngineAlignSelfTests.cs`

### Common Setup for Tests 1-5, 7

```
Container: Row, Width=300, Height=100, AlignItems=Stretch (default for tests 1-4), Gap=0
Child: TextElement, Content="test", Width="100", Height="30"
Cross axis = Y (vertical), crossAxisSize = 100
```

### Test 1: ComputeLayout_RowAlignItemsStretch_ChildAlignSelfStart_ChildNotStretched

| Parameter | Value |
|-----------|-------|
| Container | Row, W=300, H=100, Align=Stretch |
| Child | TextElement, W="100", H="30", AlignSelf=Start |

**Calculation**: align-self=Start overrides parent Stretch. Child keeps its explicit height. Y = 0 (start of cross axis).

| Property | Expected |
|----------|----------|
| childNode.X | 0 |
| childNode.Y | 0 |
| childNode.Width | 100 |
| childNode.Height | 30 |

### Test 2: ComputeLayout_RowAlignItemsStretch_ChildAlignSelfCenter_ChildCentered

| Parameter | Value |
|-----------|-------|
| Container | Row, W=300, H=100, Align=Stretch |
| Child | TextElement, W="100", H="30", AlignSelf=Center |

**Calculation**: align-self=Center overrides parent Stretch. Y = (crossAxisSize - childHeight) / 2 = (100 - 30) / 2 = 35.

| Property | Expected |
|----------|----------|
| childNode.X | 0 |
| childNode.Y | 35 |
| childNode.Width | 100 |
| childNode.Height | 30 |

### Test 3: ComputeLayout_RowAlignItemsStretch_ChildAlignSelfEnd_ChildAtEnd

| Parameter | Value |
|-----------|-------|
| Container | Row, W=300, H=100, Align=Stretch |
| Child | TextElement, W="100", H="30", AlignSelf=End |

**Calculation**: align-self=End overrides parent Stretch. Y = crossAxisSize - childHeight = 100 - 30 = 70.

| Property | Expected |
|----------|----------|
| childNode.X | 0 |
| childNode.Y | 70 |
| childNode.Width | 100 |
| childNode.Height | 30 |

### Test 4: ComputeLayout_RowAlignItemsStretch_ChildAlignSelfStretch_ChildStretched

| Parameter | Value |
|-----------|-------|
| Container | Row, W=300, H=100, Align=Stretch |
| Child | TextElement, W="100", **no explicit Height**, AlignSelf=Stretch |

**Calculation**: align-self=Stretch matches parent Stretch. Child without explicit height stretches to crossAxisSize = 100.

| Property | Expected |
|----------|----------|
| childNode.X | 0 |
| childNode.Y | 0 |
| childNode.Width | 100 |
| childNode.Height | 100 |

**Note**: Child must NOT have explicit Height for stretch to apply. If Height="30" is set, stretch is a no-op per existing behavior.

### Test 5: ComputeLayout_RowAlignItemsCenter_ChildAlignSelfEnd_OverridesParent

| Parameter | Value |
|-----------|-------|
| Container | Row, W=300, H=100, Align=**Center** |
| Child | TextElement, W="100", H="30", AlignSelf=**End** |

**Calculation**: align-self=End overrides parent Center. Y = crossAxisSize - childHeight = 100 - 30 = 70.
Without align-self, Center would give Y = (100 - 30) / 2 = 35.

| Property | Expected |
|----------|----------|
| childNode.X | 0 |
| childNode.Y | 70 |
| childNode.Width | 100 |
| childNode.Height | 30 |

### Test 6: ComputeLayout_ColumnAlignItemsStretch_ChildAlignSelfCenter_ChildCentered

| Parameter | Value |
|-----------|-------|
| Container | **Column**, W=300 (from canvas), Align=Stretch |
| Child | TextElement, Content="Short", Size="14", W="100", AlignSelf=Center |

**Calculation**: Column direction -- cross axis = X (horizontal). crossAxisSize = 300 (container width).
align-self=Center: X = (crossAxisSize - childWidth) / 2 = (300 - 100) / 2 = 100.

| Property | Expected |
|----------|----------|
| childNode.X | 100 |
| childNode.Y | 0 |
| childNode.Width | 100 |

### Test 7: ComputeLayout_AlignSelfAuto_UsesParentAlignItems

| Parameter | Value |
|-----------|-------|
| Container | Row, W=300, H=100, Align=**Center** |
| Child | TextElement, W="100", H="30", AlignSelf=**Auto** |

**Calculation**: align-self=Auto falls back to parent's align-items=Center. Same result as plain AlignItems.Center.
Y = (100 - 30) / 2 = 35.

| Property | Expected |
|----------|----------|
| childNode.X | 0 |
| childNode.Y | 35 |
| childNode.Width | 100 |
| childNode.Height | 30 |

### Test 8: ComputeLayout_MultipleChildren_DifferentAlignSelf_EachPositionedCorrectly

| Parameter | Value |
|-----------|-------|
| Container | Row, W=300, H=100, Align=Stretch |
| Child 0 | TextElement, W="60", H="30", AlignSelf=Start |
| Child 1 | TextElement, W="60", H="30", AlignSelf=Center |
| Child 2 | TextElement, W="60", H="30", AlignSelf=End |

**Calculation** (Row direction, cross axis = Y, crossAxisSize = 100):
- Child 0 (Start): X=0, Y=0, W=60, H=30
- Child 1 (Center): X=60, Y=(100-30)/2=35, W=60, H=30
- Child 2 (End): X=120, Y=100-30=70, W=60, H=30

| Child | X | Y | Width | Height |
|-------|---|---|-------|--------|
| 0 (Start) | 0 | 0 | 60 | 30 |
| 1 (Center) | 60 | 35 | 60 | 30 |
| 2 (End) | 120 | 70 | 60 | 30 |

---

## 2. Reverse Direction Tests (Task 1.7) -- 6 tests

File: `tests/FlexRender.Tests/Layout/LayoutEngineDirectionTests.cs`

### Algorithm Reference

Per IMPLEMENTATION-PLAN Task 1.8, reverse is implemented as post-layout mirror:
- **RowReverse**: layout as normal Row, then `child.X = containerWidth - child.X - child.Width`
- **ColumnReverse**: layout as normal Column, then `child.Y = containerHeight - child.Y - child.Height`

Children are processed in document order (not reversed iteration), then positions are flipped.

### Test 1: ComputeLayout_RowReverse_ChildrenOrderedRightToLeft

| Parameter | Value |
|-----------|-------|
| Container | **RowReverse**, W="400", H="100", Justify=Start |
| Child 0 | TextElement, W="100", H="50" |
| Child 1 | TextElement, W="100", H="50" |
| Child 2 | TextElement, W="100", H="50" |
| Canvas | W=400 |

**Normal Row layout** (Justify=Start):
- child[0]: X=0, W=100
- child[1]: X=100, W=100
- child[2]: X=200, W=100

**After RowReverse mirror** (`X' = 400 - X - W`):
- child[0]: X = 400 - 0 - 100 = 300
- child[1]: X = 400 - 100 - 100 = 200
- child[2]: X = 400 - 200 - 100 = 100

| Child | X | Y | Width | Height |
|-------|---|---|-------|--------|
| 0 | 300 | 0 | 100 | 50 |
| 1 | 200 | 0 | 100 | 50 |
| 2 | 100 | 0 | 100 | 50 |

**Key assertion**: child[0].X > child[1].X > child[2].X (right-to-left order).

### Test 2: ComputeLayout_RowReverse_FirstChildAtRightEdge

| Parameter | Value |
|-----------|-------|
| Container | RowReverse, W="400", H="100" |
| Child 0 | TextElement, W="80", H="50" |
| Canvas | W=400 |

**Normal Row**: child[0].X = 0, W = 80.
**After mirror**: X = 400 - 0 - 80 = 320.

| Property | Expected |
|----------|----------|
| child[0].X | 320 |
| child[0].X + child[0].Width | 400 (right edge) |
| child[0].Y | 0 |
| child[0].Width | 80 |
| child[0].Height | 50 |

### Test 3: ComputeLayout_RowReverse_WithGap_GapBetweenReversedItems

| Parameter | Value |
|-----------|-------|
| Container | RowReverse, W="400", H="100", Gap="20" |
| Child 0 | TextElement, W="100", H="50" |
| Child 1 | TextElement, W="100", H="50" |
| Canvas | W=400 |

**Normal Row with gap=20** (Justify=Start):
- child[0]: X=0, W=100
- child[1]: X=100+20=120, W=100

**After RowReverse mirror** (`X' = 400 - X - W`):
- child[0]: X = 400 - 0 - 100 = 300
- child[1]: X = 400 - 120 - 100 = 180

**Gap between reversed items**: child[0].X - (child[1].X + child[1].Width) = 300 - (180 + 100) = 20. Correct!

| Child | X | Y | Width | Height |
|-------|---|---|-------|--------|
| 0 | 300 | 0 | 100 | 50 |
| 1 | 180 | 0 | 100 | 50 |

**Key assertion**: gap = child[0].X - (child[1].X + child[1].Width) = 20.

### Test 4: ComputeLayout_ColumnReverse_ChildrenOrderedBottomToTop

| Parameter | Value |
|-----------|-------|
| Container | **ColumnReverse**, H="300", Justify=Start |
| Child 0 | TextElement, W="100", H="50" |
| Child 1 | TextElement, W="100", H="50" |
| Child 2 | TextElement, W="100", H="50" |
| Canvas | W=300 |

**Normal Column layout** (Justify=Start):
- child[0]: Y=0, H=50
- child[1]: Y=50, H=50
- child[2]: Y=100, H=50

**After ColumnReverse mirror** (`Y' = 300 - Y - H`):
- child[0]: Y = 300 - 0 - 50 = 250
- child[1]: Y = 300 - 50 - 50 = 200
- child[2]: Y = 300 - 100 - 50 = 150

| Child | X | Y | Width | Height |
|-------|---|---|-------|--------|
| 0 | 0 | 250 | 100 | 50 |
| 1 | 0 | 200 | 100 | 50 |
| 2 | 0 | 150 | 100 | 50 |

**Key assertion**: child[0].Y > child[1].Y > child[2].Y (bottom-to-top order).

### Test 5: ComputeLayout_ColumnReverse_FirstChildAtBottom

| Parameter | Value |
|-----------|-------|
| Container | ColumnReverse, H="300" |
| Child 0 | TextElement, W="100", H="80" |
| Canvas | W=300 |

**Normal Column**: child[0].Y = 0, H = 80.
**After mirror**: Y = 300 - 0 - 80 = 220.

| Property | Expected |
|----------|----------|
| child[0].Y | 220 |
| child[0].Y + child[0].Height | 300 (bottom edge) |
| child[0].X | 0 |
| child[0].Width | 100 |
| child[0].Height | 80 |

### Test 6: ComputeLayout_ColumnReverse_WithGap_GapBetweenReversedItems

| Parameter | Value |
|-----------|-------|
| Container | ColumnReverse, H="300", Gap="20" |
| Child 0 | TextElement, W="100", H="50" |
| Child 1 | TextElement, W="100", H="50" |
| Canvas | W=300 |

**Normal Column with gap=20** (Justify=Start):
- child[0]: Y=0, H=50
- child[1]: Y=50+20=70, H=50

**After ColumnReverse mirror** (`Y' = 300 - Y - H`):
- child[0]: Y = 300 - 0 - 50 = 250
- child[1]: Y = 300 - 70 - 50 = 180

**Gap between reversed items**: child[0].Y - (child[1].Y + child[1].Height) = 250 - (180 + 50) = 20. Correct!

| Child | X | Y | Width | Height |
|-------|---|---|-------|--------|
| 0 | 0 | 250 | 100 | 50 |
| 1 | 0 | 180 | 100 | 50 |

**Key assertion**: gap = child[0].Y - (child[1].Y + child[1].Height) = 20.

---

## 3. Overflow Fallback Tests (Task 1.9) -- 3 tests

File: `tests/FlexRender.Tests/Layout/LayoutEngineTests.cs` (added to existing file)

### Algorithm Reference

Per CSS spec and Yoga: when `freeSpace < 0`, SpaceBetween/SpaceAround/SpaceEvenly all fallback to **Start** behavior.
This means: `leadingMainDim = 0`, `betweenMainDim = 0` -- children packed at start, overflowing at end.

### Common Setup

```
Container: Column, Height="100", Justify=SpaceBetween|SpaceAround|SpaceEvenly
Children: 3x TextElement, each Height="50"
Total child height = 3 * 50 = 150
Free space = 100 - 150 = -50 (negative!)
Canvas: Width=300
```

Without fallback, the space-distribution would produce bizarre negative spacing.
With fallback to Start: children pack from Y=0 sequentially (overflowing past container bottom).

### Test 1: ComputeLayout_SpaceBetween_NegativeFreeSpace_FallbackToStart

| Parameter | Value |
|-----------|-------|
| Container | Column, H="100", Justify=**SpaceBetween** |
| Child 0 | TextElement, H="50" |
| Child 1 | TextElement, H="50" |
| Child 2 | TextElement, H="50" |

**Without fallback** (SpaceBetween with negative space):
- betweenMainDim = -50 / (3-1) = -25
- child[0].Y = 0, child[1].Y = 50 + (-25) = 25, child[2].Y = 25 + 50 + (-25) = 50
- This would cause overlapping children.

**With fallback to Start**:
- child[0].Y = 0
- child[1].Y = 50
- child[2].Y = 100

| Child | Y | Height |
|-------|---|--------|
| 0 | 0 | 50 |
| 1 | 50 | 50 |
| 2 | 100 | 50 |

**Key assertions**:
- child[0].Y = 0 (starts at top)
- child[1].Y = child[0].Y + child[0].Height = 50 (adjacent, no gap)
- child[2].Y = child[1].Y + child[1].Height = 100 (overflows past container H=100)

### Test 2: ComputeLayout_SpaceAround_NegativeFreeSpace_FallbackToStart

| Parameter | Value |
|-----------|-------|
| Container | Column, H="100", Justify=**SpaceAround** |
| Child 0 | TextElement, H="50" |
| Child 1 | TextElement, H="50" |
| Child 2 | TextElement, H="50" |

**With fallback to Start** (same result as SpaceBetween fallback):

| Child | Y | Height |
|-------|---|--------|
| 0 | 0 | 50 |
| 1 | 50 | 50 |
| 2 | 100 | 50 |

### Test 3: ComputeLayout_SpaceEvenly_NegativeFreeSpace_FallbackToStart

| Parameter | Value |
|-----------|-------|
| Container | Column, H="100", Justify=**SpaceEvenly** |
| Child 0 | TextElement, H="50" |
| Child 1 | TextElement, H="50" |
| Child 2 | TextElement, H="50" |

**With fallback to Start** (same result):

| Child | Y | Height |
|-------|---|--------|
| 0 | 0 | 50 |
| 1 | 50 | 50 |
| 2 | 100 | 50 |

---

## Summary Table

| Test Group | Test Count | File |
|------------|-----------|------|
| align-self | 8 | LayoutEngineAlignSelfTests.cs |
| reverse directions | 6 | LayoutEngineDirectionTests.cs |
| overflow fallback | 3 | LayoutEngineTests.cs (existing) |
| **Total** | **17** | |

## Yoga Fixture Cross-Reference

| FlexRender Test | Yoga Fixture | Yoga Expected Values |
|-----------------|--------------|---------------------|
| ChildAlignSelfStart | YGAlignSelfTest / align_self_flex_start | child: {x:0, y:0, w:10, h:10} in 100x100 container |
| ChildAlignSelfCenter | YGAlignSelfTest / align_self_center | child: {x:45, y:0, w:10, h:10} in 100x100 column container |
| ChildAlignSelfEnd | YGAlignSelfTest / align_self_flex_end | child: {x:90, y:0, w:10, h:10} in 100x100 column container |
| OverridesParent | YGAlignSelfTest / align_self_flex_end_override_flex_start | parent: align-items:flex-start, child: align-self:flex-end, child x=90 |
| RowReverse_Children | YGFlexDirectionTest / flex_direction_row_reverse | 3 children: child[0].x=90, child[1].x=80, child[2].x=70 in 100px container with 10px children |
| ColumnReverse_Children | YGFlexDirectionTest / flex_direction_column_reverse | 3 children: child[0].y=90, child[1].y=80, child[2].y=70 in 100px container with 10px children |

**Note**: Yoga fixtures use 100x100 containers with 10x10 children. Our tests use larger values (300x100, 400x100) matching existing FlexRender test conventions, but the proportional behavior is identical.
