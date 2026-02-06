# Phase 2 Algorithm Review: Correctness vs Yoga Reference

**Reviewer**: yoga-c-researcher
**Scope**: Tasks 2.1-2.4 from IMPLEMENTATION-PLAN.md
**Reference**: facebook/yoga `CalculateLayout.cpp`, `FlexLine.cpp`, `BoundAxis.h`, `Align.h`

---

## 1. ResolveFlexBasis (Task 2.4)

### Plan (lines 738-754)

```csharp
private static float ResolveFlexBasis(TemplateElement element, LayoutNode childNode,
    bool isColumn, LayoutContext context)
{
    var basisStr = element.Basis;
    if (!string.IsNullOrEmpty(basisStr) && basisStr != "auto")
    {
        var unit = UnitParser.Parse(basisStr);
        var resolved = isColumn
            ? unit.Resolve(context.ContainerHeight, context.FontSize)
            : unit.Resolve(context.ContainerWidth, context.FontSize);
        if (resolved.HasValue)
            return resolved.Value;
    }
    // auto: use main-axis dimension (Width for row, Height for column)
    return isColumn ? childNode.Height : childNode.Width;
}
```

### Yoga Reference (`computeFlexBasisForChild`, CalculateLayout.cpp:66-264)

```
Priority order:
1. If flexBasis set AND mainAxisSize defined -> max(flexBasis, padding+border)
2. If main axis = row AND width defined -> max(width, padding+border)
3. If main axis = column AND height defined -> max(height, padding+border)
4. Otherwise -> measure child for intrinsic size -> max(measured, padding+border)
```

### Verdict: CORRECT with caveats

**Priority order matches**: explicit basis > main-axis dimension > intrinsic. The plan's `return isColumn ? childNode.Height : childNode.Width` correctly falls back to the already-computed main dimension (which comes from intrinsic measurement or explicit dimension).

**Issues found**:

| # | Severity | Issue | Yoga Reference |
|---|----------|-------|---------------|
| 1 | **MEDIUM** | Missing minimum clamping to padding+border | Yoga: `maxOrDefined(resolvedFlexBasis, paddingAndBorder)` (line 104-106). Every flex basis resolution path clamps to at least padding+border. The plan does not clamp. |
| 2 | **LOW** | No `display:none` / `position:absolute` skip in basis computation | Yoga skips these in `computeFlexBasisForChildren` (lines 575-589). The plan relies on these being filtered elsewhere, which is fine if Phase 1c (display:none) and Phase 4a (absolute) are correctly implemented before Phase 2. However, during Phase 2 execution, these are not yet implemented. |
| 3 | **LOW** | Single flex child optimization missing | Yoga: if there's exactly one flexible child with both grow and shrink, set its basis to 0 instead of measuring (lines 556-571). This is a performance optimization, not a correctness issue. FlexRender can skip this. |

**Recommendation**: Add clamping to padding in ResolveFlexBasis:
```csharp
// After resolving basis:
var padding = PaddingParser.ParseAbsolute(element.Padding).ClampNegatives();
var minBasis = isColumn ? padding.Vertical : padding.Horizontal;
return Math.Max(resolved, minBasis);
```

---

## 2. Two-Pass Resolution Algorithm (Task 2.4)

### Plan (lines 786-886)

The plan describes an **iterative** resolution loop that's closer to the CSS spec than Yoga's simplified two-pass:

```
for iteration 0..itemCount+1:
  1. Calculate unfrozenFreeSpace and totalGrowFactors/totalShrinkScaled
  2. For each unfrozen item: compute hypothetical size
  3. If clamped by min/max -> freeze, mark anyNewlyFrozen
  4. If no newly frozen -> break
```

### Yoga Reference

Yoga uses a **simplified two-pass** approach (not iterative):

```
Pass 1 (distributeFreeSpaceFirstPass):
  - For each item: compute hypothetical, boundAxis it
  - If bounded != hypothetical -> freeze (remove from factors, add to deltaFreeSpace)
  - remainingFreeSpace -= deltaFreeSpace

Pass 2 (distributeFreeSpaceSecondPass):
  - For each item: compute size using updated remainingFreeSpace and factors
  - boundAxis the result
```

