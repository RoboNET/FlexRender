# Phase 2 Test Fixtures: Exact Numerical Expectations

Reference: Yoga layout algorithm, CSS Flexbox spec Section 9.7, IMPLEMENTATION-PLAN.md Tasks 2.1-2.4.

## Key Formulas

**Flex-basis resolution** (priority order):
1. Explicit `basis` value (px, %, em) -- if set and not "auto"
2. Main-axis dimension (Height for column, Width for row) -- "auto" fallback
3. Intrinsic content size -- if no explicit dimension

**Grow**: `newSize = basis + freeSpace * growFactor / totalGrowFactors`

**Factor flooring** (Yoga FlexLine.cpp:104-112, per yoga-c-researcher finding):
```
When 0 < totalGrowFactors < 1: floor totalGrowFactors to 1
When 0 < totalShrinkScaled < 1: floor totalShrinkScaled to 1
```
This prevents fractional grow factors (e.g., 0.2+0.2+0.4=0.8) from distributing only 80% of free space.
Without flooring, items would not fill the container.

**Padding floor** (Yoga: `maxOrDefined(basis, paddingAndBorder)`):
Flex basis can never go below the element's total padding on the main axis.

**Shrink** (CSS-spec, scaled by basis):
```
scaledShrinkFactor = flexShrink * basis
shrinkAmount = overflow * scaledShrinkFactor / totalScaledShrinkFactors
newSize = basis - shrinkAmount
```

**ClampSize** (min wins over max per CSS spec):
```
effectiveMax = Math.Max(min, max)
result = Math.Clamp(value, min, effectiveMax)
```

---

## 1. flex-basis Tests (Task 2.1) -- 7 tests

File: `tests/FlexRender.Tests/Layout/LayoutEngineFlexBasisTests.cs`

### Test 1: ComputeLayout_Column_FlexBasisPixels_SetsInitialSize

| Parameter | Value |
|-----------|-------|
| Container | Column, W=300 (canvas), H="200" |
| Child 0 | TextElement, Basis="50", Grow=0, Shrink=0 |
| Canvas | W=300 |

**Calculation**: Basis explicitly set to 50px. No grow, no shrink. Main axis = height.
Child gets exactly its basis as height.

| Property | Expected |
|----------|----------|
| child[0].Height | 50 |
| child[0].Y | 0 |
| child[0].Width | 300 (stretched to container, default AlignItems) |

### Test 2: ComputeLayout_Column_FlexBasisPercent_CalculatesFromParent

| Parameter | Value |
|-----------|-------|
| Container | Column, W=300 (canvas), H="200" |
| Child 0 | TextElement, Basis="25%", Grow=0, Shrink=0 |
| Canvas | W=300 |

**Calculation**: Basis = 25% of container height = 25% * 200 = 50.

| Property | Expected |
|----------|----------|
| child[0].Height | 50 |
| child[0].Y | 0 |

### Test 3: ComputeLayout_Column_FlexBasisAuto_UsesContentSize

| Parameter | Value |
|-----------|-------|
| Container | Column, W=300 (canvas), H="200" |
| Child 0 | TextElement, Content="test", Height="40", Basis="auto" |
| Canvas | W=300 |

**Calculation**: Basis="auto" falls back to main-axis dimension. Child has Height="40", so basis=40.
Child gets its height = 40.

| Property | Expected |
|----------|----------|
| child[0].Height | 40 |
| child[0].Y | 0 |

**Note**: When basis="auto" and no explicit Height, the intrinsic content size is used (depends on TextMeasurer). For unit tests without TextMeasurer, use explicit Height to control the "auto" fallback.

### Test 4: ComputeLayout_Column_FlexBasisWithGrow_GrowsFromBasis

| Parameter | Value |
|-----------|-------|
| Container | Column, W=300 (canvas), H="200" |
| Child 0 | TextElement, Basis="50", Grow=1 |
| Child 1 | TextElement, Basis="50", Grow=1 |
| Canvas | W=300 |

**Calculation**:
- Total bases = 50 + 50 = 100
- Free space = 200 - 100 = 100
- Total grow factors = 1 + 1 = 2
- Each child grows by: 100 * 1/2 = 50
- Child 0: 50 + 50 = 100
- Child 1: 50 + 50 = 100

| Child | Y | Height |
|-------|---|--------|
| 0 | 0 | 100 |
| 1 | 100 | 100 |

**Yoga cross-reference**: `flex_basis_flex_grow_column` -- 100x100 container, child[0] basis=50 grow=1, child[1] grow=1 (basis=0).
Yoga: child[0].h=75 (50+25), child[1].h=25 (0+25). Different because child[1] has no basis.
Our test uses equal bases for clearer validation.

### Test 5: ComputeLayout_Column_FlexBasisWithShrink_ShrinksFromBasis

| Parameter | Value |
|-----------|-------|
| Container | Column, W=300 (canvas), H="150" |
| Child 0 | TextElement, Basis="100", Shrink=1 |
| Child 1 | TextElement, Basis="100", Shrink=1 |
| Canvas | W=300 |

**Calculation** (CSS-spec shrink, scaled by basis):
- Total bases = 100 + 100 = 200
- Overflow = 200 - 150 = 50 (need to shrink by 50)
- freeSpace = -50
- scaledShrinkFactor[0] = 1 * 100 = 100
- scaledShrinkFactor[1] = 1 * 100 = 100
- totalScaledShrinkFactors = 200
- Child 0 shrink: basis + freeSpace * scaledFactor / total = 100 + (-50) * 100/200 = 100 - 25 = 75
- Child 1 shrink: 100 + (-50) * 100/200 = 100 - 25 = 75
- Verify: 75 + 75 = 150. Correct!

| Child | Y | Height |
|-------|---|--------|
| 0 | 0 | 75 |
| 1 | 75 | 75 |

**Yoga cross-reference**: `flex_basis_flex_shrink_column` -- 100x100 container, child[0] basis=100 shrink=1, child[1] basis=50 shrink=1(default).
Yoga: child[0].h=~66.67 (overflow=50, scaled[0]=100, scaled[1]=50, total=150, shrink[0]=50*100/150=33.33, h=100-33.33=66.67), child[1].h=~33.33.
Our test uses equal bases for clearer math.

### Test 6: ComputeLayout_Row_FlexBasisPixels_SetsInitialWidth

| Parameter | Value |
|-----------|-------|
| Container | Row, W="300", H="100" |
| Child 0 | TextElement, Content="test", Basis="80", Grow=0, Shrink=0 |
| Canvas | W=300 |

**Calculation**: Row direction, main axis = width. Basis=80px. No grow, no shrink.

| Property | Expected |
|----------|----------|
| child[0].Width | 80 |
| child[0].X | 0 |
| child[0].Height | 100 (stretched, default AlignItems=Stretch) |

### Test 7: ComputeLayout_Row_FlexBasis0_WithGrow_EqualDistribution

| Parameter | Value |
|-----------|-------|
| Container | Row, W="300", H="100" |
| Child 0 | TextElement, Basis="0", Grow=1 |
| Child 1 | TextElement, Basis="0", Grow=1 |
| Child 2 | TextElement, Basis="0", Grow=1 |
| Canvas | W=300 |

**Calculation**: Row direction, main axis = width.
- Total bases = 0 + 0 + 0 = 0
- Free space = 300 - 0 = 300
- Total grow = 3
- Each child: 0 + 300 * 1/3 = 100

| Child | X | Width | Height |
|-------|---|-------|--------|
| 0 | 0 | 100 | 100 |
| 1 | 100 | 100 | 100 |
| 2 | 200 | 100 | 100 |

**Note**: This is the classic "equal distribution" pattern. Basis=0 ensures all space is distributed via grow.

---

## 1b. Additional flex-basis Tests (from yoga-c-researcher findings) -- 3 tests

These tests cover critical edge cases identified in `10-phase2-algorithm-review.md`.

### Test 8: ComputeLayout_FlexBasis_OverridesMainAxisDimension

| Parameter | Value |
|-----------|-------|
| Container | Column, W=300 (canvas), H="200" |
| Child 0 | TextElement, Height="20", Basis="50", Grow=1 |
| Child 1 | TextElement, Height="10", Grow=1 |
| Child 2 | TextElement, Height="10", Grow=1 |
| Canvas | W=300 |

**Calculation**: flex-basis takes priority over explicit Height (Yoga: `computeFlexBasisForChild` priority 1 > 3).
- Child 0 basis = 50 (from Basis, NOT from Height=20)
- Child 1 basis = 10 (from Height, since Basis="auto")
- Child 2 basis = 10 (from Height, since Basis="auto")
- Total bases = 50 + 10 + 10 = 70
- Free space = 200 - 70 = 130
- Total grow = 3
- Grow per child = 130/3 = 43.33
- Child 0: 50 + 43.33 = 93.33
- Child 1: 10 + 43.33 = 53.33
- Child 2: 10 + 43.33 = 53.33
- Verify: 93.33 + 53.33 + 53.33 = 200. Correct!