Yoga explicitly notes this deviates from CSS spec (CalculateLayout.cpp:919-925):
> "This two pass approach for resolving min/max constraints deviates from the spec.
> The spec describes a process that needs to be repeated a variable number of times."

### Verdict: CORRECT -- actually more spec-compliant than Yoga

The plan's iterative approach is **more correct** than Yoga's two-pass because it handles cascading freeze scenarios. For example: item A's freeze changes free space, which causes item B to also hit its min constraint. Yoga would miss this; the plan's loop catches it.

**Issues found**:

| # | Severity | Issue | Yoga Reference |
|---|----------|-------|---------------|
| 1 | **HIGH** | Missing factor flooring when totalGrowFactors < 1 | Yoga (FlexLine.cpp:104-112): `if (totalFlexGrowFactors > 0 && totalFlexGrowFactors < 1) totalFlexGrowFactors = 1;`. Same for shrink. This prevents fractional grow factors (e.g., `flex-grow: 0.2, 0.2, 0.4` summing to 0.8) from distributing only 80% of free space. Without flooring, items would not fill the container. The plan does NOT include this. |
| 2 | **HIGH** | Shrink formula sign inconsistency | Yoga: `totalFlexShrinkScaledFactors += -shrink * basis` (negative accumulation). Then `childSize = basis + (remainingFreeSpace / totalFlexShrinkScaledFactors) * (-shrink * basis)`. Both `remainingFreeSpace` and `totalFlexShrinkScaledFactors` are negative, so the division is positive, and multiplied by the negative `shrinkScaledFactor`, it produces a negative result added to basis (correct shrink). **The plan uses `totalShrinkScaled += shrink * basis` (positive) and `unfrozenFreeSpace * scaledFactor / totalShrinkScaled`** where `unfrozenFreeSpace` is negative. This gives `negative * positive / positive = negative`, added to basis -- which is correct arithmetic. However, the variable naming hides the intent. **Mathematically equivalent, NOT a bug.** |
| 3 | **MEDIUM** | `unfrozenFreeSpace` recalculated per iteration but items already frozen use `sizes[i]` not `bases[i]` | This is correct: frozen items use their clamped sizes, unfrozen items use their bases. The plan gets this right (line 820: `frozen[i] ? sizes[i] : bases[i]`). |
| 4 | **MEDIUM** | Missing edge case: `totalGrowFactors == 0` when growing | If all unfrozen items have `grow=0` but there's positive free space, items should keep their basis. The plan handles this correctly: `hypothetical = grow > 0 && totalGrowFactors > 0 ? ... : bases[i]` (line 843-845). |
| 5 | **MEDIUM** | Missing edge case: `totalShrinkScaled == 0` when shrinking | Yoga (line 667-668): `if (totalFlexShrinkScaledFactors == 0) childSize = childFlexBasis + flexShrinkScaledFactor`. This is a safety fallback. The plan handles this: `totalShrinkScaled > 0 && scaledFactor > 0 ? ... : bases[i]` (line 851-852) -- item keeps basis, which is safe. However, this differs from Yoga's fallback which adds the raw scaled factor. In practice, both work because if `totalShrinkScaled == 0`, all items have shrink=0 or basis=0, so neither should shrink. |
| 6 | **LOW** | Epsilon comparison for freeze detection | Plan: `Math.Abs(clamped - hypothetical) > 0.01f`. Yoga uses exact comparison: `baseMainSize != boundMainSize`. The 0.01f threshold could cause floating-point issues. Consider using exact comparison or a smaller epsilon (e.g., `float.Epsilon * 100`). |
| 7 | **LOW** | `Span<float>` and `stackalloc` usage | Plan uses `stackalloc` for <= 32 items, heap allocation otherwise. This is a nice optimization not present in Yoga (which uses `std::vector`). No correctness concern. |

**Critical Fix Required**: Factor flooring. Add before the distribution loop:

```csharp
// Floor total factors to 1 when between 0 and 1
// This prevents fractional grow values from distributing less than 100% of free space
if (totalGrowFactors > 0 && totalGrowFactors < 1)
    totalGrowFactors = 1;
if (totalShrinkScaled > 0 && totalShrinkScaled < 1)
    totalShrinkScaled = 1;
```

---

## 3. ResolveMinMax (Task 2.4)

### Plan (lines 759-783)

```csharp
private static (float min, float max) ResolveMinMax(TemplateElement element,
    bool isColumn, LayoutContext context)
{
    float min = 0f;
    float max = float.MaxValue;
    // ... parse minStr/maxStr, resolve units
    return (min, max);
}
```

### Yoga Reference (`boundAxisWithinMinAndMax`, BoundAxis.h:30-61)

```cpp
if (max >= 0 && value > max) return max;
if (min >= 0 && value < min) return min;
return value;
```

And `boundAxis` additionally ensures value >= padding+border:
```cpp
return max(boundAxisWithinMinAndMax(...), paddingAndBorderForAxis(...));
```

### Verdict: CORRECT

The plan's approach of returning `(min, max)` and using `ClampSize` is equivalent. The only difference is that Yoga also clamps to padding+border in `boundAxis` (not just in `boundAxisWithinMinAndMax`).

**Issues found**:

| # | Severity | Issue | Yoga Reference |
|---|----------|-------|---------------|
| 1 | **MEDIUM** | Missing padding+border minimum in post-clamp | Yoga's `boundAxis` ensures value >= padding+border AFTER min/max clamping. The plan's `ClampSize` only clamps to the resolved min/max values. If `min=0` and padding=10, a child could theoretically be sized to 5px, which is below its padding. |
| 2 | **LOW** | Percentage min/max resolves against parent size | Plan: `var parentSize = isColumn ? context.ContainerHeight : context.ContainerWidth`. This is correct for most cases. Yoga resolves min-width percentages against `ownerWidth` and min-height against `axisSize` (which is `ownerHeight`). The plan matches. |

**Recommendation**: After `ClampSize`, add a padding floor:
```csharp
var padding = PaddingParser.ParseAbsolute(element.Padding).ClampNegatives();
var paddingFloor = isColumn ? padding.Vertical : padding.Horizontal;
sizes[i] = Math.Max(sizes[i], paddingFloor);
```

---

## 4. ClampSize (Task 2.4)

### Plan (lines 728-732)

```csharp
private static float ClampSize(float value, float min, float max)
{
    var effectiveMax = Math.Max(min, max);
    return Math.Clamp(value, min, effectiveMax);
}
```

### Yoga Reference

```cpp
// BoundAxis.h:52-58
if (max >= 0 && value > max) return max;
if (min >= 0 && value < min) return min;
return value;
```

### Verdict: CORRECT

Both implementations have the same behavior: min wins over max.

- Plan: `effectiveMax = Math.Max(min, max)` ensures max >= min, then clamps. If `min=100, max=50`, `effectiveMax=100`, result is clamped to `[100, 100]` = 100. Min wins.
- Yoga: checks max first, then min. If `value=80, max=50, min=100`, first clamp to 50 (max), then clamp to 100 (min) = 100. Min wins.

**Difference**: Yoga only applies min/max when they are `>= 0` (line 52: `max >= FloatOptional{0}`). The plan uses `float.MaxValue` as default max and `0` as default min, which works equivalently since `float.MaxValue` effectively means "no max constraint" and `0` means "no min constraint" (items can't have negative size anyway).

**No issues found.** ClampSize is correct.

---

## 5. Test Coverage Review (Tasks 2.1-2.2)

### Task 2.1: Flex Basis Tests (7 tests)

```
FlexBasisPixels, FlexBasisPercent, FlexBasisAuto,
FlexBasisWithGrow, FlexBasisWithShrink,
FlexBasisPixels_Row, FlexBasis0_WithGrow
```

**Missing test cases from Yoga test fixtures**:

| # | Missing Test | Yoga Fixture |
|---|-------------|-------------|
| 1 | `flex_basis_overrides_main_size` -- basis takes priority over height | `YGFlexTest.html:37-41` |
| 2 | `flex_grow_less_than_factor_one` -- fractional grow factors | `YGFlexTest.html:49-53` |
| 3 | `flex_shrink_to_zero` -- item with shrink=0 should not shrink | `YGFlexTest.html:31-35` |
| 4 | Basis with padding -- basis should not go below padding | Yoga: `maxOrDefined(basis, padding)` |

**Recommendation**: Add at least tests 1, 2, 3 which test critical behaviors that could regress.

### Task 2.2: Min/Max Tests (10 tests)

Test coverage looks comprehensive. Two additional edge cases from Yoga:

| # | Missing Test | Yoga Fixture |
|---|-------------|-------------|
| 1 | `flex_grow_within_constrained_min_max_column` | `YGMinMaxDimensionTest.html:50-53` |
| 2 | `justify_content_overflow_min_max` -- min/max on container + overflow | `YGMinMaxDimensionTest.html:29-33` |

---

## 6. Summary of Findings

### CRITICAL Issues (must fix before implementation)

| # | Issue | Severity | Location | Fix |
|---|-------|----------|----------|-----|
| 1 | **Missing factor flooring** | HIGH | Two-pass loop, before distribution | Add: `if (totalGrowFactors > 0 && totalGrowFactors < 1) totalGrowFactors = 1;` Same for shrink. Without this, `flex-grow: 0.2, 0.2, 0.4` (total=0.8) distributes only 80% of free space instead of 100%. |
| 2 | **Missing padding+border floor for flex basis** | MEDIUM | `ResolveFlexBasis()` return | Add: `return Math.Max(resolved, paddingFloor)` |

### WARNINGS (should fix, not blocking)

| # | Issue | Severity | Location | Fix |
|---|-------|----------|----------|-----|
| 3 | Missing padding+border floor after ClampSize | MEDIUM | After sizes are assigned | Add floor check |
| 4 | Epsilon 0.01f for freeze detection may cause precision issues | LOW | Freeze comparison | Consider `Math.Abs(clamped - hypothetical) > 1e-5f` or exact comparison |
| 5 | Missing tests for fractional grow, shrink=0, basis-overrides-size | LOW | Task 2.1 tests | Add 3 more test cases |

### APPROVED Items (correct as-is)

- Priority order in `ResolveFlexBasis`: explicit basis > main-axis dimension > intrinsic -- matches Yoga
- `ClampSize` min-wins-over-max semantics -- matches Yoga
- Iterative loop approach -- more spec-compliant than Yoga's two-pass
- Shrink formula `shrink * basis` scaling -- mathematically equivalent to Yoga
- `ResolveMinMax` resolution against parent size -- matches Yoga
- `Span<float>` / `stackalloc` optimization -- good performance decision
- Default min=0, max=float.MaxValue -- correct equivalence to Yoga's FloatOptional approach

---

## 7. Recommended Changes to IMPLEMENTATION-PLAN.md

### In Task 2.4, add factor flooring (inside the iteration loop, after computing totalGrowFactors/totalShrinkScaled):

```csharp
// Factor flooring: prevent fractional total factors from under-distributing space
// Per CSS spec and Yoga FlexLine.cpp:104-112
if (totalGrowFactors > 0 && totalGrowFactors < 1)
    totalGrowFactors = 1;
if (totalShrinkScaled > 0 && totalShrinkScaled < 1)
    totalShrinkScaled = 1;
```

### In Task 2.4 ResolveFlexBasis, add padding floor:

```csharp
// Flex basis minimum: can never go below padding (Yoga: maxOrDefined(basis, paddingAndBorder))
var paddingValues = PaddingParser.ParseAbsolute(element.Padding).ClampNegatives();
var paddingFloor = isColumn ? paddingValues.Vertical : paddingValues.Horizontal;
return Math.Max(resolvedBasis, paddingFloor);
```

### In Task 2.1, add test cases:

```
ComputeLayout_FlexBasis_OverridesMainAxisDimension
ComputeLayout_FractionalGrow_TotalLessThanOne_DistributesAllSpace
ComputeLayout_FlexShrinkZero_ItemDoesNotShrink
```