| Child | Y | Height |
|-------|---|--------|
| 0 | 0 | ~93.33 |
| 1 | ~93.33 | ~53.33 |
| 2 | ~146.67 | ~53.33 |

**Key assertion**: child[0].Height > child[1].Height (basis=50 > basis=10 means child 0 gets more space).
**Yoga cross-reference**: `flex_basis_overrides_main_size` -- container 100x100, child[0] h=20 basis=50 grow=1, child[1] h=10 grow=1, child[2] h=10 grow=1. Yoga: child[0]=80, child[1]=10, child[2]=10 (only child[0] has grow). Our test gives all children grow=1 for clearer grow-from-basis validation.

### Test 9: ComputeLayout_FractionalGrow_TotalLessThanOne_DistributesAllSpace

| Parameter | Value |
|-----------|-------|
| Container | Column, W=300 (canvas), H="500" |
| Child 0 | TextElement, Basis="40", Grow=0.2f, Shrink=0 |
| Child 1 | TextElement, Grow=0.2f, Shrink=0 |
| Child 2 | TextElement, Grow=0.4f, Shrink=0 |
| Canvas | W=300 |

**Calculation** (factor flooring critical here):
- Child 0 basis = 40 (explicit Basis)
- Child 1 basis = 0 (no Height, no Basis -- intrinsic ~0 for TextElement without TextMeasurer)
- Child 2 basis = 0
- Total bases = 40 + 0 + 0 = 40
- Free space = 500 - 40 = 460
- Total grow = 0.2 + 0.2 + 0.4 = 0.8

**WITHOUT factor flooring** (incorrect):
- child[0]: 40 + 460 * 0.2/0.8 = 40 + 115 = 155
- child[1]: 0 + 460 * 0.2/0.8 = 115
- child[2]: 0 + 460 * 0.4/0.8 = 230
- Total = 155 + 115 + 230 = 500. Appears correct...

**WITH factor flooring** (Yoga behavior -- totalGrow floored to 1):
- child[0]: 40 + 460 * 0.2/1 = 40 + 92 = 132
- child[1]: 0 + 460 * 0.2/1 = 92
- child[2]: 0 + 460 * 0.4/1 = 184
- Total = 132 + 92 + 184 = 408. Does NOT fill container!

**Analysis**: The CSS spec says when totalGrow < 1, distribute `freeSpace * totalGrow` (not all freeSpace). Yoga's flooring approach is equivalent: `freeSpace * factor / max(1, totalFactors)` = `freeSpace * factor` when total < 1.

Actually, re-examining: Yoga floors totalGrow to 1, which means each child gets `freeSpace * factor / 1 = freeSpace * factor`. This means the total distributed = `freeSpace * sum(factors)` = `460 * 0.8` = `368`, leaving `460 - 368 = 92` undistributed. This is the correct CSS behavior for fractional grow < 1.

**Corrected expectation** (Yoga with flooring to 1):

| Child | Y | Height |
|-------|---|--------|
| 0 | 0 | 132 (40 + 460*0.2) |
| 1 | 132 | 92 (0 + 460*0.2) |
| 2 | 224 | 184 (0 + 460*0.4) |

Total = 132 + 92 + 184 = 408. Remaining undistributed = 92. Container not fully filled (correct CSS behavior for totalGrow < 1).

**Key assertions**:
- Total children height < container height (undistributed space remains)
- child[0].Height = 132 (basis 40 + 460 * 0.2)
- child[2].Height = 2 * child[1].Height (grow ratio 0.4:0.2 = 2:1)

**Yoga cross-reference**: `flex_grow_less_than_factor_one` -- container 200x500 column, children grow=0.2/0.2/0.4 basis=40/0/0 shrink=0.
Expected: child[0]={w:200,h:132}, child[1]={w:200,h:92}, child[2]={w:200,h:184}.

### Test 10: ComputeLayout_FlexShrinkZero_ItemDoesNotShrink

| Parameter | Value |
|-----------|-------|
| Container | Column, W=300 (canvas), H="75" |
| Child 0 | TextElement, W="50", H="50", Shrink=0 |
| Child 1 | TextElement, W="50", H="50", Shrink=1 |
| Child 2 | TextElement, W="50", H="50", Shrink=0 |
| Canvas | W=300 |

**Calculation** (Column, main axis = height):
- Bases: 50, 50, 50. Total = 150. Overflow = 150 - 75 = 75. freeSpace = -75.
- scaledShrink[0] = 0*50 = 0 (shrink=0, does not participate)
- scaledShrink[1] = 1*50 = 50
- scaledShrink[2] = 0*50 = 0 (shrink=0, does not participate)
- totalScaledShrink = 50
- child[0]: shrink=0, keeps basis = 50. Frozen.
- child[1]: 50 + (-75)*50/50 = 50 - 75 = -25 -> max(0, -25) = 0
- child[2]: shrink=0, keeps basis = 50. Frozen.

Wait -- that gives total = 50 + 0 + 50 = 100, which exceeds the 75px container. The children overflow.

Actually, looking more carefully: with the iterative algorithm, child[1] would shrink to absorb ALL overflow since it's the only shrinkable item. But it can't go below 0.

- child[1] hypothetical: 50 + (-75)*50/50 = 50 - 75 = -25
- Clamped to 0 (sizes can't be negative). But with basis=50 and unfrozenFreeSpace=-75, there's more overflow than child[1] can absorb.
- Result: child[0]=50, child[1]=0, child[2]=50. Total=100, overflows container (75). This is correct behavior -- shrink=0 items refuse to shrink.

**Revised**: Let's use H="125" to make the overflow manageable:

| Parameter | Value |
|-----------|-------|
| Container | Column, W=300 (canvas), H="125" |
| Child 0 | TextElement, W="50", H="50", Shrink=0 |
| Child 1 | TextElement, W="50", H="50", Shrink=1 |
| Child 2 | TextElement, W="50", H="50", Shrink=0 |
| Canvas | W=300 |

**Calculation**:
- Bases: 50, 50, 50. Total = 150. Overflow = 25. freeSpace = -25.
- child[0]: shrink=0, scaledShrink=0. Keeps basis=50.
- child[1]: shrink=1, scaledShrink=1*50=50
- child[2]: shrink=0, scaledShrink=0. Keeps basis=50.
- totalScaledShrink = 50
- child[1] hypothetical: 50 + (-25)*50/50 = 50 - 25 = 25
- Total: 50 + 25 + 50 = 125. Correct!

| Child | Y | Height |
|-------|---|--------|
| 0 | 0 | 50 |
| 1 | 50 | 25 |
| 2 | 75 | 50 |

**Key assertions**:
- child[0].Height = 50 (shrink=0, unchanged)
- child[2].Height = 50 (shrink=0, unchanged)
- child[1].Height = 25 (only shrinkable item absorbs all overflow)
- Total = 125 = container height

**Yoga cross-reference**: `flex_shrink_to_zero` -- container h=75, children 50px each with shrink=0/1/0.
Yoga: child[0]=50, child[1]=0 (75px overflow, only child[1] shrinks, capped at 0), child[2]=50.

---

## 2. min/max Tests (Task 2.2) -- 10 tests

File: `tests/FlexRender.Tests/Layout/LayoutEngineMinMaxTests.cs`

### Test 1: ComputeLayout_MinWidth_ChildDoesNotShrinkBelowMinWidth

| Parameter | Value |
|-----------|-------|
| Container | Row, W="200", H="100" |
| Child 0 | TextElement, W="150", MinWidth="100", Shrink=1 |
| Child 1 | TextElement, W="150", Shrink=1 |
| Canvas | W=200 |

**Calculation** (Row, main axis = width):
- Bases: child[0]=150, child[1]=150 (from Width, since basis=auto)
- Total bases = 300. Overflow = 300 - 200 = 100
- freeSpace = -100
- Without min/max:
  - scaledShrink[0] = 1 * 150 = 150, scaledShrink[1] = 1 * 150 = 150, total = 300
  - child[0] hypothetical: 150 + (-100) * 150/300 = 150 - 50 = 100
  - child[1] hypothetical: 150 + (-100) * 150/300 = 150 - 50 = 100
- Clamp child[0]: max(100, min(float.MaxValue, 100)) = 100. MinWidth=100, so clamped at 100. Happens to match -- no freezing needed.
- Both fit: 100 + 100 = 200. Correct!

| Child | X | Width |
|-------|---|-------|
| 0 | 0 | 100 |
| 1 | 100 | 100 |

**Key assertion**: child[0].Width >= 100 (MinWidth respected).

### Test 2: ComputeLayout_MaxWidth_ChildDoesNotGrowBeyondMaxWidth

| Parameter | Value |
|-----------|-------|
| Container | Row, W="300", H="100" |
| Child 0 | TextElement, Basis="0", Grow=1, MaxWidth="80" |
| Child 1 | TextElement, Basis="0", Grow=1 |
| Canvas | W=300 |

**Calculation** (Row, main axis = width):
- Bases: child[0]=0, child[1]=0. Total=0.
- freeSpace = 300
- Iteration 1:
  - totalGrow = 2. Each hypothetical = 0 + 300*1/2 = 150
  - child[0]: clamp(150, 0, 80) = 80. Clamped! Freeze child[0] at 80.
- Iteration 2:
  - Unfrozen freeSpace = 300 - 80 - 0 = 220
  - child[1]: 0 + 220*1/1 = 220. No min/max constraint. Final = 220.
- Total: 80 + 220 = 300. Correct!

| Child | X | Width |
|-------|---|-------|
| 0 | 0 | 80 |
| 1 | 80 | 220 |

**Key assertion**: child[0].Width <= 80 (MaxWidth respected).

### Test 3: ComputeLayout_MinHeight_ChildDoesNotShrinkBelowMinHeight

| Parameter | Value |
|-----------|-------|
| Container | Column, W=300, H="100" |
| Child 0 | TextElement, H="80", MinHeight="60", Shrink=1 |
| Child 1 | TextElement, H="80", Shrink=1 |
| Canvas | W=300 |

**Calculation** (Column, main axis = height):
- Bases: 80, 80. Total=160. Overflow = 160 - 100 = 60. freeSpace = -60.
- Iteration 1:
  - scaledShrink[0] = 1*80 = 80, scaledShrink[1] = 1*80 = 80, total=160
  - child[0] hypothetical: 80 + (-60)*80/160 = 80 - 30 = 50
  - child[1] hypothetical: 80 + (-60)*80/160 = 80 - 30 = 50
  - child[0]: clamp(50, 60, float.MaxValue) = 60. Clamped! Freeze at 60.
- Iteration 2:
  - Unfrozen freeSpace = 100 - 60 - 80 = -40
  - child[1] scaledShrink = 1*80 = 80, totalScaled = 80
  - child[1] hypothetical: 80 + (-40)*80/80 = 80 - 40 = 40
  - No min/max on child[1]. Final = 40.
- Total: 60 + 40 = 100. Correct!

| Child | Y | Height |
|-------|---|--------|
| 0 | 0 | 60 |
| 1 | 60 | 40 |

**Key assertion**: child[0].Height >= 60 (MinHeight respected).

### Test 4: ComputeLayout_MaxHeight_ChildDoesNotGrowBeyondMaxHeight

| Parameter | Value |
|-----------|-------|
| Container | Column, W=300, H="200" |
| Child 0 | TextElement, Basis="0", Grow=1, MaxHeight="60" |
| Child 1 | TextElement, Basis="0", Grow=1 |
| Canvas | W=300 |

**Calculation** (Column, main axis = height):
- Bases: 0, 0. freeSpace = 200.
- Iteration 1:
  - totalGrow=2. Each hypothetical = 0 + 200*1/2 = 100
  - child[0]: clamp(100, 0, 60) = 60. Clamped! Freeze at 60.
- Iteration 2:
  - Unfrozen freeSpace = 200 - 60 - 0 = 140
  - child[1]: 0 + 140*1/1 = 140. Final = 140.
- Total: 60 + 140 = 200. Correct!

| Child | Y | Height |
|-------|---|--------|
| 0 | 0 | 60 |
| 1 | 60 | 140 |

**Key assertion**: child[0].Height <= 60 (MaxHeight respected).

### Test 5: ComputeLayout_MinWidth_WithFlexShrink_RespectsMinWidth

| Parameter | Value |
|-----------|-------|
| Container | Row, W="100", H="100" |
| Child 0 | TextElement, W="80", MinWidth="60", Shrink=1 |
| Child 1 | TextElement, W="80", Shrink=1 |
| Canvas | W=100 |

**Calculation** (Row, main axis = width):
- Bases: 80, 80. Total=160. Overflow = 60. freeSpace = -60.
- Iteration 1:
  - scaledShrink[0]=1*80=80, scaledShrink[1]=1*80=80, total=160
  - child[0] hypothetical: 80 + (-60)*80/160 = 80 - 30 = 50
  - child[1] hypothetical: 80 + (-60)*80/160 = 80 - 30 = 50
  - child[0]: clamp(50, 60, float.MaxValue) = 60. Clamped! Freeze at 60.
- Iteration 2:
  - Unfrozen freeSpace = 100 - 60 - 80 = -40
  - child[1]: 80 + (-40)*80/80 = 80 - 40 = 40. Final = 40.
- Total: 60 + 40 = 100. Correct!

| Child | X | Width |
|-------|---|-------|
| 0 | 0 | 60 |
| 1 | 60 | 40 |

**Key assertion**: child[0].Width >= 60 even though shrink would take it to 50.

### Test 6: ComputeLayout_MaxWidth_WithFlexGrow_RespectsMaxWidth

| Parameter | Value |
|-----------|-------|
| Container | Row, W="300", H="100" |
| Child 0 | TextElement, W="50", Grow=1, MaxWidth="100" |
| Child 1 | TextElement, W="50", Grow=1 |
| Canvas | W=300 |

**Calculation** (Row, main axis = width):
- Bases: 50, 50. Total=100. freeSpace = 200.
- Iteration 1:
  - totalGrow=2. Each hypothetical = 50 + 200*1/2 = 150
  - child[0]: clamp(150, 0, 100) = 100. Clamped! Freeze at 100.
- Iteration 2:
  - Unfrozen freeSpace = 300 - 100 - 50 = 150
  - child[1]: 50 + 150*1/1 = 200. Final = 200.
- Total: 100 + 200 = 300. Correct!

| Child | X | Width |
|-------|---|-------|
| 0 | 0 | 100 |
| 1 | 100 | 200 |

### Test 7: ComputeLayout_MinMaxWidth_ClampsToRange

| Parameter | Value |
|-----------|-------|
| Container | Row, W="300", H="100" |
| Child 0 | TextElement, Basis="0", Grow=1, MinWidth="80", MaxWidth="120" |
| Child 1 | TextElement, Basis="0", Grow=1 |
| Canvas | W=300 |

**Calculation** (Row, main axis = width):
- Bases: 0, 0. freeSpace = 300.
- Iteration 1:
  - totalGrow=2. Each hypothetical = 0 + 300*1/2 = 150
  - child[0]: clamp(150, 80, 120) = 120. Clamped! Freeze at 120.
- Iteration 2:
  - Unfrozen freeSpace = 300 - 120 = 180
  - child[1]: 0 + 180 = 180. Final = 180.
- Total: 120 + 180 = 300. Correct!

| Child | X | Width |
|-------|---|-------|
| 0 | 0 | 120 |
| 1 | 120 | 180 |

**Key assertion**: 80 <= child[0].Width <= 120 (clamped to range).

### Test 8: ComputeLayout_MinMaxHeight_ClampsToRange

| Parameter | Value |
|-----------|-------|
| Container | Column, W=300, H="300" |
| Child 0 | TextElement, Basis="0", Grow=1, MinHeight="50", MaxHeight="80" |
| Child 1 | TextElement, Basis="0", Grow=1 |
| Canvas | W=300 |

**Calculation** (Column, main axis = height):
- Bases: 0, 0. freeSpace = 300.
- Iteration 1:
  - totalGrow=2. Each hypothetical = 0 + 300/2 = 150
  - child[0]: clamp(150, 50, 80) = 80. Clamped! Freeze at 80.
- Iteration 2:
  - Unfrozen freeSpace = 300 - 80 = 220
  - child[1]: 0 + 220 = 220. Final = 220.
- Total: 80 + 220 = 300. Correct!

| Child | Y | Height |
|-------|---|--------|
| 0 | 0 | 80 |
| 1 | 80 | 220 |

**Key assertion**: 50 <= child[0].Height <= 80.

### Test 9: ComputeLayout_MinWidth_PercentValue_CalculatesFromParent

| Parameter | Value |
|-----------|-------|
| Container | Row, W="200", H="100" |
| Child 0 | TextElement, Basis="0", Grow=1, MinWidth="25%" |
| Child 1 | TextElement, Basis="0", Grow=1 |
| Canvas | W=200 |

**Calculation** (Row, main axis = width):
- MinWidth = 25% of 200 = 50
- Bases: 0, 0. freeSpace = 200.
- Iteration 1:
  - totalGrow=2. Each hypothetical = 0 + 200/2 = 100
  - child[0]: clamp(100, 50, float.MaxValue) = 100. NOT clamped (100 >= 50). No freeze.
  - No items newly frozen. Done.
- Final: child[0]=100, child[1]=100.

| Child | X | Width |
|-------|---|-------|
| 0 | 0 | 100 |
| 1 | 100 | 100 |

**Key assertions**:
- child[0].Width >= 50 (MinWidth=25% of 200=50 respected)
- child[0].Width = 100 (grow distributes equally, min doesn't constrain here)

**Note**: This test validates percent parsing for min/max. The min doesn't actually constrain in this case, but the parsing and resolution must work correctly.

### Test 10: ComputeLayout_TwoPassResolution_MultipleConstrainedItems_CorrectDistribution

| Parameter | Value |
|-----------|-------|
| Container | Row, W="300", H="100" |
| Child 0 | TextElement, Basis="0", Grow=1, MaxWidth="50" |
| Child 1 | TextElement, Basis="0", Grow=1, MaxWidth="80" |
| Child 2 | TextElement, Basis="0", Grow=1 |
| Canvas | W=300 |

**Calculation** (Row, main axis = width -- demonstrates iterative freeze):
- Bases: 0, 0, 0. freeSpace = 300.
- **Iteration 1**:
  - totalGrow=3. Each hypothetical = 0 + 300/3 = 100
  - child[0]: clamp(100, 0, 50) = 50. Clamped! Freeze.
  - child[1]: clamp(100, 0, 80) = 80. Clamped! Freeze.
  - child[2]: 100. Not clamped.
  - anyNewlyFrozen = true.
- **Iteration 2**:
  - Unfrozen freeSpace = 300 - 50 - 80 - 0 = 170
  - totalGrow (unfrozen) = 1
  - child[2]: 0 + 170*1/1 = 170. Not clamped.
  - anyNewlyFrozen = false. Done.
- Total: 50 + 80 + 170 = 300. Correct!

| Child | X | Width |
|-------|---|-------|
| 0 | 0 | 50 |
| 1 | 50 | 80 |
| 2 | 130 | 170 |

**Key assertion**: This test validates the iterative two-pass resolution. Multiple items get frozen in one iteration, and the remaining space is redistributed to unfrozen items.

---

## Summary Table

| Test Group | Test Count | File |
|------------|-----------|------|
| flex-basis (core) | 7 | LayoutEngineFlexBasisTests.cs |
| flex-basis (edge cases) | 3 | LayoutEngineFlexBasisTests.cs |
| min/max | 10 | LayoutEngineMinMaxTests.cs |
| **Total** | **20** | |

## Quick Reference: All Tests at a Glance

### flex-basis

| # | Test Name | Container | Children | Expected |
|---|-----------|-----------|----------|----------|
| 1 | Column_FlexBasisPixels_SetsInitialSize | Col 300x200 | Basis="50" | H=50 |
| 2 | Column_FlexBasisPercent_CalculatesFromParent | Col 300x200 | Basis="25%" | H=50 (25% of 200) |
| 3 | Column_FlexBasisAuto_UsesContentSize | Col 300x200 | Basis="auto", H="40" | H=40 |
| 4 | Column_FlexBasisWithGrow_GrowsFromBasis | Col 300x200 | 2x Basis="50" Grow=1 | H=100, 100 |
| 5 | Column_FlexBasisWithShrink_ShrinksFromBasis | Col 300x150 | 2x Basis="100" Shrink=1 | H=75, 75 |
| 6 | Row_FlexBasisPixels_SetsInitialWidth | Row 300x100 | Basis="80" | W=80 |
| 7 | Row_FlexBasis0_WithGrow_EqualDistribution | Row 300x100 | 3x Basis="0" Grow=1 | W=100, 100, 100 |
| 8 | FlexBasis_OverridesMainAxisDimension | Col 300x200 | H="20" Basis="50" G=1 + 2x H="10" G=1 | H=~93.3, ~53.3, ~53.3 |
| 9 | FractionalGrow_TotalLessThanOne | Col 300x500 | G=0.2 B="40" + G=0.2 + G=0.4, all S=0 | H=132, 92, 184 (total=408, underfilled) |
| 10 | FlexShrinkZero_ItemDoesNotShrink | Col 300x125 | S=0 H=50 + S=1 H=50 + S=0 H=50 | H=50, 25, 50 |

### min/max

| # | Test Name | Container | Children | Expected |
|---|-----------|-----------|----------|----------|
| 1 | MinWidth_ChildDoesNotShrinkBelowMinWidth | Row 200x100 | W=150 MinW=100 + W=150 | W=100, 100 |
| 2 | MaxWidth_ChildDoesNotGrowBeyondMaxWidth | Row 300x100 | B=0 G=1 MaxW=80 + B=0 G=1 | W=80, 220 |
| 3 | MinHeight_ChildDoesNotShrinkBelowMinHeight | Col 300x100 | H=80 MinH=60 + H=80 | H=60, 40 |
| 4 | MaxHeight_ChildDoesNotGrowBeyondMaxHeight | Col 300x200 | B=0 G=1 MaxH=60 + B=0 G=1 | H=60, 140 |
| 5 | MinWidth_WithFlexShrink_RespectsMinWidth | Row 100x100 | W=80 MinW=60 + W=80 | W=60, 40 |
| 6 | MaxWidth_WithFlexGrow_RespectsMaxWidth | Row 300x100 | W=50 G=1 MaxW=100 + W=50 G=1 | W=100, 200 |
| 7 | MinMaxWidth_ClampsToRange | Row 300x100 | B=0 G=1 MinW=80 MaxW=120 + B=0 G=1 | W=120, 180 |
| 8 | MinMaxHeight_ClampsToRange | Col 300x300 | B=0 G=1 MinH=50 MaxH=80 + B=0 G=1 | H=80, 220 |
| 9 | MinWidth_PercentValue_CalculatesFromParent | Row 200x100 | B=0 G=1 MinW=25% + B=0 G=1 | W=100, 100 |
| 10 | TwoPassResolution_MultipleConstrainedItems | Row 300x100 | B=0 G=1 MaxW=50 + MaxW=80 + unconstrained | W=50, 80, 170 |

## Yoga Fixture Cross-Reference

| FlexRender Test | Yoga Fixture | Notes |
|-----------------|--------------|-------|
| FlexBasisPixels (Column) | YGFlexTest / flex_basis_flex_grow_column | Yoga: 100x100, basis=50+grow=1 and grow=1(no basis). Child[0]=75, child[1]=25 |
| FlexBasisPixels (Row) | YGFlexTest / flex_basis_flex_grow_row | Yoga: 100x100 row, basis=50+grow=1 and grow=1. Child[0]=75, child[1]=25 |
| FlexBasisWithShrink | YGFlexTest / flex_basis_flex_shrink_column | Yoga: 100x100, basis=100 shrink=1 + basis=50. Shrink scaled by basis |
| FlexBasis_Overrides | YGFlexTest / flex_basis_overrides_main_size | Yoga: 100x100, child h=20 basis=50 grow=1 + h=10 grow=1 + h=10 grow=1 |
| FractionalGrow | YGFlexTest / flex_grow_less_than_factor_one | Yoga: 200x500, grow=0.2/0.2/0.4 basis=40/0/0. H=132/92/184 (factor flooring) |
| ShrinkZero | YGFlexTest / flex_shrink_to_zero | Yoga: h=75, children 50px shrink=0/1/0. child[1] shrinks to 0 |
| MaxWidth | YGMinMaxDimensionTest / max_width | Yoga: 100x100 container, child max-width=50 -> w=50 |
| MaxHeight | YGMinMaxDimensionTest / max_height | Yoga: 100x100 row, child max-height=50 -> h=50 |
| MinMaxWidth flexing | YGMinMaxDimensionTest / child_min_max_width_flexing | Yoga: 120x50 row, child[0] min-w=60 basis=0 grow=1, child[1] max-w=20 basis=50% grow=1 |
| TwoPassResolution | No direct Yoga equivalent | Validates iterative freeze algorithm (our custom test) |

**Note**: Our test values use larger containers (200-300px) matching FlexRender test conventions, while Yoga fixtures typically use 100x100. The algorithm behavior is identical.

## yoga-c-researcher Findings Integration

Source: `docs/flexbox-expansion/10-phase2-algorithm-review.md`

### Critical Implementation Notes

| # | Finding | Severity | Impact on Tests |
|---|---------|----------|----------------|
| 1 | **Factor flooring**: `totalGrowFactors` between 0 and 1 must be floored to 1 | HIGH | Test 9 (FractionalGrow) validates this. Without flooring, grow=0.2+0.2+0.4=0.8 would distribute 100% of freeSpace instead of 80%. |
| 2 | **Padding floor**: flex basis minimum = padding on main axis | MEDIUM | Not directly tested (would require padding + basis interaction test). Implementation should add `Math.Max(resolved, paddingFloor)` in `ResolveFlexBasis`. |
| 3 | **Shrink formula sign**: Plan uses positive accumulation (`shrink * basis`), Yoga uses negative. Mathematically equivalent. | INFO | No test needed -- verified as correct arithmetic. |
| 4 | **Epsilon for freeze**: Plan uses 0.01f, Yoga uses exact comparison. | LOW | Consider `float.Epsilon * 100` or exact comparison. Tests should use 0.1f precision in Assert.Equal. |

### Recommended Test Precision

All assertions should use `Assert.Equal(expected, actual, 0.1f)` to accommodate floating-point arithmetic:
```csharp
Assert.Equal(132f, child[0].Height, 0.1f);  // Not exact match
Assert.Equal(93.33f, child[0].Height, 1f);  // Wider tolerance for repeating decimals
```
